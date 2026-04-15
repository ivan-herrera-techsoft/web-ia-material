# Spec: Domain Service

## Propósito

Define los contratos que debe cumplir un Domain Service: la capa donde vive la lógica de negocio, las validaciones y la orquestación de repositorios. El Domain Service trabaja exclusivamente con entidades de dominio y es el único responsable de confirmar cambios en base de datos.

> **Spec relacionado:** Los repositorios que el Domain Service consume deben cumplir → [Spec: Repositorios de infraestructura](crear-repositorio.md). Las entidades que crea o modifica deben cumplir → [Spec: Entidades de Dominio](crear-entidad.md).

---

## Contratos del Domain Service

### SC-DS-01: Logger obligatorio

El Domain Service usa `LoggerWrapper<{Entidad}DomainService>` como logger. Queda prohibido usar `ILogger<T>` en esta capa.

```csharp
// CORRECTO
public class CanalDomainService(
    ICanalRepository repositorioCanal,
    LoggerWrapper<CanalDomainService> logger)
{
    private readonly LoggerWrapper<CanalDomainService> _logger = logger;
}

// INCORRECTO
public class CanalDomainService(ILogger<CanalDomainService> logger) { }
```

### SC-DS-02: Niveles de log por tipo de operacion

Dos niveles aplican en el Domain Service:

- `LogDebug` — al inicio de lecturas y validaciones internas; puede haber varios por método
- `LogInformation` — exactamente uno al final de cada método **público de escritura** exitoso; describe el evento de dominio ocurrido

```csharp
public async Task<Canal> Guardar(string nombre, string descripcion, CancellationToken ct = default)
{
    await ValidarNombreUnico(nombre, ct);
    var canal = new Canal(nombre, descripcion);
    await _repositorioCanal.Crear(canal, ct);
    await _repositorioCanal.SaveChanges(..., ct);
    _logger.LogInformation("Canal creado con id: {CanalId}", canal.Id);  // uno, al final
    return canal;
}

private async Task ValidarNombreUnico(string nombre, CancellationToken ct = default)
{
    _logger.LogDebug("Validando unicidad de nombre: {Nombre}", nombre);  // LogDebug en paso interno
    // ...
}
```

Tres restricciones sobre `LogInformation` en el Domain Service:
- Solo en métodos **públicos** — nunca en métodos privados
- Solo en métodos de **escritura** — nunca en lecturas o consultas
- Solo **uno por método** — si el método produce varios eventos, registrar el de mayor relevancia

### SC-DS-03: Composicion de queries

El Domain Service compone el `IQueryable` con los filtros de negocio y lo pasa al repositorio para materialización. El Domain Service decide el criterio de filtrado; el repositorio decide cómo ejecutarlo.

```csharp
// Filtro de negocio en el Domain Service
var canal = await _repositorioCanal.ObtenerCanal(
    _repositorioCanal.ConsultarCanales()
        .Where(c => c.Nombre == nombre)
        .OrderBy(c => c.Id),
    ct);
```

Cuando el caso de uso necesita propiedades de navegación, se llama a los métodos `Incluir*` del repositorio antes de aplicar el filtro final:

```csharp
var query = _repositorioVersion.ConsultarVersiones();
query = _repositorioVersion.IncluirProyecto(query);
query = _repositorioVersion.IncluirConjuntoScripts(query);
var version = await _repositorioVersion.ObtenerVersion(
    query.Where(v => v.Id == versionId).OrderByDescending(v => v.FechaCreacionUtc), ct);
```

### SC-DS-04: Excepciones tipadas

Las excepciones lanzadas desde el Domain Service son siempre del paquete `Bisoft.Exceptions`:

- **Entidad no encontrada:** `TNotFoundException.EntityNotFound(msg, dict)`
- **Regla de negocio violada:** `new TInvalidOperationException(code, msg, dict)` — el código proviene de `DomainConstants.ExceptionCodes.Operation`
- **Credenciales incorrectas:** `TUnauthorizedAccessException.IncorrectCredentials(msg)`

Queda prohibido lanzar excepciones genéricas (`Exception`, `InvalidOperationException`) o retornar `null` cuando se espera una entidad.

```csharp
// CORRECTO
return await _repositorioCanal.ObtenerPorId(canalId, ct)
    ?? throw TNotFoundException.EntityNotFound(
        "No existe un canal con id {CanalId}",
        new Dictionary<string, object> { ["CanalId"] = canalId });

// INCORRECTO
return await _repositorioCanal.ObtenerPorId(canalId, ct)
    ?? throw new Exception("No encontrado");
```

### SC-DS-05: Validaciones de negocio como metodos privados

Las validaciones que requieren acceso a datos (unicidad, estado del sistema, reglas cruzadas) se implementan como métodos privados del Domain Service. Reciben los parámetros necesarios y lanzan `TInvalidOperationException` si la regla se viola.

```csharp
private async Task ValidarNombreUnico(string nombre, CancellationToken ct = default)
{
    _logger.LogDebug("Validando unicidad de nombre: {Nombre}", nombre);
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

### SC-DS-06: SaveChanges con metadata

El Domain Service confirma los cambios llamando a `SaveChanges` del repositorio. Siempre propaga el ID de la entidad afectada en el diccionario de metadata para que el repositorio con cache pueda invalidar la entrada correspondiente.

```csharp
// CORRECTO — metadata para invalidar cache
await _repositorioCanal.SaveChanges(
    new Dictionary<string, string> { ["CanalId"] = canal.Id.ToString() }, ct);

// INCORRECTO — sin metadata
await _repositorioCanal.SaveChanges(ct: ct);
```

### SC-DS-07: Frontera de responsabilidades

El Domain Service trabaja exclusivamente con entidades de dominio. Queda prohibido:

- Mapear DTOs (eso lo hace el Application Service con Mapster)
- Llamar a otros Domain Services (la orquestación entre servicios va en el Application Service)
- Hacer `LogInformation` (es responsabilidad del Application Service)

```csharp
// INCORRECTO — mapear un DTO desde el Domain Service
var response = canal.Adapt<ObtenerCanalResponse>();

// INCORRECTO — llamar a otro DomainService
await _proyectoDomainService.Validar(proyectoId, ct);
```
