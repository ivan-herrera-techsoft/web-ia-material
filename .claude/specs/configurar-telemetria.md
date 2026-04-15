# Spec: Telemetría con OpenTelemetry

Contratos de implementación para la configuración de OpenTelemetry en Bisoft Atenea.

---

## SC-TEL-01 — ConfigureTelemetry recibe GeneralConfiguration, no IConfiguration

`ConfigureTelemetry` **debe** recibir `GeneralConfiguration` como parámetro. No accede a
`IConfiguration` directamente ni lee secciones por string.

**Correcto**
```csharp
public static IServiceCollection ConfigureTelemetry(
    this IServiceCollection services,
    GeneralConfiguration configuration)
{
    if (!configuration.Telemetry.Enabled)
        return services;
    // usa configuration.Telemetry.TracesDestination, etc.
}
```

**Incorrecto**
```csharp
// ❌ Recibe IConfiguration directamente — salta la validación del reader
public static IServiceCollection ConfigureTelemetry(
    this IServiceCollection services,
    IConfiguration configuration)
{
    if (!configuration.GetValue<bool>("Telemetry:Enabled"))
        return services;
}
```

> `GeneralConfiguration` ya contiene la configuración validada por `TelemetryConfigurationsReader`.
> Leer directamente de `IConfiguration` en `ConfigureTelemetry` duplica la lógica y evita la
> validación de URLs y tipos en startup.

---

## SC-TEL-02 — Guard al inicio: si Enabled == false, retornar sin registrar

La primera línea de `ConfigureTelemetry` **debe** ser el guard de habilitación. Si la
telemetría está desactivada, el método retorna inmediatamente sin registrar ningún servicio
de OpenTelemetry.

**Correcto**
```csharp
public static IServiceCollection ConfigureTelemetry(
    this IServiceCollection services, GeneralConfiguration configuration)
{
    if (!configuration.Telemetry.Enabled)
        return services;

    services.AddOpenTelemetry()...;
    return services;
}
```

**Incorrecto**
```csharp
// ❌ AddOpenTelemetry siempre se llama — registra servicios aunque esté desactivado
public static IServiceCollection ConfigureTelemetry(...)
{
    services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            if (!configuration.Telemetry.Enabled) return;
            tracing.AddOtlpExporter(...);
        });
    return services;
}
```

---

## SC-TEL-03 — Protocolo OTLP: HttpProtobuf con ExportProcessorType.Simple

El exportador de trazas **debe** usar `OtlpExportProtocol.HttpProtobuf` y
`ExportProcessorType.Simple`. No se usa gRPC.

**Correcto**
```csharp
.AddOtlpExporter(options =>
{
    options.Endpoint            = new Uri(configuration.Telemetry.TracesDestination);
    options.Protocol            = OtlpExportProtocol.HttpProtobuf;
    options.ExportProcessorType = ExportProcessorType.Simple;
})
```

**Incorrecto**
```csharp
// ❌ Sin especificar protocolo — usa gRPC por defecto, incompatible con Grafana Tempo HTTP
.AddOtlpExporter(options =>
{
    options.Endpoint = new Uri(configuration.Telemetry.TracesDestination);
})

// ❌ Batch en ambientes de volumen moderado — innecesario y agrega complejidad
options.ExportProcessorType = ExportProcessorType.Batch;
```

---

## SC-TEL-04 — TelemetryConfigurationsReader valida URLs y tipos en startup

El reader **debe** validar que las URLs sean correctas (`.ToUrlWithoutSlash`) y que
`Enabled` sea un booleano válido (`.ToBool`) antes de construir el `TelemetryConfiguration`.
Todas las propiedades soportan override por variable de entorno.

**Correcto**
```csharp
private static string GetTelemetryDestination(this IConfiguration configuration, string telemetryParameter)
{
    var value = configuration[$"Telemetry:{telemetryParameter}Destination"]
        .TryOverwriteWithEnviromentValue($"TELEMETRY_{telemetryParameter.ToUpper()}_DESTINATION");
    return value.ToUrlWithoutSlash(TEnvironmentException.InvalidConfiguration(
        TEnvironmentException.Sources.APPSETTINGS,
        $"El valor de '{telemetryParameter}Destination' debe ser una URL válida, pero se recibió '{value}'"));
}
```

**Incorrecto**
```csharp
// ❌ Sin validación — una URL malformada falla en runtime, no en startup
private static TelemetryConfiguration GetTelemetryConfiguration(this IConfiguration configuration)
{
    return new TelemetryConfiguration
    {
        TracesDestination = configuration["Telemetry:TracesDestination"]!,
        LogsDestination   = configuration["Telemetry:LogsDestination"]!
    };
}
```

---

## SC-TEL-05 — TraceEnricher agrega TraceId y SpanId a los logs

`TraceEnricher` **debe** leer `Activity.Current` y agregar tanto `TraceId` como `SpanId`
como propiedades de cada evento de Serilog. No se incluye solo uno de los dos.

**Correcto**
```csharp
public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
{
    var activity = Activity.Current;
    if (activity != null)
    {
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("SpanId",  activity.SpanId.ToString()));
    }
}
```

**Incorrecto**
```csharp
// ❌ Solo TraceId — no permite correlacionar con el span exacto en Grafana
logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));

// ❌ Sin null-check de Activity.Current — falla fuera de contexto HTTP
logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("TraceId", Activity.Current.TraceId.ToString()));
```

---

## SC-TEL-06 — TraceEnricher y GrafanaLoki solo se agregan cuando Enabled == true

La integración del enricher y el sink de Loki en el logger **debe** estar dentro de un
bloque condicional `if (configuration.Telemetry.Enabled)`.

**Correcto**
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

**Incorrecto**
```csharp
// ❌ Siempre agrega GrafanaLoki aunque la telemetría esté desactivada
loggerConfiguration
    .Enrich.With<TraceEnricher>()
    .WriteTo.GrafanaLoki(configuration.Telemetry.LogsDestination, ...);
```

---

## SC-TEL-07 — UseOpenTelemetryPrometheusScrapingEndpoint condicional en Program.cs

El endpoint `/metrics` **debe** activarse condicionalmente según `Telemetry.Enabled`,
y registrarse **antes** de `AddHealthChecks` y `MapHome`.

**Correcto**
```csharp
if (generalConfiguration.Telemetry.Enabled)
    app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY)
   .MapHome(FIXED_RATE_LIMITING_POLICY);
```

**Incorrecto**
```csharp
// ❌ Siempre registra el endpoint aunque OpenTelemetry no esté configurado
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// ❌ Después de AddHealthChecks — orden incorrecto
app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY).MapHome(FIXED_RATE_LIMITING_POLICY);
app.UseOpenTelemetryPrometheusScrapingEndpoint();
```

---

## SC-TEL-08 — serviceName y serviceVersion desde ApiConstants

El recurso de OpenTelemetry **debe** usar `ApiConstants.APP_NAME` como `serviceName`
y `ApiConstants.ASSEMBLY_VERSION` como `serviceVersion`. No se hardcodean strings.

**Correcto**
```csharp
.ConfigureResource(resource => resource
    .AddService(
        serviceName:    APP_NAME,           // ApiConstants.APP_NAME
        serviceVersion: ASSEMBLY_VERSION))  // ApiConstants.ASSEMBLY_VERSION
```

**Incorrecto**
```csharp
// ❌ Nombre hardcodeado — no respeta la constante del proyecto
.ConfigureResource(resource => resource
    .AddService(serviceName: "MiServicio", serviceVersion: "1.0.0"))
```
