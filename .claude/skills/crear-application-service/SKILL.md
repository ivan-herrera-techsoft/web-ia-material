---
name: crear-application-service
description: Crea un Application Service que orquesta Domain Services, mapea DTOs con Mapster y expone reportes
argument-hint: <NombreEntidad en español> ej. "Canal", "Version", "Producto"
---

Crear el Application Service para la entidad **$ARGUMENTS**.

Antes de generar los archivos, **preguntar al desarrollador**:
1. ¿Qué operaciones necesita? (listar, obtenerPorId, guardar, actualizar, eliminar — o combinacion)
2. ¿Hay operaciones en lote que procesen una coleccion en un solo request? (migraciones, importaciones, procesamiento masivo)
3. ¿Necesita reportes o proyecciones que agreguen datos de varias fuentes?
4. ¿El listado principal usa paginacion?
5. ¿Necesita acceder directamente a algun repositorio para proyecciones de solo lectura?

---

## Paso 1 — DTOs

Crear en `Application/Dtos/{Modulo}/` como `record` para cada operacion que lo requiera:

```csharp
// Request de creacion
public record Crear$ARGUMENTSRequest(
    string {Campo1},
    string {Campo2});

// Request de actualizacion
public record Actualizar$ARGUMENTSRequest(
    string {Campo1},
    string {Campo2});

// Response de detalle
public record Obtener$ARGUMENTSResponse(
    Guid Id,
    string {Campo1},
    string {Campo2},
    bool Activo,
    DateTime FechaCreacionUtc);

// Response de listado (puede ser mas reducido que el de detalle)
public record Obtener$ARGUMENTSsResponse(
    Guid Id,
    string {Campo1},
    bool Activo);
```

**Reglas:**
- `record` para todos los DTOs — son inmutables y sin logica
- Ubicar en `Application/Dtos/{Modulo}/` (subcarpeta por modulo o entidad)
- Sufijo `Request` para entrada, `Response` para salida
- El Response de listado puede omitir campos que no se muestran en tablas

---

## Paso 2 — Application Service

Crear `Application/Services/$ARGUMENTSService.cs`:

```csharp
using Bisoft.Logging.Util;
using {Namespace}.Application.Dtos.{Modulo};
using {Namespace}.Domain.Contracts.Repositories;
using {Namespace}.Domain.Services;
using Mapster;

namespace {Namespace}.Application.Services;

public class $ARGUMENTSService(
    $ARGUMENTSDomainService servicioDominio,
    I$ARGUMENTSRepository repositorio$ARGUMENTS,
    LoggerWrapper<$ARGUMENTSService> logger)
{
    private readonly $ARGUMENTSDomainService _servicioDominio = servicioDominio;
    private readonly I$ARGUMENTSRepository _repositorio$ARGUMENTS = repositorio$ARGUMENTS;
    private readonly LoggerWrapper<$ARGUMENTSService> _logger = logger;

    // --- Lecturas ---

    public IQueryable<Obtener$ARGUMENTSsResponse> Obtener$ARGUMENTSs()
        => _repositorio$ARGUMENTS.Consultar$ARGUMENTSs()
            .ProjectToType<Obtener$ARGUMENTSsResponse>();

    public async Task<Obtener$ARGUMENTSResponse> ObtenerPorId(Guid {argumentsId}, CancellationToken ct = default)
    {
        var {arguments} = await _servicioDominio.ObtenerPorId({argumentsId}, ct);
        return {arguments}.Adapt<Obtener$ARGUMENTSResponse>();
    }

    // --- Escrituras ---

    public async Task Guardar(Crear$ARGUMENTSRequest solicitud, CancellationToken ct = default)
    {
        await _servicioDominio.Guardar(solicitud.{Campo1}, solicitud.{Campo2}, ct);
        _logger.LogInformation("{arguments} creado a partir de solicitud");
    }

    public async Task Actualizar(Guid {argumentsId}, Actualizar$ARGUMENTSRequest solicitud, CancellationToken ct = default)
    {
        await _servicioDominio.Actualizar({argumentsId}, solicitud.{Campo1}, solicitud.{Campo2}, ct);
        _logger.LogInformation("{arguments} actualizado con id: {{ArgumentsId}}", {argumentsId});
    }

    public async Task Eliminar(Guid {argumentsId}, CancellationToken ct = default)
    {
        await _servicioDominio.Eliminar({argumentsId}, ct);
        _logger.LogInformation("{arguments} eliminado con id: {{ArgumentsId}}", {argumentsId});
    }
}
```

**Reglas:**
- `LoggerWrapper<$ARGUMENTSService>` — nunca `ILogger<T>`
- `LogInformation` exactamente uno por metodo publico de escritura, al final
- Las escrituras **delegan completamente** al DomainService — sin crear ni modificar entidades aqui
- Las lecturas pueden acceder al repositorio directamente con `ProjectToType<T>()` sin pasar por el DomainService
- Sin logica de negocio — si hay una validacion, pertenece al DomainService

---

## Paso 3 — Paginacion en listados (si aplica)

Si el listado usa paginacion, el Application Service retorna `IQueryable` y la paginacion se aplica en el endpoint:

```csharp
// Application Service — expone IQueryable proyectado
public IQueryable<Obtener$ARGUMENTSsResponse> Obtener$ARGUMENTSs()
    => _repositorio$ARGUMENTS.Consultar$ARGUMENTSs()
        .ProjectToType<Obtener$ARGUMENTSsResponse>();

// Endpoint — aplica paginacion sobre el IQueryable
private static IResult Handler(
    $ARGUMENTSService servicio,
    [AsParameters] SolicitudPaginacion paginacion,
    HttpContext contexto)
{
    var resultado = servicio.Obtener$ARGUMENTSs().ToListaPaginada(paginacion);
    contexto.AgregarHeaderPaginacion(resultado);
    return Results.Ok(resultado);
}
```

---

## Paso 4 — Reportes (si aplica)

Un reporte es una proyeccion de solo lectura que puede agregar datos de varias fuentes. Se implementa como un metodo en el Application Service que materializa y transforma datos sin pasar por el DomainService.

```csharp
// Reporte que agrega datos de varias entidades
public async Task<IEnumerable<Reporte$ARGUMENTSResponse>> ObtenerReporte$ARGUMENTS(
    DateTime fechaDesdeUtc,
    DateTime fechaHastaUtc,
    CancellationToken ct = default)
{
    _logger.LogDebug("Generando reporte de {argumentsPlural} desde {Desde} hasta {Hasta}",
        fechaDesdeUtc, fechaHastaUtc);

    var {argumentsPlural} = await _repositorio$ARGUMENTS.Obtener$ARGUMENTSs(
        _repositorio$ARGUMENTS.Consultar$ARGUMENTSs()
            .Where(x => x.FechaCreacionUtc >= fechaDesdeUtc && x.FechaCreacionUtc <= fechaHastaUtc)
            .OrderByDescending(x => x.FechaCreacionUtc),
        ct);

    return {argumentsPlural}.Adapt<IEnumerable<Reporte$ARGUMENTSResponse>>();
}
```

**Reglas de reportes:**
- Siempre `LogDebug` al inicio con los parametros del reporte (filtros, fechas, etc.)
- Sin `LogInformation` — un reporte es una lectura, no un evento de negocio
- Puede acceder a multiples repositorios para agregar datos
- Materializa con `await` — nunca retornar `IQueryable` en un reporte
- El DTO de reporte es un `record` separado, nunca reutilizar el DTO de listado

---

## Paso 5 — Operaciones en lote (si aplica)

Cuando una operacion requiere ejecutar el mismo evento de dominio múltiples veces sobre una coleccion en un solo request, usar `MultiResultHandler`:

```csharp
using {Namespace}.Application.Dtos.MultiResult;

public async Task<MultiResultHandler> Importar$ARGUMENTSs(
    IEnumerable<Importar$ARGUMENTSRequest> solicitudes,
    CancellationToken ct = default)
{
    _logger.LogDebug("Iniciando importacion de {Total} {argumentsPlural}", solicitudes.Count());
    var handler = new MultiResultHandler();

    foreach (var solicitud in solicitudes)
    {
        try
        {
            await _servicioDominio.Guardar(solicitud.{Campo1}, solicitud.{Campo2}, ct);
            handler.AddCreateAction(solicitud.{ClaveNegocio}, $"{arguments} {solicitud.{Campo1}} creado exitosamente");
        }
        catch (Exception ex)
        {
            handler.AddError(solicitud.{ClaveNegocio}, ex.Message);
        }
    }

    _logger.LogInformation("Importacion de {argumentsPlural} completada. Procesados: {Processed}, Fallidos: {Failed}",
        handler.Processed, handler.Failed);
    return handler;
}
```

El endpoint consume `MultiResultHandler` via `CustomResult.PartialContent` — ver skill `crear-endpoint`.

**Reglas:**
- El `try/catch` va en el Application Service — el DomainService lanza excepciones normalmente
- El `Key` del `Result` es el identificador de negocio (RFC, codigo, email), no el ID de BD
- `LogDebug` al inicio con el total, `LogInformation` al final con `Processed` y `Failed`
- Usar `AddCreateAction` / `AddUpdateAction` / `AddDeleteAction` segun el tipo de operacion

---

## Paso 6 — Registro DI

En `Api/Extensions/Configuration/ApplicationServiceExtensions.cs`:

```csharp
services.AddScoped<$ARGUMENTSService>();
```

---

## Resumen de archivos generados

```
Application/
├── Dtos/
│   └── {Modulo}/
│       ├── Crear$ARGUMENTSRequest.cs       (o en un solo archivo por entidad)
│       ├── Actualizar$ARGUMENTSRequest.cs
│       ├── Obtener$ARGUMENTSResponse.cs
│       └── Obtener$ARGUMENTSsResponse.cs
└── Services/
    └── $ARGUMENTSService.cs
```

Registro en `Api/Extensions/Configuration/ApplicationServiceExtensions.cs`.
