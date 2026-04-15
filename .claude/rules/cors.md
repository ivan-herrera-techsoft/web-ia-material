---
description: Convenciones para la configuración de CORS con orígenes explícitos y header X-Pagination expuesto
globs: "**/Extensions/**,**/Configuration/**"
---

# Reglas de CORS

## Principio general

CORS se configura **explícitamente** con orígenes, headers y métodos permitidos. Nunca usar `AllowAnyOrigin()` en producción. Los orígenes y headers expuestos se leen desde variables de entorno o appsettings, **nunca hardcodeados**.

---

## DTO de configuración

```csharp
// Api/Dtos/Configurations/CorsConfiguration.cs
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

Se integra como propiedad `required` dentro de `GeneralConfiguration`:

```csharp
public required CorsConfiguration Cors { get; set; }
```

---

## Lector de configuración

```csharp
// Api/Extensions/Configuration/CorsConfigurationsReader.cs
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

**Prioridad de lectura**: variable de entorno → appsettings. Si ninguno está presente, lanza `TEnvironmentException.MissingConfiguration`.

---

## Registro en ServiceExtensions

La constante de política se define en `ApiConstants`:

```csharp
// Api/Helpers/ApiConstants.cs
public const string ALLOW_ALL_CORS_POLICY = "AllowAll";
```

El método de extensión recibe `GeneralConfiguration` (que ya contiene los orígenes y headers leídos):

```csharp
// Api/Extensions/ServiceExtensions.cs
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

---

## Middleware pipeline (Program.cs)

`UseCors` va **antes** de `UseAuthentication` y `UseAuthorization`:

```csharp
app.UseCors(ALLOW_ALL_CORS_POLICY)
   .UseRateLimiter()
   .UseAuthentication()
   .UseAuthorization()
   ...
```

---

## Headers expuestos obligatorios

`Cors:Headers` debe incluir al menos `X-Pagination` para que el cliente pueda leer la cabecera de paginación. Agregar cualquier otro header custom que el frontend necesite leer.

```json
"Cors": {
  "Origins": ["http://localhost:4200"],
  "Headers": ["X-Pagination"]
}
```

---

## Variables de entorno (producción)

| Variable       | Descripción                              | Ejemplo                                              |
|----------------|------------------------------------------|------------------------------------------------------|
| `CORS_ORIGINS` | Orígenes separados por coma              | `https://app.empresa.com,https://admin.empresa.com`  |
| `CORS_HEADERS` | Headers expuestos separados por coma     | `X-Pagination,X-Request-Id`                          |

---

## Flujo de integración

```
GetConfiguration()
  └─ GetCorsConfiguration()
       ├─ GetCorsOrigins()   → CORS_ORIGINS env var → Cors:Origins appsettings
       └─ GetCorsHeaders()   → CORS_HEADERS env var → Cors:Headers appsettings

builder.Services
  .ConfigureCors(generalConfiguration)   → AddCors con política ALLOW_ALL_CORS_POLICY

app.UseCors(ALLOW_ALL_CORS_POLICY)       → activación en middleware pipeline
```
