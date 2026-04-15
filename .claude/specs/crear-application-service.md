# Spec: Application Service

## Propósito

Define los contratos que debe cumplir un Application Service en términos de DDD: coordinación de casos de uso, mapeo entre entidades y DTOs, y generación de reportes. El Application Service es el punto de entrada de las operaciones del sistema hacia la capa de dominio; no contiene lógica de negocio y no toma decisiones sobre el estado de las entidades.

> **Spec relacionado:** Las operaciones de escritura se delegan al Domain Service → [Spec: Domain Service](crear-domain-service.md). Los DTOs de request se validan en el endpoint antes de llegar aquí → [Rules: endpoints.md](../rules/endpoints.md).

---

## Contratos del Application Service

### SC-AS-01: Frontera de responsabilidades (DDD)

En DDD, el Application Service coordina pero no decide. Las tres responsabilidades exclusivas de esta capa son:

1. **Orquestar** — invocar el DomainService con los parámetros extraídos del DTO de entrada
2. **Mapear** — transformar entidades en DTOs de respuesta con Mapster
3. **Observar** — registrar el resultado con `LogInformation` tras operaciones exitosas

Queda prohibido en el Application Service:

- Crear o modificar entidades de dominio directamente (responsabilidad del DomainService)
- Aplicar lógica de negocio o validaciones de dominio
- Llamar a `SaveChanges` (responsabilidad del DomainService)
- Retornar entidades de dominio.

```csharp
// CORRECTO — el Application Service solo orquesta y mapea
public async Task Guardar(CrearCanalRequest solicitud, CancellationToken ct = default)
{
    await _servicioDominio.Guardar(solicitud.Nombre, solicitud.Descripcion, ct);
    _logger.LogInformation("Canal creado a partir de solicitud");
}

// INCORRECTO — el Application Service no crea entidades ni llama SaveChanges
public async Task Guardar(CrearCanalRequest solicitud, CancellationToken ct = default)
{
    var canal = new Canal(solicitud.Nombre, solicitud.Descripcion);  // ❌ dominio
    await _repositorioCanal.Crear(canal, ct);                        // ❌ persistencia
    await _repositorioCanal.SaveChanges(ct: ct);                     // ❌ transaccion
}
```

### SC-AS-02: Acceso directo a repositorios en lecturas

Para operaciones de solo lectura (listados, proyecciones, reportes), el Application Service puede acceder directamente al repositorio sin pasar por el DomainService. Esto evita materializar entidades completas cuando solo se necesita una proyección.

```csharp
// CORRECTO — proyeccion directa desde repositorio sin materializar entidades
public IQueryable<ObtenerCanalesResponse> ObtenerCanales()
    => _repositorioCanal.ConsultarCanales()
        .ProjectToType<ObtenerCanalesResponse>();

// INCORRECTO — materializar entidades para luego mappearlas (costoso)
public async Task<List<ObtenerCanalesResponse>> ObtenerCanales(CancellationToken ct = default)
{
    var canales = await _servicioDominio.ObtenerTodosLosCanales(ct);  // lista de entidades completas
    return canales.Adapt<List<ObtenerCanalesResponse>>();
}
```

### SC-AS-03: Mapeo con Mapster

Todo mapeo entre entidades y DTOs usa Mapster o proyecciones de LINQ.

| Escenario | Patron |
|---|---|
| Entidad → DTO de respuesta | `entidad.Adapt<Response>()` |
| IQueryable → IQueryable proyectado | `query.ProjectToType<Response>()` |
| Lista de entidades → lista de DTOs | `lista.Adapt<IEnumerable<Response>>()` |

### SC-AS-04: DTOs como records

Todos los DTOs de entrada y salida se declaran como `record`. No tienen lógica, solo transportan datos.

```csharp
// Request — inmutable, transporta los datos del cliente
public record CrearCanalRequest(string Nombre, string Descripcion);

// Response — inmutable, transporta los datos al cliente
public record ObtenerCanalResponse(Guid Id, string Nombre, bool Activo, DateTime FechaCreacionUtc);
```

Los records de Response de listado pueden omitir campos que no se muestran en tablas para evitar over-fetching. Para el detalle se usa un Response más completo.

### SC-AS-05: Logging en Application Service

`LogInformation` exactamente uno al final de cada método que realice una operación además de una llamada al dominio. `LogDebug` al inicio de reportes u operaciones de lectura que lo justifiquen.

```csharp
// Reporte — LogDebug al inicio con los parametros de filtrado
public async Task<IEnumerable<ReporteCanalResponse>> ObtenerReporte(
    DateTime desdeUtc, DateTime hastaUtc, CancellationToken ct = default)
{
    _logger.LogDebug("Generando reporte de canales desde {Desde} hasta {Hasta}", desdeUtc, hastaUtc);
    // ...
}
```

### SC-AS-06: Paginacion via IQueryable

Cuando el listado soporta paginación, el Application Service retorna `IQueryable<Response>` proyectado. La paginación se aplica en el endpoint, no en el servicio.

```csharp
// Application Service — IQueryable proyectado, sin materializar ni paginar
public IQueryable<ObtenerCanalesResponse> ObtenerCanales()
    => _repositorioCanal.ConsultarCanales()
        .ProjectToType<ObtenerCanalesResponse>();

// Endpoint — aplica paginacion
var resultado = servicio.ObtenerCanales().ToListaPaginada(paginacion);
contexto.AgregarHeaderPaginacion(resultado);
return Results.Ok(resultado);
```

Queda prohibido materializar el listado en el Application Service antes de paginar.

### SC-AS-07: Reportes

Un reporte es una proyección de solo lectura que puede agregar datos de varias fuentes y aplicar transformaciones que no corresponden a un caso de uso CRUD. Se diferencia de un listado en que siempre se materializa y puede combinar múltiples consultas.

Reglas de reportes:
- `LogDebug` al inicio con los parámetros de filtrado (fechas, criterios, etc.)
- Siempre materializar con `await` — nunca retornar `IQueryable` en un reporte
- Puede acceder a múltiples repositorios para agregar datos de distintas fuentes


### SC-AS-08: Operaciones en lote (MultiResultHandler)

Cuando un caso de uso requiere ejecutar el mismo evento de dominio múltiples veces sobre una colección de elementos en una sola llamada (ej. importación de clientes, migración masiva, procesamiento de lotes), se usa el patrón `MultiResultHandler` + `Result`.

**Clases involucradas (capa Application):**

- `Result` — representa el resultado de una operación individual: `Key` (identificador del elemento), `IsSuccess`, `Message`, `Action` (Created / Updated / Deleted)
- `MultiResultHandler` — acumula resultados separados en `Success` y `Errors`; expone `Processed` (total) y `Failed` (errores). Métodos: `AddCreateAction`, `AddUpdateAction`, `AddDeleteAction`, `AddError`

**Patrón en el Application Service:**

El Application Service itera la colección, llama al DomainService por cada elemento dentro de un `try/catch`, y registra el resultado en el handler. Retorna el `MultiResultHandler` al endpoint.

```csharp
public async Task<MultiResultHandler> ImportarClientes(
    IEnumerable<ImportarClienteRequest> solicitudes,
    CancellationToken ct = default)
{
    _logger.LogDebug("Iniciando importacion de {Total} clientes", solicitudes.Count());
    var handler = new MultiResultHandler();

    foreach (var solicitud in solicitudes)
    {
        try
        {
            await _servicioDominio.Guardar(solicitud.Nombre, solicitud.Rfc, ct);
            handler.AddCreateAction(solicitud.Rfc, $"Cliente {solicitud.Nombre} creado exitosamente");
        }
        catch (Exception ex)
        {
            handler.AddError(solicitud.Rfc, ex.Message);
        }
    }

    _logger.LogInformation("Importacion completada. Procesados: {Processed}, Fallidos: {Failed}",
        handler.Processed, handler.Failed);
    return handler;
}
```

**En el endpoint:**

El endpoint entrega el `MultiResultHandler` a `CustomResult.PartialContent`, que retorna HTTP 206 con la respuesta formateada según el header `X-Response-Format`:

```csharp
private static async Task<IResult> Handler(
    ClienteService servicio,
    IEnumerable<ImportarClienteRequest> solicitudes,
    HttpContext contexto,
    CancellationToken ct)
{
    var resultado = await servicio.ImportarClientes(solicitudes, ct);
    return CustomResult.PartialContent(resultado, contexto);
}
```

**Dos formatos de respuesta** controlados por el cliente via header `X-Response-Format`:

| Formato | Contenido |
|---|---|
| `simple-multi-result` (default) | `Processed`, `Failed`, `Errors[]` |
| `extended-multi-result` | `Processed`, `Failed`, `Errors[]`, `Success[]` |

**Reglas:**
- El `Key` del `Result` es el identificador de negocio del elemento (RFC, código, email), no el ID de BD
- El `try/catch` va en el Application Service, no en el DomainService — el DomainService lanza excepciones normalmente
- Siempre un `LogDebug` al inicio con el total de elementos a procesar
- Siempre un `LogInformation` al final con `Processed` y `Failed`
- El endpoint usa **siempre** `CustomResult.PartialContent` para este tipo de operacion — nunca `Results.Ok`
