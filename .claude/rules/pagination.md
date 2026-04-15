---
description: Paginacion y ordenamiento en Bisoft Atenea — PaginationRequest, PagedList, X-Pagination
globs: "**/Dtos/Pagination/**,**/Helpers/PaginationHeaderUtil.cs,**/Endpoints/**"
---

## Componentes del sistema de paginación

```
Api/
├── Dtos/Pagination/
│   ├── PaginationRequest.cs   ← parámetros de entrada (query string)
│   └── PagedList.cs           ← resultado paginado (List<T> + metadatos)
├── Helpers/
│   └── PaginationHeaderUtil.cs ← extension de HttpContext para header X-Pagination
└── appsettings.json
    └── Cors.Headers: ["X-Pagination"]   ← exposición CORS obligatoria
```

---

## PaginationRequest

Clase con `BindAsync` para que Minimal API la reciba como `[AsParameters]` desde query string.

```csharp
namespace Company.Product.Module.Api.Dtos.Pagination;

public class PaginationRequest
{
    [FromQuery]
    public int? PageNumber { get; set; } = 0;

    private int _pageSize = 10;
    [FromQuery]
    public int? PageSize
    {
        get => _pageSize;
        set
        {
            if (value != null && value >= ApiConstants.MIN_PAGE_SIZE && value <= ApiConstants.MAX_PAGE_SIZE)
                _pageSize = value.Value;
            else
                _pageSize = ApiConstants.MAX_PAGE_SIZE;
        }
    }

    [FromQuery]
    public string? Sort { get; set; }
    [FromQuery]
    public string? Filter { get; set; }

    public static ValueTask<PaginationRequest?> BindAsync(HttpContext context)
    {
        var query = context.Request.Query;
        if (!int.TryParse(query["PageNumber"], out var pageNumber) || pageNumber < 0)
            pageNumber = 0;
        if (!int.TryParse(query["PageSize"], out var pageSize))
            pageSize = 10;
        return ValueTask.FromResult<PaginationRequest?>(new PaginationRequest
        {
            PageNumber = pageNumber,
            PageSize   = pageSize,
            Sort       = query["Sort"],
            Filter     = query["Filter"]
        });
    }
}
```

Constantes en `ApiConstants`:
- `MAX_PAGE_SIZE = 100` — límite superior; si el cliente envía más, se usa este valor.
- `MIN_PAGE_SIZE = 1` — límite inferior.

**Comportamiento especial de `PageNumber`:**
- `PageNumber = 0` → devuelve **todos los registros** sin paginar (útil para dropdowns o exportaciones).
- `PageNumber >= 1` → paginación normal: `Skip((PageNumber - 1) * PageSize).Take(PageSize)`.

---

## PagedList\<T\>

Hereda de `List<T>`. El `static ToPagedList()` materializa el `IQueryable<T>` ya filtrado y ordenado:

```csharp
namespace Company.Product.Module.Api.Dtos.Pagination;

public class PagedList<T> : List<T>
{
    public int CurrentPage  { get; private set; }
    public int TotalPages   { get; private set; }
    public int PageSize     { get; private set; }
    public int TotalCount   { get; private set; }

    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext     => CurrentPage < TotalPages;

    public PagedList(List<T> items, int count, int pageNumber, int pageSize)
    {
        TotalCount  = count;
        PageSize    = pageSize;
        CurrentPage = pageNumber;
        TotalPages  = (int)Math.Ceiling(count / (double)pageSize);
        AddRange(items);
    }

    public static async Task<PagedList<T>> ToPagedList(IQueryable<T> source, PaginationRequest request)
    {
        var pageNumber = request.PageNumber ?? 0;
        var pageSize   = request.PageSize   ?? ApiConstants.MAX_PAGE_SIZE;
        var count      = await source.CountAsync();
        var items      = pageNumber == 0
            ? await source.ToListAsync()
            : await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedList<T>(items, count, pageNumber, pageSize);
    }
}
```

> `ToPagedList` se llama **después** de aplicar filtros y ordenamiento sobre el `IQueryable`. Nunca antes.

---

## PaginationHeaderUtil

Extension de `HttpContext` que serializa los metadatos de paginación en el header `X-Pagination`. Se llama en el endpoint, antes de `Results.Ok()`:

```csharp
public static class PaginationHeaderUtil
{
    public static HttpContext AddPaginationHeader<T>(
        this HttpContext context, PagedList<T> pagedList) where T : class
    {
        var headers = context.Response.Headers;
        const string headerKey = "X-Pagination";
        if (!headers.ContainsKey(headerKey))
        {
            headers.Append(headerKey, JsonSerializer.Serialize(new
            {
                totalPages   = pagedList.TotalPages,
                currentPage  = pagedList.CurrentPage,
                pageSize     = pagedList.PageSize,
                totalCount   = pagedList.TotalCount,
                hasPrevious  = pagedList.HasPrevious,
                hasNext      = pagedList.HasNext
            }));
        }
        return context;
    }
}
```

El guard `if (!headers.ContainsKey(headerKey))` evita escribir el header dos veces (p. ej. en rutas con cache donde el mismo contexto podría llamarse más de una vez).

---

## Exposición CORS del header X-Pagination

El header `X-Pagination` debe estar declarado en `appsettings.json` dentro de `Cors.Headers`:

```json
{
  "Cors": {
    "Origins": ["http://localhost:4200"],
    "Headers": ["X-Pagination"]
  }
}
```

`CorsConfigurationsReader` lo lee y `ConfigureCors` lo pasa a `.WithExposedHeaders(...)`. Sin esta entrada, el browser bloqueará el acceso al header desde el frontend.

---

## Patrón completo en un endpoint

### 1. Application Service — expone `IQueryable<T>`

El servicio de aplicación devuelve `IQueryable` sin materializar. El filtro se aplica con `Select` y proyeccion Mapster o proyección directa, pero sin `ToList()`:

```csharp
public IQueryable<ObtenerCanalesResponse> ObtenerCanales()
{
    var query = _repositorioCanal.ConsultarCanales();
    return query.Select(c => new ObtenerCanalesResponse(c.Id, c.Nombre, c.Activo));
}
```

### 2. Endpoint — `Operation()` aplica filtro, sort y paginación

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

private static async Task<PagedList<ObtenerCanalesResponse>> Operation(
    CanalService canalService, PaginationRequest request)
{
    var query = canalService.ObtenerCanales();

    if (!string.IsNullOrEmpty(request.Filter))
    {
        var filter = request.Filter.ToLower();
        query = query.Where(c => c.Nombre.ToLower().Contains(filter));
    }

    if (!string.IsNullOrEmpty(request.Sort))
    {
        if (!ObtenerCanalesSorts(query).TryGetValue(request.Sort, out var sorted))
            throw TArgumentException.NotSupported("Parámetro de ordenamiento no soportado");
        query = sorted;
    }

    return await PagedList<ObtenerCanalesResponse>.ToPagedList(query, request);
}
```

### 3. Endpoint handler — header antes de Ok

```csharp
async (HttpContext context, CanalService canalService, [AsParameters] PaginationRequest request) =>
{
    var response = await Operation(canalService, request);
    context.AddPaginationHeader(response);      // ← primero el header
    return Results.Ok(response);
}
```

---

## Convenciones de ordenamiento

- Claves del diccionario en **snake_case** y en **inglés o español consistente** con el proyecto.
- Siempre incluir variante `_desc` para cada campo ordenable.
- El campo `id` (o `id_desc`) debe estar siempre como mínimo.
- Sort inválido → `TArgumentException.NotSupported(...)` → HTTP 400.

```
"id"         → OrderBy(c => c.Id)
"id_desc"    → OrderByDescending(c => c.Id)
"nombre"     → OrderBy(c => c.Nombre)
"nombre_desc"→ OrderByDescending(c => c.Nombre)
```

---

## Convención de filtrado

- Filtro siempre en minúsculas: `request.Filter.ToLower()`.
- Comparación sobre el campo en minúsculas: `.ToLower().Contains(filter)`.
- Solo un campo de filtro libre. Filtros estructurados adicionales van como parámetros separados.
- El filtro se aplica **antes** del sort, y ambos **antes** de `ToPagedList`.

---

## Respuesta X-Pagination (JSON serializado en el header)

```json
{
  "totalPages": 5,
  "currentPage": 2,
  "pageSize": 20,
  "totalCount": 95,
  "hasPrevious": true,
  "hasNext": true
}
```
