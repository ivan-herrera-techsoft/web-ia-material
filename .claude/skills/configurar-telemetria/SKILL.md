---
name: configurar-telemetria
description: Configura OpenTelemetry (trazas OTLP + métricas Prometheus) con correlación Serilog/Loki
---

Configurar telemetría con OpenTelemetry para el proyecto actual.

## 1. Agregar paquetes NuGet

```xml
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="*" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="*" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="*" />
<PackageReference Include="Serilog.Sinks.Grafana.Loki" Version="*" />
```

## 2. DTO TelemetryConfiguration

Crear `Api/Dtos/Configurations/TelemetryConfiguration.cs`:

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

Agregar la propiedad a `GeneralConfiguration`:

```csharp
public required TelemetryConfiguration Telemetry { get; set; }
```

## 3. TelemetryConfigurationsReader

Crear `Api/Extensions/Configuration/TelemetryConfigurationsReader.cs`:

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

Agregar la llamada en `ConfigurationsReader.cs → GetConfiguration()`:

```csharp
Telemetry = configuration.GetTelemetryConfiguration(),
```

## 4. TraceEnricher

Crear `Api/Helpers/Telemetry/TraceEnricher.cs`:

```csharp
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

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

## 5. Integración con Serilog (LoggerConfigurationExtensions)

En `Api/Extensions/LoggerConfigurationExtensions.cs`, dentro del método `AddConfiguration`,
agregar el bloque condicional de telemetría:

```csharp
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

## 6. ConfigureTelemetry en ServiceExtensions

En `Api/Extensions/ServiceExtensions.cs`, agregar:

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
                options.Endpoint            = new Uri(configuration.Telemetry.TracesDestination);
                options.Protocol            = OtlpExportProtocol.HttpProtobuf;
                options.ExportProcessorType = ExportProcessorType.Simple;
            }))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter());

    return services;
}
```

Registrar en `Program.cs` junto a los demás servicios:

```csharp
builder.Services
    // ...
    .ConfigureTelemetry(generalConfiguration);
```

## 7. Prometheus scraping endpoint en Program.cs

Agregar condicionalmente **antes** de `AddHealthChecks` y `MapHome`:

```csharp
if (generalConfiguration.Telemetry.Enabled)
    app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY)
   .MapHome(FIXED_RATE_LIMITING_POLICY);
```

## 8. appsettings.json

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

## Checklist

- [ ] Paquetes NuGet de OpenTelemetry agregados
- [ ] `TelemetryConfiguration` creado en `Dtos/Configurations/`
- [ ] `Telemetry` agregado a `GeneralConfiguration`
- [ ] `TelemetryConfigurationsReader.cs` creado con los tres helpers y env var overrides
- [ ] `GetTelemetryConfiguration()` llamado desde `GetConfiguration()` en `ConfigurationsReader`
- [ ] `TraceEnricher.cs` creado en `Helpers/Telemetry/`
- [ ] Bloque Loki + `TraceEnricher` agregado al logger condicionalmente
- [ ] `ConfigureTelemetry()` con guard `if (!Enabled) return services`
- [ ] Protocolo OTLP: `HttpProtobuf`, `ExportProcessorType.Simple`
- [ ] `UseOpenTelemetryPrometheusScrapingEndpoint()` condicional en Program.cs
- [ ] Sección `Telemetry` en appsettings.json con `Enabled: false` por defecto
