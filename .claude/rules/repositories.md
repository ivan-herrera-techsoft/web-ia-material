---
description: Convenciones para repositorios e interfaces de contratos en infraestructura
globs: "**/Repositories/**,**/Contracts/Repositories/**"
---

## Interfaz de repositorio

- Ubicacion: `Domain/Contracts/Repositories/I{Entidad}Repository.cs`
- Hereda de `IEFRepository` (de `Bisoft.DatabaseConnections.Util.Abstractions`)
- Metodos en **español**, sin sufijo Async

```csharp
using Bisoft.DatabaseConnections.Util.Abstractions;

public interface ICanalRepository : IEFRepository
{
    // IQueryable para filtros variables — el servicio compone la query
    IQueryable<Canal> ConsultarCanales();

    // Materializar la query (el repo ejecuta el FirstOrDefault / ToList)
    Task<Canal?> ObtenerCanal(IOrderedQueryable<Canal> query, CancellationToken ct = default);
    Task<List<Canal>> ObtenerCanales(IQueryable<Canal> query, CancellationToken ct = default);

    // Metodos con filtro fijo (candidatos a cache)
    Task<Canal?> ObtenerPorId(Guid canalId, CancellationToken ct = default);

    // Escritura
    Task Crear(Canal canal, CancellationToken ct = default);
    Task Actualizar(Canal canal, CancellationToken ct = default);
    void Eliminar(Canal canal);
}
```

**Cuando incluir `ObtenerPorId` separado:** cuando el repositorio implementara cache.
`SaveChanges` **no** se declara en la interfaz especifica — viene de `IEFRepository`.

---

## Propiedades de navegacion (Incluir*)

Las propiedades de navegacion **no se incluyen automáticamente** en `ConsultarCanales()`. Cada propiedad de navegacion que puede necesitarse se expone como un metodo `Incluir{NavProp}` que recibe y retorna el `IQueryable<T>`. El Domain Service compone los includes que necesita para cada caso de uso.

**Interfaz:**

```csharp
public interface IVersionRepository : IEFRepository
{
    IQueryable<Version> ConsultarVersiones();
    IQueryable<Version> IncluirProyecto(IQueryable<Version> query);
    IQueryable<Version> IncluirEsquema(IQueryable<Version> query);
    IQueryable<Version> IncluirManifiesto(IQueryable<Version> query);
    IQueryable<Version> IncluirConjuntoScripts(IQueryable<Version> query);
    // ... materialización y escritura
}
```

**Implementacion:**

```csharp
public IQueryable<Version> IncluirProyecto(IQueryable<Version> query)
    => query.Include(v => v.Proyecto)
            .ThenInclude(p => p.Ambientes);

public IQueryable<Version> IncluirEsquema(IQueryable<Version> query)
    => query.Include(v => v.Esquema);

public IQueryable<Version> IncluirConjuntoScripts(IQueryable<Version> query)
    => query.Include(v => v.ConjuntoScripts)
            .ThenInclude(cs => cs.Scripts)
            .ThenInclude(s => s.Cambios);
```

**Uso en el Domain Service:**

```csharp
var query = _repositorioVersion.ConsultarVersiones();
query = _repositorioVersion.IncluirProyecto(query);
query = _repositorioVersion.IncluirConjuntoScripts(query);
var version = await _repositorioVersion.ObtenerVersion(
    query.Where(v => v.Id == versionId).OrderByDescending(v => v.FechaCreacionUtc), ct);
```

**Reglas:**
- Un metodo `Incluir*` por propiedad de navegacion — nunca un `IncluirTodo()`
- El metodo encapsula los `ThenInclude` necesarios; el Domain Service no conoce la profundidad
- Los metodos `Incluir*` no aplican filtros — solo agregan includes a la query recibida
- Sin `LogDebug` en `Incluir*` — no ejecutan consulta, solo componen la expresion

---

## Implementacion de repositorio

- Ubicacion: `Infrastructure/Repositories/{Modulo}/{Entidad}Repository.cs`
- Hereda de `EFRepository<TContext>` (de `Bisoft.DatabaseConnections.Repositories`)
- Constructor recibe `(TContext context, LoggerWrapper<{Entidad}Repository> logger)` y pasa a `base`
- `_context` y `_logger` vienen de la clase base — **no redeclarar**
- `LogDebug` en cada operacion de datos; incluir el identificador relevante
- Sin logica de negocio — solo acceso a datos

```csharp
using Bisoft.DatabaseConnections.Repositories;
using Bisoft.Logging.Util;

public class CanalRepository : EFRepository<CanalContext>, ICanalRepository
{
    public CanalRepository(CanalContext context, LoggerWrapper<CanalRepository> logger)
        : base(context, logger) { }

    public IQueryable<Canal> ConsultarCanales()
    {
        _logger.LogDebug("Creando consulta de canales");
        return _context.Canales;
    }

    public async Task<Canal?> ObtenerCanal(IOrderedQueryable<Canal> query, CancellationToken ct = default)
    {
        _logger.LogDebug("Consultando canal");
        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<List<Canal>> ObtenerCanales(IQueryable<Canal> query, CancellationToken ct = default)
    {
        _logger.LogDebug("Consultando lista de canales");
        return await query.ToListAsync(ct);
    }

    public virtual async Task<Canal?> ObtenerPorId(Guid canalId, CancellationToken ct = default)
    {
        _logger.LogDebug("Consultando canal con id: {CanalId}", canalId);
        return await _context.Canales
            .Where(c => c.Id == canalId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task Crear(Canal canal, CancellationToken ct = default)
    {
        _logger.LogDebug("Creando canal con id: {CanalId}", canal.Id);
        await _context.Canales.AddAsync(canal, ct);
    }

    public Task Actualizar(Canal canal, CancellationToken ct = default)
    {
        _logger.LogDebug("Actualizando canal con id: {CanalId}", canal.Id);
        _context.Canales.Update(canal);
        return Task.CompletedTask;
    }

    public void Eliminar(Canal canal)
    {
        _logger.LogDebug("Eliminando canal con id: {CanalId}", canal.Id);
        _context.Canales.Remove(canal);
    }
}
```

**`virtual` en `ObtenerPorId`:** declarar `virtual` cuando el cached repository necesita sobreescribirlo.

---

## Repositorio con cache

Cuando la entidad es un catalogo poco modificado o tiene filtros constantes:

- Ubicacion: `Infrastructure/Repositories/{Modulo}/{Entidad}CachedRepository.cs`
- Hereda de `{Entidad}Repository` (no es pure Decorator, es herencia)
- Sobreescribe solo los metodos de lectura con filtro fijo (ej. `ObtenerPorId`)
- Sobreescribe `SaveChanges` para invalidar la cache tras escritura
- Inyecta `IMemoryCache` y `ICacheConfiguration`
- El `LoggerWrapper` recibe el tipo del padre: `LoggerWrapper<{Entidad}Repository>`

```csharp
public class CanalCachedRepository : CanalRepository
{
    private readonly IMemoryCache _cache;
    private readonly ICacheConfiguration _configuracionCache;

    public CanalCachedRepository(
        CanalContext context,
        LoggerWrapper<CanalRepository> logger,
        IMemoryCache cache,
        ICacheConfiguration configuracionCache)
        : base(context, logger)
    {
        _cache = cache;
        _configuracionCache = configuracionCache;
    }

    public override async Task<Canal?> ObtenerPorId(Guid canalId, CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(canalId.ToString(), async entrada =>
        {
            entrada.SetSlidingExpiration(_configuracionCache.CacheSlidingDuration);
            entrada.SetAbsoluteExpiration(_configuracionCache.CacheAbsoluteDuration);
            _logger.LogDebug("Consultando canal con id: {CanalId}", canalId);
            return await _context.Canales
                .Where(c => c.Id == canalId)
                .FirstOrDefaultAsync(ct);
        }) ?? null;
    }

    public override Task SaveChanges(Dictionary<string, string>? transactionMetadata = null, CancellationToken ct = default)
    {
        if (transactionMetadata?.TryGetValue("CanalId", out var canalId) ?? false)
        {
            _logger.LogDebug("Invalidando cache para canal con id: {CanalId}", canalId);
            _cache.Remove(canalId);
        }
        return base.SaveChanges(transactionMetadata, ct);
    }
}
```

---

## Naming de metodos

| Operacion                     | Patron                  | Ejemplo                              |
|-------------------------------|-------------------------|--------------------------------------|
| IQueryable listado            | `Consultar{Entidad}s`   | `ConsultarCanales()`                 |
| Materializar uno (query)      | `Obtener{Entidad}`      | `ObtenerCanal(query)`                |
| Materializar lista (query)    | `Obtener{Entidad}s`     | `ObtenerCanales(query)`              |
| Obtener por ID (filtro fijo)  | `ObtenerPorId`          | `ObtenerPorId(id)`                   |
| Crear                         | `Crear`                 | `Crear(canal)`                       |
| Actualizar                    | `Actualizar`            | `Actualizar(canal)`                  |
| Eliminar                      | `Eliminar`              | `Eliminar(canal)`                    |

---

## Reglas

- `EFRepository<TContext>` como base — nunca heredar de `DbContext` directamente
- `LoggerWrapper<{Entidad}Repository>` como logger — nunca `ILogger<T>`
- `_context` y `_logger` vienen de la base — no redeclarar ni inyectar de nuevo
- `LogDebug` en cada operacion con el identificador relevante
- `SaveChanges` se llama desde el Domain Service, **no** desde el repositorio especifico
- Los metodos de escritura (`Crear`, `Actualizar`, `Eliminar`) no llaman `SaveChanges`
- `virtual` en metodos sobreescritos por el repositorio cached
- Sin logica de negocio — solo acceso a datos
- `CancellationToken ct` en todos los metodos async
- Siempre implementar una interfaz por repositorio (segregacion de interfaces)
