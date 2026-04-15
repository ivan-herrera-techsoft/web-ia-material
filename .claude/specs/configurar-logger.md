# Spec: Configurar Logger (Serilog + LoggerWrapper)

## Contratos

---

### SC-LOG-01 — LoggerWrapper<T> es el único tipo de logger permitido

**Justificación**: `LoggerWrapper<T>` del paquete `Bisoft.Logging.Util` es el wrapper estándar Bisoft que encapsula Serilog y garantiza structured logging uniforme en todas las capas. Usar `ILogger<T>` directamente acopla el código al sistema de logging de .NET y no aprovecha las abstracciones del ecosistema Bisoft.

**Correcto**:
```csharp
public class CanalDomainService(ICanalRepository repositorioCanal, LoggerWrapper<CanalDomainService> logger)
{
    private readonly LoggerWrapper<CanalDomainService> _logger = logger;
}
```

**Incorrecto**:
```csharp
public class CanalDomainService(ICanalRepository repositorioCanal, ILogger<CanalDomainService> logger)
{
    private readonly ILogger<CanalDomainService> _logger = logger;
}
```

---

### SC-LOG-02 — Los repositorios heredan _logger de EFRepository<T>, no lo redeclaran

**Justificación**: `EFRepository<T>` (de `Bisoft.DatabaseConnections`) ya declara `protected readonly LoggerWrapper<T> _logger`. Redeclararlo en el repositorio hijo genera un campo duplicado que shadowing al del padre y puede causar comportamiento inconsistente.

**Correcto**:
```csharp
public class CanalRepository : EFRepository<ModuloContext>, ICanalRepository
{
    public CanalRepository(ModuloContext context, LoggerWrapper<CanalRepository> logger)
        : base(context, logger) { }

    public IQueryable<Canal> ConsultarCanales()
    {
        _logger.LogDebug("Creando consulta de canales");  // usa _logger del padre
        return _context.Canales;
    }
}
```

**Incorrecto**:
```csharp
public class CanalRepository : EFRepository<ModuloContext>, ICanalRepository
{
    private readonly LoggerWrapper<CanalRepository> _logger;  // redeclaración — incorrecto

    public CanalRepository(ModuloContext context, LoggerWrapper<CanalRepository> logger)
        : base(context, logger)
    {
        _logger = logger;  // shadowing del campo del padre
    }
}
```

---

### SC-LOG-03 — LogDebug para acceso a datos y validaciones internas

**Justificación**: Las operaciones de lectura y validación son frecuentes y de bajo nivel. `LogDebug` permite rastrearlas en desarrollo sin contaminar los logs de producción, donde el nivel mínimo suele ser `Information` o superior.

**Correcto**:
```csharp
public IQueryable<Canal> ConsultarCanales()
{
    _logger.LogDebug("Creando consulta de canales");
    return _context.Canales;
}

private async Task ValidarNombreUnico(string nombre, CancellationToken ct)
{
    _logger.LogDebug("Validando unicidad de nombre: {Nombre}", nombre);
    ...
}
```

**Incorrecto**:
```csharp
public IQueryable<Canal> ConsultarCanales()
{
    _logger.LogInformation("Consultando canales");  // nivel demasiado alto para una lectura
    return _context.Canales;
}
```

---

### SC-LOG-04 — Un solo LogInformation al final de cada método público de escritura en DomainService

**Justificación**: `LogInformation` en el Domain Service registra que un evento de dominio ocurrió exitosamente. Debe ser exactamente uno por método público de escritura, colocado al final tras el `SaveChanges`, para que quede constancia del resultado. Más de uno en el mismo método genera ruido; ninguno impide la auditoría.

**Correcto**:
```csharp
public async Task<Canal> Guardar(string nombre, CancellationToken ct = default)
{
    _logger.LogDebug("Validando unicidad: {Nombre}", nombre);
    await ValidarNombreUnico(nombre, ct);
    var canal = new Canal(nombre);
    await _repositorioCanal.Crear(canal, ct);
    await _repositorioCanal.SaveChanges(
        new Dictionary<string, string> { ["CanalId"] = canal.Id.ToString() }, ct);
    _logger.LogInformation("Canal creado con id: {CanalId}", canal.Id);  // uno, al final
    return canal;
}
```

**Incorrecto**:
```csharp
public async Task<Canal> Guardar(string nombre, CancellationToken ct = default)
{
    _logger.LogInformation("Iniciando creación de canal");   // incorrecto: al inicio
    await ValidarNombreUnico(nombre, ct);
    var canal = new Canal(nombre);
    await _repositorioCanal.Crear(canal, ct);
    await _repositorioCanal.SaveChanges(..., ct);
    _logger.LogInformation("Canal creado");                  // incorrecto: dos LogInformation
    return canal;
}
```

---

### SC-LOG-05 — Nunca interpolación de strings en logs

**Justificación**: Serilog usa structured logging: los parámetros nombrados se almacenan como propiedades separadas en el log store, permitiendo filtrado, búsqueda y correlación. La interpolación de strings convierte todo en texto plano y destruye la estructura.

**Correcto**:
```csharp
_logger.LogInformation("Canal creado con id: {CanalId}", canal.Id);
_logger.LogWarning("No se encontró canal con id: {CanalId}", canalId);
```

**Incorrecto**:
```csharp
_logger.LogInformation($"Canal creado con id: {canal.Id}");     // interpolación
_logger.LogWarning("No se encontró canal con id: " + canalId);  // concatenación
```

---

### SC-LOG-06 — Console sink solo en DEBUG, GrafanaLoki solo si Telemetry.Enabled

**Justificación**: El sink de Console en producción generaría volumen innecesario en stdout y consumiría recursos. GrafanaLoki requiere que el servicio de telemetría esté activo; intentar escribir en él sin infraestructura disponible fallaría silenciosamente o generaría errores en el pipeline de logs.

**Correcto**:
```csharp
var isDebug =
#if DEBUG
    true;
#else
    false;
#endif
if (isDebug)
    loggerConfiguration.WriteTo.Console();

if (configuration.Telemetry.Enabled)
    loggerConfiguration.WriteTo.GrafanaLoki(...);
```

**Incorrecto**:
```csharp
loggerConfiguration.WriteTo.Console();   // siempre activo — contamina producción
loggerConfiguration.WriteTo.GrafanaLoki(...);  // sin condición de telemetría
```

---

### SC-LOG-07 — Microsoft.EntityFrameworkCore.Database.Command siempre filtrado

**Justificación**: EF Core genera un log por cada query SQL ejecutada. En producción esto satura los sinks con mensajes de bajo valor. El filtro es permanente e incondicional en todos los proyectos.

**Correcto**:
```csharp
return loggerConfiguration
    .Filter.ByExcluding(Matching.FromSource("Microsoft.EntityFrameworkCore.Database.Command"));
```

**Incorrecto**:
```csharp
// No filtrar EF Core — miles de logs de queries en producción
return loggerConfiguration.CreateLogger();
```

---

### SC-LOG-08 — ConfigureLogger llama a AddLoggerWrapper() y AddSerilog()

**Justificación**: `AddLoggerWrapper()` registra `LoggerWrapper<T>` como servicio genérico abierto, disponible para inyección en todas las capas. `AddSerilog()` integra el `Log.Logger` estático de Serilog con el sistema de logging del host de .NET, necesario para que los logs de infraestructura del framework también pasen por Serilog.

**Correcto**:
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

**Incorrecto**:
```csharp
// Sin AddLoggerWrapper — LoggerWrapper<T> no resolvible en DI
Log.Logger = new LoggerConfiguration().CreateLogger();
services.AddSerilog();

// Sin AddSerilog — logs del framework (.NET) no pasan por Serilog
Log.Logger = new LoggerConfiguration().CreateLogger();
services.AddLoggerWrapper();
```

---

### SC-LOG-09 — Niveles de log por tipo de operación

**Justificación**: Los niveles de Serilog tienen semántica específica. Usarlos incorrectamente distorsiona la señal en los dashboards de monitoreo: un error que solo debería ser warning activa alertas innecesarias; una operación exitosa loggeada como Debug desaparece en producción.

| Nivel          | Cuándo                                                   |
|----------------|----------------------------------------------------------|
| `LogDebug`     | Acceso a datos, validaciones internas, consultas         |
| `LogInformation` | Operación de negocio completada (escrituras exitosas)  |
| `LogWarning`   | Excepciones tipadas de dominio (fallos esperados)        |
| `LogError`     | Excepciones no controladas                               |
| `LogCritical`  | Errores de arranque de la aplicación                     |

**Correcto**:
```csharp
// Repository — lectura
_logger.LogDebug("Consultando canal con id: {CanalId}", id);

// DomainService — escritura exitosa
_logger.LogInformation("Canal eliminado con id: {CanalId}", canal.Id);

// Middleware — excepción tipada
_logger.LogWarning(ex, "Operación no permitida [Codigo: {Codigo}]", ex.Code);

// Middleware — excepción inesperada
_logger.LogError(exception, "Error no controlado: {Message}", exception.Message);
```

**Incorrecto**:
```csharp
_logger.LogError("Canal no encontrado");   // LogError para algo esperado — debe ser LogWarning
_logger.LogInformation("Consultando...");  // LogInformation en lecturas — debe ser LogDebug
```
