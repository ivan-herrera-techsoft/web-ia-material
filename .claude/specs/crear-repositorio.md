# Spec: Repositorios de infraestructura

## Propósito

Define los contratos que debe cumplir la capa de repositorios: la interfaz en Dominio, la implementación en Infraestructura y el repositorio con cache. Garantiza que el acceso a datos sea uniforme, trazable y sin lógica de negocio, y que el Domain Service sea el único responsable de confirmar los cambios.

> **Spec relacionado:** Los `DbSet<T>` que los repositorios consumen están definidos en el Context correspondiente. Ver → [Spec: DbContext de Infraestructura](crear-contexto.md) — SC-CTX-03.

---

## Contratos de la interfaz

### SC-REP-01: Herencia obligatoria

Toda interfaz de repositorio hereda de `IEFRepository` del paquete `Bisoft.DatabaseConnections.Util.Abstractions`. `SaveChanges` viene de ahí y **no se redeclara** en la interfaz específica.

```csharp
// CORRECTO
public interface ICanalRepository : IEFRepository { ... }

// INCORRECTO
public interface ICanalRepository
{
    Task SaveChanges(...);  // no redeclarar
}
```

### SC-REP-02: Separación IQueryable / materialización

La interfaz expone dos niveles de acceso a datos:

**Nivel 1 — Composición:** `IQueryable<T>` para que el Domain Service pueda aplicar filtros, ordenamiento y paginación antes de ejecutar la consulta.

**Nivel 2 — Materialización:** métodos que reciben la query ya compuesta y la ejecutan. Esto mantiene la lógica de filtrado en el Domain Service y la ejecución en la infraestructura.

```csharp
// Nivel 1: el Domain Service compone la query
IQueryable<Canal> ConsultarCanales();

// Nivel 2: el repositorio materializa la query que le llega
Task<Canal?> ObtenerCanal(IOrderedQueryable<Canal> query, CancellationToken ct = default);
Task<List<Canal>> ObtenerCanales(IQueryable<Canal> query, CancellationToken ct = default);
```

Los métodos de materialización reciben la query como parámetro — no aplican filtros propios.

### SC-REP-03: Propiedades de navegacion opcionales (Incluir*)

Las propiedades de navegacion no se cargan automáticamente en `Consultar{Entidad}s()`. Cada propiedad de navegacion que puede necesitarse en algún caso de uso se expone como un metodo `Incluir{NavProp}` que recibe y retorna el mismo `IQueryable<T>`. El Domain Service compone los includes que necesita antes de materializar.

```csharp
// En la interfaz — un metodo por propiedad de navegacion
IQueryable<Version> IncluirProyecto(IQueryable<Version> query);
IQueryable<Version> IncluirEsquema(IQueryable<Version> query);
IQueryable<Version> IncluirConjuntoScripts(IQueryable<Version> query);
```

Queda prohibido un metodo `IncluirTodo()` o similar que cargue todas las navegaciones incondicionalmente.

**En el Domain Service, la composicion es explícita por caso de uso:**

```csharp
var query = _repositorioVersion.ConsultarVersiones();
query = _repositorioVersion.IncluirProyecto(query);
query = _repositorioVersion.IncluirConjuntoScripts(query);
var version = await _repositorioVersion.ObtenerVersion(
    query.Where(v => v.Id == versionId).OrderByDescending(v => v.FechaCreacionUtc), ct);
```

Los métodos `Incluir*` no aplican filtros propios — solo agregan `Include` / `ThenInclude` a la query recibida. El repositorio encapsula la profundidad del include (los `ThenInclude` anidados); el Domain Service no necesita conocerla.

```csharp
// Implementacion — encapsula la profundidad
public IQueryable<Version> IncluirConjuntoScripts(IQueryable<Version> query)
    => query.Include(v => v.ConjuntoScripts)
            .ThenInclude(cs => cs.Scripts)
            .ThenInclude(s => s.Cambios);
```

Los métodos `Incluir*` no llevan `LogDebug` porque no ejecutan consulta — solo componen una expresion.

### SC-REP-04: Métodos con filtro fijo

Cuando existe un repositorio con cache, se declaran métodos adicionales con filtro fijo (ej. `ObtenerPorId`). Estos son los únicos candidatos a ser cacheados porque su resultado es determinista para un valor de clave dado.

```csharp
// Solo se declara si habrá cache, o si el acceso por ID es muy frecuente
Task<Canal?> ObtenerPorId(Guid canalId, CancellationToken ct = default);
```

---

## Contratos de la implementación

### SC-REP-05: Herencia obligatoria

Todo repositorio hereda de `EFRepository<TContext>` del paquete `Bisoft.DatabaseConnections.Repositories`. Queda prohibido heredar directamente de `DbContext` o acceder al contexto por otro medio.

```csharp
// CORRECTO
public class CanalRepository : EFRepository<CanalContext>, ICanalRepository

// INCORRECTO
public class CanalRepository : ICanalRepository
{
    private readonly CanalContext _context;  // inyectar el contexto directamente
}
```

### SC-REP-06: Constructor y LoggerWrapper

El constructor recibe `(TContext context, LoggerWrapper<{Entidad}Repository> logger)` y los pasa a `base`. Los campos `_context` y `_logger` **no se redeclaran** — vienen de la clase base. El tipo genérico del logger es siempre la clase concreta del repositorio, no la interfaz.

```csharp
// CORRECTO
public CanalRepository(CanalContext context, LoggerWrapper<CanalRepository> logger)
    : base(context, logger) { }

// INCORRECTO — no usar ILogger<T> en repositorios
public CanalRepository(CanalContext context, ILogger<CanalRepository> logger) { }
```

### SC-REP-07: Logging en operaciones de datos

Cada método del repositorio registra exactamente un `LogDebug` antes de ejecutar la operación. El mensaje incluye el identificador relevante como parámetro nombrado. Nunca interpolación de strings.

```csharp
// CORRECTO
_logger.LogDebug("Consultando canal con id: {CanalId}", canalId);

// INCORRECTO
_logger.LogDebug($"Consultando canal con id: {canalId}");
_logger.LogInformation("...");  // nivel incorrecto para acceso a datos
```

### SC-REP-08: Escritura sin SaveChanges

Los métodos de escritura (`Crear`, `Actualizar`, `Eliminar`) **no llaman `SaveChanges`**. La confirmación de cambios es responsabilidad exclusiva del Domain Service, que puede agrupar múltiples operaciones en una sola transacción.

```csharp
public async Task Crear(Canal canal, CancellationToken ct = default)
{
    _logger.LogDebug("Creando canal con id: {CanalId}", canal.Id);
    await _context.Canales.AddAsync(canal, ct);
    // NO llamar SaveChanges aquí
}
```

---

## Contratos del repositorio con cache

### SC-REP-09: Herencia sobre el repositorio base

El repositorio con cache hereda del repositorio concreto, no implementa la interfaz directamente. Esto permite reutilizar todos los métodos del base y sobreescribir solo los que se benefician de cache.

```csharp
// CORRECTO
public class CanalCachedRepository : CanalRepository

// INCORRECTO
public class CanalCachedRepository : EFRepository<CanalContext>, ICanalRepository
```

El tipo genérico del `LoggerWrapper` es el del padre (`CanalRepository`), no el del cached.

### SC-REP-10: IQueryable no se cachea

`ConsultarCanales()` y los métodos de materialización (`ObtenerCanal`, `ObtenerCanales`) **no se sobreescriben** en el repositorio con cache. Un `IQueryable` nunca se cachea porque sus filtros son variables y su resultado no es determinista para una misma clave.

### SC-REP-11: Invalidación de cache en SaveChanges

El repositorio con cache sobreescribe `SaveChanges` para invalidar las entradas afectadas antes de delegar a `base`. El Domain Service propaga el ID de la entidad modificada en el diccionario de metadata.

```csharp
// Repositorio cached
public override Task SaveChanges(Dictionary<string, string>? transactionMetadata = null, CancellationToken ct = default)
{
    if (transactionMetadata?.TryGetValue("CanalId", out var canalId) ?? false)
    {
        _logger.LogDebug("Invalidando cache para canal con id: {CanalId}", canalId);
        _cache.Remove(canalId);
    }
    return base.SaveChanges(transactionMetadata, ct);
}

// Domain Service — propagar el ID al confirmar cambios
await _repositorioCanal.SaveChanges(
    new Dictionary<string, string> { ["CanalId"] = canal.Id.ToString() }, ct);
```
