---
description: Convenciones para telemetría con OpenTelemetry en Bisoft Atenea
globs: "**/Extensions/Configuration/TelemetryConfigurationsReader.cs,**/Helpers/Telemetry/**,**/Extensions/ServiceExtensions.cs"
---

## Telemetría con OpenTelemetry

La telemetría es **opcional** y se habilita vía `Telemetry:Enabled`. Cuando está activa
proporciona trazas distribuidas (OTLP → Tempo/Jaeger) y métricas (Prometheus). Se integra
con Serilog para correlacionar logs con trazas usando `TraceId` y `SpanId`.

## TelemetryConfiguration — DTO

En `Api/Dtos/Configurations/TelemetryConfiguration.cs`:

```csharp
namespace Company.Product.Module.Api.Dtos.Configurations;

public class TelemetryConfiguration
{
    public bool Enabled { get; set; }
    public required string TracesDestination { get; set; }
    public required string LogsDestination { get; set; }
    public LogEventLevel LogsMinimumLevel { get; set; }
}
```

Se expone como propiedad `Telemetry` de `GeneralConfiguration`:

```csharp
public class GeneralConfiguration
{
    public required TelemetryConfiguration Telemetry { get; set; }
    // ... resto de propiedades
}
```

## TelemetryConfigurationsReader

En `Api/Extensions/Configuration/TelemetryConfigurationsReader.cs`, clase `partial` de
`ConfigurationExtensions`:

```csharp
namespace Company.Product.Module.Api.Extensions.Configuration;

public static partial class ConfigurationExtensions
{
    private static TelemetryConfiguration GetTelemetryConfiguration(this IConfiguration configuration)
    {
        return new TelemetryConfiguration
        {
            Enabled           = configuration.GetTelemetryEnabled(),
            TracesDestination = configuration.GetTelemetryDestination("Traces"),
            LogsDestination   = configuration.GetTelemetryDestination("Logs"),
            LogsMinimumLevel  = ParseLogEventLevel(
                configuration["Telemetry:LogsMinimumLevel"]
                    .TryOverwriteWithEnviromentValue("TELEMETRY_LOGS_MINIMUM_LEVEL"))
        };
    }

    private static bool GetTelemetryEnabled(this IConfiguration configuration)
    {
        var value = configuration["Telemetry:Enabled"]
            .TryOverwriteWithEnviromentValue("TELEMETRY_ENABLED");
        return value.ToBool(TEnvironmentException.InvalidConfiguration(
            TEnvironmentException.Sources.APPSETTINGS,
            $"El valor de 'Enabled' de Telemetry debe ser un booleano válido, pero se recibió '{value}'"));
    }

    private static string GetTelemetryDestination(this IConfiguration configuration, string telemetryParameter)
    {
        var value = configuration[$"Telemetry:{telemetryParameter}Destination"]
            .TryOverwriteWithEnviromentValue($"TELEMETRY_{telemetryParameter.ToUpper()}_DESTINATION");
        return value.ToUrlWithoutSlash(TEnvironmentException.InvalidConfiguration(
            TEnvironmentException.Sources.APPSETTINGS,
            $"El valor de '{telemetryParameter}Destination' de Telemetry debe ser una URL válida, pero se recibió '{value}'"));
    }
}
```

Se invoca desde `GetConfiguration()` en `ConfigurationsReader.cs`:

```csharp
Telemetry = configuration.GetTelemetryConfiguration(),
```

### Variables de entorno de override

| Clave appsettings                   | Variable de entorno                  |
|-------------------------------------|--------------------------------------|
| `Telemetry:Enabled`                 | `TELEMETRY_ENABLED`                  |
| `Telemetry:TracesDestination`       | `TELEMETRY_TRACES_DESTINATION`       |
| `Telemetry:LogsDestination`         | `TELEMETRY_LOGS_DESTINATION`         |
| `Telemetry:LogsMinimumLevel`        | `TELEMETRY_LOGS_MINIMUM_LEVEL`       |

### Helpers de validación usados

- `.TryOverwriteWithEnviromentValue(envVar)` — sobreescribe el valor si la variable de entorno existe.
- `.ToBool(exceptionWhenInvalid)` — parsea a `bool`; lanza `TEnvironmentException` si el valor no es válido.
- `.ToUrlWithoutSlash(exceptionWhenInvalid)` — valida que sea una URL bien formada sin slash final.
- `ParseLogEventLevel(string)` — parsea el nivel de Serilog; helper compartido en `ConfigurationExtensions`.

## ConfigureTelemetry — registro de OpenTelemetry

En `Api/Extensions/ServiceExtensions.cs`. Recibe `GeneralConfiguration`, **no** `IConfiguration`:

```csharp
public static IServiceCollection ConfigureTelemetry(
    this IServiceCollection services,
    GeneralConfiguration configuration)
{
    if (!configuration.Telemetry.Enabled)
        return services;

    services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(
                serviceName: APP_NAME,
                serviceVersion: ASSEMBLY_VERSION))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint        = new Uri(configuration.Telemetry.TracesDestination);
                options.Protocol        = OtlpExportProtocol.HttpProtobuf;
                options.ExportProcessorType = ExportProcessorType.Simple;
            }))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter());

    return services;
}
```

Puntos clave:
- **Guard al inicio**: si `Telemetry.Enabled == false`, retorna sin registrar nada.
- **Protocolo OTLP**: `HttpProtobuf` (no gRPC). Compatible con Grafana Tempo y Jaeger.
- **`ExportProcessorType.Simple`**: exporta spans síncronamente — adecuado para ambientes
  con volumen moderado. En alta carga considerar `Batch`.
- **Métricas con Prometheus**: `AddPrometheusExporter()` habilita el scraping endpoint.

## TraceEnricher — correlación logs ↔ trazas

En `Api/Helpers/Telemetry/TraceEnricher.cs`. Implementa `ILogEventEnricher` de Serilog:

```csharp
namespace Company.Product.Module.Api.Helpers.Telemetry;

public class TraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            logEvent.AddOrUpdateProperty(
                propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
            logEvent.AddOrUpdateProperty(
                propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
        }
    }
}
```

Lee `Activity.Current` de `System.Diagnostics` (integrado con OpenTelemetry) y agrega
`TraceId` y `SpanId` a cada evento de log, lo que permite correlacionar logs de Serilog
con las trazas en Grafana.

## Integración con Serilog (Logger)

Cuando `Telemetry.Enabled == true`, el logger agrega el enricher y el sink de Loki:

```csharp
// En LoggerConfigurationExtensions.AddConfiguration()
if (configuration.Telemetry.Enabled)
{
    loggerConfiguration
        .Enrich.With<TraceEnricher>()
        .WriteTo.GrafanaLoki(
            configuration.Telemetry.LogsDestination,
            restrictedToMinimumLevel: configuration.Telemetry.LogsMinimumLevel,
            labels:
            [
                new LokiLabel { Key = "service_name", Value = ApiConstants.APP_NAME },
                new LokiLabel { Key = "traceId",      Value = "{TraceId}" },
                new LokiLabel { Key = "spanId",        Value = "{SpanId}" }
            ]);
}
```

Los labels `traceId` y `spanId` en Loki permiten navegar desde un log directamente
a la traza correspondiente en Grafana Tempo.

## Endpoint de Prometheus — Program.cs

El endpoint `/metrics` se activa condicionalmente después de construir la app:

```csharp
if (generalConfiguration.Telemetry.Enabled)
    app.UseOpenTelemetryPrometheusScrapingEndpoint();
```

Se registra **antes** de `AddHealthChecks` y `MapHome` pero **después** del middleware
de autenticación y autorización. No requiere autenticación (Prometheus lo accede directamente).

## appsettings.json

```json
{
  "Telemetry": {
    "Enabled": false,
    "TracesDestination": "http://localhost:4318/v1/traces",
    "LogsDestination":   "http://localhost:3100",
    "LogsMinimumLevel":  "Error"
  }
}
```

- `Enabled: false` en desarrollo por defecto — se activa vía env var en producción.
- `TracesDestination`: endpoint OTLP/HTTP del collector (Grafana Tempo, Jaeger, etc.).
- `LogsDestination`: endpoint de Grafana Loki (sin `/loki/api/v1/push` — la librería lo agrega).
- `LogsMinimumLevel`: nivel mínimo para enviar a Loki (`Error` en prod, `Warning` en staging).

## Reglas críticas

- `ConfigureTelemetry` recibe `GeneralConfiguration`, no `IConfiguration` directamente.
- El guard `if (!Enabled) return services` es la primera línea del método.
- `TraceEnricher` solo se agrega al logger cuando `Telemetry.Enabled == true`.
- `UseOpenTelemetryPrometheusScrapingEndpoint()` solo se llama cuando `Enabled == true`.
- Protocolo siempre `OtlpExportProtocol.HttpProtobuf` (no gRPC).
- `APP_NAME` y `ASSEMBLY_VERSION` de `ApiConstants` como `serviceName` y `serviceVersion`.
- `LogsDestination` no debe tener slash al final — validado con `.ToUrlWithoutSlash()`.
