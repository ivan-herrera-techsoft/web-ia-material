# Spec: Autenticación JWT

## Propósito

Define los contratos para la configuración de autenticación JWT con Access Token de corta vida y Refresh Token persistido en BD (via `Bisoft.Security.RefreshTokens.EntityFramework`) en Bisoft Atenea.

> **Spec relacionado:** Los endpoints de seguridad siguen el patrón de → [Spec: Endpoints](crear-endpoint.md). Los cookies requieren CORS correctamente configurado → [Rules: cors.md](../rules/cors.md).

---

## Contratos

### SC-AUTH-01: ClockSkew debe ser TimeSpan.Zero

`TokenValidationParameters.ClockSkew` debe ser `TimeSpan.Zero`. No debe dejarse en el valor por defecto (5 minutos) ni configurarse con ningún otro margen.

**Justificación:** el valor por defecto de `ClockSkew` es 5 minutos, lo que significa que un token expirado hace menos de 5 minutos aún sería válido. En un sistema con refresh tokens, el access token debe expirar exactamente cuando dice expirar; el cliente es responsable de refrescarlo antes de tiempo usando el refresh token.

✅ Correcto:
```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateLifetime = true,
    ClockSkew        = TimeSpan.Zero,   // expiración exacta
    // ...
};
```

❌ Incorrecto:
```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateLifetime = true,
    // ClockSkew sin definir → 5 minutos de gracia no deseados
};
```

---

### SC-AUTH-02: OnMessageReceived extrae el token de la cookie

`JwtBearerEvents.OnMessageReceived` debe leer el access token desde la cookie `ApiConstants.Cookies.ACCESS_TOKEN` cuando el header `Authorization` no está presente. Esto permite que SPAs funcionen con cookies HttpOnly sin exponer el token en JavaScript.

**Justificación:** si el cliente envía el token en cookie (flujo SPA), el middleware JwtBearer no lo encontrará en el header `Authorization` y rechazará la solicitud. `OnMessageReceived` es el único punto de extensión para inyectar el token desde otro origen antes de la validación.

✅ Correcto:
```csharp
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
```

❌ Incorrecto:
```csharp
// Sin OnMessageReceived — solo funciona con header Authorization: Bearer
// Los clientes SPA que usan cookies HttpOnly quedan sin autenticación
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters { /* ... */ };
        });
```

---

### SC-AUTH-03: Las cookies son HttpOnly, Secure y SameSite=None

Las cookies de access token y refresh token deben configurarse con `HttpOnly = true`, `Secure = true` y `SameSite = SameSiteMode.None`. No se deben crear cookies accesibles desde JavaScript.

**Justificación:** `HttpOnly` evita que JavaScript acceda a los tokens (protección contra XSS). `Secure` garantiza que solo se envíen sobre HTTPS. `SameSite = None` es necesario para solicitudes cross-origin desde el frontend SPA (requiere CORS con `AllowCredentials`).

✅ Correcto:
```csharp
var opts = new CookieOptions
{
    HttpOnly    = true,
    IsEssential = true,
    Secure      = true,
    SameSite    = SameSiteMode.None,
    Expires     = isPersistent ? DateTimeOffset.UtcNow.Add(_cfg.Duration) : null
};
context.Response.Cookies.Append(ApiConstants.Cookies.ACCESS_TOKEN, token, opts);
```

❌ Incorrecto:
```csharp
// Cookie accesible desde JavaScript — vulnerable a XSS
context.Response.Cookies.Append("accessToken", token, new CookieOptions
{
    HttpOnly = false,   // accesible desde JS
    Secure   = false    // se envía también sobre HTTP
});
```

---

### SC-AUTH-04: Solo un claim en el access token — userId

El access token debe contener únicamente el claim `ApiConstants.Claims.USER_ID` (`"userid"`). No se deben incluir roles, permisos, email ni ningún dato adicional del usuario en el JWT.

**Justificación:** el access token es de corta vida y no debe contener datos que puedan quedar obsoletos antes de su expiración. Roles y permisos se verifican en el momento de la solicitud consultando la BD, no desde el token. Incluir datos sensibles en el payload JWT los expone a cualquiera que lo decodifique (el JWT solo está firmado, no cifrado).

✅ Correcto:
```csharp
var claims = new[]
{
    new Claim(ApiConstants.Claims.USER_ID, tokenValues.UserId)
};
```

❌ Incorrecto:
```csharp
var claims = new[]
{
    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
    new Claim(ClaimTypes.Role, user.Role),          // roles en el token → pueden quedar obsoletos
    new Claim(ClaimTypes.Email, user.Email),        // PII en el token
    new Claim("permissions", JsonSerializer.Serialize(user.Permissions))
};
```

---

### SC-AUTH-05: UseAuthentication antes de UseAuthorization en el pipeline

En el pipeline de `WebApplication`, `UseAuthentication()` debe llamarse siempre antes de `UseAuthorization()`. `UseCors()` va primero de todos.

**Justificación:** `UseAuthentication` parsea y valida el token y puebla `HttpContext.User`. `UseAuthorization` lee `HttpContext.User` para evaluar las políticas. Si el orden se invierte, `UseAuthorization` opera sobre un usuario sin autenticar y rechaza todas las solicitudes protegidas.

✅ Correcto:
```csharp
app.UseCors(ALLOW_ALL_CORS_POLICY)
   .UseRateLimiter()
   .UseAuthentication()    // 1. parsea el token → puebla HttpContext.User
   .UseAuthorization()     // 2. evalúa políticas sobre el usuario autenticado
   .UseRequestLocalization(...)
   .UseMiddleware<ErrorHandlerMiddleware>();
```

❌ Incorrecto:
```csharp
app.UseAuthorization()     // evalúa antes de autenticar → rechaza todo
   .UseAuthentication();
```

---

### SC-AUTH-06: [AllowAnonymous] en la lambda de endpoints públicos

Los endpoints `IniciarSesion` y `RefrescarToken` deben tener `[AllowAnonymous]` en la lambda. No se puede llamar `.AllowAnonymous()` fluent sobre el grupo `"auth"` porque afectaría a `CerrarSesion` y `CerrarTodasLasSesiones`, que sí requieren autenticación.

**Justificación:** el grupo raíz `"api"` tiene `.RequireAuthorization()` aplicado. Necesitar acceso anónimo en endpoints individuales dentro de un grupo autenticado requiere el atributo en la lambda para sobrescribir la política del grupo solo en ese endpoint. Ver → [SC-EP-07](crear-endpoint.md#sc-ep-07).

✅ Correcto:
```csharp
endpointGroup.MapPost("login", [AllowAnonymous]
    async (UsuarioService svc, ...) => { ... }
)
.HasApiVersion(ApiConstants.VERSION_1)
// ...
```

❌ Incorrecto:
```csharp
// En SecurityEndpointGroup — anula auth para TODOS los endpoints del grupo
var group = appEndpoints.MapGroup("auth")
    .WithTags("Security")
    .AllowAnonymous();   // CerrarSesion quedaría desprotegido
```

---

### SC-AUTH-07: Key del JWT con mínimo 32 caracteres

La clave JWT (`Jwt:Key` en appsettings) debe tener al menos 32 caracteres. El lector de configuración debe validar esta longitud con `ValidateLength(minLength: 32, ...)` y lanzar `TEnvironmentException.InvalidConfiguration` si no se cumple.

**Justificación:** `HmacSha256` requiere una clave de al menos 256 bits (32 bytes). Una clave más corta hace el token criptográficamente débil y puede causar una excepción en tiempo de ejecución al firmar el primer token.

✅ Correcto:
```csharp
private static string GetJwtKey(this IConfiguration configuration)
{
    var key = configuration["Jwt:Key"].TryOverwriteWithEnviromentValue("JWT_KEY");
    var ex  = TEnvironmentException.InvalidConfiguration(
        TEnvironmentException.Sources.APPSETTINGS,
        "La key del JWT no tiene el formato correcto");
    key.ValidateNull(ex).ValidateLength(minLength: 32, maxLength: 1024, exceptionWhenInvalid: ex);
    return key;
}
```

❌ Incorrecto:
```csharp
// Sin validación de longitud — una key de 10 chars pasaría y fallaría al firmar
var key = configuration["Jwt:Key"];
if (string.IsNullOrEmpty(key))
    throw new Exception("Jwt:Key es requerida");
return key;
```

---

### SC-AUTH-08: Jwt:Key declarado en SensitiveData

La clave `Jwt:Key` debe declararse en la sección `SensitiveData` de `appsettings.json` para que el mecanismo de encriptación de configuraciones la cifre en producción. No debe hardcodearse ni quedar en texto plano en el repositorio.

**Justificación:** el mecanismo `SetEncryption()` / `SensitiveData` cifra los valores de esas claves en el archivo de configuración antes de publicar. Sin esta declaración, la clave JWT quedaría en texto plano en el servidor.

✅ Correcto (`appsettings.json`):
```json
{
  "Jwt": {
    "Key": "dev-key-solo-para-local-no-commitear"
  },
  "SensitiveData": {
    "Jwt:Key": ""
  }
}
```

❌ Incorrecto:
```json
{
  "Jwt": {
    "Key": "mi-clave-secreta-productiva-en-texto-plano"
  }
  // Sin SensitiveData — la clave queda expuesta en el repositorio y en el servidor
}
```

---

### SC-AUTH-09: TokenService vive en la capa Api, no en Application

`TokenService` debe ubicarse en `Api/Services/`, no en `Application/Services/`. El servicio maneja `HttpContext`, cookies y claims JWT, que son conceptos de la capa de presentación (API), no de la capa de aplicación.

**Justificación:** la capa Application no debe tener referencias a `HttpContext` ni a detalles de transporte HTTP. Poner `TokenService` en Application rompe la separación de capas y hace el servicio de aplicación difícil de testear (requiere mockear `HttpContext`).

✅ Correcto:
```
Api/
└── Services/
    └── TokenService.cs   ← maneja HttpContext, cookies, JWT
Application/
└── Services/
    └── UsuarioService.cs ← orquesta lógica de negocio, sin Http
```

❌ Incorrecto:
```
Application/
└── Services/
    ├── UsuarioService.cs
    └── TokenService.cs   ← depende de HttpContext → rompe la capa
```
