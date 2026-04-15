---
name: crear-cached-repository
description: Crea un repositorio con cache en memoria sobre un repositorio base existente usando herencia
argument-hint: <NombreEntidad en español> ej. "Usuario", "Canal"
---

Crear el repositorio con cache para la entidad **$ARGUMENTS**.

> **Prerequisito:** `$ARGUMENTSRepository` y `I$ARGUMENTSRepository` deben existir antes de ejecutar este skill.
> Los metodos de lectura con filtro fijo en el repositorio base deben estar marcados como `virtual`.

---

## Paso 1 — Cached Repository

Crear `Infrastructure/Repositories/{Modulo}/$ARGUMENTSCachedRepository.cs`:

```csharp
using Bisoft.DatabaseConnections.Configuration;
using Bisoft.Logging.Util;
using {Namespace}.Domain.Entities.{Modulo};
using {Namespace}.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace {Namespace}.Infrastructure.Repositories.{Modulo};

public class $ARGUMENTSCachedRepository : $ARGUMENTSRepository
{
    private readonly IMemoryCache _cache;
    private readonly ICacheConfiguration _configuracionCache;

    public $ARGUMENTSCachedRepository(
        {Modulo}Context context,
        LoggerWrapper<$ARGUMENTSRepository> logger,
        IMemoryCache cache,
        ICacheConfiguration configuracionCache)
        : base(context, logger)
    {
        _cache = cache;
        _configuracionCache = configuracionCache;
    }

    public override async Task<$ARGUMENTS?> ObtenerPorId(Guid {argumentsId}, CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync({argumentsId}.ToString(), async entrada =>
        {
            entrada.SetSlidingExpiration(_configuracionCache.CacheSlidingDuration);
            entrada.SetAbsoluteExpiration(_configuracionCache.CacheAbsoluteDuration);
            _logger.LogDebug("Consultando {arguments} con id: {{ArgumentsId}}", {argumentsId});
            return await _context.{ArgumentsPlural}
                .Where(x => x.Id == {argumentsId})
                .FirstOrDefaultAsync(ct);
        }) ?? null;
    }

    public override Task SaveChanges(Dictionary<string, string>? transactionMetadata = null, CancellationToken ct = default)
    {
        if (transactionMetadata?.TryGetValue("{ArgumentsId}", out var {argumentsId}Id) ?? false)
        {
            _logger.LogDebug("Invalidando cache para {arguments} con id: {{ArgumentsId}}", {argumentsId}Id);
            _cache.Remove({argumentsId}Id);
        }
        return base.SaveChanges(transactionMetadata, ct);
    }
}
```

**Reglas:**
- Hereda de `$ARGUMENTSRepository` — no implementa la interfaz directamente
- `LoggerWrapper<$ARGUMENTSRepository>` (el tipo padre, no el cached)
- `_context` y `_logger` vienen de la base — **no redeclarar**
- Sobreescribir **solo** los metodos de lectura con filtro fijo (`ObtenerPorId`, etc.)
- Sobreescribir `SaveChanges` para invalidar la cache al escribir
- `IQueryable<T>` **no se cachea** — no sobreescribir `Consultar$ARGUMENTSs()`

---

## Paso 2 — Verificar `virtual` en el repositorio base

En `$ARGUMENTSRepository`, confirmar que los metodos sobreescritos esten marcados `virtual`:

```csharp
// Debe ser virtual
public virtual async Task<$ARGUMENTS?> ObtenerPorId(Guid {argumentsId}, CancellationToken ct = default)
```

Si no lo está, agregarlo antes de continuar.

---

## Paso 3 — Registro DI en ServiceExtensions

En `Api/Extensions/Configuration/InfrastructureServiceExtensions.cs`:

```csharp
// El repositorio base se registra como clase concreta para que el cached pueda inyectarlo via herencia
services.AddScoped<$ARGUMENTSRepository>();
services.AddScoped<I$ARGUMENTSRepository, $ARGUMENTSCachedRepository>();
```

> Si el cache está deshabilitado en algun entorno, usar un flag de configuracion:
> ```csharp
> if (configuration.CacheHabilitado)
>     services.AddScoped<I$ARGUMENTSRepository, $ARGUMENTSCachedRepository>();
> else
>     services.AddScoped<I$ARGUMENTSRepository, $ARGUMENTSRepository>();
> ```

---

## Paso 4 — Propagar metadata a SaveChanges desde el DomainService

Para que la cache se invalide correctamente, el DomainService debe pasar el ID en el diccionario:

```csharp
// En {Entidad}DomainService, al llamar SaveChanges:
await _repositorio$ARGUMENTS.SaveChanges(
    new Dictionary<string, string> { ["{ArgumentsId}"] = {arguments}.Id.ToString() },
    ct);
```

---

## Resumen de archivos generados

```
Infrastructure/
└── Repositories/
    └── {Modulo}/
        └── $ARGUMENTSCachedRepository.cs   (nuevo)
```

Modificaciones en:
- `$ARGUMENTSRepository.cs` → metodos sobreescritos marcados `virtual`
- `InfrastructureServiceExtensions.cs` → registro DI del cached
