# Spec: Configurar Rate Limiting

## Contratos

---

### SC-RL-01 — Límite como int en GeneralConfiguration, no como DTO

**Justificación**: El número máximo de llamadas es un escalar simple. No requiere un DTO propio que agregue complejidad. Se gestiona directamente como `int` en `GeneralConfiguration` y se lee y valida en `ConfigurationsReader`.

**Correcto**:
```csharp
// GeneralConfiguration.cs
public required int RateLimiterMaxCalls { get; set; }

// ConfigurationsReader.cs
RateLimiterMaxCalls = configuration.GetRateLimiterMaxCalls(),
```

**Incorrecto**:
```csharp
// No crear un DTO para un solo valor escalar
public class RateLimiterConfiguration
{
    public int MaxCallsPerMinute { get; set; }
}
public required RateLimiterConfiguration RateLimiter { get; set; }
```

---

### SC-RL-02 — Prioridad variable de entorno sobre appsettings

**Justificación**: En producción el límite de llamadas puede diferir por entorno o cliente sin necesidad de modificar archivos de configuración. La variable de entorno `MAX_CALLS` sobreescribe el valor de appsettings mediante `TryOverwriteWithEnviromentValue`.

**Correcto**:
```csharp
var value = configuration["MaxCallsPerMinute"].TryOverwriteWithEnviromentValue("MAX_CALLS");
```

**Incorrecto**:
```csharp
// Leer solo de appsettings, sin soporte para variable de entorno
var maxCalls = configuration.GetValue<int>("RateLimiterMaxCalls");
```

---

### SC-RL-03 — Fallo explícito si falta o es inválido

**Justificación**: Un rate limiter sin límite configurado dejaría la API sin protección silenciosamente. Fallar al inicio con `TEnvironmentException` expone el error de configuración en el momento del arranque, no en tiempo de ejecución.

**Correcto**:
```csharp
value.ValidateNull(TEnvironmentException.MissingConfiguration(
        TEnvironmentException.Sources.APPSETTINGS,
        "Falta la máxima cantidad de llamadas"
    ));
if (!int.TryParse(value, out var result))
    throw TEnvironmentException.InvalidConfiguration(
        TEnvironmentException.Sources.APPSETTINGS,
        "La máxima cantidad de llamadas tiene un valor inválido"
    );
```

**Incorrecto**:
```csharp
// Usar valor por defecto silencioso
var maxCalls = configuration.GetValue<int>("MaxCallsPerMinute", defaultValue: 100);
```

---

### SC-RL-04 — FixedWindowLimiter con ventana de 1 minuto

**Justificación**: La ventana fija de 1 minuto es el estándar del template. Una ventana en segundos produciría rechazos agresivos para cualquier cliente legítimo que haga más de N llamadas por segundo.

**Correcto**:
```csharp
_.AddFixedWindowLimiter(policyName: FIXED_RATE_LIMITING_POLICY, options =>
{
    options.PermitLimit = configuration.RateLimiterMaxCalls;
    options.Window = TimeSpan.FromMinutes(1);
    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    options.QueueLimit = 0;
});
```

**Incorrecto**:
```csharp
options.Window = TimeSpan.FromSeconds(1);   // ventana demasiado corta
options.QueueLimit = 10;                     // cola innecesaria que retrasa respuestas
```

---

### SC-RL-05 — OnRejected con Retry-After y HTTP 429

**Justificación**: El header `Retry-After` permite al cliente saber cuánto tiempo esperar antes de reintentar. Sin él, el cliente debe adivinar. La respuesta debe ser HTTP 429 (no 503 ni otro código) con un mensaje en español consistente con el resto de la API.

**Correcto**:
```csharp
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
```

**Incorrecto**:
```csharp
// Solo código de estado sin cuerpo ni Retry-After
_.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
```

---

### SC-RL-06 — Constante FIXED_RATE_LIMITING_POLICY para nombre de política

**Justificación**: El nombre de la política se usa en el registro (`AddFixedWindowLimiter`) y en cada grupo de endpoints (`RequireRateLimiting`). Una constante garantiza consistencia y evita errores por typo que resultarían en que el rate limiter no se aplique silenciosamente.

**Correcto**:
```csharp
// ApiConstants.cs
public const string FIXED_RATE_LIMITING_POLICY = "Fixed";

// ServiceExtensions.cs
_.AddFixedWindowLimiter(policyName: FIXED_RATE_LIMITING_POLICY, ...);

// Program.cs
app.MapEndpoints(FIXED_RATE_LIMITING_POLICY, generalConfiguration.Cache);
```

**Incorrecto**:
```csharp
_.AddFixedWindowLimiter(policyName: "Fixed", ...);
app.MapEndpoints("fixed", generalConfiguration.Cache);   // casing distinto → política no encontrada
```

---

### SC-RL-07 — UseRateLimiter después de UseCors y antes de UseAuthentication

**Justificación**: CORS debe procesar primero las solicitudes OPTIONS de preflight. Si `UseRateLimiter` va antes, los preflight consumen cuota del rate limiter y pueden recibir 429 aunque el cliente legítimo aún no haya excedido el límite real.

**Correcto**:
```csharp
app.UseCors(ALLOW_ALL_CORS_POLICY)
   .UseRateLimiter()
   .UseAuthentication()
   .UseAuthorization()
```

**Incorrecto**:
```csharp
app.UseRateLimiter()      // antes de CORS — penaliza preflight
   .UseCors(ALLOW_ALL_CORS_POLICY)
   .UseAuthentication()
```

---

### SC-RL-08 — RequireRateLimiting aplicado a todos los grupos de endpoints

**Justificación**: La política debe cubrir todos los endpoints del sistema: API, health checks y home. No aplicarla a alguno crea un vector de abuso donde un cliente malicioso puede saturar el servidor a través de los endpoints sin límite.

**Correcto**:
```csharp
// Grupo API
app.MapGroup("api").RequireRateLimiting(rateLimitingPolicy)...

// Health checks
app.MapHealthChecks("/health-check").AllowAnonymous().RequireRateLimiting(rateLimitingPolicy);
app.AddLiveness().RequireRateLimiting(rateLimitingPolicy);
app.AddReadiness().RequireRateLimiting(rateLimitingPolicy);

// Home
app.Home().RequireRateLimiting(rateLimitingPolicy);
```

**Incorrecto**:
```csharp
// Omitir health checks o home del rate limiter
app.MapGroup("api").RequireRateLimiting(rateLimitingPolicy);
app.MapHealthChecks("/health-check");   // sin RequireRateLimiting → vector de abuso
```
