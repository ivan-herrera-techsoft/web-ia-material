---
name: crear-domain-service
description: Crea un Domain Service con logica de negocio, validaciones y orquestacion de repositorios
argument-hint: <NombreEntidad en español> ej. "Canal", "Version", "Producto"
---

Crear el Domain Service para la entidad **$ARGUMENTS**.

Antes de generar el archivo, **preguntar al desarrollador**:
1. ¿Qué operaciones necesita? (guardar, actualizar, eliminar, alternar, consultar — o combinacion)
2. ¿Hay validaciones de unicidad u otras reglas de negocio antes de crear o actualizar?
3. ¿Qué propiedades de navegacion necesita cargar en alguna operacion? (para usar `Incluir*`)
4. ¿El repositorio tiene cache? (para propagar metadata correcta en `SaveChanges`)

---

## Paso 1 — Domain Service

Crear `Domain/Services/$ARGUMENTSDomainService.cs`:

```csharp
using Bisoft.Exceptions;
using Bisoft.Logging.Util;
using {Namespace}.Domain.Contracts.Repositories;
using {Namespace}.Domain.Entities.{Modulo};
using static {Namespace}.Domain.DomainConstants;

namespace {Namespace}.Domain.Services;

public class $ARGUMENTSDomainService(
    I$ARGUMENTSRepository repositorio$ARGUMENTS,
    LoggerWrapper<$ARGUMENTSDomainService> logger)
{
    private readonly I$ARGUMENTSRepository _repositorio$ARGUMENTS = repositorio$ARGUMENTS;
    private readonly LoggerWrapper<$ARGUMENTSDomainService> _logger = logger;

    // --- Consultas ---

    public IQueryable<$ARGUMENTS> Consultar$ARGUMENTSs()
        => _repositorio$ARGUMENTS.Consultar$ARGUMENTSs();

    public async Task<$ARGUMENTS> ObtenerPorId(Guid {argumentsId}, CancellationToken ct = default)
    {
        _logger.LogDebug("Obteniendo {arguments} con id: {{ArgumentsId}}", {argumentsId});
        return await _repositorio$ARGUMENTS.ObtenerPorId({argumentsId}, ct)
            ?? throw TNotFoundException.EntityNotFound(
                "No existe un {arguments} con id {{ArgumentsId}}",
                new Dictionary<string, object> { ["{ArgumentsId}"] = {argumentsId} });
    }

    // --- Escritura ---

    public async Task<$ARGUMENTS> Guardar({params}, CancellationToken ct = default)
    {
        // Validaciones de negocio (unicidad, reglas cruzadas, etc.)
        await Validar{ReglaDeNegocio}({param}, ct);

        // Crear la entidad invocando Guards via constructor
        var {arguments} = new $ARGUMENTS({params});

        // Persistir y confirmar
        await _repositorio$ARGUMENTS.Crear({arguments}, ct);
        await _repositorio$ARGUMENTS.SaveChanges(
            new Dictionary<string, string> { ["{ArgumentsId}"] = {arguments}.Id.ToString() }, ct);

        _logger.LogInformation("{arguments} creado con id: {{ArgumentsId}}", {arguments}.Id);
        return {arguments};
    }

    public async Task Actualizar(Guid {argumentsId}, {params}, CancellationToken ct = default)
    {
        // Obtener la entidad (lanza TNotFoundException si no existe)
        var {arguments} = await ObtenerPorId({argumentsId}, ct);

        // Validaciones de negocio si aplica
        await Validar{ReglaDeNegocio}({param}, ct);

        // Aplicar cambios via metodo de dominio
        {arguments}.Actualizar({params});

        // Persistir y confirmar
        await _repositorio$ARGUMENTS.Actualizar({arguments}, ct);
        await _repositorio$ARGUMENTS.SaveChanges(
            new Dictionary<string, string> { ["{ArgumentsId}"] = {arguments}.Id.ToString() }, ct);

        _logger.LogInformation("{arguments} actualizado con id: {{ArgumentsId}}", {argumentsId});
    }

    public async Task Eliminar(Guid {argumentsId}, CancellationToken ct = default)
    {
        var {arguments} = await ObtenerPorId({argumentsId}, ct);
        _repositorio$ARGUMENTS.Eliminar({arguments});
        await _repositorio$ARGUMENTS.SaveChanges(
            new Dictionary<string, string> { ["{ArgumentsId}"] = {arguments}.Id.ToString() }, ct);
        _logger.LogInformation("{arguments} eliminado con id: {{ArgumentsId}}", {argumentsId});
    }

    // --- Validaciones privadas ---

    private async Task Validar{ReglaDeNegocio}(string {campo}, CancellationToken ct = default)
    {
        _logger.LogDebug("Validando {regla} para {arguments}: {{Campo}}", {campo});
        var existente = await _repositorio$ARGUMENTS.ObtenerCanal(
            _repositorio$ARGUMENTS.Consultar$ARGUMENTSs()
                .Where(x => x.{Campo} == {campo})
                .OrderBy(x => x.Id),
            ct);

        if (existente is not null)
            throw new TInvalidOperationException(
                ExceptionCodes.Operation.{ARGUMENTS_REGLA},
                "Ya existe un {arguments} con {campo} {{Campo}}",
                new Dictionary<string, object> { ["{Campo}"] = {campo} });
    }
}
```

**Reglas clave:**
- `LoggerWrapper<$ARGUMENTSDomainService>` — nunca `ILogger<T>`
- `LogDebug` al inicio de lecturas y validaciones internas
- `LogInformation` exactamente uno al final de cada metodo publico de escritura exitoso — solo en metodos publicos, solo al final, solo uno por metodo
- `TNotFoundException.EntityNotFound(msg, dict)` cuando no se encuentra la entidad
- `TInvalidOperationException(code, msg, dict)` para reglas de negocio; codigo en `DomainConstants.ExceptionCodes.Operation`
- `SaveChanges` siempre con metadata `{ ["{EntidadId}"] = id.ToString() }` para invalidar cache
- Sin mapeo de DTOs — trabaja solo con entidades de dominio
- No llamar a otros Domain Services — la orquestacion entre servicios va en el Application Service

---

## Paso 2 — Consultas con navegacion (si aplica)

Cuando el caso de uso necesite cargar propiedades de navegacion, componer los `Incluir*` antes de materializar:

```csharp
public async Task<$ARGUMENTS> ObtenerConDetalle(Guid {argumentsId}, CancellationToken ct = default)
{
    _logger.LogDebug("Obteniendo {arguments} con detalle, id: {{ArgumentsId}}", {argumentsId});
    var query = _repositorio$ARGUMENTS.Consultar$ARGUMENTSs();
    query = _repositorio$ARGUMENTS.Incluir{NavProp1}(query);
    query = _repositorio$ARGUMENTS.Incluir{NavProp2}(query);
    return await _repositorio$ARGUMENTS.Obtener$ARGUMENTS(
        query.Where(x => x.Id == {argumentsId}).OrderByDescending(x => x.FechaCreacionUtc), ct)
        ?? throw TNotFoundException.EntityNotFound(
            "No existe un {arguments} con id {{ArgumentsId}}",
            new Dictionary<string, object> { ["{ArgumentsId}"] = {argumentsId} });
}
```

---

## Paso 3 — Registro DI

En `Api/Extensions/Configuration/DomainServiceExtensions.cs` (o el archivo que registre servicios de dominio):

```csharp
services.AddScoped<$ARGUMENTSDomainService>();
```

---

## Resumen de archivos generados

```
Domain/
└── Services/
    └── $ARGUMENTSDomainService.cs
```

Registro en `Api/Extensions/Configuration/DomainServiceExtensions.cs`.
