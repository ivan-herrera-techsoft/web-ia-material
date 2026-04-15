---
description: Referencia rápida del orden obligatorio de Program.cs — servicios, pipeline y reglas críticas
globs: "**/Program.cs,**/Extensions/ServiceExtensions*,**/Extensions/WebApplication*"
---

## Orden obligatorio de Program.cs

### 1. SetComponentPrefix — PRIMERA instrucción

```csharp
TException.SetComponentPrefix("PREFIJO");  // antes de todo, incluso de CreateBuilder
```

### 2. Builder y configuración

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.SetEncryption();                          // antes de GetConfiguration
var generalConfiguration = builder.Configuration.GetConfiguration();  // único objeto de config
```

### 3. Registro de servicios — orden exacto

```csharp
builder.Services
    .ConfigureAuthentication(generalConfiguration)    // 1
    .ConfigureApiVersioning()                         // 2
    .ConfigureSwagger()                               // 3
    .ConfigureCors(generalConfiguration)              // 4
    .ConfigureHealthChecks(generalConfiguration)      // 5
    .ConfigureLogger(generalConfiguration)            // 6
    .ConfigureContexts(generalConfiguration)          // 7
    .ConfigureServices(generalConfiguration)          // 8
    .InjectConfigurations(generalConfiguration)       // 9
    .ConfigureRateLimiter(generalConfiguration)       // 10
    .ConfigureLocalization()                          // 11
    .ConfigureTelemetry(generalConfiguration)         // 12
    .ConfigureAutomatedServices(generalConfiguration) // 13
    .AddAuthorization();                              // 14

if (generalConfiguration.Cache.CacheEnabled)
    builder.Services.AddMemoryCache();               // condicional
```

### 4. Build y pipeline — orden exacto

```csharp
var app = builder.Build();

app.UseCors(ALLOW_ALL_CORS_POLICY)          // 1 — siempre primero (preflight OPTIONS)
   .UseRateLimiter()                         // 2
   .UseAuthentication()                      // 3 — antes de Authorization
   .UseAuthorization()                       // 4
   .UseRequestLocalization(...)             // 5
   .UseMiddleware<ErrorHandlerMiddleware>(); // 6 — siempre último antes de endpoints

if (generalConfiguration.Telemetry.Enabled)
    app.UseOpenTelemetryPrometheusScrapingEndpoint();  // condicional

if (app.Environment.IsDevelopment())
    app.UseVersionedSwagger();                          // solo Development

app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY)
   .MapHome(FIXED_RATE_LIMITING_POLICY)
   .MapEndpoints(FIXED_RATE_LIMITING_POLICY, generalConfiguration.Cache);
```

---

## Reglas críticas

- `TException.SetComponentPrefix` es la **primera línea** — antes de `WebApplication.CreateBuilder`
- `SetEncryption()` va antes de `GetConfiguration()` — encripta `SensitiveData` antes de leerlo
- Todos los `ConfigureXxx` reciben `generalConfiguration`, **nunca** `builder.Configuration`
- `UseAuthentication` siempre **antes** de `UseAuthorization`
- `UseCors` siempre **primero** en el pipeline
- `UseMiddleware<ErrorHandlerMiddleware>` siempre **último** antes de endpoints
- `UseVersionedSwagger` solo dentro de `if (IsDevelopment())`
- `AddMemoryCache` y `UseOpenTelemetryPrometheusScrapingEndpoint` son condicionales

---

## GeneralConfiguration — patrón de configuración

`GetConfiguration()` retorna un objeto `GeneralConfiguration` validado. Los `ConfigurationsReader` individuales leen cada sección, aplican `TryOverwriteWithEnviromentValue` para soporte de variables de entorno, y lanzan `TEnvironmentException` si los valores son inválidos — garantizando fallo en startup, no en runtime.

```csharp
// CORRECTO
builder.Services.ConfigureCors(generalConfiguration);

// INCORRECTO — nunca pasar IConfiguration directamente
builder.Services.ConfigureCors(builder.Configuration);
```
