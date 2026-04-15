# Spec: Configurar CORS

## Contratos

---

### SC-CORS-01 — CorsConfiguration como DTO dedicado

**Justificación**: Los orígenes y headers expuestos son datos de configuración propios de CORS. Encapsularlos en un DTO tipado permite validarlos en el momento de lectura y propagarlos a través de `GeneralConfiguration` sin acoplar el registro de servicios a `IConfiguration`.

**Correcto**:
```csharp
public class CorsConfiguration
{
    public string[] Origins { get; }
    public string[] Headers { get; }

    public CorsConfiguration(string[] origins, string[] headers)
    {
        Origins = origins;
        Headers = headers;
    }
}

// En GeneralConfiguration:
public required CorsConfiguration Cors { get; set; }
```

**Incorrecto**:
```csharp
// Leer IConfiguration directamente en ConfigureCors
public static IServiceCollection ConfigureCors(
    this IServiceCollection services, IConfiguration configuration)
{
    var origins = configuration.GetSection("Cors:Origins").Get<string[]>();
    ...
}
```

---

### SC-CORS-02 — Prioridad variable de entorno sobre appsettings

**Justificación**: En producción los orígenes y headers se definen como variables de entorno para no hardcodear URLs en archivos de configuración que viven en el repositorio. El lector siempre consulta la variable de entorno primero y cae en appsettings solo si no existe.

**Correcto**:
```csharp
private static string[] GetCorsOrigins(this IConfiguration configuration)
{
    var envValue = Environment.GetEnvironmentVariable("CORS_ORIGINS");
    if (!string.IsNullOrWhiteSpace(envValue))
        return envValue.Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var origins = configuration.GetSection("Cors:Origins").Get<string[]>();
    if (origins == null || origins.Length == 0)
        throw TEnvironmentException.MissingConfiguration(
            TEnvironmentException.Sources.APPSETTINGS,
            "La configuración 'Cors:Origins' es obligatoria y no fue encontrada."
        );

    return origins;
}
```

**Incorrecto**:
```csharp
// Solo leer de appsettings, sin soporte para variable de entorno
var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
```

---

### SC-CORS-03 — Fallo explícito si falta la configuración

**Justificación**: Una aplicación sin CORS configurado puede operar, pero rechazará todas las solicitudes del frontend en producción sin un error claro. Fallar al inicio con `TEnvironmentException.MissingConfiguration` identifica el problema inmediatamente en lugar de manifestarlo como errores 403 en tiempo de ejecución.

**Correcto**:
```csharp
if (origins == null || origins.Length == 0)
    throw TEnvironmentException.MissingConfiguration(
        TEnvironmentException.Sources.APPSETTINGS,
        "La configuración 'Cors:Origins' es obligatoria y no fue encontrada."
    );
```

**Incorrecto**:
```csharp
// Usar valor por defecto silencioso
var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
```

---

### SC-CORS-04 — Nunca AllowAnyOrigin()

**Justificación**: `AllowAnyOrigin()` deshabilita la protección CORS para todos los dominios. Cualquier sitio malicioso puede hacer solicitudes autenticadas en nombre del usuario. En producción esto es inaceptable. En desarrollo también se evita para detectar problemas de configuración temprano.

**Correcto**:
```csharp
builder.WithOrigins(configuration.Cors.Origins)
       .AllowAnyMethod()
       .AllowAnyHeader()
       .AllowCredentials()
```

**Incorrecto**:
```csharp
builder.AllowAnyOrigin()
       .AllowAnyMethod()
       .AllowAnyHeader()
// También incorrecto: AllowAnyOrigin() con AllowCredentials() lanza excepción en .NET
```

---

### SC-CORS-05 — WithExposedHeaders obligatorio para X-Pagination

**Justificación**: Los navegadores bloquean el acceso a headers de respuesta custom a menos que el servidor los declare en `Access-Control-Expose-Headers`. Si `X-Pagination` no está expuesto, el frontend no puede leer los metadatos de paginación aunque el header esté presente en la respuesta.

**Correcto**:
```csharp
builder.WithOrigins(configuration.Cors.Origins)
       .WithExposedHeaders(configuration.Cors.Headers)   // incluye X-Pagination
       .AllowAnyMethod()
       .AllowAnyHeader()
       .AllowCredentials()
```

```json
"Cors": {
  "Origins": ["http://localhost:4200"],
  "Headers": ["X-Pagination"]
}
```

**Incorrecto**:
```csharp
// Headers expuestos hardcodeados en lugar de venir de configuración
builder.WithExposedHeaders("X-Pagination")
```

---

### SC-CORS-06 — Política nombrada con constante ALLOW_ALL_CORS_POLICY

**Justificación**: Usar una constante garantiza que el nombre de la política sea el mismo al registrarla (`AddPolicy`) y al activarla (`UseCors`). Un string literal puede diferir por un typo y CORS quedará silenciosamente desactivado.

**Correcto**:
```csharp
// ApiConstants.cs
public const string ALLOW_ALL_CORS_POLICY = "AllowAll";

// ServiceExtensions.cs
options.AddPolicy(ALLOW_ALL_CORS_POLICY, builder => ...);

// Program.cs
app.UseCors(ALLOW_ALL_CORS_POLICY);
```

**Incorrecto**:
```csharp
options.AddPolicy("AllowAll", builder => ...);
app.UseCors("allowAll");   // typo de casing — política no encontrada
```

---

### SC-CORS-07 — UseCors antes de UseAuthentication en el pipeline

**Justificación**: ASP.NET Core procesa el middleware en el orden en que se registra. Si `UseAuthentication` va antes que `UseCors`, las solicitudes OPTIONS de preflight (sin credenciales) pueden ser rechazadas antes de que CORS tenga oportunidad de responder con los headers correctos.

**Correcto**:
```csharp
app.UseCors(ALLOW_ALL_CORS_POLICY)
   .UseRateLimiter()
   .UseAuthentication()
   .UseAuthorization()
```

**Incorrecto**:
```csharp
app.UseAuthentication()
   .UseAuthorization()
   .UseCors(ALLOW_ALL_CORS_POLICY)   // CORS demasiado tarde en el pipeline
```

---

### SC-CORS-08 — ConfigureCors recibe GeneralConfiguration, no IConfiguration

**Justificación**: `GeneralConfiguration` ya contiene los orígenes y headers leídos y validados. Recibirlo en `ConfigureCors` evita releer `IConfiguration` y duplicar la lógica de lectura. La validación y el parseo ocurren una sola vez al inicio, en `GetCorsConfiguration()`.

**Correcto**:
```csharp
public static IServiceCollection ConfigureCors(
    this IServiceCollection services,
    GeneralConfiguration configuration)
{
    services.AddCors(options =>
    {
        options.AddPolicy(ALLOW_ALL_CORS_POLICY,
            builder => builder
                .WithOrigins(configuration.Cors.Origins)
                ...);
    });
    return services;
}
```

**Incorrecto**:
```csharp
public static IServiceCollection ConfigureCors(
    this IServiceCollection services,
    IConfiguration configuration)   // releer IConfiguration en lugar de usar GeneralConfiguration
{
    var origins = configuration.GetSection("Cors:Origins").Get<string[]>();
    ...
}
```
