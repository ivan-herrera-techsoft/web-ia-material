# Skill: Configurar Rate Limiting

Configura el limitador de peticiones (Rate Limiter) con ventana fija en un proyecto Atenea Minimal API.

---

## Paso 1 — Propiedad en GeneralConfiguration

Agregar la propiedad del límite como `int` directo (no requiere DTO):

```csharp
// Api/Dtos/Configurations/GeneralConfiguration.cs
public required int RateLimiterMaxCalls { get; set; }
```

---

## Paso 2 — Lector de configuración

En `Api/Extensions/Configuration/ConfigurationsReader.cs`, dentro de la clase `ConfigurationExtensions` (partial), agregar el método privado y asignarlo en `GetConfiguration()`:

```csharp
// Método privado de lectura
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

Asignar en `GetConfiguration()`:

```csharp
return new GeneralConfiguration()
{
    ...
    RateLimiterMaxCalls = configuration.GetRateLimiterMaxCalls(),
    ...
};
```

---

## Paso 3 — Constante de política en ApiConstants

Verificar que `Api/Helpers/ApiConstants.cs` contiene:

```csharp
public const string FIXED_RATE_LIMITING_POLICY = "Fixed";
```

Si no existe, agregarla.

---

## Paso 4 — ConfigureRateLimiter en ServiceExtensions

En `Api/Extensions/ServiceExtensions.cs` agregar:

```csharp
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

Encadenar en `Program.cs`:

```csharp
builder.Services
    ...
    .ConfigureRateLimiter(generalConfiguration)
    ...
```

---

## Paso 5 — Middleware pipeline

En `Program.cs`, verificar que `UseRateLimiter` está después de `UseCors` y antes de `UseAuthentication`:

```csharp
app.UseCors(ALLOW_ALL_CORS_POLICY)
   .UseRateLimiter()
   .UseAuthentication()
   .UseAuthorization()
   ...
```

---

## Paso 6 — Aplicar política a endpoints

La constante `FIXED_RATE_LIMITING_POLICY` se pasa como parámetro a las extensiones de mapeo en `Program.cs`:

```csharp
app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY)
   .MapHome(FIXED_RATE_LIMITING_POLICY);
app.MapEndpoints(FIXED_RATE_LIMITING_POLICY, generalConfiguration.Cache);
```

Cada método de mapeo aplica `RequireRateLimiting(rateLimitingPolicy)` a sus rutas internamente:

```csharp
// WebApplicationExtensions.cs
var apiEndpoints = app.MapGroup("api")
                      .RequireRateLimiting(rateLimitingPolicy)
                      ...

// HealthChecksMapping.cs
app.MapHealthChecks("/health-check").AllowAnonymous().RequireRateLimiting(rateLimitingPolicy);
```

---

## Paso 7 — Configuración en appsettings

En `appsettings.json` y `appsettings.Development.json`:

```json
"MaxCallsPerMinute": 100
```

Ajustar el valor según el entorno. En producción se puede sobreescribir con la variable de entorno `MAX_CALLS`.

---

## Verificación

- [ ] `GeneralConfiguration` tiene `required int RateLimiterMaxCalls`
- [ ] `GetRateLimiterMaxCalls` lee `MAX_CALLS` env var primero, luego `MaxCallsPerMinute` appsettings
- [ ] Lanza `TEnvironmentException.MissingConfiguration` si falta y `InvalidConfiguration` si no es número
- [ ] `FIXED_RATE_LIMITING_POLICY = "Fixed"` existe en `ApiConstants`
- [ ] `OnRejected` agrega `Retry-After` header y devuelve HTTP 429
- [ ] Ventana fija de **1 minuto** (`TimeSpan.FromMinutes(1)`)
- [ ] `QueueLimit = 0` (sin cola)
- [ ] `UseRateLimiter()` está después de `UseCors` en el pipeline
- [ ] Todos los grupos de endpoints aplican `RequireRateLimiting`
