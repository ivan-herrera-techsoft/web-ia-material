---
description: Convenciones para Health Checks en la capa API
globs: "**/HealthChecks/**,**/Endpoints/HealthChecksMapping*"
---

## Endpoints obligatorios

Todo proyecto expone exactamente estos cuatro endpoints, mapeados en `Api/Extensions/Endpoints/HealthChecksMapping.cs`:

| Endpoint          | Proposito                                              | Predicate                      |
|-------------------|--------------------------------------------------------|--------------------------------|
| `/health-check`   | Estado general — anonimo, sin filtro                   | Sin predicate                  |
| `/health-details` | Detalle JSON via `UIResponseWriter` — anonimo          | Sin predicate                  |
| `/health/live`    | Liveness: la app esta viva (K8s)                       | Solo check `"Liveness"`        |
| `/health/ready`   | Readiness: lista para recibir trafico (K8s)            | Checks con tag `"ready"`       |

---

## Mapeo de endpoints (HealthChecksMapping.cs)

Archivo: `Api/Extensions/Endpoints/HealthChecksMapping.cs`  
Clase: `public static partial class WebApplicationExtensions`

```csharp
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace {Namespace}.Api.Extensions.Endpoints;

public static partial class WebApplicationExtensions
{
    // Sin rate limiting
    public static WebApplication AddHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health-check").AllowAnonymous();
        app.HealthDetails();
        app.AddLiveness();
        app.AddReadiness();
        return app;
    }

    // Con rate limiting
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

---

## Registro de checks (ServiceExtensions.cs)

El metodo `ConfigureHealthChecks` vive en `ServiceExtensions.cs`. Siempre incluye:

- **Liveness** — lambda que retorna `Healthy` con la version del ensamblado; nombre fijo `"Liveness"`
- **Un check de BD por contexto** — instancia directa de `DatabaseHealthCheck`; tag `"ready"`

```csharp
public static IServiceCollection ConfigureHealthChecks(
    this IServiceCollection services,
    GeneralConfiguration configuration)
{
    services.AddHealthChecks()
        .AddCheck("Liveness", () => HealthCheckResult.Healthy($"API iniciada correctamente. v{ASSEMBLY_VERSION}"))
        .AddCheck("Storage", new DatabaseHealthCheck(configuration.{NombreConexion}), tags: ["ready"]);
        // Un .AddCheck("Storage{X}", new DatabaseHealthCheck(...), tags: ["ready"]) por cada contexto adicional
    return services;
}
```

---

## Checks personalizados

### DatabaseHealthCheck

Archivo: `Api/Helpers/HealthChecks/DatabaseHealthCheck.cs`  
Verifica conectividad via `ConnectionStringValidatorFactory` de `Bisoft.DatabaseConnections`.

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

### HttpHealthCheck

Archivo: `Api/Helpers/HealthChecks/HttpHealthCheck.cs`  
Verifica disponibilidad de un servicio HTTP externo. Opcionalmente valida autenticacion con un login previo.

```csharp
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

Registro de `HttpHealthCheck` en `ConfigureHealthChecks`:

```csharp
.AddCheck("ServicioExterno", new HttpHealthCheck(configuration.UrlServicioExterno), tags: ["ready"])

// Con validacion de login
.AddCheck("ServicioExterno", new HttpHealthCheck(
    configuration.UrlServicioExterno,
    configuration.UrlLogin,
    new { usuario = configuration.Usuario, password = configuration.Password }
), tags: ["ready"])
```

---

## Registro en Program.cs

```csharp
// Registro de servicios
builder.Services.ConfigureHealthChecks(generalConfiguration);

// Pipeline — con rate limiting
app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY);

// Pipeline — sin rate limiting
app.AddHealthChecks();
```

---

## Naming y reglas

- Clase check: `{Descripcion}HealthCheck` — ej. `DatabaseHealthCheck`, `HttpHealthCheck`
- Nombre del check en `AddCheck`: `"Liveness"` fijo para liveness; descriptivo en español para los demas (`"Storage"`, `"ServicioNotificaciones"`)
- Tag `"ready"` en todos los checks que no sean liveness
- Los checks deben ser rapidos (< 5 s) — sin operaciones bloqueantes largas
- No exponer datos sensibles (connection strings, tokens) en la descripcion del resultado
