---
name: configurar-autenticacion
description: Configura autenticacion JWT con access token y refresh token en Bisoft Atenea
---

## Paso 1 — Dependencias NuGet

Agregar los paquetes al `.csproj` del proyecto Api:

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.*" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.*" />
<PackageReference Include="Bisoft.Security.RefreshTokens.EntityFramework" Version="*" />
```

---

## Paso 2 — appsettings.json

Agregar la sección `Jwt` y la connection string de refresh tokens:

```json
{
  "Jwt": {
    "Key":    "clave-secreta-de-al-menos-32-caracteres-de-longitud",
    "Issuer": "nombre-del-servicio",
    "Audience": "nombre-del-servicio",
    "AccessDurationInMinutes": 60,
    "RefreshDurationInMinutes": 1440
  },
  "ConnectionStrings": {
    "RefreshTokens": {
      "ConnectionString": "Server=...;Initial Catalog=...;User Id=...;Password=...;",
      "Provider": "Microsoft.EntityFrameworkCore.SqlServer"
    }
  },
  "SensitiveData": {
    "Jwt:Key": "",
    "ConnectionStrings:RefreshTokens:ConnectionString": ""
  }
}
```

Variables de entorno de override: `JWT_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_DURATION`.

---

## Paso 3 — DTOs de configuración

Crear `Api/Dtos/Configurations/AccessTokensConfigurations.cs`:

```csharp
namespace Company.Product.Module.Api.Dtos.Configurations;

public record AccessTokensConfigurations(string Key, string Issuer, string Audience, TimeSpan Duration);
```

Crear `Api/Dtos/Configurations/JwtConfigurations.cs`:

```csharp
using Bisoft.Security.RefreshTokens.EntityFramework.Configurations;

namespace Company.Product.Module.Api.Dtos.Configurations;

public class JwtConfigurations
{
    public AccessTokensConfigurations AccessTokens { get; }
    public RefreshTokensConfigurations RefreshTokens { get; }

    public JwtConfigurations(
        AccessTokensConfigurations accessTokens,
        RefreshTokensConfigurations refreshTokens)
    {
        AccessTokens  = accessTokens;
        RefreshTokens = refreshTokens;
    }
}
```

Agregar la propiedad en `GeneralConfiguration`:

```csharp
public JwtConfigurations Jwt { get; }
```

---

## Paso 4 — JwtConfigurationsReader

Crear `Api/Extensions/Configuration/JwtConfigurationsReader.cs` como partial de `ConfigurationExtensions`:

```csharp
namespace Company.Product.Module.Api.Extensions.Configuration;

public static partial class ConfigurationExtensions
{
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
                maxSessionsAtTheTime: 0));
    }

    private static string GetJwtKey(this IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"].TryOverwriteWithEnviromentValue("JWT_KEY");
        var ex  = TEnvironmentException.InvalidConfiguration(
            TEnvironmentException.Sources.APPSETTINGS,
            "La key del JWT no tiene el formato correcto");
        key.ValidateNull(ex).ValidateLength(minLength: 32, maxLength: 1024, exceptionWhenInvalid: ex);
        return key;
    }

    private static string GetJwtIssuer(this IConfiguration configuration)
    {
        var issuer = configuration["Jwt:Issuer"].TryOverwriteWithEnviromentValue("JWT_ISSUER");
        var ex = TEnvironmentException.InvalidConfiguration(
            TEnvironmentException.Sources.APPSETTINGS,
            "El issuer del JWT no tiene el formato correcto");
        issuer.ValidateNull(ex);
        return issuer;
    }

    private static string GetJwtAudience(this IConfiguration configuration)
    {
        var audience = configuration["Jwt:Audience"].TryOverwriteWithEnviromentValue("JWT_AUDIENCE");
        var ex = TEnvironmentException.InvalidConfiguration(
            TEnvironmentException.Sources.APPSETTINGS,
            "La audience del JWT no tiene el formato correcto");
        audience.ValidateNull(ex);
        return audience;
    }

    private static TimeSpan GetAccessTokenDuration(this IConfiguration configuration)
        => configuration.GetDurationFromConfiguration(
            configKey: "Jwt:AccessDurationInMinutes",
            envVar: "JWT_DURATION",
            invalidMessage: "La duración del access token JWT no tiene el formato correcto");

    private static TimeSpan GetRefreshTokenDuration(this IConfiguration configuration)
        => configuration.GetDurationFromConfiguration(
            configKey: "Jwt:RefreshDurationInMinutes",
            envVar: "REFRESH_JWT_DURATION",
            invalidMessage: "La duración del refresh token JWT no tiene el formato correcto");
}
```

---

## Paso 5 — ConfigureAuthentication en ServiceExtensions

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

---

## Paso 6 — InjectConfigurations y AddAuthorization en Program.cs

En `builder.Services`, agregar:

```csharp
builder.Services
    .ConfigureAuthentication(generalConfiguration)
    // ... otros servicios ...
    .InjectConfigurations(generalConfiguration)
    .AddAuthorization();

// Refresh tokens (paquete externo, al final):
builder.Services.ConfigureRefreshTokensServices(generalConfiguration.Jwt.RefreshTokens);
```

En `InjectConfigurations`:

```csharp
services.AddSingleton(Options.Create(configuration.Jwt));
services.AddSingleton(Options.Create(configuration.Jwt.AccessTokens));
```

---

## Paso 7 — TokenService

Crear `Api/Services/TokenService.cs`:

```csharp
namespace Company.Product.Module.Api.Services;

public class TokenService(
    IOptions<AccessTokensConfigurations> jwtConfiguration,
    IOptions<RefreshTokensConfigurations> refreshTokenConfiguration)
{
    private readonly AccessTokensConfigurations _cfg = jwtConfiguration.Value;

    public TokenValues ReadToken(HttpContext context)
    {
        var id = context.User?.Claims
            .FirstOrDefault(e => e.Type == ApiConstants.Claims.USER_ID)?.Value
            ?? throw TUnauthorizedAccessException.InvalidToken(
                "El token de acceso no cuenta con el id del usuario");
        return new TokenValues { UserId = id };
    }

    public string CreateAccessToken(TokenValues tokenValues)
    {
        var claims = new[] { new Claim(ApiConstants.Claims.USER_ID, tokenValues.UserId) };
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg.Key));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token  = new JwtSecurityToken(
            issuer: _cfg.Issuer,
            audience: _cfg.Audience,
            expires: DateTime.Now.Add(_cfg.Duration),
            signingCredentials: creds,
            claims: claims);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static int GetTimeStampExpiration(string token)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return (int)((DateTimeOffset)jwt.ValidTo).ToUnixTimeSeconds();
    }

    public void SetTokenInCookie(HttpContext context, IniciarSesionResponse response, bool isPersistent)
    {
        var opts = new CookieOptions
        {
            HttpOnly    = true,
            IsEssential = true,
            Secure      = true,
            SameSite    = SameSiteMode.None,
            Expires     = isPersistent ? DateTimeOffset.UtcNow.Add(_cfg.Duration) : null
        };
        context.Response.Cookies.Append(ApiConstants.Cookies.ACCESS_TOKEN,  response.AccessToken,  opts);
        context.Response.Cookies.Append(ApiConstants.Cookies.REFRESH_TOKEN, response.RefreshToken, opts);
    }

    public string GetRefreshToken(HttpContext context)
    {
        context.Request.Cookies.TryGetValue(ApiConstants.Cookies.REFRESH_TOKEN, out var token);
        if (string.IsNullOrEmpty(token))
            throw TUnauthorizedAccessException.InvalidToken("El token no fue encontrado");
        return token;
    }

    public void RemoveTokenInCookie(HttpContext context)
    {
        context.Response.Cookies.Delete(ApiConstants.Cookies.ACCESS_TOKEN);
        context.Response.Cookies.Delete(ApiConstants.Cookies.REFRESH_TOKEN);
    }
}
```

Registrar en `ConfigureServices`:

```csharp
services.AddScoped<TokenService>();
```

---

## Paso 8 — Constantes en ApiConstants

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

## Paso 9 — Pipeline en Program.cs

El orden es obligatorio:

```csharp
app.UseCors(ALLOW_ALL_CORS_POLICY)
   .UseRateLimiter()
   .UseAuthentication()
   .UseAuthorization()
   .UseRequestLocalization(...)
   .UseMiddleware<ErrorHandlerMiddleware>();
```

---

## Paso 10 — SecurityEndpointGroup y endpoints

Crear `Api/Endpoints/Security/SecurityEndpointGroup.cs`:

```csharp
public static class SecurityEndpointGroup
{
    public static RouteGroupBuilder MapSecurityEndpoints(this RouteGroupBuilder appEndpoints)
    {
        var group = appEndpoints.MapGroup("auth").WithTags("Security");
        group.MapEndpoints();
        return appEndpoints;
    }
    private static RouteGroupBuilder MapEndpoints(this RouteGroupBuilder endpointGroup)
    {
        endpointGroup.MapIniciarSesion()
                     .MapRefrescarToken()
                     .MapCerrarSesion()
                     .MapCerrarTodasLasSesiones();
        return endpointGroup;
    }
}
```

Endpoints anónimos (`IniciarSesion`, `RefrescarToken`) llevan `[AllowAnonymous]` en la lambda:

```csharp
endpointGroup.MapPost("login", [AllowAnonymous]
    async (UsuarioService svc, SessionService sessionSvc, TokenService tokenSvc,
           HttpContext ctx, [FromBody] IniciarSesionRequest req, CancellationToken ct) =>
    {
        var usuario  = await svc.ConsultarUsuario(req.Nombre, req.Password, ct);
        var sesion   = await sessionSvc.CreateSession(usuario.Id.ToString(), req.Direccion ?? "", ct);
        var token    = tokenSvc.CreateAccessToken(new TokenValues { UserId = usuario.Id.ToString() });
        var response = new IniciarSesionResponse
        {
            AccessToken      = token,
            AccessExpiration = TokenService.GetTimeStampExpiration(token),
            RefreshToken     = sesion.RefreshToken,
            RefreshExpiration = sesion.RefreshTokenExpiresIn
        };
        tokenSvc.SetTokenInCookie(ctx, response, req.IsPersistent);
        return Results.Ok(response);
    }
)
.HasApiVersion(ApiConstants.VERSION_1)
.Produces<IniciarSesionResponse>(StatusCodes.Status200OK)
.WithDescription("Autentica al usuario y devuelve tokens de acceso.")
.WithSummary(ENDPOINT_NAME)
.WithName(ENDPOINT_NAME);
return endpointGroup;
```

---

## Checklist

- [ ] Paquetes `JwtBearer` y `Bisoft.Security.RefreshTokens.EntityFramework` instalados
- [ ] Sección `Jwt` en appsettings con `Key` (≥32 chars), `Issuer`, `Audience`, `AccessDurationInMinutes`, `RefreshDurationInMinutes`
- [ ] `Jwt:Key` en `SensitiveData` para encriptación
- [ ] `AccessTokensConfigurations` (record) y `JwtConfigurations` creados
- [ ] `JwtConfigurationsReader` con validaciones de env var y longitud de key
- [ ] `ConfigureAuthentication` con `ClockSkew = TimeSpan.Zero` y `OnMessageReceived` para cookies
- [ ] `AddAuthorization()` en `builder.Services` (sin políticas)
- [ ] `InjectConfigurations` registra `Options.Create(configuration.Jwt.AccessTokens)`
- [ ] `ConfigureRefreshTokensServices(generalConfiguration.Jwt.RefreshTokens)` al final de servicios
- [ ] `TokenService` registrado como Scoped en `ConfigureServices`
- [ ] Pipeline: `UseAuthentication` antes de `UseAuthorization`, `UseCors` primero
- [ ] `[AllowAnonymous]` en lambda de `IniciarSesion` y `RefrescarToken`
- [ ] `GetRefreshToken` en `CerrarSesion` y `RefrescarToken` (no inyectar como parámetro)
