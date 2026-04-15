---
name: configurar-proyecto
description: Configura la estructura inicial de un proyecto Bisoft Atenea (.NET 10 Minimal API)
argument-hint: Company.Product.Module ej. "Bisoft.Inventario"
disable-model-invocation: true
---

Antes de crear el proyecto, preguntar al desarrollador:

1. **Tipo de arquitectura**: Hermes (pequeño), Atenea (microservicio) o Titan (gran escala)
2. **Esquema de autenticación**: API Key, Cookies, JWT, OAuth 2.0/OIDC o Mixto
3. **Proveedor de base de datos**: SqlServer, PostgreSQL, SQLite o multi-proveedor
4. **Features opcionales**: Cache, Background Services, Telemetría, Notificaciones

---

## 0. Crear el proyecto desde el template NuGet (recomendado)

El proyecto se genera automáticamente usando el template oficial de Bisoft disponible
en la fuente privada de NuGet (Azure DevOps Artifacts). Esto crea toda la estructura
de capas, archivos base y configuración de arranque listos para personalizar.

### 0.1 Verificar que la fuente privada está configurada

La fuente privada debe estar en `nuget.config` en la raíz de la solución:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="Bisoft" value="https://pkgs.dev.azure.com/Bisoft/_packaging/Bisoft/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

Si no existe, crearla antes de continuar. Las credenciales deben estar configuradas
en el gestor de credenciales de Windows o mediante `dotnet nuget add source`.

### 0.2 Instalar el template

```bash
dotnet new install Bisoft.Templates.Atenea.MinimalApi --nuget-source https://pkgs.dev.azure.com/Bisoft/_packaging/Bisoft/nuget/v3/index.json
```

Verificar que se instaló correctamente:

```bash
dotnet new list | grep bisoft
```

### 0.3 Crear la solución desde el template

```bash
dotnet new bisoft-atenea -n $ARGUMENTS -o $ARGUMENTS
```

Esto genera la solución completa con:
- Las cuatro capas (Api, Application, Domain, Infrastructure)
- `Program.cs` con el orden correcto de bootstrap
- `appsettings.json` con todas las secciones obligatorias
- `ErrorHandlerMiddleware`, `SharedResources`, `HomeMapping`, `HealthChecksMapping`
- `ConfigurationsReader` y readers individuales por subsistema
- `ApiConstants` con los valores placeholder

### 0.4 Actualizar placeholders tras la generación

Después de generar el proyecto, actualizar estos valores obligatoriamente:

| Archivo                    | Campo                        | Valor a cambiar                   |
|----------------------------|------------------------------|-----------------------------------|
| `ApiConstants.cs`          | `APP_NAME`                   | Nombre real del servicio          |
| `Program.cs`               | `SetComponentPrefix("PREFIJO")`| Prefijo de 3 letras del módulo  |
| `appsettings.json`         | `Jwt:Issuer`, `Jwt:Audience` | Nombre real del servicio          |
| `appsettings.json`         | `Jwt:Key`                    | Clave segura ≥ 32 caracteres      |
| `ConnectionStrings`        | Cadenas de conexión          | Conexión real a la BD             |

> Si el template no está disponible o hay problemas con la fuente privada, continuar
> con los pasos manuales desde el **paso 1** en adelante.

---

## 1. Estructura de solución

```
$ARGUMENTS.sln
├── $ARGUMENTS.Api/
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Helpers/
│   │   └── ApiConstants.cs
│   ├── Endpoints/
│   ├── BackgroundServices/
│   ├── Middlewares/
│   │   └── ErrorHandlerMiddleware.cs
│   ├── Helpers/HealthChecks/
│   │   └── DatabaseHealthCheck.cs
│   ├── Seeders/
│   ├── Resources/
│   │   ├── SharedResources.cs
│   │   ├── SharedResources.resx
│   │   ├── SharedResources.es-MX.resx
│   │   └── SharedResources.es-GT.resx
│   ├── Services/
│   │   └── TokenService.cs          (si usa JWT)
│   └── Extensions/
│       ├── ServiceExtensions.cs
│       ├── WebApplicationExtensions.cs
│       ├── LoggerConfigurationExtensions.cs
│       └── Configuration/
│           ├── ConfigurationsReader.cs
│           ├── JwtConfigurationsReader.cs
│           ├── TelemetryConfigurationsReader.cs
│           ├── CacheConfigurationsReader.cs
│           ├── CorsConfigurationsReader.cs
│           └── AutomatedServicesConfigurationsReader.cs
│       └── Endpoints/
│           ├── HomeMapping.cs
│           └── HealthChecksMapping.cs
├── $ARGUMENTS.Application/
│   ├── Services/
│   └── Dtos/
├── $ARGUMENTS.Domain/
│   ├── Entities/
│   ├── Contracts/Repositories/
│   ├── Services/
│   ├── Validators/
│   └── DomainConstants.cs
├── $ARGUMENTS.Infrastructure/
│   ├── Contexts/
│   ├── Repositories/
│   ├── Mapping/
│   └── Strategies/
└── $ARGUMENTS.Test/
```

## 2. .csproj — configuración obligatoria

En todos los proyectos de la solución:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

> `<Nullable>enable</Nullable>` es obligatorio en todos los proyectos.

## 3. ApiConstants

En `Api/Helpers/ApiConstants.cs` — actualizar `APP_NAME` con el nombre real del servicio:

```csharp
namespace $ARGUMENTS.Api.Helpers;

public static class ApiConstants
{
    // ⚠ Cambiar "NombreDelServicio" por el nombre real — aparece en Loki, Prometheus y Home
    public const string APP_NAME = "NombreDelServicio";

    public static class Cookies
    {
        public const string ACCESS_TOKEN  = "accessToken";
        public const string REFRESH_TOKEN = "refreshToken";
    }
    public static class Claims
    {
        public const string USER_ID = "userid";
    }

    private static readonly ApiVersion _version1 = new(1, 0);
    public static ApiVersion VERSION_1 => _version1;

    public static string LOGGER_TABLE_NAME      => $"{APP_NAME} Logs";
    public const string  ALLOW_ALL_CORS_POLICY  = "AllowAll";
    public const string  FIXED_RATE_LIMITING_POLICY = "Fixed";
    public const int     MAX_PAGE_SIZE = 100;
    public const int     MIN_PAGE_SIZE = 1;

    public static string ASSEMBLY_VERSION =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
}
```

> **Nunca** dejar `APP_NAME = "PLCHLDR"` ni `APP_NAME = "Security App"` en producción.

## 4. GeneralConfiguration y DTOs de configuración

Crear en `Api/Dtos/Configurations/`:

```csharp
// GeneralConfiguration.cs
public class GeneralConfiguration
{
    public required IDbConnectionConfiguration    SeguridadConnection  { get; set; }
    public required GeneralLoggerConfiguration    Logger               { get; set; }
    public required JwtConfigurations             Jwt                  { get; set; }
    public required TelemetryConfiguration        Telemetry            { get; set; }
    public required ApiCacheConfiguration         Cache                { get; set; }
    public required AutomatedServicesConfigurations AutomatedServices  { get; set; }
    public required CorsConfiguration             Cors                 { get; set; }
    public required int                           RateLimiterMaxCalls  { get; set; }
}
```

Crear los demás DTOs de configuración siguiendo los specs de cada subsistema:
- `JwtConfigurations`, `AccessTokensConfigurations` → `configurar-autenticacion`
- `TelemetryConfiguration` → `configurar-telemetria`
- `ApiCacheConfiguration`, `CorsConfiguration` → `configurar-cors`, `configurar-paginacion`

## 5. ConfigurationsReader

Crear `Api/Extensions/Configuration/ConfigurationsReader.cs`:

```csharp
namespace $ARGUMENTS.Api.Extensions.Configuration;

public static partial class ConfigurationExtensions
{
    public static GeneralConfiguration GetConfiguration(this IConfiguration configuration)
    {
        var seguridadConnection = configuration.GetConnectionConfiguration("Seguridad");
        return new GeneralConfiguration
        {
            SeguridadConnection = seguridadConnection,
            Logger              = configuration.GetGeneralLoggerConfiguration(seguridadConnection),
            Jwt                 = configuration.GetJwtConfiguration(),
            RateLimiterMaxCalls = configuration.GetRateLimiterMaxCalls(),
            Telemetry           = configuration.GetTelemetryConfiguration(),
            AutomatedServices   = configuration.GetAutomatedServicesConfigurations(),
            Cors                = configuration.GetCorsConfiguration(),
            Cache               = configuration.GetCacheConfiguration()
        };
    }

    private static int GetRateLimiterMaxCalls(this IConfiguration configuration)
    {
        var value = configuration["MaxCallsPerMinute"]
            .TryOverwriteWithEnviromentValue("MAX_CALLS");
        value.ValidateNull(TEnvironmentException.MissingConfiguration(
            TEnvironmentException.Sources.APPSETTINGS, "Falta MaxCallsPerMinute"));
        if (!int.TryParse(value, out var result))
            throw TEnvironmentException.InvalidConfiguration(
                TEnvironmentException.Sources.APPSETTINGS, "MaxCallsPerMinute tiene un valor inválido");
        return result;
    }

    public static IConfiguration SetEncryption(this IConfiguration configuration)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            configuration.UseSentitiveProtection(_ =>
            {
                _.Scope = DataProtectionScope.LocalMachine;
            });
        }
        return configuration;
    }
}
```

Crear los readers individuales para cada subsistema (ver specs correspondientes):
`JwtConfigurationsReader.cs`, `TelemetryConfigurationsReader.cs`, `CacheConfigurationsReader.cs`,
`CorsConfigurationsReader.cs`, `AutomatedServicesConfigurationsReader.cs`.

## 6. Program.cs

Seguir **este orden estricto**. Ver los comentarios de contrato de cada bloque:

```csharp
namespace $ARGUMENTS.Api;

public static class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            // ─── BOOTSTRAP ────────────────────────────────────────────────
            // SC-PROJ-01: SetComponentPrefix es la PRIMERA instrucción
            TException.SetComponentPrefix("PREFIJO");          // ← cambiar por prefijo real (3 letras)

            var builder = WebApplication.CreateBuilder(args);

            // SC-PROJ-02: SetEncryption antes de GetConfiguration
            builder.Configuration.SetEncryption();

            // SC-PROJ-02: GetConfiguration valida TODA la configuración en startup
            var generalConfiguration = builder.Configuration.GetConfiguration();

            // ─── REGISTRO DE SERVICIOS (SC-PROJ-04) ───────────────────────
            builder.Services
                .ConfigureAuthentication(generalConfiguration)   // JWT
                .ConfigureApiVersioning()
                .ConfigureSwagger()
                .ConfigureCors(generalConfiguration)
                .ConfigureHealthChecks(generalConfiguration)
                .ConfigureLogger(generalConfiguration)
                .ConfigureContexts(generalConfiguration)         // DbContext
                .ConfigureServices(generalConfiguration)         // App + Domain + Repos
                .InjectConfigurations(generalConfiguration)      // AddSingleton(Options.Create(...))
                .ConfigureRateLimiter(generalConfiguration)
                .ConfigureLocalization()
                .ConfigureTelemetry(generalConfiguration)
                .ConfigureAutomatedServices(generalConfiguration)
                .AddAuthorization();

            // SC-PROJ-10: AddMemoryCache condicional
            if (generalConfiguration.Cache.CacheEnabled)
                builder.Services.AddMemoryCache();

            // Servicios de paquetes externos (si aplica)
            builder.Services.ConfigureRefreshTokensServices(generalConfiguration.Jwt.RefreshTokens);

            var app = builder.Build();

            // ─── PIPELINE DE MIDDLEWARE (SC-PROJ-03) ──────────────────────
            app.UseCors(ALLOW_ALL_CORS_POLICY)          // 1 — CORS primero (preflight OPTIONS)
               .UseRateLimiter()                        // 2
               .UseAuthentication()                     // 3 — antes de UseAuthorization
               .UseAuthorization()                      // 4
               .UseRequestLocalization(
                   app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value)
               .UseMiddleware<ErrorHandlerMiddleware>(); // 6 — último middleware

            // 7 — Prometheus: condicional, antes de health checks
            if (generalConfiguration.Telemetry.Enabled)
                app.UseOpenTelemetryPrometheusScrapingEndpoint();

            // 8 — Swagger: solo en Development
            if (app.Environment.IsDevelopment())
                app.UseVersionedSwagger();

            // 9 — Health checks + Home encadenados
            app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY)
               .MapHome(FIXED_RATE_LIMITING_POLICY);

            // 10 — Endpoints de negocio
            app.MapEndpoints(FIXED_RATE_LIMITING_POLICY, generalConfiguration.Cache);

            // ─── SEEDERS ─────────────────────────────────────────────────
            // await AppSeeder.SeedAsync(app.Services);

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            await ManageException(ex);
        }
    }

    private static async Task ManageException(Exception exception)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("Logs\\app.log")
            .Destructure.ToMaximumDepth(3)
            .CreateLogger();
        Log.Logger.Fatal(exception, "Error al configurar la aplicación");
        await Task.CompletedTask;
    }
}
```

> **No usar excepciones propias de dominio** (`NotFoundException`, `ValidationException`, etc.).
> Usar exclusivamente los tipos de `Bisoft.Exceptions`:
> `TArgumentException`, `TNotFoundException`, `TInvalidOperationException`,
> `TUnauthorizedAccessException`, `TEnvironmentException`. (SC-PROJ-12)

## 7. appsettings.json

Estructura completa obligatoria (SC-PROJ-06 y SC-PROJ-07):

```json
{
  "ConnectionStrings": {
    "Seguridad": ""
  },
  "MaxCallsPerMinute": 100,
  "Jwt": {
    "Key": "",
    "Issuer": "$ARGUMENTS",
    "Audience": "$ARGUMENTS",
    "AccessDurationInMinutes": 60,
    "RefreshDurationInMinutes": 1440
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:4200" ]
  },
  "Logger": {
    "LogHttpRequests": false,
    "Sqlite": {
      "Path": "Logs\\Logs.db",
      "MinimumLevel": "Information"
    }
  },
  "AutomatedServices": {},
  "Cache": {
    "CacheEnabled": false
  },
  "Telemetry": {
    "Enabled": false,
    "TracesDestination": "http://localhost:4318/v1/traces",
    "LogsDestination": "http://localhost:3100",
    "LogsMinimumLevel": "Error"
  },
  "SensitiveData": {
    "Jwt:Key": ""
  }
}
```

> `SensitiveData` es obligatorio. `Jwt:Key` debe referenciarse aquí para ser encriptado
> por `SetEncryption()` en producción (Windows, DPAPI). (SC-PROJ-07)

## 8. Subsistemas — consultar specs individuales

Cada subsistema tiene su propio spec con contratos detallados:

| Subsistema           | Spec                          | Skill                          |
|----------------------|-------------------------------|--------------------------------|
| CORS                 | `configurar-cors.md`          | `configurar-cors`              |
| Rate Limiting        | `configurar-rate-limiting.md` | `configurar-rate-limiting`     |
| API Versioning       | `configurar-api-versioning.md`| `configurar-api-versioning`    |
| Swagger              | `configurar-swagger.md`       | `configurar-swagger`           |
| Logger               | `configurar-logger.md`        | `configurar-logger`            |
| Error Handler        | `configurar-error-handler.md` | `configurar-error-handler`     |
| Localización         | `configurar-localizacion.md`  | `configurar-localizacion`      |
| Health Checks        | `configurar-health-checks.md` | `configurar-health-checks`     |
| Home page            | `configurar-home.md`          | `configurar-home`              |
| Autenticación JWT    | `configurar-autenticacion.md` | `configurar-autenticacion`     |
| Telemetría           | `configurar-telemetria.md`    | `configurar-telemetria`        |
| Paginación           | `configurar-paginacion.md`    | `configurar-paginacion`        |
| Background Services  | `crear-background-service.md` | `crear-background-service`     |

## Checklist de verificación

- [ ] Cuatro capas: Api, Application, Domain, Infrastructure (+ Test)
- [ ] `<Nullable>enable</Nullable>` en todos los `.csproj`
- [ ] `APP_NAME` actualizado (no `"PLCHLDR"` ni `"Security App"`)
- [ ] `TException.SetComponentPrefix("PREFIJO")` — primera línea del try en Program.cs
- [ ] `builder.Configuration.SetEncryption()` antes de `GetConfiguration()`
- [ ] `GetConfiguration()` devuelve `GeneralConfiguration` validado
- [ ] Todos los `ConfigureXxx` reciben `generalConfiguration` (no `builder.Configuration`)
- [ ] Orden de servicios: Auth → Versioning → Swagger → CORS → Health → Logger → Contexts → Services → InjectConfigs → RateLimit → Localization → Telemetry → AutomatedServices → Authorization
- [ ] Orden de middleware: CORS → Rate → Auth → Authz → Localization → ErrorHandler
- [ ] `AddMemoryCache()` condicional en `CacheEnabled`
- [ ] `UseOpenTelemetryPrometheusScrapingEndpoint()` condicional en `Telemetry.Enabled`
- [ ] `UseVersionedSwagger()` solo en `IsDevelopment()`
- [ ] `AddHealthChecks(...).MapHome(...)` encadenados
- [ ] `appsettings.json` con las 9 secciones obligatorias
- [ ] `SensitiveData` con `"Jwt:Key": ""`
- [ ] **Sin excepciones propias** en Domain — usar `Bisoft.Exceptions`
- [ ] Subsistemas verificados con sus specs individuales
