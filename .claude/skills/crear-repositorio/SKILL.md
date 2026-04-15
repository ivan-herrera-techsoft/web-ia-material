---
name: crear-repositorio
description: Scaffolda interfaz de repositorio (Domain), implementacion (Infrastructure) y registro en ServiceExtensions para una entidad
argument-hint: <NombreEntidad en español> ej. "Canal", "Usuario", "Producto"
---

Crear el repositorio completo para la entidad **$ARGUMENTS**.

Antes de generar los archivos, **preguntar al desarrollador**:
1. ¿Qué modulo pertenece la entidad? (ej. `Seguridad`, `Notificaciones`, `Ventas`)
2. ¿El repositorio necesita cache? (si/no). Si sí → ejecutar también `crear-cached-repository`
3. ¿Qué operaciones necesita? (listar, obtenerPorId, crear, actualizar, eliminar — o combinacion)
4. ¿Tiene propiedades de navegacion que se cargarán de forma opcional? (listar con su profundidad, ej. `Proyecto → Ambientes`, `ConjuntoScripts → Scripts → Cambios`)

---

## Paso 1 — Interfaz de repositorio

Crear `Domain/Contracts/Repositories/I$ARGUMENTSRepository.cs`:

```csharp
using Bisoft.DatabaseConnections.Util.Abstractions;
using {Namespace}.Domain.Entities.{Modulo};

namespace {Namespace}.Domain.Contracts.Repositories;

public interface I$ARGUMENTSRepository : IEFRepository
{
    // IQueryable para filtros variables — el servicio compone la query
    IQueryable<$ARGUMENTS> Consultar$ARGUMENTSs();

    // Materializar la query
    Task<$ARGUMENTS?> Obtener$ARGUMENTS(IOrderedQueryable<$ARGUMENTS> query, CancellationToken ct = default);
    Task<List<$ARGUMENTS>> Obtener$ARGUMENTSs(IQueryable<$ARGUMENTS> query, CancellationToken ct = default);

    // Metodo con filtro fijo — declarar virtual si habrá repositorio cached
    Task<$ARGUMENTS?> ObtenerPorId(Guid {argumentsId}, CancellationToken ct = default);

    // Escritura
    Task Crear($ARGUMENTS {arguments}, CancellationToken ct = default);
    Task Actualizar($ARGUMENTS {arguments}, CancellationToken ct = default);
    void Eliminar($ARGUMENTS {arguments});
}
```

Si la entidad tiene propiedades de navegacion opcionales, agregar en la interfaz un metodo `Incluir{NavProp}` por cada una:

```csharp
// Propiedades de navegacion opcionales — un metodo por propiedad
IQueryable<$ARGUMENTS> Incluir{NavProp1}(IQueryable<$ARGUMENTS> query);
IQueryable<$ARGUMENTS> Incluir{NavProp2}(IQueryable<$ARGUMENTS> query);
```

**Reglas:**
- Hereda `IEFRepository` — `SaveChanges` viene de ahí, no se redeclara
- Solo incluir los metodos que realmente se usan
- Si no habrá cache, omitir `ObtenerPorId` y usar solo `IQueryable` + materialización
- Un metodo `Incluir*` por propiedad de navegacion — nunca un `IncluirTodo()`

---

## Paso 2 — Implementacion del repositorio

Crear `Infrastructure/Repositories/{Modulo}/$ARGUMENTSRepository.cs`:

```csharp
using Bisoft.DatabaseConnections.Repositories;
using Bisoft.Logging.Util;
using {Namespace}.Domain.Contracts.Repositories;
using {Namespace}.Domain.Entities.{Modulo};
using {Namespace}.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace {Namespace}.Infrastructure.Repositories.{Modulo};

public class $ARGUMENTSRepository : EFRepository<{Modulo}Context>, I$ARGUMENTSRepository
{
    public $ARGUMENTSRepository({Modulo}Context context, LoggerWrapper<$ARGUMENTSRepository> logger)
        : base(context, logger) { }

    public IQueryable<$ARGUMENTS> Consultar$ARGUMENTSs()
    {
        _logger.LogDebug("Creando consulta de {argumentsPlural}");
        return _context.{ArgumentsPlural};
    }

    public async Task<$ARGUMENTS?> Obtener$ARGUMENTS(IOrderedQueryable<$ARGUMENTS> query, CancellationToken ct = default)
    {
        _logger.LogDebug("Consultando {arguments}");
        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<List<$ARGUMENTS>> Obtener$ARGUMENTSs(IQueryable<$ARGUMENTS> query, CancellationToken ct = default)
    {
        _logger.LogDebug("Consultando lista de {argumentsPlural}");
        return await query.ToListAsync(ct);
    }

    // Marcar virtual si habrá repositorio cached que lo sobreescriba
    public virtual async Task<$ARGUMENTS?> ObtenerPorId(Guid {argumentsId}, CancellationToken ct = default)
    {
        _logger.LogDebug("Consultando {arguments} con id: {{ArgumentsId}}", {argumentsId});
        return await _context.{ArgumentsPlural}
            .Where(x => x.Id == {argumentsId})
            .FirstOrDefaultAsync(ct);
    }

    public async Task Crear($ARGUMENTS {arguments}, CancellationToken ct = default)
    {
        _logger.LogDebug("Creando {arguments} con id: {{ArgumentsId}}", {arguments}.Id);
        await _context.{ArgumentsPlural}.AddAsync({arguments}, ct);
    }

    public Task Actualizar($ARGUMENTS {arguments}, CancellationToken ct = default)
    {
        _logger.LogDebug("Actualizando {arguments} con id: {{ArgumentsId}}", {arguments}.Id);
        _context.{ArgumentsPlural}.Update({arguments});
        return Task.CompletedTask;
    }

    public void Eliminar($ARGUMENTS {arguments})
    {
        _logger.LogDebug("Eliminando {arguments} con id: {{ArgumentsId}}", {arguments}.Id);
        _context.{ArgumentsPlural}.Remove({arguments});
    }
}
```

Si la entidad tiene propiedades de navegacion, agregar un metodo `Incluir*` por cada una. Cada metodo recibe y retorna el `IQueryable<T>` sin aplicar filtros propios:

```csharp
public IQueryable<$ARGUMENTS> Incluir{NavProp1}(IQueryable<$ARGUMENTS> query)
    => query.Include(x => x.{NavProp1});

// Con ThenInclude cuando la navegacion tiene profundidad
public IQueryable<$ARGUMENTS> Incluir{NavProp2}(IQueryable<$ARGUMENTS> query)
    => query.Include(x => x.{NavProp2})
            .ThenInclude(y => y.{SubNavProp})
            .ThenInclude(z => z.{SubSubNavProp});
```

Los metodos `Incluir*` no llevan `LogDebug` — no ejecutan consulta, solo componen la expresion.

**Reglas clave:**
- `_context` y `_logger` vienen de `EFRepository<T>` — **no redeclarar**
- `LoggerWrapper<$ARGUMENTSRepository>` — nunca `ILogger<T>`
- `LogDebug` en cada operacion con el identificador relevante
- Los metodos de escritura **no llaman `SaveChanges`** — lo hace el DomainService
- Marcar `virtual` los metodos que el repositorio cached sobreescribirá
- `Incluir*` no lleva `LogDebug` y no aplica filtros — solo agrega includes

---

## Paso 3 — Registro en ServiceExtensions

En `Api/Extensions/Configuration/InfrastructureServiceExtensions.cs` (o el archivo que registre infraestructura), agregar dentro del metodo `ConfigureRepositories`:

```csharp
// Sin cache
services.AddScoped<I$ARGUMENTSRepository, $ARGUMENTSRepository>();

// Con cache (usar uno u otro segun corresponda)
services.AddScoped<$ARGUMENTSRepository>(); // base siempre necesaria para el cached
services.AddScoped<I$ARGUMENTSRepository, $ARGUMENTSCachedRepository>();
```

Si no existe aún el metodo `ConfigureRepositories`, crearlo como extension method:

```csharp
public static IServiceCollection ConfigureRepositories(this IServiceCollection services)
{
    services.AddScoped<I$ARGUMENTSRepository, $ARGUMENTSRepository>();
    // ... otros repositorios
    return services;
}
```

---

## Paso 4 — Verificacion de DbSet en el Context

Verificar que `Infrastructure/Contexts/{Modulo}Context.cs` tenga el `DbSet` correspondiente:

```csharp
public DbSet<$ARGUMENTS> {ArgumentsPlural} { get; set; }
```

Si no existe → agregarlo al contexto antes de continuar.

---

## Resumen de archivos generados

```
Domain/
└── Contracts/
    └── Repositories/
        └── I$ARGUMENTSRepository.cs

Infrastructure/
└── Repositories/
    └── {Modulo}/
        └── $ARGUMENTSRepository.cs
```

Registro en `Api/Extensions/Configuration/InfrastructureServiceExtensions.cs`.
