---
name: configurar-health-checks
description: Configura los cuatro endpoints de health check obligatorios, DatabaseHealthCheck y opcionalmente HttpHealthCheck
argument-hint: Sin argumentos — aplica al proyecto actual
---

Configurar los health checks del proyecto.

Antes de generar los archivos, **preguntar al desarrollador**:
1. ¿Cuántos contextos de BD tiene el proyecto? (un `DatabaseHealthCheck` por contexto)
2. ¿Hay servicios HTTP externos que deban monitorearse? (para agregar `HttpHealthCheck`)
3. ¿El servicio HTTP externo requiere autenticacion previa? (para el overload con `urlLogin`)
4. ¿Se usa rate limiting en el proyecto? (para decidir que overload de `AddHealthChecks` usar)

---

## Paso 1 — DatabaseHealthCheck

Crear `Api/Helpers/HealthChecks/DatabaseHealthCheck.cs`:

```csharp
using Bisoft.DatabaseConnections.Configuration;
using Bisoft.DatabaseConnections.Factories;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace {Namespace}.Api.Helpers.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbConnectionConfiguration _config;

    public DatabaseHealthCheck(IDbConnectionConfiguration config)
    {
        _config = config;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var factory = ConnectionStringValidatorFactory.GetConnectionStringValidator(_config);
        var canConnect = factory.CanConnect(_config);
        return Task.FromResult(canConnect
            ? new HealthCheckResult(HealthStatus.Healthy, "Servicio de almacenamiento disponible.")
            : new HealthCheckResult(context.Registration.FailureStatus, "Servicio de almacenamiento no disponible."));
    }
}
```

---

## Paso 2 — HttpHealthCheck (si hay servicios HTTP externos)

Crear `Api/Helpers/HealthChecks/HttpHealthCheck.cs`:

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text;
using System.Text.Json;

namespace {Namespace}.Api.Helpers.HealthChecks;

public class HttpHealthCheck : IHealthCheck
{
    private readonly string _url;
    private readonly string _urlLogin;
    private readonly object? _loginRequest;

    public HttpHealthCheck(string url, string urlLogin = "", object? loginRequest = null)
    {
        _url = url;
        _urlLogin = urlLogin;
        _loginRequest = loginRequest;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient();
        try
        {
            var response = await httpClient.GetAsync(_url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return HealthCheckResult.Unhealthy("Conexión no disponible con el servicio.");

            if (!string.IsNullOrEmpty(_urlLogin) && _loginRequest != null)
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(_loginRequest), Encoding.UTF8, "application/json");
                var loginResponse = await httpClient.PostAsync(_urlLogin, content, cancellationToken);
                if (!loginResponse.IsSuccessStatusCode)
                    return HealthCheckResult.Unhealthy("Acceso denegado al servicio.");
            }
            return HealthCheckResult.Healthy("Conexión disponible con el servicio.");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("Conexión no disponible con el servicio.");
        }
    }
}
```

---

## Paso 3 — Mapeo de endpoints

Crear `Api/Extensions/Endpoints/HealthChecksMapping.cs`:

```csharp
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace {Namespace}.Api.Extensions.Endpoints;

public static partial class WebApplicationExtensions
{
    public static WebApplication AddHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health-check").AllowAnonymous();
        app.HealthDetails();
        app.AddLiveness();
        app.AddReadiness();
        return app;
    }

    public static WebApplication AddHealthChecks(this WebApplication app, string rateLimitingPolicy)
    {
        app.MapHealthChecks("/health-check").AllowAnonymous().RequireRateLimiting(rateLimitingPolicy);
        app.HealthDetails().RequireRateLimiting(rateLimitingPolicy);
        app.AddLiveness().RequireRateLimiting(rateLimitingPolicy);
        app.AddReadiness().RequireRateLimiting(rateLimitingPolicy);
        return app;
    }

    private static IEndpointConventionBuilder HealthDetails(this WebApplication app)
        => app.MapHealthChecks("/health-details", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }).AllowAnonymous();

    private static IEndpointConventionBuilder AddLiveness(this WebApplication app)
        => app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = (check) => check.Name == "Liveness"
        });

    private static IEndpointConventionBuilder AddReadiness(this WebApplication app)
        => app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = (check) => check.Tags.Contains("ready")
        });
}
```

**Importante:** esta clase es `partial` para poder coexistir con `WebApplicationExtensions.cs` que tiene `MapEndpoints` y `UseVersionedSwagger`.

---

## Paso 4 — Registro en ServiceExtensions

En `Api/Extensions/ServiceExtensions.cs`, agregar o completar `ConfigureHealthChecks`:

```csharp
public static IServiceCollection ConfigureHealthChecks(
    this IServiceCollection services,
    GeneralConfiguration configuration)
{
    services.AddHealthChecks()
        // Liveness siempre presente — lambda con version del ensamblado
        .AddCheck("Liveness", () => HealthCheckResult.Healthy($"API iniciada correctamente. v{ASSEMBLY_VERSION}"))
        // Un check por contexto de BD — tag "ready" obligatorio
        .AddCheck("Storage", new DatabaseHealthCheck(configuration.{NombreConexion}), tags: ["ready"]);
        // Si hay servicios HTTP externos:
        // .AddCheck("ServicioExterno", new HttpHealthCheck(configuration.UrlServicio), tags: ["ready"])
    return services;
}
```

---

## Paso 5 — Registro en Program.cs

Verificar que `Program.cs` tenga ambas llamadas:

```csharp
// Registro de servicios
builder.Services.ConfigureHealthChecks(generalConfiguration);

// Pipeline — preferir el overload con rate limiting si el proyecto lo usa
app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY);
```

---

## Resumen de archivos generados

```
Api/
├── Helpers/
│   └── HealthChecks/
│       ├── DatabaseHealthCheck.cs
│       └── HttpHealthCheck.cs          (solo si hay servicios HTTP externos)
└── Extensions/
    └── Endpoints/
        └── HealthChecksMapping.cs
```

Modificaciones en:
- `Api/Extensions/ServiceExtensions.cs` → método `ConfigureHealthChecks`
- `Api/Program.cs` → llamada a `app.AddHealthChecks(...)`
