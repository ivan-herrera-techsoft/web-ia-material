---
description: Convenciones para Serilog y LoggerWrapper — sinks por entorno, niveles de log y parámetros nombrados
globs: "**/*.cs"
---

# Reglas de Logger (Serilog + LoggerWrapper)

## Principio general

El logger se implementa con **Serilog** y se accede en todas las capas a través de `LoggerWrapper<T>` del paquete `Bisoft.Logging.Util`. Nunca se inyecta `ILogger<T>` directamente. La configuración de sinks y niveles se lee de appsettings y variables de entorno, centralizada en `LoggerConfigurationExtensions`.

---

## DTOs de configuración

### DbLoggerConfiguration

```csharp
// Api/Dtos/Configurations/DbLoggerConfiguration.cs
public class DbLoggerConfiguration : IDbConnectionConfiguration
{
    public string DatabaseProvider { get; }
    public string DatabaseConnectionString { get; }
    public LogEventLevel MinimumLevel { get; set; }

    // Constructor para Sqlite (solo path)
    public DbLoggerConfiguration(string connectionString, string provider, LogEventLevel minimumLevel) { ... }

    // Constructor para proveedor existente (reutiliza IDbConnectionConfiguration)
    public DbLoggerConfiguration(IDbConnectionConfiguration connectionConfiguration, LogEventLevel minimumLevel) { ... }
}
```

### GeneralLoggerConfiguration

```csharp
// Api/Dtos/Configurations/GeneralLoggerConfiguration.cs
public class GeneralLoggerConfiguration
{
    public bool LogHttpRequests { get; set; }
    public required DbLoggerConfiguration Sqlite { get; set; }
    public required DbLoggerConfiguration MainDatabase { get; set; }
    // Agregar más proveedores si el proyecto lo requiere
}
```

Se integra como `required GeneralLoggerConfiguration Logger { get; set; }` en `GeneralConfiguration`.

---

## Lector de configuración

```csharp
// Api/Extensions/Configuration/LoggerConfigurationsReader.cs
private static GeneralLoggerConfiguration GetGeneralLoggerConfiguration(
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
```

**Sqlite**: Lee `Logger:Sqlite:Path` / `LOGGER_SQLITE_CONNECTION_STRING`. En Linux antepone `/logs/` al path automáticamente.

**MainDatabase**: Reutiliza la connection string del módulo; solo lee su nivel mínimo desde `Logger:Main:MinimumLevel` / `LOGGER_MAIN_MINIMUM_LEVEL`.

**LogHttpRequests**: Lee `Logger:LogHttpRequests` / `LOG_HTTP`. Si está en `false`, filtra los logs de `Microsoft.AspNetCore`.

Todos los valores se validan al inicio: si faltan o son inválidos se lanza `TEnvironmentException`.

---

## LoggerConfigurationExtensions — AddConfiguration

```csharp
// Api/Extensions/LoggerConfigurationExtensions.cs
public static LoggerConfiguration AddConfiguration(
    this LoggerConfiguration loggerConfiguration,
    GeneralConfiguration configuration)
{
    loggerConfiguration
        .AddSqliteConfiguration(configuration.Logger.Sqlite)
        .AddMainDatabase(configuration.Logger.MainDatabase);

    // GrafanaLoki — solo si Telemetry está habilitado
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

    // Filtrar requests HTTP si no se desean en el log
    if (!configuration.Logger.LogHttpRequests)
        loggerConfiguration.Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore"));

    // Console solo en DEBUG
    var isDebug =
#if DEBUG
        true;
#else
        false;
#endif
    if (isDebug)
        loggerConfiguration.WriteTo.Console();

    // Siempre: override de nivel para NotificationCenter y filtro de EF queries
    return loggerConfiguration
        .MinimumLevel.Override("Bisoft.NotificationCenter", LogEventLevel.Debug)
        .Filter.ByExcluding(Matching.FromSource("Microsoft.EntityFrameworkCore.Database.Command"));
}
```

**`AddMainDatabase`** hace switch sobre `DatabaseProvider` y delega a `AddSqlServerConfiguration`, `AddPostgresConfiguration` o `AddSqliteConfiguration`.

**`LOGGER_TABLE_NAME`**: `$"{APP_NAME} Logs"` — nombre de tabla para los sinks de base de datos, convertido con `.ToCamelCase()` para SqlServer o `snake_case` para Postgres.

---

## Registro en ServiceExtensions

```csharp
// Api/Extensions/ServiceExtensions.cs
public static IServiceCollection ConfigureLogger(
    this IServiceCollection services,
    GeneralConfiguration configuration)
{
    Log.Logger = new LoggerConfiguration()
        .AddConfiguration(configuration)
        .Destructure.ToMaximumDepth(3)
        .CreateLogger();

    services.AddLoggerWrapper();   // registra LoggerWrapper<T> en el contenedor
    services.AddSerilog();         // integra Log.Logger con el host de .NET

    return services;
}
```

`AddLoggerWrapper()` proviene de `Bisoft.Logging.Util` y registra `LoggerWrapper<T>` como servicio genérico abierto, disponible en todas las capas por inyección de dependencias.

---

## Logger de emergencia (ManageException)

Si el arranque falla, `Program.cs` crea un logger mínimo para registrar el error fatal:

```csharp
private static async Task ManageException(Exception exception)
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .WriteTo.File("Logs\\app.log")
        .Destructure.ToMaximumDepth(3)
        .CreateLogger();

    Log.Logger.Fatal(exception, "Error al configurar la aplicación");
    ...
}
```

---

## LoggerWrapper — uso por capa

`LoggerWrapper<T>` es el único tipo de logger permitido. Se inyecta vía constructor principal o constructor clásico.

### Repositorio

```csharp
public class CanalRepository : EFRepository<ModuloContext>, ICanalRepository
{
    // _logger viene de EFRepository<T> — no redeclarar
    public CanalRepository(ModuloContext context, LoggerWrapper<CanalRepository> logger)
        : base(context, logger) { }

    public IQueryable<Canal> ConsultarCanales()
    {
        _logger.LogDebug("Creando consulta de canales");
        return _context.Canales;
    }
}
```

### Domain Service

```csharp
public class CanalDomainService(ICanalRepository repositorioCanal, LoggerWrapper<CanalDomainService> logger)
{
    private readonly ICanalRepository _repositorioCanal = repositorioCanal;
    private readonly LoggerWrapper<CanalDomainService> _logger = logger;

    public async Task<Canal> Guardar(string nombre, CancellationToken ct = default)
    {
        _logger.LogDebug("Validando unicidad de nombre: {Nombre}", nombre);
        await ValidarNombreUnico(nombre, ct);
        var canal = new Canal(nombre);
        await _repositorioCanal.Crear(canal, ct);
        await _repositorioCanal.SaveChanges(
            new Dictionary<string, string> { ["CanalId"] = canal.Id.ToString() }, ct);
        _logger.LogInformation("Canal creado con id: {CanalId}", canal.Id);
        return canal;
    }
}
```

### Application Service

```csharp
public class CanalService(CanalDomainService servicioDominio, LoggerWrapper<CanalService> logger)
{
    private readonly LoggerWrapper<CanalService> _logger = logger;

    public async Task<CrearCanalResponse> Guardar(CrearCanalRequest solicitud, CancellationToken ct = default)
    {
        var canal = await servicioDominio.Guardar(solicitud.Nombre, ct);
        _logger.LogInformation("Canal guardado correctamente con id: {CanalId}", canal.Id);
        return canal.Adapt<CrearCanalResponse>();
    }
}
```

### Middleware

```csharp
// ErrorHandlerMiddleware — LogWarning para excepciones tipadas, LogError para no controladas
_logger.LogWarning(ex, "Operación no permitida [Codigo: {Codigo}]: {Message}.", ex.Code, ex.Message);
_logger.LogError(exception, "Error no controlado: {Message}.", exception.Message);
```

---

## Niveles de log por contexto

| Nivel            | Cuándo usarlo                                                         |
|------------------|-----------------------------------------------------------------------|
| `LogDebug`       | Inicio de lecturas, validaciones internas, consultas a BD             |
| `LogInformation` | Operación de negocio completada exitosamente (escrituras confirmadas) |
| `LogWarning`     | Fallos esperados — excepciones tipadas de dominio                     |
| `LogError`       | Excepciones no controladas — errores inesperados en runtime           |
| `LogCritical`    | Errores en el arranque de la aplicación                               |

---

## Sinks por entorno

| Sink          | Entorno              | Condición                          |
|---------------|----------------------|------------------------------------|
| Console       | Desarrollo (DEBUG)   | `#if DEBUG`                        |
| SQLite        | Desarrollo           | Siempre (nivel mínimo configurable)|
| Base de datos | Todos                | Proveedor del módulo (nivel Error) |
| GrafanaLoki   | Producción           | `Telemetry.Enabled = true`         |

---

## Reglas de uso de log

- **Parámetros nombrados siempre**: `_logger.LogDebug("Consultando {CanalId}", id)` — nunca interpolación de string.
- **Un `LogInformation` por método público de escritura** en Domain Services — al final, tras el `SaveChanges`.
- **Nunca** `LogInformation` en métodos privados ni en lecturas.
- **Nunca** `ILogger<T>` — siempre `LoggerWrapper<T>`.
- `_logger` en repositorios: heredado de `EFRepository<T>`, **no redeclarar**.

---

## Configuración en appsettings

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

## Variables de entorno

| Variable                          | Descripción                              |
|-----------------------------------|------------------------------------------|
| `LOGGER_SQLITE_CONNECTION_STRING` | Path del archivo SQLite de logs          |
| `LOGGER_SQLITE_MINIMUM_LEVEL`     | Nivel mínimo para el sink SQLite         |
| `LOGGER_MAIN_MINIMUM_LEVEL`       | Nivel mínimo para el sink de BD principal|
| `LOG_HTTP`                        | `true`/`false` — loggear requests HTTP  |
