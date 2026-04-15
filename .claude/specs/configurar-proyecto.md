# Spec: Configuración de proyecto Bisoft Atenea

## Propósito

Define los contratos de configuración que todo proyecto Atenea DEBE cumplir una vez creado desde el template. Complementa [Spec: Creación de proyecto](crear-proyecto.md) — cubre la configuración de cada subsistema individual.

> **Referencia cruzada:** Para la creación inicial desde template NuGet, ver → [crear-proyecto.md](crear-proyecto.md). Para el detalle de cada subsistema, consultar los specs de `configurar-*` individuales.

---

## SC-CFG-01: Program.cs sigue el orden estricto de bootstrap

El archivo `Program.cs` DEBE respetar el orden exacto definido en CLAUDE.md. Cualquier desviación causa errores en runtime difíciles de diagnosticar.

**Orden de servicios obligatorio:**
`ConfigureAuthentication` → `ConfigureApiVersioning` → `ConfigureSwagger` → `ConfigureCors` → `ConfigureHealthChecks` → `ConfigureLogger` → `ConfigureContexts` → `ConfigureServices` → `InjectConfigurations` → `ConfigureRateLimiter` → `ConfigureLocalization` → `ConfigureTelemetry` → `ConfigureAutomatedServices` → `AddAuthorization`

**Orden del pipeline obligatorio:**
`UseCors` → `UseRateLimiter` → `UseAuthentication` → `UseAuthorization` → `UseRequestLocalization` → `UseMiddleware<ErrorHandlerMiddleware>`

---

## SC-CFG-02: GeneralConfiguration es el único objeto de configuración

Todos los `ConfigureXxx()` reciben `generalConfiguration` (tipo `GeneralConfiguration`), nunca `IConfiguration` directamente. `GetConfiguration()` valida y encapsula toda la configuración en startup.

```csharp
// CORRECTO
var generalConfiguration = builder.Configuration.GetConfiguration();
builder.Services.ConfigureCors(generalConfiguration);

// INCORRECTO
builder.Services.ConfigureCors(builder.Configuration);
```

---

## SC-CFG-03: Autenticación configurada según el esquema elegido

El esquema de autenticación se define **antes de comenzar** (SC-PROJ — preguntar al desarrollador). Los esquemas soportados son JWT, API Key, Cookies, OAuth 2.0/OIDC y Mixto. Para el detalle de cada uno, ver → [Spec: Autenticación](configurar-autenticacion.md).

Si se usa JWT, el endpoint `/auth/refresh` es obligatorio.

---

## SC-CFG-04: CORS configurado con orígenes explícitos

La política `ALLOW_ALL_CORS_POLICY` DEBE definirse con orígenes concretos leídos de configuración. Nunca `AllowAnyOrigin()` en producción. El header `X-Pagination` DEBE estar expuesto. Ver → [Spec: CORS](configurar-cors.md).

---

## SC-CFG-05: Los 4 endpoints de health check están registrados

Los endpoints `/health-check`, `/health-details`, `/health/live` y `/health/ready` son obligatorios en todo proyecto. Ver → [Spec: Health Checks](configurar-health-checks.md).

---

## SC-CFG-06: Rate limiting aplicado con constante FIXED_RATE_LIMITING_POLICY

La constante `FIXED_RATE_LIMITING_POLICY` se define en el mismo archivo que la llama. El límite se lee de `generalConfiguration.MaxCallsPerMinute`. Ver → [Spec: Rate Limiting](configurar-rate-limiting.md).

---

## SC-CFG-07: Logger configurado con los 3 sinks por entorno

Serilog DEBE configurarse con: Console (siempre), SQLite (Development), GrafanaLoki (producción, condicional con Telemetría). Ver → [Spec: Logger](configurar-logger.md).

---

## SC-CFG-08: ErrorHandlerMiddleware es el único manejador de errores

No se usa `app.UseExceptionHandler()` ni `app.UseDeveloperExceptionPage()`. El único mecanismo es `UseMiddleware<ErrorHandlerMiddleware>()`. Ver → [Spec: Error Handler](configurar-error-handler.md).

---

## SC-CFG-09: Swagger solo en Development

`UseVersionedSwagger()` va dentro de `if (app.Environment.IsDevelopment())`. Nunca en producción. Ver → [Spec: Swagger](configurar-swagger.md).

---

## SC-CFG-10: Telemetría deshabilitada por defecto

La sección `Telemetry` en `appsettings.json` tiene `Enabled: false`. Se activa explícitamente por entorno vía variable de entorno `TELEMETRY_ENABLED`. Ver → [Spec: Telemetría](configurar-telemetria.md).

---

## SC-CFG-11: Background Services con registro condicional

Cada servicio automatizado se registra condicionalmente según `{Servicio}Enabled` en configuración. Nunca se registra incondicionalmente un `IHostedService`. Ver → [Spec: Background Services](crear-background-service.md).

---

## SC-CFG-12: AddMemoryCache condicional según CacheEnabled

```csharp
if (generalConfiguration.Cache.CacheEnabled)
    builder.Services.AddMemoryCache();
```

Nunca registrar `AddMemoryCache()` si no hay repositorios cached activos.

---

## Checklist de verificación

Al terminar de configurar un proyecto, verificar:

- [ ] `dotnet build` sin errores ni warnings
- [ ] `dotnet run` arranca sin excepciones de startup
- [ ] GET `/` responde con la página de bienvenida
- [ ] GET `/health-check` responde `Healthy`
- [ ] GET `/health/live` responde 200
- [ ] GET `/health/ready` responde 200
- [ ] Swagger accesible en Development en `/swagger`
- [ ] Todos los `ConfigureXxx` usan `generalConfiguration`, no `builder.Configuration`
- [ ] `TException.SetComponentPrefix` es la primera línea de `Program.cs`
