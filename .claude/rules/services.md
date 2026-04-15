---
description: Convenciones para Domain Services y Application Services
globs: "**/Services/**"
---

## Domain Service vs Application Service

| Aspecto          | Domain Service                         | Application Service                       |
|------------------|----------------------------------------|-------------------------------------------|
| Ubicacion        | `Domain/Services/`                     | `Application/Services/`                   |
| Responsabilidad  | Reglas de negocio, validaciones        | Orquestacion, mapeo de DTOs               |
| Dependencias     | Repositorios, LoggerWrapper            | Domain Services, Repositorios, LoggerWrapper, Mapster |
| Errores          | Lanza excepciones tipadas de dominio   | Deja pasar al middleware                  |
| Trabaja con      | Entidades de dominio                   | DTOs (request/response)                   |
| Naming           | `{Entidad}DomainService`               | `{Entidad}Service`                        |
| LogInformation   | Si — uno al final de cada evento publico exitoso | Si — tras operaciones exitosas      |

---

## Domain Service

- Constructor principal; campos `_repositorio{Entidad}` y `_logger`
- `LoggerWrapper<{Entidad}DomainService>` como logger — nunca `ILogger<T>`
- `LogDebug` al inicio de lecturas y validaciones internas (puede haber varios por metodo)
- `LogInformation` exactamente uno al final de cada metodo publico de escritura exitoso — describe el evento de dominio ocurrido
- Metodos en **español**, sin sufijo Async
- Lanza `TNotFoundException.EntityNotFound` cuando no encuentra la entidad
- Lanza `TInvalidOperationException(code, msg, dict)` para reglas de negocio
- Llama `SaveChanges` con metadata al finalizar cada operacion de escritura

### Composicion de queries

El Domain Service compone el `IQueryable` con los filtros de negocio y lo pasa al repositorio para materializacion:

```csharp
// Composicion de query con filtro de negocio
var query = _repositorioCanal.ConsultarCanales()
    .Where(c => c.Nombre == nombre)
    .OrderBy(c => c.Id);
var canal = await _repositorioCanal.ObtenerCanal(query, ct);
```

Cuando se necesitan propiedades de navegacion, se llama primero a los metodos `Incluir*`:

```csharp
var query = _repositorioVersion.ConsultarVersiones();
query = _repositorioVersion.IncluirProyecto(query);
query = _repositorioVersion.IncluirConjuntoScripts(query);
var version = await _repositorioVersion.ObtenerVersion(
    query.Where(v => v.Id == versionId).OrderByDescending(v => v.FechaCreacionUtc), ct);
```

### Validaciones de negocio

Las validaciones que requieren acceso a datos se implementan como metodos privados:

```csharp
private async Task ValidarNombreUnico(string nombre, CancellationToken ct = default)
{
    var existente = await _repositorioCanal.ObtenerCanal(
        _repositorioCanal.ConsultarCanales()
            .Where(c => c.Nombre == nombre)
            .OrderBy(c => c.Id),
        ct);

    if (existente is not null)
        throw new TInvalidOperationException(
            ExceptionCodes.Operation.CANAL_NOMBRE_DUPLICADO,
            "Ya existe un canal con el nombre {Nombre}",
            new Dictionary<string, object> { ["Nombre"] = nombre });
}
```

### SaveChanges con metadata

Al confirmar cambios, propagar el ID en el diccionario para que el repositorio cached invalide su cache:

```csharp
await _repositorioCanal.SaveChanges(
    new Dictionary<string, string> { ["CanalId"] = canal.Id.ToString() }, ct);
```

### Ejemplo completo

```csharp
public class CanalDomainService(
    ICanalRepository repositorioCanal,
    LoggerWrapper<CanalDomainService> logger)
{
    private readonly ICanalRepository _repositorioCanal = repositorioCanal;
    private readonly LoggerWrapper<CanalDomainService> _logger = logger;

    public IQueryable<Canal> ConsultarCanales()
        => _repositorioCanal.ConsultarCanales();

    public async Task<Canal> ObtenerPorId(Guid canalId, CancellationToken ct = default)
    {
        _logger.LogDebug("Obteniendo canal con id: {CanalId}", canalId);
        return await _repositorioCanal.ObtenerPorId(canalId, ct)
            ?? throw TNotFoundException.EntityNotFound(
                "No existe un canal con id {CanalId}",
                new Dictionary<string, object> { ["CanalId"] = canalId });
    }

    public async Task<Canal> Guardar(string nombre, string descripcion, CancellationToken ct = default)
    {
        await ValidarNombreUnico(nombre, ct);
        var canal = new Canal(nombre, descripcion);
        await _repositorioCanal.Crear(canal, ct);
        await _repositorioCanal.SaveChanges(
            new Dictionary<string, string> { ["CanalId"] = canal.Id.ToString() }, ct);
        _logger.LogInformation("Canal creado con id: {CanalId}", canal.Id);
        return canal;
    }

    public async Task Actualizar(Guid canalId, string nombre, string descripcion, CancellationToken ct = default)
    {
        var canal = await ObtenerPorId(canalId, ct);
        await ValidarNombreUnico(nombre, ct);
        canal.Actualizar(nombre, descripcion);
        await _repositorioCanal.Actualizar(canal, ct);
        await _repositorioCanal.SaveChanges(
            new Dictionary<string, string> { ["CanalId"] = canal.Id.ToString() }, ct);
        _logger.LogInformation("Canal actualizado con id: {CanalId}", canalId);
    }

    public async Task Eliminar(Guid canalId, CancellationToken ct = default)
    {
        var canal = await ObtenerPorId(canalId, ct);
        _repositorioCanal.Eliminar(canal);
        await _repositorioCanal.SaveChanges(
            new Dictionary<string, string> { ["CanalId"] = canal.Id.ToString() }, ct);
        _logger.LogInformation("Canal eliminado con id: {CanalId}", canalId);
    }

    private async Task ValidarNombreUnico(string nombre, CancellationToken ct = default)
    {
        _logger.LogDebug("Validando unicidad de nombre de canal: {Nombre}", nombre);
        var existente = await _repositorioCanal.ObtenerCanal(
            _repositorioCanal.ConsultarCanales()
                .Where(c => c.Nombre == nombre)
                .OrderBy(c => c.Id),
            ct);

        if (existente is not null)
            throw new TInvalidOperationException(
                ExceptionCodes.Operation.CANAL_NOMBRE_DUPLICADO,
                "Ya existe un canal con el nombre {Nombre}",
                new Dictionary<string, object> { ["Nombre"] = nombre });
    }
}
```

---

## Application Service

- Constructor principal; campos `_servicioDominio`, `_repositorio{Entidad}` y `_logger`
- `LoggerWrapper<{Entidad}Service>` como logger — nunca `ILogger<T>`
- `LogInformation` uno al final de cada metodo publico de escritura exitoso
- `LogDebug` al inicio de reportes con los parametros de filtrado
- Sin logica de negocio — solo orquestacion, mapeo y observacion
- **Escrituras**: delegar completamente al DomainService — sin crear ni modificar entidades aqui
- **Lecturas**: acceder directamente al repositorio con `ProjectToType<T>()` sin pasar por el DomainService
- **Reportes**: materializar con `await`, pueden combinar multiples repositorios, DTO propio

```csharp
public class CanalService(
    CanalDomainService servicioDominio,
    ICanalRepository repositorioCanal,
    LoggerWrapper<CanalService> logger)
{
    private readonly CanalDomainService _servicioDominio = servicioDominio;
    private readonly ICanalRepository _repositorioCanal = repositorioCanal;
    private readonly LoggerWrapper<CanalService> _logger = logger;

    // Lectura — proyeccion directa desde repositorio, sin materializar
    public IQueryable<ObtenerCanalesResponse> ObtenerCanales()
        => _repositorioCanal.ConsultarCanales()
            .ProjectToType<ObtenerCanalesResponse>();

    // Detalle — delegar al DomainService para obtener la entidad, luego mappear
    public async Task<ObtenerCanalResponse> ObtenerPorId(Guid canalId, CancellationToken ct = default)
    {
        var canal = await _servicioDominio.ObtenerPorId(canalId, ct);
        return canal.Adapt<ObtenerCanalResponse>();
    }

    // Escritura — delegar al DomainService, luego LogInformation
    public async Task Guardar(CrearCanalRequest solicitud, CancellationToken ct = default)
    {
        await _servicioDominio.Guardar(solicitud.Nombre, solicitud.Descripcion, ct);
        _logger.LogInformation("Canal creado a partir de solicitud");
    }

    public async Task Actualizar(Guid canalId, ActualizarCanalRequest solicitud, CancellationToken ct = default)
    {
        await _servicioDominio.Actualizar(canalId, solicitud.Nombre, solicitud.Descripcion, ct);
        _logger.LogInformation("Canal actualizado con id: {CanalId}", canalId);
    }

    public async Task Eliminar(Guid canalId, CancellationToken ct = default)
    {
        await _servicioDominio.Eliminar(canalId, ct);
        _logger.LogInformation("Canal eliminado con id: {CanalId}", canalId);
    }

    // Reporte — LogDebug con filtros, materializar, DTO independiente
    public async Task<IEnumerable<ReporteCanalResponse>> ObtenerReporteCanales(
        DateTime desdeUtc, DateTime hastaUtc, CancellationToken ct = default)
    {
        _logger.LogDebug("Generando reporte de canales desde {Desde} hasta {Hasta}", desdeUtc, hastaUtc);
        var canales = await _repositorioCanal.ObtenerCanales(
            _repositorioCanal.ConsultarCanales()
                .Where(c => c.FechaCreacionUtc >= desdeUtc && c.FechaCreacionUtc <= hastaUtc)
                .OrderByDescending(c => c.FechaCreacionUtc),
            ct);
        return canales.Adapt<IEnumerable<ReporteCanalResponse>>();
    }
}
```

---

## Mapster en Application Service

- `entidad.Adapt<Response>()` — entidad a DTO de respuesta
- `query.ProjectToType<Dto>()` — proyeccion IQueryable sin materializar
- `solicitud.Adapt(entidadExistente)` — aplicar cambios de DTO a entidad existente (sin sobrescribir campos no mapeados)
- No usar `solicitud.Adapt<Entidad>()` para crear entidades — usar constructor o metodo `Crear` de la entidad

---

## Excepciones tipadas (Bisoft.Exceptions)

```csharp
// Entidad no encontrada
TNotFoundException.EntityNotFound("No existe un canal con id {CanalId}",
    new Dictionary<string, object> { ["CanalId"] = canalId });

// Regla de negocio violada (codigo en DomainConstants.ExceptionCodes.Operation)
throw new TInvalidOperationException(
    ExceptionCodes.Operation.CANAL_NOMBRE_DUPLICADO,
    "Ya existe un canal con el nombre {Nombre}",
    new Dictionary<string, object> { ["Nombre"] = nombre });

// Acceso no autorizado
TUnauthorizedAccessException.IncorrectCredentials("El nombre de usuario o la contraseña son incorrectos");
```
