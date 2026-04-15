# Skill: Configurar CORS

Configura Cross-Origin Resource Sharing (CORS) en un proyecto Atenea Minimal API.

## Preguntas previas

Antes de comenzar, determinar:
- ¿Cuáles son los orígenes iniciales? (para el appsettings de desarrollo)
- ¿Hay headers custom adicionales que el frontend necesite leer además de `X-Pagination`?

---

## Paso 1 — DTO de configuración

Crear `Api/Dtos/Configurations/CorsConfiguration.cs`:

```csharp
namespace Company.Product.Module.Api.Dtos.Configurations;

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
```

Agregar la propiedad a `GeneralConfiguration`:

```csharp
public required CorsConfiguration Cors { get; set; }
```

---

## Paso 2 — Lector de configuración

Crear `Api/Extensions/Configuration/CorsConfigurationsReader.cs`:

```csharp
namespace Company.Product.Module.Api.Extensions.Configuration;

public static class CorsConfigurationsReader
{
    public static CorsConfiguration GetCorsConfiguration(this IConfiguration configuration)
    {
        return new CorsConfiguration(configuration.GetCorsOrigins(), configuration.GetCorsHeaders());
    }

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

    private static string[] GetCorsHeaders(this IConfiguration configuration)
    {
        var envValue = Environment.GetEnvironmentVariable("CORS_HEADERS");
        if (!string.IsNullOrWhiteSpace(envValue))
            return envValue.Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var headers = configuration.GetSection("Cors:Headers").Get<string[]>();
        if (headers == null || headers.Length == 0)
            throw TEnvironmentException.MissingConfiguration(
                TEnvironmentException.Sources.APPSETTINGS,
                "La configuración 'Cors:Headers' es obligatoria y no fue encontrada."
            );

        return headers;
    }
}
```

Invocar desde `GetConfiguration()` en `ConfigurationsReader.cs`:

```csharp
Cors = configuration.GetCorsConfiguration(),
```

---

## Paso 3 — Constante de política en ApiConstants

Verificar que `Api/Helpers/ApiConstants.cs` contiene:

```csharp
public const string ALLOW_ALL_CORS_POLICY = "AllowAll";
```

Si no existe, agregarla.

---

## Paso 4 — ConfigureCors en ServiceExtensions

En `Api/Extensions/ServiceExtensions.cs` agregar:

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
                .WithExposedHeaders(configuration.Cors.Headers)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
    });
    return services;
}
```

Encadenar la llamada en `Program.cs` dentro del bloque de `builder.Services`:

```csharp
builder.Services
    ...
    .ConfigureCors(generalConfiguration)
    ...
```

---

## Paso 5 — Middleware pipeline

En `Program.cs`, asegurarse de que `UseCors` va **antes** de `UseAuthentication` y `UseAuthorization`:

```csharp
app.UseCors(ALLOW_ALL_CORS_POLICY)
   .UseRateLimiter()
   .UseAuthentication()
   .UseAuthorization()
   ...
```

---

## Paso 6 — Configuración en appsettings

En `appsettings.json` y `appsettings.Development.json`, agregar la sección `Cors`:

```json
"Cors": {
  "Origins": [
    "http://localhost:4200"
  ],
  "Headers": [
    "X-Pagination"
  ]
}
```

Ajustar los orígenes según el entorno. En producción se recomienda usar la variable de entorno `CORS_ORIGINS`.

---

## Verificación

- [ ] `CorsConfiguration.cs` existe con propiedades `Origins` y `Headers`
- [ ] `CorsConfigurationsReader.cs` lee primero variable de entorno, luego appsettings
- [ ] Lanza `TEnvironmentException.MissingConfiguration` si falta la configuración
- [ ] `ConfigureCors` usa `WithOrigins`, `WithExposedHeaders`, `AllowAnyMethod`, `AllowAnyHeader`, `AllowCredentials`
- [ ] **Nunca** aparece `AllowAnyOrigin()`
- [ ] `UseCors` está antes de `UseAuthentication` en el pipeline
- [ ] `appsettings` incluye `Cors:Origins` y `Cors:Headers`
- [ ] `X-Pagination` está en `Cors:Headers`
