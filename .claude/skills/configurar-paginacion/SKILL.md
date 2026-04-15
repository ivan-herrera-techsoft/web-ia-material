---
name: configurar-paginacion
description: Agrega paginacion y ordenamiento a un endpoint GET de listado en Bisoft Atenea
argument-hint: [Entidad] ej. "Canal", "TipoEvento"
---

Agregar paginación y ordenamiento al endpoint GET de listado para **$ARGUMENTS**.

Los archivos base `PaginationRequest.cs`, `PagedList.cs` y `PaginationHeaderUtil.cs` ya existen en el proyecto. Este skill implementa la paginación en la capa de Application y en el endpoint.

---

## Paso 1 — Verificar los archivos base

Confirmar que existen:

- `Api/Dtos/Pagination/PaginationRequest.cs`
- `Api/Dtos/Pagination/PagedList.cs`
- `Api/Helpers/PaginationHeaderUtil.cs`

Si no existen, crearlos con el contenido de [Rules: pagination.md](../../rules/pagination.md).

---

## Paso 2 — Verificar X-Pagination en CORS

En `appsettings.json`, el array `Cors.Headers` debe incluir `"X-Pagination"`:

```json
{
  "Cors": {
    "Origins": ["http://localhost:4200"],
    "Headers": ["X-Pagination"]
  }
}
```

Sin esta entrada, el navegador bloqueará el acceso al header desde el frontend aunque el servidor lo envíe.

---

## Paso 3 — Application Service expone IQueryable

El método de listado en el Application Service debe devolver `IQueryable<{Entidad}Response>`, **no** una colección materializada. La proyección a DTO se hace con `Select` o `ProjectToType`, pero sin llamar `ToList()`:

```csharp
// En {Entidad}Service.cs
public IQueryable<Obtener{Entidad}sResponse> Obtener{Entidad}s()
{
    var query = _repositorio{Entidad}.Consultar{Entidad}s();
    return query.Select(e => new Obtener{Entidad}sResponse(e.Id, e.Nombre /*, ... */));
}
```

> Si el repositorio ya expone `IQueryable` (patrón de datos frecuentemente modificados), no materializar en ningún punto intermedio hasta `ToPagedList`.

---

## Paso 4 — Definir el diccionario de sorts en el endpoint

En el archivo del endpoint (`Obtener{Entidad}s.cs`), agregar un método privado estático que construye el diccionario de ordenamiento a partir del `IQueryable` recibido:

```csharp
private static IReadOnlyDictionary<string, IOrderedQueryable<Obtener{Entidad}sResponse>>
    Obtener{Entidad}sSorts(IQueryable<Obtener{Entidad}sResponse> query) =>
    new Dictionary<string, IOrderedQueryable<Obtener{Entidad}sResponse>>
    {
        { "id",          query.OrderBy(e => e.Id) },
        { "id_desc",     query.OrderByDescending(e => e.Id) },
        { "nombre",      query.OrderBy(e => e.Nombre) },
        { "nombre_desc", query.OrderByDescending(e => e.Nombre) }
        // Agregar un par asc/desc por cada campo ordenable
    };
```

Convenciones:
- Claves en `snake_case`.
- Siempre incluir variante `_desc` para cada campo.
- `"id"` / `"id_desc"` son obligatorios como mínimo.

---

## Paso 5 — Implementar Operation()

Agregar el método privado estático `Operation` que aplica filtro, ordenamiento y devuelve el `PagedList`:

```csharp
private static async Task<PagedList<Obtener{Entidad}sResponse>> Operation(
    {Entidad}Service {entidad}Service, PaginationRequest request)
{
    var query = {entidad}Service.Obtener{Entidad}s();

    // Filtro libre (opcional, ajustar campo según entidad)
    if (!string.IsNullOrEmpty(request.Filter))
    {
        var filter = request.Filter.ToLower();
        query = query.Where(e => e.Nombre.ToLower().Contains(filter));
    }

    // Ordenamiento
    if (!string.IsNullOrEmpty(request.Sort))
    {
        if (!Obtener{Entidad}sSorts(query).TryGetValue(request.Sort, out var sorted))
            throw TArgumentException.NotSupported("Parámetro de ordenamiento no soportado");
        query = sorted;
    }

    return await PagedList<Obtener{Entidad}sResponse>.ToPagedList(query, request);
}
```

El orden es siempre: **filtro → sort → `ToPagedList`**. Nunca invertir.

---

## Paso 6 — Handler del endpoint con AddPaginationHeader

En el handler inline del endpoint, llamar `AddPaginationHeader` **antes** de `Results.Ok`:

```csharp
async (
    HttpContext context,
    {Entidad}Service {entidad}Service,
    [AsParameters] PaginationRequest request
) =>
{
    var response = await Operation({entidad}Service, request);
    context.AddPaginationHeader(response);     // ← header antes de Ok
    return Results.Ok(response);
}
```

El parámetro `[AsParameters] PaginationRequest` permite que Minimal API mapee los cuatro query params (`PageNumber`, `PageSize`, `Sort`, `Filter`) directamente al objeto usando `BindAsync`.

---

## Paso 7 — Metadata del endpoint

El `Produces` debe declarar `PagedList<Obtener{Entidad}sResponse>`, no el tipo base:

```csharp
private static RouteHandlerBuilder AddMetadata(this RouteHandlerBuilder endpoint)
{
    return endpoint
        .HasApiVersion(ApiConstants.VERSION_1)
        .Produces<PagedList<Obtener{Entidad}sResponse>>(StatusCodes.Status200OK)
        .WithDescription("Consulta los {entidades} registrados en el sistema.")
        .WithSummary(ENDPOINT_NAME)
        .WithName(ENDPOINT_NAME);
}
```

---

## Checklist

- [ ] `X-Pagination` declarado en `Cors.Headers` en `appsettings.json`
- [ ] Application Service devuelve `IQueryable<T>` (sin `ToList()`)
- [ ] Diccionario de sorts con al menos `id` / `id_desc` y variante asc/desc por campo
- [ ] Filtro aplicado con `.ToLower().Contains(filter.ToLower())` sobre el `IQueryable`
- [ ] Sort inválido lanza `TArgumentException.NotSupported`
- [ ] Orden de operaciones: filtro → sort → `ToPagedList`
- [ ] `context.AddPaginationHeader(response)` antes de `Results.Ok(response)`
- [ ] `[AsParameters] PaginationRequest request` en el lambda del handler
- [ ] `Produces<PagedList<T>>` en los metadatos del endpoint
