# Spec: Paginación y Ordenamiento

## Propósito

Define los contratos para implementar paginación y ordenamiento en endpoints GET de listado en Bisoft Atenea. La paginación se basa en `PaginationRequest` (entrada) y `PagedList<T>` (salida), con metadatos expuestos en el header `X-Pagination`.

> **Spec relacionado:** El endpoint completo sigue el patrón de → [Spec: Endpoints](crear-endpoint.md). La exposición del header requiere configuración CORS → [Rules: cors.md](../rules/cors.md).

---

## Contratos

### SC-PAG-01: Application Service devuelve IQueryable, no colección materializada

El método de listado en el Application Service debe devolver `IQueryable<T>`, nunca `List<T>`, `IEnumerable<T>` ni `ICollection<T>`. La materialización ocurre una sola vez en `PagedList.ToPagedList()`, dentro del endpoint.

**Justificación:** `ToPagedList` ejecuta dos queries sobre el `IQueryable` — `CountAsync()` para el total y `ToListAsync()` para la página. Si el servicio materializa antes, el count es sobre la colección ya cargada en memoria (correcto pero ineficiente); peor aún, el `Skip/Take` no se traduce a SQL sino que se aplica en memoria sobre todos los registros.

✅ Correcto:
```csharp
public IQueryable<ObtenerCanalesResponse> ObtenerCanales()
{
    var query = _repositorioCanal.ConsultarCanales();
    return query.Select(c => new ObtenerCanalesResponse(c.Id, c.Nombre, c.Activo));
}
```

❌ Incorrecto:
```csharp
public async Task<IEnumerable<ObtenerCanalesResponse>> ObtenerCanales(CancellationToken ct)
{
    var canales = await _repositorioCanal.ConsultarCanales().ToListAsync(ct);
    return canales.Adapt<IEnumerable<ObtenerCanalesResponse>>();
    // Skip/Take posterior en el endpoint opera sobre memoria, no sobre BD
}
```

---

### SC-PAG-02: Orden de operaciones — filtro → sort → ToPagedList

Dentro del método `Operation()` del endpoint, el orden debe ser siempre: aplicar filtro primero, luego sort, y finalmente llamar `ToPagedList`. Nunca invertir este orden ni mezclar la materialización con el filtrado.

**Justificación:** el filtro reduce el conjunto de registros antes del sort, lo que es más eficiente. `ToPagedList` ejecuta el `COUNT` y el `SELECT` sobre el `IQueryable` final; si se llama antes del filtro, el total paginado será incorrecto.

✅ Correcto:
```csharp
private static async Task<PagedList<ObtenerCanalesResponse>> Operation(
    CanalService canalService, PaginationRequest request)
{
    var query = canalService.ObtenerCanales();          // 1. obtener IQueryable

    if (!string.IsNullOrEmpty(request.Filter))          // 2. filtrar
    {
        var filter = request.Filter.ToLower();
        query = query.Where(c => c.Nombre.ToLower().Contains(filter));
    }

    if (!string.IsNullOrEmpty(request.Sort))            // 3. ordenar
    {
        if (!ObtenerCanalesSorts(query).TryGetValue(request.Sort, out var sorted))
            throw TArgumentException.NotSupported("Parámetro de ordenamiento no soportado");
        query = sorted;
    }

    return await PagedList<ObtenerCanalesResponse>.ToPagedList(query, request); // 4. paginar
}
```

❌ Incorrecto:
```csharp
var pagedList = await PagedList<ObtenerCanalesResponse>.ToPagedList(query, request); // pagina antes de filtrar
if (!string.IsNullOrEmpty(request.Filter))
    pagedList = pagedList.Where(c => c.Nombre.Contains(filter)).ToList(); // filtra en memoria
```

---

### SC-PAG-03: Diccionario de sorts con variante _desc por campo

El diccionario de sorts debe incluir siempre tanto la variante ascendente como la descendente para cada campo ordenable. La clave `"id"` / `"id_desc"` es obligatoria como mínimo. Las claves van en `snake_case`.

**Justificación:** el cliente necesita poder invertir el orden sin recibir un 400. Omitir `_desc` obliga al cliente a implementar lógica de inversión en el frontend o a no poder ordenar de forma descendente.

✅ Correcto:
```csharp
private static IReadOnlyDictionary<string, IOrderedQueryable<ObtenerCanalesResponse>>
    ObtenerCanalesSorts(IQueryable<ObtenerCanalesResponse> query) =>
    new Dictionary<string, IOrderedQueryable<ObtenerCanalesResponse>>
    {
        { "id",          query.OrderBy(c => c.Id) },
        { "id_desc",     query.OrderByDescending(c => c.Id) },
        { "nombre",      query.OrderBy(c => c.Nombre) },
        { "nombre_desc", query.OrderByDescending(c => c.Nombre) }
    };
```

❌ Incorrecto:
```csharp
// Solo ascendente — el cliente no puede ordenar de forma descendente
new Dictionary<string, IOrderedQueryable<ObtenerCanalesResponse>>
{
    { "id",     query.OrderBy(c => c.Id) },
    { "nombre", query.OrderBy(c => c.Nombre) }
};
```

---

### SC-PAG-04: Sort inválido lanza TArgumentException.NotSupported

Cuando `request.Sort` tiene un valor que no existe como clave en el diccionario, debe lanzarse `TArgumentException.NotSupported(...)`. No retornar una lista sin ordenar ni ignorar silenciosamente el parámetro.

**Justificación:** silenciar un sort inválido confunde al cliente que espera ver los datos ordenados por un campo específico. Un HTTP 400 explícito señala que el valor enviado no está en el contrato de la API.

✅ Correcto:
```csharp
if (!ObtenerCanalesSorts(query).TryGetValue(request.Sort, out var sorted))
    throw TArgumentException.NotSupported("Parámetro de ordenamiento no soportado");
query = sorted;
```

❌ Incorrecto:
```csharp
if (ObtenerCanalesSorts(query).TryGetValue(request.Sort, out var sorted))
    query = sorted;
// Si no existe la clave, se ignora — el cliente no sabe que el sort fue ignorado
```

---

### SC-PAG-05: AddPaginationHeader se llama antes de Results.Ok

`context.AddPaginationHeader(response)` debe invocarse inmediatamente antes de `return Results.Ok(response)`. No se puede agregar el header después de que la respuesta se ha comenzado a escribir.

**Justificación:** en HTTP, los headers deben enviarse antes del body. `Results.Ok()` inicia la escritura del body. Si se intenta agregar el header después, ASP.NET Core lo ignora silenciosamente o lanza una excepción.

✅ Correcto:
```csharp
var response = await Operation(canalService, request);
context.AddPaginationHeader(response);      // header antes del body
return Results.Ok(response);
```

❌ Incorrecto:
```csharp
var response = await Operation(canalService, request);
var result = Results.Ok(response);          // inicia escritura
context.AddPaginationHeader(response);      // demasiado tarde — header ignorado
return result;
```

---

### SC-PAG-06: X-Pagination debe estar en Cors.Headers de appsettings

El header `X-Pagination` debe estar declarado en el array `Cors.Headers` de `appsettings.json`. `CorsConfigurationsReader` lo lee y `ConfigureCors` lo expone con `.WithExposedHeaders()`. Sin esta entrada, el navegador bloqueará el acceso al header desde JavaScript aunque el servidor lo envíe.

**Justificación:** CORS restringe qué headers de respuesta son accesibles al código JavaScript del navegador. Solo los headers en la lista `expose-headers` son visibles desde `response.headers.get('X-Pagination')` en el frontend.

✅ Correcto (`appsettings.json`):
```json
{
  "Cors": {
    "Origins": ["http://localhost:4200"],
    "Headers": ["X-Pagination"]
  }
}
```

❌ Incorrecto:
```json
{
  "Cors": {
    "Origins": ["http://localhost:4200"],
    "Headers": []
  }
}
// X-Pagination llega al navegador pero JavaScript no puede leerlo
```

---

### SC-PAG-07: PageNumber = 0 devuelve todos los registros

Cuando el cliente envía `PageNumber=0` (o no envía el parámetro, ya que el default es 0), el endpoint debe devolver todos los registros sin paginar. No debe interpretarse como "página inválida" ni retornar un error.

**Justificación:** este comportamiento está implementado en `PagedList.ToPagedList`: `if (pageNumber == 0) await source.ToListAsync()`. Es el mecanismo para exportaciones, dropdowns o cualquier caso donde se necesiten todos los datos.

✅ Correcto — comportamiento en `ToPagedList`:
```csharp
var items = pageNumber == 0
    ? await source.ToListAsync()                                              // todos
    : await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();  // página
```

❌ Incorrecto:
```csharp
// Tratar PageNumber=0 como error
if (request.PageNumber == 0)
    throw TArgumentException.OutOfRange("PageNumber debe ser mayor a 0");
```

---

### SC-PAG-08: Filtro se aplica en minúsculas sobre el campo

El filtro libre se aplica convirtiendo tanto el valor del request como el campo de la entidad a minúsculas antes del `.Contains()`. Siempre sobre el `IQueryable`, nunca sobre una colección materializada.

**Justificación:** la comparación case-insensitive debe traducirse a SQL (`LOWER()` o `ILIKE` según el proveedor). Hacer el `.ToLower()` en ambos lados garantiza la portabilidad entre SQL Server, PostgreSQL y SQLite.

✅ Correcto:
```csharp
if (!string.IsNullOrEmpty(request.Filter))
{
    var filter = request.Filter.ToLower();
    query = query.Where(c => c.Nombre.ToLower().Contains(filter));
}
```

❌ Incorrecto:
```csharp
// Comparación case-sensitive — no encuentra "Canal" si el cliente escribe "canal"
query = query.Where(c => c.Nombre.Contains(request.Filter));

// O materializado antes — carga todos los registros en memoria
var todos = await query.ToListAsync();
var filtrados = todos.Where(c => c.Nombre.ToLower().Contains(request.Filter!.ToLower()));
```

---

### SC-PAG-09: Produces declara PagedList<T>, no el tipo base

El fluent `.Produces<T>()` en la metadata del endpoint debe usar `PagedList<Obtener{Entidad}sResponse>` como tipo, no `IEnumerable<T>`, `List<T>` ni el DTO directamente.

**Justificación:** Swagger genera el schema de respuesta a partir del tipo declarado en `Produces`. Usar `PagedList<T>` expone los campos de paginación (`currentPage`, `totalPages`, etc.) en la documentación OpenAPI, lo que ayuda al consumidor a entender el contrato completo de la respuesta.

✅ Correcto:
```csharp
.Produces<PagedList<ObtenerCanalesResponse>>(StatusCodes.Status200OK)
```

❌ Incorrecto:
```csharp
.Produces<IEnumerable<ObtenerCanalesResponse>>(StatusCodes.Status200OK)
// Swagger no documenta los metadatos de paginación
```
