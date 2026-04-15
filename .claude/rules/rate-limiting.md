---
description: Convenciones para rate limiting con FixedWindowLimiter y la constante FIXED_RATE_LIMITING_POLICY
globs: "**/Extensions/**,**/Configuration/**"
---

# Reglas de Rate Limiting

## Principio general

Rate limiting se implementa con **ventana fija** (`FixedWindowLimiter`) usando la política `FIXED_RATE_LIMITING_POLICY`. El límite de llamadas se lee de configuración (appsettings o variable de entorno) y se aplica globalmente al grupo de endpoints de API, incluyendo health checks y home.

---

## Lectura de configuración

El límite máximo de llamadas por minuto se gestiona como un `int` directo en `GeneralConfiguration` (no requiere DTO propio). Se lee en `ConfigurationsReader.cs`:

```csharp
// Api/Extensions/Configuration/ConfigurationsReader.cs
private static int GetRateLimiterMaxCalls(this IConfiguration configuration)
{
    var value = configuration["MaxCallsPerMinute"].TryOverwriteWithEnviromentValue("MAX_CALLS");
    value.ValidateNull(TEnvironmentException.MissingConfiguration(
            TEnvironmentException.Sources.APPSETTINGS,
            "Falta la máxima cantidad de llamadas"
        ));
    if (!int.TryParse(value, out var result))
        throw TEnvironmentException.InvalidConfiguration(
            TEnvironmentException.Sources.APPSETTINGS,
            "La máxima cantidad de llamadas tiene un valor inválido"
        );
    return result;
}
```

Se asigna en `GetConfiguration()`:

```csharp
RateLimiterMaxCalls = configuration.GetRateLimiterMaxCalls(),
```

Y en `GeneralConfiguration`:

```csharp
public required int RateLimiterMaxCalls { get; set; }
```

**Prioridad**: variable de entorno `MAX_CALLS` → clave `MaxCallsPerMinute` en appsettings. Si falta → `TEnvironmentException.MissingConfiguration`. Si no es número → `TEnvironmentException.InvalidConfiguration`.

---

## Constante de política

```csharp
// Api/Helpers/ApiConstants.cs
public const string FIXED_RATE_LIMITING_POLICY = "Fixed";
```

---

## Registro en ServiceExtensions

```csharp
// Api/Extensions/ServiceExtensions.cs
public static IServiceCollection ConfigureRateLimiter(
    this IServiceCollection services,
    GeneralConfiguration configuration)
{
    services.AddRateLimiter(_ =>
    {
        _.OnRejected = (context, _) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
            }
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.WriteAsync("Demasiados requests. Intente más tarde.", cancellationToken: _);
            return new ValueTask();
        };

        _.AddFixedWindowLimiter(policyName: FIXED_RATE_LIMITING_POLICY, options =>
        {
            options.PermitLimit = configuration.RateLimiterMaxCalls;
            options.Window = TimeSpan.FromMinutes(1);
            options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            options.QueueLimit = 0;
        });
    });
    return services;
}
```

Puntos clave:
- `OnRejected`: agrega `Retry-After` header si está disponible, devuelve HTTP 429 con mensaje en español.
- Ventana fija de **1 minuto** (no segundos).
- `QueueLimit = 0`: sin cola, rechaza inmediatamente al superar el límite.
- Recibe `GeneralConfiguration`, no `IConfiguration`.

---

## Middleware pipeline (Program.cs)

`UseRateLimiter` va **después** de `UseCors` y **antes** de `UseAuthentication`:

```csharp
app.UseCors(ALLOW_ALL_CORS_POLICY)
   .UseRateLimiter()
   .UseAuthentication()
   .UseAuthorization()
   ...
```

---

## Aplicación en endpoints

La política se aplica **por grupo** en `MapEndpoints`, `AddHealthChecks` y `MapHome`:

```csharp
// WebApplicationExtensions.cs — grupo principal de API
var apiEndpoints = app.MapGroup("api")
                      .WithApiVersionSet(versionSet)
                      .RequireRateLimiting(rateLimitingPolicy)
                      .AddOpenApi()
                      .RequireAuthorization();

// HealthChecksMapping.cs
app.MapHealthChecks("/health-check").AllowAnonymous().RequireRateLimiting(rateLimitingPolicy);
app.HealthDetails().RequireRateLimiting(rateLimitingPolicy);
app.AddLiveness().RequireRateLimiting(rateLimitingPolicy);
app.AddReadiness().RequireRateLimiting(rateLimitingPolicy);

// HomeMapping.cs
app.Home().RequireRateLimiting(rateLimitingPolicy);
```

La constante `FIXED_RATE_LIMITING_POLICY` se pasa como parámetro desde `Program.cs`:

```csharp
app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY)
   .MapHome(FIXED_RATE_LIMITING_POLICY);
app.MapEndpoints(FIXED_RATE_LIMITING_POLICY, generalConfiguration.Cache);
```

---

## Configuración en appsettings

```json
"MaxCallsPerMinute": 100
```

---

## Variable de entorno (producción)

| Variable    | Descripción                       | Ejemplo |
|-------------|-----------------------------------|---------|
| `MAX_CALLS` | Máximo de requests por minuto     | `200`   |

---

## Respuesta al exceder el límite

```
HTTP 429 Too Many Requests
Retry-After: 60
Body: Demasiados requests. Intente más tarde.
```
