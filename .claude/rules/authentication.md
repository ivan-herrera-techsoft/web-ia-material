---
description: Autenticacion JWT con Access Token + Refresh Token en Bisoft Atenea
globs: "**/Services/TokenService.cs,**/Extensions/ServiceExtensions.cs,**/Dtos/Configurations/Jwt*,**/Endpoints/Security/**"
---

## Esquema general

La autenticación usa **JWT stateless** (access token) combinado con **Refresh Tokens persistidos en BD** (via `Bisoft.Security.RefreshTokens.EntityFramework`). El access token se transporta por cookie `HttpOnly` o por header `Authorization: Bearer`. El refresh token se guarda también en cookie `HttpOnly`.

```
Login  →  AccessToken (JWT, corta vida) + RefreshToken (BD, larga vida, en cookie)
Request →  JwtBearerMiddleware valida AccessToken (cookie o header)
Expirado → POST /auth/refresh → nuevo AccessToken usando RefreshToken
Logout  →  Invalida RefreshToken en BD, borra cookies
```

---

## DTOs de configuración

```csharp
// Api/Dtos/Configurations/AccessTokensConfigurations.cs
public record AccessTokensConfigurations(string Key, string Issuer, string Audience, TimeSpan Duration);

// Api/Dtos/Configurations/JwtConfigurations.cs  
public class JwtConfigurations
{
    public AccessTokensConfigurations AccessTokens { get; }
    public RefreshTokensConfigurations RefreshTokens { get; }   // del paquete Bisoft.Security.RefreshTokens

    public JwtConfigurations(
        AccessTokensConfigurations accessTokens,
        RefreshTokensConfigurations refreshTokens)
    {
        AccessTokens  = accessTokens;
        RefreshTokens = refreshTokens;
    }
}
```

`JwtConfigurations` es el wrapper raíz. `AccessTokensConfigurations` es un record inmutable. `RefreshTokensConfigurations` viene del paquete externo.

---

## Lectura de configuración — JwtConfigurationsReader

En `Api/Extensions/Configuration/JwtConfigurationsReader.cs` (partial de `ConfigurationExtensions`):

```csharp
private static JwtConfigurations GetJwtConfiguration(this IConfiguration configuration)
{
    var key      = configuration.GetJwtKey();
    var issuer   = configuration.GetJwtIssuer();
    var audience = configuration.GetJwtAudience();
    var refreshConn = configuration.GetConnectionConfiguration("RefreshTokens");

    return new JwtConfigurations(
        accessTokens: new AccessTokensConfigurations(
            key, issuer, audience,
            configuration.GetAccessTokenDuration()),
        refreshTokens: new RefreshTokensConfigurations(
            refreshConn.DatabaseProvider,
            refreshConn.DatabaseConnectionString,
            cacheEnabled: false,
            cacheSlidingDuration: TimeSpan.MinValue,
            cacheAbsoluteDuration: TimeSpan.MinValue,
            configuration.GetTimedServiceEnabledConfiguration("ExpiredSessionsCleaner"),
            configuration.GetTimedServiceScheduleConfiguration("ExpiredSessionsCleaner"),
            key, issuer, audience,
            configuration.GetRefreshTokenDuration(),
            autoLogoutOldestSession: false,
            maxSessionsAtTheTime: 0
        )
    );
}

private static string GetJwtKey(this IConfiguration configuration)
{
    var key = configuration["Jwt:Key"].TryOverwriteWithEnviromentValue("JWT_KEY");
    var ex = TEnvironmentException.InvalidConfiguration(
        TEnvironmentException.Sources.APPSETTINGS,
        "La key del JWT no tiene el formato correcto");
    key.ValidateNull(ex).ValidateLength(minLength: 32, maxLength: 1024, exceptionWhenInvalid: ex);
    return key;
}
```

Env vars de override: `JWT_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_DURATION`, `REFRESH_JWT_DURATION` (o similar). La clave tiene validación mínima de 32 caracteres.

---

## appsettings.json

```json
{
  "Jwt": {
    "Key":    "clave-secreta-de-al-menos-32-caracteres-de-longitud",
    "Issuer": "nombre-del-servicio",
    "Audience": "nombre-del-servicio",
    "AccessDurationInMinutes": 60,
    "RefreshDurationInMinutes": 1440
  },
  "SensitiveData": {
    "Jwt:Key": ""
  }
}
```

- `Key` va en `SensitiveData` para que sea encriptado en producción.
- `AccessDurationInMinutes`: duración del access token (corta, ej. 60 minutos).
- `RefreshDurationInMinutes`: duración del refresh token (larga, ej. 1440 = 1 día, o hasta 10080 = 7 días).
- La connection string de refresh tokens va en `ConnectionStrings:RefreshTokens`.

---

## ConfigureAuthentication

En `ServiceExtensions.cs`:

```csharp
public static IServiceCollection ConfigureAuthentication(
    this IServiceCollection services,
    GeneralConfiguration configuration)
{
    var jwt = configuration.Jwt;
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew                = TimeSpan.Zero,
                    ValidIssuer              = jwt.AccessTokens.Issuer,
                    ValidAudience            = jwt.AccessTokens.Audience,
                    IssuerSigningKey         = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.AccessTokens.Key))
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        ctx.Request.Cookies.TryGetValue(
                            ApiConstants.Cookies.ACCESS_TOKEN, out var accessToken);
                        if (!string.IsNullOrEmpty(accessToken))
                            ctx.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });
    return services;
}
```

Puntos clave:
- `ClockSkew = TimeSpan.Zero` — sin margen de gracia al validar la expiración.
- `OnMessageReceived` — extrae el token de la cookie `accessToken` si no viene en el header `Authorization`.
- `AddAuthorization()` se registra por separado en `Program.cs` (sin políticas; la autorización es por presencia de token).

---

## InjectConfigurations — registro de IOptions

```csharp
public static IServiceCollection InjectConfigurations(
    this IServiceCollection services,
    GeneralConfiguration configuration)
{
    services.AddSingleton(Options.Create(configuration.Jwt));
    services.AddSingleton(Options.Create(configuration.Jwt.AccessTokens));
    services.AddSingleton(Options.Create(configuration.Cache));
    services.AddSingleton(Options.Create(configuration.AutomatedServices));
    return services;
}
```

`TokenService` recibe `IOptions<AccessTokensConfigurations>` e `IOptions<RefreshTokensConfigurations>` directamente. Estos se resuelven porque el paquete `Bisoft.Security.RefreshTokens` los registra internamente al llamar `ConfigureRefreshTokensServices`.

---

## ConfigureRefreshTokensServices (paquete externo)

```csharp
// En Program.cs — después de builder.Services
builder.Services.ConfigureRefreshTokensServices(generalConfiguration.Jwt.RefreshTokens);
```

Este método del paquete registra: `SessionService`, `RefreshTokenService`, el `DbContext` de refresh tokens y la lógica de rotación y expiración.

---

## TokenService — Api/Services/

Clase en la capa Api (no Application) porque maneja cookies HTTP y claims JWT:

```csharp
public class TokenService(
    IOptions<AccessTokensConfigurations> jwtConfiguration,
    IOptions<RefreshTokensConfigurations> refreshTokenConfiguration)
{
    private readonly AccessTokensConfigurations _cfg = jwtConfiguration.Value;

    // Leer userId del claim del token validado
    public TokenValues ReadToken(HttpContext context)
    {
        var id = context.User?.Claims
            .FirstOrDefault(e => e.Type == ApiConstants.Claims.USER_ID)?.Value
            ?? throw TUnauthorizedAccessException.InvalidToken(
                "El token de acceso no cuenta con el id del usuario");
        return new TokenValues { UserId = id };
    }

    // Crear access token con claim userId
    public string CreateAccessToken(TokenValues tokenValues)
    {
        var claims = new[] { new Claim(ApiConstants.Claims.USER_ID, tokenValues.UserId) };
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _cfg.Issuer,
            audience: _cfg.Audience,
            expires: DateTime.Now.Add(_cfg.Duration),
            signingCredentials: credentials,
            claims: claims);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Expiration como Unix timestamp (int)
    public static int GetTimeStampExpiration(string token)
    {
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return (int)((DateTimeOffset)jwtToken.ValidTo).ToUnixTimeSeconds();
    }

    // Guardar tokens en cookies HttpOnly
    public void SetTokenInCookie(HttpContext context, IniciarSesionResponse response, bool isPersistent)
    {
        var opts = new CookieOptions
        {
            HttpOnly   = true,
            IsEssential = true,
            Secure     = true,
            SameSite   = SameSiteMode.None,
            Expires    = isPersistent ? DateTimeOffset.UtcNow.Add(_cfg.Duration) : null
        };
        context.Response.Cookies.Append(ApiConstants.Cookies.ACCESS_TOKEN,  response.AccessToken,  opts);
        context.Response.Cookies.Append(ApiConstants.Cookies.REFRESH_TOKEN, response.RefreshToken, opts);
    }

    // Leer refresh token de cookie
    public string GetRefreshToken(HttpContext context)
    {
        context.Request.Cookies.TryGetValue(ApiConstants.Cookies.REFRESH_TOKEN, out var token);
        if (string.IsNullOrEmpty(token))
            throw TUnauthorizedAccessException.InvalidToken("El token no fue encontrado");
        return token;
    }

    // Borrar ambas cookies en logout
    public void RemoveTokenInCookie(HttpContext context)
    {
        context.Response.Cookies.Delete(ApiConstants.Cookies.ACCESS_TOKEN);
        context.Response.Cookies.Delete(ApiConstants.Cookies.REFRESH_TOKEN);
    }
}
```

Constantes relacionadas en `ApiConstants`:
```csharp
public static class Cookies
{
    public const string ACCESS_TOKEN  = "accessToken";
    public const string REFRESH_TOKEN = "refreshToken";
}
public static class Claims
{
    public const string USER_ID = "userid";
}
```

---

## Endpoints de seguridad — SecurityEndpointGroup

```
Auth/
├── SecurityEndpointGroup.cs   → MapGroup("auth").WithTags("Security")
├── IniciarSesion.cs           → POST /auth/login      [AllowAnonymous]
├── RefrescarToken.cs          → POST /auth/refresh     [AllowAnonymous]
├── CerrarSesion.cs            → POST /auth/revoke      (requiere auth)
└── CerrarTodasLasSesiones.cs  → POST /auth/revoke-all  (requiere auth)
```

`[AllowAnonymous]` va en la lambda de `IniciarSesion` y `RefrescarToken`. El resto hereda `RequireAuthorization` del grupo raíz.

---

## Pipeline — orden obligatorio

```csharp
app.UseCors(ALLOW_ALL_CORS_POLICY)
   .UseRateLimiter()
   .UseAuthentication()        // ← antes de UseAuthorization
   .UseAuthorization()
   .UseRequestLocalization(...)
   .UseMiddleware<ErrorHandlerMiddleware>();
```

`UseAuthentication` debe ir **antes** de `UseAuthorization`. `UseCors` va primero para que el preflight (OPTIONS) no sea bloqueado por rate limiter ni auth.

---

## Flujo completo de login

```
POST /auth/login { nombre, password, isPersistent? }
  → UsuarioService.ConsultarUsuario(nombre, password)    // valida credenciales
  → SessionService.CreateSession(userId, direccion)      // crea refresh token en BD
  → TokenService.CreateAccessToken(new TokenValues { UserId })
  → TokenService.SetTokenInCookie(context, response, isPersistent)
  → Results.Ok(IniciarSesionResponse)

POST /auth/refresh  (cookie refreshToken)
  → TokenService.GetRefreshToken(context)
  → SessionService.RefreshSession(oldRefreshToken)       // rota el refresh token
  → TokenService.CreateAccessToken(new TokenValues { UserId = session.UserId })
  → Results.Ok(IniciarSesionResponse)

POST /auth/revoke  (requiere auth)
  → TokenService.GetRefreshToken(context)
  → SessionService.Logout(refreshToken)                  // invalida sesión en BD
  → Results.Ok(CerrarSesionResponse)
```
