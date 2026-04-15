# Spec: Creación de proyecto Bisoft Atenea

Contratos transversales de un proyecto nuevo. Cada contrato cubre requisitos que
abarcan múltiples archivos y no pueden verificarse con los specs individuales.
Para el detalle de cada subsistema, consultar el spec correspondiente.

---

## SC-PROJ-00 — El proyecto se genera desde el template NuGet Bisoft.Templates.Atenea.MinimalApi

Un proyecto Atenea nuevo **debe** crearse usando el template oficial del feed privado de
NuGet (Azure DevOps Artifacts), no manualmente archivo por archivo. Esto garantiza que
la estructura de capas, el orden de bootstrap y los archivos base estén correctos desde
el inicio.

**Correcto**
```bash
# Instalar el template desde la fuente privada
dotnet new install Bisoft.Templates.Atenea.MinimalApi \
  --nuget-source https://pkgs.dev.azure.com/Bisoft/_packaging/Bisoft/nuget/v3/index.json

# Generar la solución
dotnet new bisoft-atenea -n Company.Product.Module -o Company.Product.Module
```

Tras la generación, actualizar obligatoriamente los placeholders:
- `ApiConstants.APP_NAME` → nombre real del servicio
- `TException.SetComponentPrefix("PREFIJO")` → prefijo de 3 letras del módulo
- `Jwt:Key` en `appsettings.json` → clave segura ≥ 32 caracteres
- `Jwt:Issuer` / `Jwt:Audience` → nombre real del servicio
- `ConnectionStrings` → cadenas de conexión reales

**Incorrecto**
```bash
# ❌ Crear el proyecto manualmente con dotnet new webapi — no tiene la estructura Atenea
dotnet new webapi -n Company.Product.Module

# ❌ Copiar y pegar archivos de otro proyecto sin usar el template — introduce deuda técnica
```

> El template incluye: las cuatro capas, `Program.cs` con bootstrap correcto, todas las
> secciones obligatorias de `appsettings.json`, `ErrorHandlerMiddleware`, `SharedResources`,
> `HomeMapping`, `HealthChecksMapping`, `ConfigurationsReader` y los readers individuales.
> Si el template no está disponible (feed caído, credenciales expiradas), usar el skill
> `configurar-proyecto` en modo manual como fallback documentado.

---

## SC-PROJ-01 — TException.SetComponentPrefix es la primera instrucción de Program.cs

`TException.SetComponentPrefix(prefijo)` **debe** ser la primera instrucción ejecutada
dentro del bloque `try` de `Program.Main`. Ningún otro servicio ni configuración puede
llamarse antes.

**Correcto**
```csharp
public static async Task Main(string[] args)
{
    try
    {
        TException.SetComponentPrefix("GEN");          // ← primera línea
        builder.Configuration.SetEncryption();
        var generalConfiguration = builder.Configuration.GetConfiguration();
        // ...
    }
}
```

**Incorrecto**
```csharp
// ❌ SetComponentPrefix después de leer configuración — las excepciones de startup
//    ya se habrían lanzado sin el prefijo correcto
var generalConfiguration = builder.Configuration.GetConfiguration();
TException.SetComponentPrefix("GEN");
```

> Ver: `configurar-localizacion.md` → SC-LOC-01

---

## SC-PROJ-02 — GetConfiguration() valida toda la configuración en startup

Toda la configuración del proyecto **debe** leerse y validarse en startup a través de
`configuration.GetConfiguration()`, que devuelve un `GeneralConfiguration` validado.
Este objeto se pasa a todos los `ConfigureXxx`. No se lee `IConfiguration` directamente
en los métodos de registro.

**Correcto**
```csharp
builder.Configuration.SetEncryption();
var generalConfiguration = builder.Configuration.GetConfiguration();  // valida todo

builder.Services
    .ConfigureAuthentication(generalConfiguration)
    .ConfigureCors(generalConfiguration)
    .ConfigureLogger(generalConfiguration)
    // todos reciben GeneralConfiguration
```

**Incorrecto**
```csharp
// ❌ IConfiguration pasada directamente — cada método lee sin validar
builder.Services
    .ConfigureAuthentication(builder.Configuration)
    .ConfigureCors(builder.Configuration)
```

> Ver: `configurar-autenticacion.md` → SC-AUTH-01 — `configurar-telemetria.md` → SC-TEL-01

---

## SC-PROJ-03 — Orden exacto del pipeline de middleware

El pipeline **debe** seguir este orden sin excepción. Alterarlo rompe CORS preflight,
autenticación o localización:

```csharp
app.UseCors(ALLOW_ALL_CORS_POLICY)      // 1 — CORS primero (preflight OPTIONS)
   .UseRateLimiter()                    // 2 — limitar antes de autenticar
   .UseAuthentication()                 // 3 — antes de Authorization
   .UseAuthorization()                  // 4
   .UseRequestLocalization(...)         // 5 — después de auth
   .UseMiddleware<ErrorHandlerMiddleware>(); // 6 — último middleware

if (generalConfiguration.Telemetry.Enabled)
    app.UseOpenTelemetryPrometheusScrapingEndpoint(); // 7 — condicional, antes de health checks

if (app.Environment.IsDevelopment())
    app.UseVersionedSwagger();          // 8 — solo desarrollo

app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY)
   .MapHome(FIXED_RATE_LIMITING_POLICY); // 9 — encadenados
app.MapEndpoints(...);                  // 10 — endpoints de negocio
```

**Incorrecto**
```csharp
// ❌ UseAuthorization antes de UseAuthentication
app.UseAuthorization();
app.UseAuthentication();

// ❌ UseCors después de UseAuthentication — el preflight OPTIONS falla
app.UseAuthentication();
app.UseCors(ALLOW_ALL_CORS_POLICY);

// ❌ ErrorHandlerMiddleware antes de UseAuthorization — no captura 401/403 del pipeline
app.UseMiddleware<ErrorHandlerMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
```

> Ver: `configurar-cors.md` → SC-CORS-02 — `configurar-autenticacion.md` → SC-AUTH-05
> `configurar-error-handler.md` — `configurar-telemetria.md` → SC-TEL-07

---

## SC-PROJ-04 — Orden de registro de servicios en builder.Services

El registro de servicios **debe** respetar este orden para evitar dependencias circulares
y garantizar que `IOptions<T>` esté disponible cuando los servicios que lo consumen se registran:

```csharp
builder.Services
    .ConfigureAuthentication(generalConfiguration)   // JWT + RefreshTokens
    .ConfigureApiVersioning()
    .ConfigureSwagger()
    .ConfigureCors(generalConfiguration)
    .ConfigureHealthChecks(generalConfiguration)
    .ConfigureLogger(generalConfiguration)
    .ConfigureContexts(generalConfiguration)         // DbContext
    .ConfigureServices(generalConfiguration)         // App + Domain Services + Repos
    .InjectConfigurations(generalConfiguration)      // AddSingleton(Options.Create(...))
    .ConfigureRateLimiter(generalConfiguration)
    .ConfigureLocalization()
    .ConfigureTelemetry(generalConfiguration)
    .ConfigureAutomatedServices(generalConfiguration)
    .AddAuthorization();

if (generalConfiguration.Cache.CacheEnabled)
    builder.Services.AddMemoryCache();               // condicional
```

**Incorrecto**
```csharp
// ❌ InjectConfigurations antes de ConfigureContexts — los contextos aún no están registrados
builder.Services
    .InjectConfigurations(generalConfiguration)
    .ConfigureContexts(generalConfiguration)

// ❌ AddAuthorization omitido — los endpoints con [Authorize] fallan en runtime
builder.Services
    .ConfigureAuthentication(generalConfiguration)
    // sin AddAuthorization()
```

---

## SC-PROJ-05 — APP_NAME actualizado y distinto del placeholder

`ApiConstants.APP_NAME` **debe** tener el nombre real del servicio. El valor `"PLCHLDR"`
o `"Security App"` del template son placeholders y nunca llegan a producción.

**Correcto**
```csharp
public const string APP_NAME = "Inventario";
```

**Incorrecto**
```csharp
// ❌ Nombre de placeholder del template — aparece en Loki, Prometheus y la home page
public const string APP_NAME = "PLCHLDR";
public const string APP_NAME = "Security App";
```

> Ver: `configurar-home.md` → SC-HOME-08 — `configurar-telemetria.md` → SC-TEL-08

---

## SC-PROJ-06 — Secciones obligatorias en appsettings.json

Todo proyecto **debe** tener las siguientes secciones en `appsettings.json`. Las
opcionales se incluyen cuando aplica.

```json
{
  "ConnectionStrings": {                      // obligatorio
    "{ModuloConnection}": ""
  },
  "MaxCallsPerMinute": 100,                   // obligatorio — leído por GetRateLimiterMaxCalls
  "Jwt": {                                    // obligatorio si usa JWT
    "Key": "",
    "Issuer": "",
    "Audience": "",
    "AccessDurationInMinutes": 60,
    "RefreshDurationInMinutes": 1440
  },
  "Cors": {                                   // obligatorio
    "AllowedOrigins": []
  },
  "Logger": {                                 // obligatorio
    "LogHttpRequests": false,
    "Sqlite": { "Path": "Logs\\Logs.db", "MinimumLevel": "Information" }
  },
  "AutomatedServices": {},                    // obligatorio (vacío si no hay services)
  "Cache": { "CacheEnabled": false },         // obligatorio
  "Telemetry": {                              // obligatorio
    "Enabled": false,
    "TracesDestination": "http://localhost:4318/v1/traces",
    "LogsDestination": "http://localhost:3100",
    "LogsMinimumLevel": "Error"
  },
  "SensitiveData": {                          // obligatorio
    "Jwt:Key": ""
  }
}
```

**Incorrecto**
```json
// ❌ Falta MaxCallsPerMinute — GetRateLimiterMaxCalls lanza TEnvironmentException en startup
// ❌ Falta SensitiveData — Jwt:Key queda en texto plano en producción
// ❌ Falta Telemetry — GetTelemetryConfiguration lanza al leer la sección
```

> Ver: `configurar-rate-limiting.md` — `configurar-autenticacion.md` → SC-AUTH-08
> `configurar-telemetria.md` — `configurar-logger.md`

---

## SC-PROJ-07 — Jwt:Key en el nodo SensitiveData para encriptación

La clave JWT **debe** estar referenciada en `SensitiveData` para que `SetEncryption()`
la encripte en producción (Windows, DPAPI). Sin esta referencia, la clave viaja en texto
plano en `appsettings.json`.

**Correcto**
```json
{
  "Jwt": { "Key": "clave-real-de-al-menos-32-caracteres" },
  "SensitiveData": {
    "Jwt:Key": ""
  }
}
```

**Incorrecto**
```json
// ❌ Sin nodo SensitiveData — la Key queda expuesta en texto plano
{
  "Jwt": { "Key": "mi-clave-secreta" }
}
```

> Ver: `configurar-autenticacion.md` → SC-AUTH-08

---

## SC-PROJ-08 — Nullable reference types habilitado en el .csproj

El proyecto **debe** tener `<Nullable>enable</Nullable>` en el archivo `.csproj`. Esto
es parte del estándar de la arquitectura Bisoft Atenea.

**Correcto**
```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

**Incorrecto**
```xml
<!-- ❌ Sin Nullable enable — no se detectan referencias nulas en compilación -->
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
</PropertyGroup>
```

---

## SC-PROJ-09 — UseVersionedSwagger solo en Development

Swagger **debe** activarse únicamente en el entorno de desarrollo. Nunca se expone en
producción.

**Correcto**
```csharp
if (app.Environment.IsDevelopment())
    app.UseVersionedSwagger();
```

**Incorrecto**
```csharp
// ❌ Swagger siempre activo — expone la API en producción
app.UseVersionedSwagger();
```

> Ver: `configurar-swagger.md`

---

## SC-PROJ-10 — AddMemoryCache condicional en CacheEnabled

`AddMemoryCache()` **debe** registrarse únicamente si `generalConfiguration.Cache.CacheEnabled == true`.

**Correcto**
```csharp
if (generalConfiguration.Cache.CacheEnabled)
    builder.Services.AddMemoryCache();
```

**Incorrecto**
```csharp
// ❌ Siempre registrado — consume memoria aunque el cache esté desactivado
builder.Services.AddMemoryCache();
```

> Ver: `configurar-cors.md` — `crear-cached-repository.md`

---

## SC-PROJ-11 — Estructura de capas Atenea completa

La solución **debe** tener exactamente estas capas para arquitectura Atenea:

```
Company.Product.Module.sln
├── Company.Product.Module.Api/           ← Minimal API, Endpoints, Extensions, Middlewares
├── Company.Product.Module.Application/   ← Services, Dtos
├── Company.Product.Module.Domain/        ← Entities, Contracts, Services, Validators, Exceptions
├── Company.Product.Module.Infrastructure/← Contexts, Repositories, Mapping, Strategies
└── Company.Product.Module.Test/          ← Pruebas unitarias e integración
```

Dependencias permitidas:
- `Api` → `Application` → `Domain` ← `Infrastructure`
- `Infrastructure` → `Domain` (implementa contratos)
- **Nunca** `Domain` → `Application` ni `Domain` → `Infrastructure`

**Incorrecto**
```
// ❌ Arquitectura Hermes con todo en un solo proyecto (no es Atenea)
Company.Product.Module.Api/   // contiene Application + Infrastructure también

// ❌ Infrastructure referencia Application
// ❌ Domain referencia Infrastructure
```

---

## SC-PROJ-12 — Excepciones tipadas de Bisoft.Exceptions, no excepciones base de C#

Las excepciones de negocio y configuración **deben** usar los tipos de `Bisoft.Exceptions`:
`TArgumentException`, `TNotFoundException`, `TInvalidOperationException`,
`TUnauthorizedAccessException`, `TEnvironmentException`. No se crean clases base propias.

**Correcto**
```csharp
// En Domain/Validators/
nombre.ValidateNull(TArgumentException.NullOrEmpty("nombre"));

// En Application/Services/
throw TNotFoundException.Create("canal", id);

// En Extensions/Configuration/
throw TEnvironmentException.InvalidConfiguration(
    TEnvironmentException.Sources.APPSETTINGS, "Configuración inválida");
```

**Incorrecto**
```csharp
// ❌ Excepciones base propias — no tienen códigos ni localización
public class NotFoundException(string mensaje) : Exception(mensaje);
public class ValidationException(string mensaje) : Exception(mensaje);

throw new NotFoundException($"Canal {id} no encontrado");
```

> Ver: `configurar-error-handler.md` — `crear-entidad.md` — `configurar-localizacion.md`

---

## SC-PROJ-13 — ErrorHandlerMiddleware maneja todas las excepciones del pipeline

El proyecto **debe** tener `ErrorHandlerMiddleware` registrado como último middleware
y mapeado para todos los tipos de `TException`. No se usan `app.UseExceptionHandler()`
ni `app.UseProblemDetails()` de forma independiente.

**Correcto**
```csharp
// En pipeline — último middleware
.UseMiddleware<ErrorHandlerMiddleware>();

// ErrorHandlerMiddleware captura TArgumentException, TNotFoundException,
// TInvalidOperationException, TUnauthorizedAccessException, TEnvironmentException
```

**Incorrecto**
```csharp
// ❌ UseExceptionHandler en lugar de ErrorHandlerMiddleware — no maneja TException
app.UseExceptionHandler("/error");

// ❌ Sin middleware de errores — las excepciones llegan como 500 sin formato
```

> Ver: `configurar-error-handler.md`

---

## SC-PROJ-14 — Health checks obligatorios: /health-check, /health-details, /health/live, /health/ready

Los cuatro endpoints de health **deben** estar registrados. `AddHealthChecks()` los
registra todos. No se registran manualmente endpoint por endpoint.

**Correcto**
```csharp
app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY)
   .MapHome(FIXED_RATE_LIMITING_POLICY);
// Registra: /health-check, /health-details, /health/live, /health/ready
```

**Incorrecto**
```csharp
// ❌ Solo /health-check — faltan /health/live y /health/ready para orquestadores
app.MapHealthChecks("/health-check").AllowAnonymous();
```

> Ver: `configurar-health-checks.md`

---

## Checklist de verificación de proyecto nuevo

Usar esta lista para validar que el proyecto está correctamente configurado antes de
hacer el primer commit:

### Estructura
- [ ] Cuatro capas: Api, Application, Domain, Infrastructure (+ Test)
- [ ] `<Nullable>enable</Nullable>` en todos los `.csproj`
- [ ] Namespaces file-scoped en todos los archivos

### Program.cs
- [ ] `TException.SetComponentPrefix(prefijo)` — primera línea del try
- [ ] `builder.Configuration.SetEncryption()` — antes de `GetConfiguration()`
- [ ] `GetConfiguration()` devuelve `GeneralConfiguration` validado
- [ ] Todos los `ConfigureXxx` reciben `GeneralConfiguration`
- [ ] Orden de middleware: CORS → Rate → Auth → Authz → Localization → ErrorHandler
- [ ] `AddMemoryCache()` condicional en `CacheEnabled`
- [ ] `UseOpenTelemetryPrometheusScrapingEndpoint()` condicional en `Telemetry.Enabled`
- [ ] `UseVersionedSwagger()` solo en `IsDevelopment()`
- [ ] `AddHealthChecks(...).MapHome(...)` encadenados

### ApiConstants
- [ ] `APP_NAME` ≠ `"PLCHLDR"` ni `"Security App"`
- [ ] `ASSEMBLY_VERSION` desde `Assembly.GetExecutingAssembly()`
- [ ] `ALLOW_ALL_CORS_POLICY`, `FIXED_RATE_LIMITING_POLICY` definidos

### appsettings.json
- [ ] Sección `ConnectionStrings` con las conexiones del módulo
- [ ] Sección `MaxCallsPerMinute` con valor entero
- [ ] Sección `Jwt` con Key, Issuer, Audience, duraciones
- [ ] Sección `Cors` con `AllowedOrigins`
- [ ] Sección `Logger` con configuración Sqlite
- [ ] Sección `AutomatedServices` (vacío si no aplica)
- [ ] Sección `Cache` con `CacheEnabled`
- [ ] Sección `Telemetry` con `Enabled: false` y destinos
- [ ] Nodo `SensitiveData` con `"Jwt:Key": ""`

### Subsistemas (verificar spec correspondiente)
- [ ] CORS → `configurar-cors.md`
- [ ] Rate Limiting → `configurar-rate-limiting.md`
- [ ] API Versioning → `configurar-api-versioning.md`
- [ ] Swagger → `configurar-swagger.md`
- [ ] Logger (Serilog) → `configurar-logger.md`
- [ ] Error Handler Middleware → `configurar-error-handler.md`
- [ ] Localización + SharedResources → `configurar-localizacion.md`
- [ ] Health Checks → `configurar-health-checks.md`
- [ ] Home page → `configurar-home.md`
- [ ] Autenticación JWT → `configurar-autenticacion.md`
- [ ] Telemetría → `configurar-telemetria.md`
- [ ] Paginación (si el módulo lista recursos) → `configurar-paginacion.md`
- [ ] Background Services (si aplica) → `crear-background-service.md`
