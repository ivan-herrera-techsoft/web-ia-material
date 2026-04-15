# Skill: Configurar Logger (Serilog + LoggerWrapper)

Configura el sistema de logging con Serilog y `LoggerWrapper<T>` en un proyecto Atenea Minimal API.

---

## Paso 1 — DTOs de configuración

**`Api/Dtos/Configurations/DbLoggerConfiguration.cs`**:

```csharp
using Bisoft.DatabaseConnections.Configuration;
using Serilog.Events;

namespace Company.Product.Module.Api.Dtos.Configurations;

public class DbLoggerConfiguration : IDbConnectionConfiguration
{
    public string DatabaseProvider { get; }
    public string DatabaseConnectionString { get; }
    public LogEventLevel MinimumLevel { get; set; }

    public DbLoggerConfiguration(string connectionString, string provider, LogEventLevel minimumLevel)
    {
        DatabaseConnectionString = connectionString;
        DatabaseProvider = provider;
        MinimumLevel = minimumLevel;
    }

    public DbLoggerConfiguration(IDbConnectionConfiguration connectionConfiguration, LogEventLevel minimumLevel)
    {
        DatabaseConnectionString = connectionConfiguration.DatabaseConnectionString;
        DatabaseProvider = connectionConfiguration.DatabaseProvider;
        MinimumLevel = minimumLevel;
    }
}
```

**`Api/Dtos/Configurations/GeneralLoggerConfiguration.cs`**:

```csharp
namespace Company.Product.Module.Api.Dtos.Configurations;

public class GeneralLoggerConfiguration
{
    public bool LogHttpRequests { get; set; }
    public required DbLoggerConfiguration Sqlite { get; set; }
    public required DbLoggerConfiguration MainDatabase { get; set; }
}
```

Agregar a `GeneralConfiguration`:

```csharp
public required GeneralLoggerConfiguration Logger { get; set; }
```

---

## Paso 2 — Lector de configuración

En `Api/Extensions/Configuration/` crear `LoggerConfigurationsReader.cs` como clase `partial` de `ConfigurationExtensions`:

```csharp
namespace Company.Product.Module.Api.Extensions.Configuration;

public static partial class ConfigurationExtensions
{
    internal static GeneralLoggerConfiguration GetGeneralLoggerConfiguration(
        this IConfiguration configuration,
        IDbConnectionConfiguration mainConnectionConfiguration)
    {
        return new GeneralLoggerConfiguration()
        {
            LogHttpRequests = configuration.GetLogHttpRequests(),
            Sqlite = configuration.GetLoggerSqliteConfiguration(),
            MainDatabase = configuration.GetLoggerDbConfiguration(mainConnectionConfiguration),
        };
    }

    private static DbLoggerConfiguration GetLoggerSqliteConfiguration(this IConfiguration configuration)
    {
        var connectionString = configuration["Logger:Sqlite:Path"]
            .TryOverwriteWithEnviromentValue("LOGGER_SQLITE_CONNECTION_STRING")
            ?? throw TEnvironmentException.MissingConfiguration(
                TEnvironmentException.Sources.APPSETTINGS,
                "Falta la configuración de la ruta de la base de datos Sqlite para el logger"
            );
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            connectionString = Path.Combine("/logs", connectionString);
        var minimumLevel = ParseLogEventLevel(
            configuration["Logger:Sqlite:MinimumLevel"]
                .TryOverwriteWithEnviromentValue("LOGGER_SQLITE_MINIMUM_LEVEL"));
        return new DbLoggerConfiguration(connectionString, SQLITE, minimumLevel);
    }

    private static DbLoggerConfiguration GetLoggerDbConfiguration(
        this IConfiguration configuration,
        IDbConnectionConfiguration connectionConfiguration)
    {
        var minimumLevel = ParseLogEventLevel(
            configuration["Logger:Main:MinimumLevel"]
                .TryOverwriteWithEnviromentValue("LOGGER_MAIN_MINIMUM_LEVEL"));
        return new DbLoggerConfiguration(connectionConfiguration, minimumLevel);
    }

    private static bool GetLogHttpRequests(this IConfiguration configuration)
    {
        var logHttpRequests = configuration["Logger:LogHttpRequests"]
            .TryOverwriteWithEnviromentValue("LOG_HTTP");
        return logHttpRequests.ToBool(TEnvironmentException.InvalidConfiguration(
            TEnvironmentException.Sources.APPSETTINGS,
            $"El valor de 'LogHttpRequests' debe ser un booleano válido, pero se recibió '{logHttpRequests}'"
        ));
    }

    private static LogEventLevel ParseLogEventLevel(string? minimumLevelString)
    {
        if (string.IsNullOrEmpty(minimumLevelString))
            throw TEnvironmentException.InvalidConfiguration(
                TEnvironmentException.Sources.APPSETTINGS,
                "El nivel mínimo de log no puede estar vacío");
        if (!Enum.TryParse<LogEventLevel>(minimumLevelString, true, out var result))
            throw TEnvironmentException.InvalidConfiguration(
                TEnvironmentException.Sources.APPSETTINGS,
                $"El nivel mínimo de log '{minimumLevelString}' no es válido");
        return result;
    }
}
```

Invocar desde `GetConfiguration()` en `ConfigurationsReader.cs`:

```csharp
Logger = configuration.GetGeneralLoggerConfiguration(securityConnection),
```

---

## Paso 3 — LoggerConfigurationExtensions

Crear `Api/Extensions/LoggerConfigurationExtensions.cs`:

```csharp
using Bisoft.DatabaseConnections;
using Serilog;
using Serilog.Filters;
using Serilog.Sinks.Grafana.Loki;

namespace Company.Product.Module.Api.Extensions;

public static class LoggerConfigurationExtensions
{
    public static LoggerConfiguration AddConfiguration(
        this LoggerConfiguration loggerConfiguration,
        GeneralConfiguration configuration)
    {
        loggerConfiguration
            .AddSqliteConfiguration(configuration.Logger.Sqlite)
            .AddMainDatabase(configuration.Logger.MainDatabase);

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
                        new LokiLabel { Key = "traceId",       Value = "{TraceId}" },
                        new LokiLabel { Key = "spanId",         Value = "{SpanId}" }
                    ]);
        }

        if (!configuration.Logger.LogHttpRequests)
            loggerConfiguration.Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore"));

        var isDebug =
#if DEBUG
            true;
#else
            false;
#endif
        if (isDebug)
            loggerConfiguration.WriteTo.Console();

        return loggerConfiguration
            .MinimumLevel.Override("Bisoft.NotificationCenter", LogEventLevel.Debug)
            .Filter.ByExcluding(Matching.FromSource("Microsoft.EntityFrameworkCore.Database.Command"));
    }

    private static LoggerConfiguration AddMainDatabase(
        this LoggerConfiguration loggerConfiguration,
        DbLoggerConfiguration configuration)
    {
        return configuration.DatabaseProvider switch
        {
            DatabaseProviders.SQLSERVER => loggerConfiguration.AddSqlServerConfiguration(configuration),
            DatabaseProviders.POSTGRES  => loggerConfiguration.AddPostgresConfiguration(configuration),
            DatabaseProviders.SQLITE    => loggerConfiguration.AddSqliteConfiguration(configuration),
            _ => throw TArgumentException.NotSupported(
                $"El proveedor '{configuration.DatabaseProvider}' no es compatible.")
        };
    }

    private static LoggerConfiguration AddSqliteConfiguration(
        this LoggerConfiguration loggerConfiguration,
        DbLoggerConfiguration configuration)
    {
        // Implementar según el sink de Serilog para SQLite
        return loggerConfiguration.WriteTo.SQLite(
            configuration.DatabaseConnectionString,
            restrictedToMinimumLevel: configuration.MinimumLevel);
    }

    // AddSqlServerConfiguration y AddPostgresConfiguration según los sinks disponibles
}
```

---

## Paso 4 — LOGGER_TABLE_NAME en ApiConstants

Verificar que `Api/Helpers/ApiConstants.cs` contiene:

```csharp
public static string LOGGER_TABLE_NAME => $"{APP_NAME} Logs";
```

Se usa como nombre de tabla en los sinks de base de datos (convertido a `camelCase` para SqlServer, `snake_case` para Postgres).

---

## Paso 5 — ConfigureLogger en ServiceExtensions

```csharp
public static IServiceCollection ConfigureLogger(
    this IServiceCollection services,
    GeneralConfiguration configuration)
{
    Log.Logger = new LoggerConfiguration()
        .AddConfiguration(configuration)
        .Destructure.ToMaximumDepth(3)
        .CreateLogger();

    services.AddLoggerWrapper();
    services.AddSerilog();
    return services;
}
```

Encadenar en `Program.cs`:

```csharp
builder.Services
    ...
    .ConfigureLogger(generalConfiguration)
    ...
```

---

## Paso 6 — ManageException en Program.cs

Logger de emergencia para errores de arranque:

```csharp
private static async Task ManageException(Exception exception)
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .WriteTo.File("Logs\\app.log")
        .Destructure.ToMaximumDepth(3)
        .CreateLogger();

    Log.Logger.Fatal(exception, "Error al configurar la aplicación");

    if (exception.InnerException?.Source?.StartsWith("Serilog") ?? false)
        Log.Logger.Fatal("Error al configurar el logger (appsettings)");
}
```

---

## Paso 7 — Uso de LoggerWrapper en nuevas clases

Al crear cualquier clase que necesite logging, inyectar `LoggerWrapper<NombreClase>`:

**Domain Service** (constructor clásico si es legacy, principal si es nuevo):
```csharp
public class CanalDomainService(ICanalRepository repositorioCanal, LoggerWrapper<CanalDomainService> logger)
{
    private readonly LoggerWrapper<CanalDomainService> _logger = logger;
    // LogDebug en validaciones, LogInformation al final de escrituras exitosas
}
```

**Application Service**:
```csharp
public class CanalService(CanalDomainService servicioDominio, LoggerWrapper<CanalService> logger)
{
    private readonly LoggerWrapper<CanalService> _logger = logger;
    // LogInformation tras escrituras exitosas, LogDebug en reportes
}
```

**Repositorio** — `_logger` viene de `EFRepository<T>`, no redeclarar:
```csharp
public class CanalRepository : EFRepository<ModuloContext>, ICanalRepository
{
    public CanalRepository(ModuloContext context, LoggerWrapper<CanalRepository> logger)
        : base(context, logger) { }
    // LogDebug en todas las consultas
}
```

---

## Paso 8 — Configuración en appsettings

```json
"Logger": {
  "LogHttpRequests": false,
  "Sqlite": {
    "Path": "Logs\\Logs.db",
    "MinimumLevel": "Information"
  },
  "Main": {
    "MinimumLevel": "Error"
  }
}
```

---

## Verificación

- [ ] `DbLoggerConfiguration` y `GeneralLoggerConfiguration` existen en `Dtos/Configurations/`
- [ ] `LoggerConfigurationsReader` es clase `partial` de `ConfigurationExtensions`
- [ ] Cada valor de configuración valida con `TEnvironmentException` si está ausente o es inválido
- [ ] `AddConfiguration` filtra siempre `Microsoft.EntityFrameworkCore.Database.Command`
- [ ] Console sink solo en `#if DEBUG`
- [ ] GrafanaLoki solo si `Telemetry.Enabled`
- [ ] `ConfigureLogger` llama a `AddLoggerWrapper()` y `AddSerilog()`
- [ ] Todas las clases usan `LoggerWrapper<T>`, nunca `ILogger<T>`
- [ ] Repositorios no redeclaran `_logger` (viene de `EFRepository<T>`)
- [ ] `LogInformation` solo en escrituras públicas exitosas en DomainService y ApplicationService
