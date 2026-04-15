# Spec: Repositorio con Caché

## Propósito

Define los contratos para crear un repositorio con caché en memoria que extiende un repositorio base existente. El repositorio cached invalida su caché automáticamente al confirmar cambios via `SaveChanges`.

> **Prerequisito:** El repositorio base (`{Entidad}Repository`) y su interfaz (`I{Entidad}Repository`) DEBEN existir antes de crear el cached. Ver → [Spec: Repositorios de infraestructura](crear-repositorio.md).

---

## Contratos obligatorios

### SC-CACHE-01: Herencia del repositorio base, no de EFRepository

El repositorio cached DEBE heredar del **repositorio base concreto** (`{Entidad}Repository`), no de `EFRepository<TContext>` directamente. Esto permite que los métodos base se sobreescriban via polimorfismo.

```csharp
// CORRECTO
public class CanalCachedRepository : CanalRepository { ... }

// INCORRECTO
public class CanalCachedRepository : EFRepository<CanalContext>, ICanalRepository { ... }
```

---

### SC-CACHE-02: Logger tipado con el repositorio BASE

El tipo genérico de `LoggerWrapper<T>` DEBE ser el repositorio **padre** (`{Entidad}Repository`), no el cached. Esto mantiene consistencia en los logs con el repositorio base.

```csharp
public CanalCachedRepository(
    CanalContext context,
    LoggerWrapper<CanalRepository> logger,   // tipo del padre
    IMemoryCache cache,
    ICacheConfiguration configuracionCache)
    : base(context, logger) { }
```

`_context` y `_logger` **no se redeclaran** — vienen de `EFRepository<TContext>`.

---

### SC-CACHE-03: Solo los métodos de lectura con filtro fijo se sobreescriben

Únicamente los métodos que retornan una colección materializada con filtro constante (ej. `ObtenerPorId`, `ObtenerTodos`) se sobreescriben para aplicar caché. Los métodos que retornan `IQueryable<T>` **nunca se cachean**.

```csharp
// CORRECTO — sobreescribir lectura por ID
public override async Task<Canal?> ObtenerPorId(Guid canalId, CancellationToken ct = default)
{
    return await _cache.GetOrCreateAsync(canalId.ToString(), async entrada =>
    {
        entrada.SetSlidingExpiration(_configuracionCache.CacheSlidingDuration);
        entrada.SetAbsoluteExpiration(_configuracionCache.CacheAbsoluteDuration);
        _logger.LogDebug("Consultando canal con id: {CanalId}", canalId);
        return await _context.Canales
            .Where(x => x.Id == canalId)
            .FirstOrDefaultAsync(ct);
    }) ?? null;
}

// INCORRECTO — IQueryable no se cachea
public override IQueryable<Canal> ConsultarCanales() { ... }  // ❌
```

---

### SC-CACHE-04: Métodos del repositorio base marcados como virtual

Antes de crear el repositorio cached, verificar que los métodos que se van a sobreescribir estén marcados como `virtual` en el repositorio base. Si no lo están, agregarlos.

```csharp
// En CanalRepository — DEBE ser virtual
public virtual async Task<Canal?> ObtenerPorId(Guid canalId, CancellationToken ct = default)
{ ... }
```

---

### SC-CACHE-05: SaveChanges invalida la caché via metadata

El método `SaveChanges` se sobreescribe para extraer el ID de la entidad del diccionario de metadata y removerlo de la caché. Luego delega al base.

```csharp
public override Task SaveChanges(
    Dictionary<string, string>? transactionMetadata = null,
    CancellationToken ct = default)
{
    if (transactionMetadata?.TryGetValue("CanalId", out var canalId) ?? false)
    {
        _logger.LogDebug("Invalidando cache para canal con id: {CanalId}", canalId);
        _cache.Remove(canalId);
    }
    return base.SaveChanges(transactionMetadata, ct);
}
```

La clave del diccionario DEBE coincidir con la que el Domain Service pasa en `SaveChanges`.

---

### SC-CACHE-06: Registro DI — base como concreto, cached como implementación de interfaz

En el contenedor de DI, el repositorio base se registra como clase **concreta** (para que el cached pueda inyectarlo via herencia), y el cached se registra como implementación de la interfaz.

```csharp
services.AddScoped<CanalRepository>();
services.AddScoped<ICanalRepository, CanalCachedRepository>();
```

Si el caché puede estar deshabilitado por configuración:

```csharp
if (configuration.Cache.CacheEnabled)
    services.AddScoped<ICanalRepository, CanalCachedRepository>();
else
    services.AddScoped<ICanalRepository, CanalRepository>();
```

---

### SC-CACHE-07: AddMemoryCache condicional en Program.cs

El servicio `IMemoryCache` DEBE estar registrado. Se agrega condicionalmente en `Program.cs` según la configuración de caché:

```csharp
if (generalConfiguration.Cache.CacheEnabled)
    builder.Services.AddMemoryCache();
```

Si `CacheEnabled` es `false`, el repositorio cached nunca se registra (SC-CACHE-06), por lo que `IMemoryCache` tampoco se necesita.

---

### SC-CACHE-08: Duración de caché desde ICacheConfiguration

Las duraciones se leen de `ICacheConfiguration` (paquete `Bisoft.DatabaseConnections`), nunca hardcodeadas:

- `CacheSlidingDuration` — duración deslizante (se renueva con cada acceso)
- `CacheAbsoluteDuration` — duración máxima absoluta independiente de accesos

```csharp
entrada.SetSlidingExpiration(_configuracionCache.CacheSlidingDuration);
entrada.SetAbsoluteExpiration(_configuracionCache.CacheAbsoluteDuration);
```
