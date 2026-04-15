---
name: crear-endpoint
description: Crea un nuevo Minimal API endpoint (y si aplica, su EndpointGroup) siguiendo la estructura estandar de Bisoft Atenea
argument-hint: [Operacion] [Entidad] ej. "CrearCanal", "ObtenerReporteVenta"
---

Crear el endpoint Minimal API para la operacion **$ARGUMENTS** siguiendo la estructura del proyecto.

## Paso 1 — Identificar el EndpointGroup

Buscar en `Api/Endpoints/` si ya existe una carpeta y un EndpointGroup para la entidad.

- **Si existe**: ir al Paso 2 y agregar el endpoint al grupo existente.
- **Si no existe**: crear primero el EndpointGroup (Paso 5) y luego el endpoint (Paso 2).

---

## Paso 2 — Crear el archivo del endpoint

Crear `Api/Endpoints/{Entidad}/{Operacion}{Entidad}.cs`.

El handler va **siempre inline** como lambda dentro de `MapGet/MapPost/MapPut/MapDelete`.
No existe un metodo separado llamado `Handler`.

### Endpoint simple (POST / PUT / DELETE / GET por id)

```csharp
namespace Company.Product.Module.Api.Endpoints.{Entidad}s;

public static class {Operacion}{Entidad}
{
    private const string ENDPOINT_NAME = "{Nombre legible en español}";

    public static RouteGroupBuilder Map{Operacion}{Entidad}(this RouteGroupBuilder endpointGroup)
    {
        endpointGroup.Map{HttpVerb}("{ruta}",
            async (
                {Entidad}Service {entidad}Service,
                [FromBody] {Operacion}{Entidad}Request request,   // solo POST/PUT
                CancellationToken ct
            ) =>
            {
                var response = await {entidad}Service.{MetodoEnEspanol}(request, ct);
                return Results.{Resultado}("", response);
            }
        )
        .HasApiVersion(ApiConstants.VERSION_1)
        .Produces<{Operacion}{Entidad}Response>(StatusCodes.Status{Codigo}OK)
        .WithDescription("{Descripcion completa en español.}")
        .WithSummary(ENDPOINT_NAME)
        .WithName(ENDPOINT_NAME);
        return endpointGroup;
    }
}
```

### Endpoint GET listado (con paginacion y opcionalmente cache)

Cuando el listado puede tener cache, se usan tres metodos privados:
`Map()` (sin cache), `MapCache()` (con cache) y `AddMetadata()` (fluent compartido).
El metodo publico `Map{Operacion}` brancha segun `cacheConfiguration.CacheEnabled`.

```csharp
namespace Company.Product.Module.Api.Endpoints.{Entidad}s;

public static class Obtener{Entidad}s
{
    private const string ENDPOINT_NAME = "Obtener {entidades}";

    public static RouteGroupBuilder MapObtener{Entidad}s(
        this RouteGroupBuilder endpointGroup,
        ApiCacheConfiguration cacheConfiguration)
    {
        if (cacheConfiguration.CacheEnabled)
            endpointGroup.MapCache().AddMetadata();
        else
            endpointGroup.Map().AddMetadata();

        return endpointGroup;
    }

    private static RouteHandlerBuilder Map(this RouteGroupBuilder endpointGroup)
    {
        return endpointGroup.MapGet("",
            async (
                HttpContext context,
                {Entidad}Service {entidad}Service,
                [AsParameters] PaginationRequest request
            ) =>
            {
                var response = await Operation({entidad}Service, request);
                context.AddPaginationHeader(response);
                return Results.Ok(response);
            }
        );
    }

    private static readonly SemaphoreSlim _lock = new(1, 1);

    private static RouteHandlerBuilder MapCache(this RouteGroupBuilder endpointGroup)
    {
        return endpointGroup.MapGet("",
            async (
                HttpContext context,
                {Entidad}Service {entidad}Service,
                [AsParameters] PaginationRequest request,
                IMemoryCache cache,
                IOptions<ApiCacheConfiguration> cacheConfiguration
            ) =>
            {
                var cacheKey = $"Obtener{Entidad}s-{request.PageNumber}-{request.PageSize}-{request.Sort}-{request.Filter}";
                await _lock.WaitAsync();
                if (cache.TryGetValue(cacheKey, out PagedList<Obtener{Entidad}sResponse>? cached))
                {
                    _lock.Release();
                    context.AddPaginationHeader(cached!);
                    return Results.Ok(cached);
                }
                try
                {
                    var response = await Operation({entidad}Service, request);
                    cache.Set(cacheKey, response,
                        new MemoryCacheEntryOptions
                        {
                            SlidingExpiration = cacheConfiguration.Value.CacheSlidingDuration
                        });
                    context.AddPaginationHeader(response);
                    return Results.Ok(response);
                }
                finally
                {
                    _lock.Release();
                }
            }
        );
    }

    private static IReadOnlyDictionary<string, IOrderedQueryable<Obtener{Entidad}sResponse>>
        Obtener{Entidad}sSorts(IQueryable<Obtener{Entidad}sResponse> query) =>
        new Dictionary<string, IOrderedQueryable<Obtener{Entidad}sResponse>>
        {
            { "id",       query.OrderBy(a => a.Id) },
            { "id_desc",  query.OrderByDescending(a => a.Id) },
            { "nombre",   query.OrderBy(a => a.Nombre) },
            { "nombre_desc", query.OrderByDescending(a => a.Nombre) }
        };

    private static async Task<PagedList<Obtener{Entidad}sResponse>> Operation(
        {Entidad}Service {entidad}Service, PaginationRequest request)
    {
        var query = {entidad}Service.Obtener{Entidad}s();

        if (!string.IsNullOrEmpty(request.Filter))
        {
            var filter = request.Filter.ToLower();
            query = query.Where(c => c.Nombre.ToLower().Contains(filter));
        }
        if (!string.IsNullOrEmpty(request.Sort))
        {
            if (!Obtener{Entidad}sSorts(query).TryGetValue(request.Sort, out var sorted))
                throw TArgumentException.NotSupported("Parametro de ordenamiento no soportado");
            query = sorted;
        }
        return await PagedList<Obtener{Entidad}sResponse>.ToPagedList(query, request);
    }

    private static RouteHandlerBuilder AddMetadata(this RouteHandlerBuilder endpoint)
    {
        return endpoint
            .HasApiVersion(ApiConstants.VERSION_1)
            .Produces<PagedList<Obtener{Entidad}sResponse>>(StatusCodes.Status200OK)
            .WithDescription("Consulta los {entidades} registrados en el sistema.")
            .WithSummary(ENDPOINT_NAME)
            .WithName(ENDPOINT_NAME);
    }
}
```

### Endpoint anonimo

El atributo `[AllowAnonymous]` va sobre la lambda, no en el grupo:

```csharp
endpointGroup.MapPost("login", [AllowAnonymous]
    async (
        {Entidad}Service {entidad}Service,
        [FromBody] IniciarSesionRequest request,
        CancellationToken ct
    ) =>
    {
        var response = await {entidad}Service.IniciarSesion(request, ct);
        return Results.Ok(response);
    }
)
.HasApiVersion(ApiConstants.VERSION_1)
.Produces<IniciarSesionResponse>(StatusCodes.Status200OK)
.WithDescription("Autentica al usuario y devuelve tokens de acceso.")
.WithSummary(ENDPOINT_NAME)
.WithName(ENDPOINT_NAME);
return endpointGroup;
```

---

## Paso 3 — Verbo HTTP y respuesta segun operacion

| Operacion      | Verbo    | Resultado exitoso                     | StatusCode    |
|----------------|----------|---------------------------------------|---------------|
| Listado        | GET      | `Results.Ok(pageList)`               | 200 OK        |
| Detalle por id | GET      | `Results.Ok(response)`               | 200 OK        |
| Crear          | POST     | `Results.Created("", response)`      | 201 Created   |
| Actualizar     | PUT      | `Results.Ok(response)`               | 200 OK        |
| Eliminar       | DELETE   | `Results.NoContent()`                | 204 No Content|
| Toggle         | PATCH    | `Results.Ok(response)`               | 200 OK        |

---

## Paso 4 — Agregar el endpoint al EndpointGroup existente

En `{Entidad}sEndpointGroup.cs`, en el metodo privado `MapEndpoints`, encadenar la llamada:

```csharp
private static RouteGroupBuilder MapEndpoints(
    this RouteGroupBuilder endpointGroup,
    ApiCacheConfiguration cacheConfiguration)
{
    endpointGroup.MapObtener{Entidad}s(cacheConfiguration)
                 .MapCrear{Entidad}()       // <-- agregar aqui
                 .MapActualizar{Entidad}()
                 .MapEliminar{Entidad}();
    return endpointGroup;
}
```

Si el nuevo endpoint no recibe `ApiCacheConfiguration`, el metodo publico puede omitir ese parametro.

---

## Paso 5 — Crear el EndpointGroup (solo si no existe)

Crear `Api/Endpoints/{Entidad}s/{Entidad}sEndpointGroup.cs`:

```csharp
namespace Company.Product.Module.Api.Endpoints.{Entidad}s;

public static class {Entidad}sEndpointGroup
{
    public static RouteGroupBuilder Map{Entidad}sEndpoints(
        this RouteGroupBuilder appEndpoints,
        ApiCacheConfiguration cacheConfiguration)
    {
        var group = appEndpoints.MapGroup("{entidades}").WithTags("{Entidades}");
        group.MapEndpoints(cacheConfiguration);
        return appEndpoints;           // devolver siempre el grupo raiz
    }

    private static RouteGroupBuilder MapEndpoints(
        this RouteGroupBuilder endpointGroup,
        ApiCacheConfiguration cacheConfiguration)
    {
        endpointGroup.MapObtener{Entidad}s(cacheConfiguration)
                     .MapCrear{Entidad}();
        return endpointGroup;
    }
}
```

Luego en `Extensions/WebApplicationExtensions.cs`, encadenar la llamada en `MapEndpoints`:

```csharp
apiEndpoints.MapSecurityEndpoints()
            .MapCanalesEndpoints(cacheConfiguration)
            .Map{Entidad}sEndpoints(cacheConfiguration);   // <-- agregar aqui
```

---

## Paso 6 — Checklist final

- [ ] Un archivo por operacion (`CrearCanal.cs`, `ObtenerCanales.cs`, etc.)
- [ ] `private const string ENDPOINT_NAME` definido
- [ ] Handler inline como lambda (sin metodo `Handler` separado)
- [ ] Fluent completo: `.HasApiVersion()`, `.Produces<T>()`, `.WithDescription()`, `.WithSummary()`, `.WithName()`
- [ ] `RequireAuthorization` y `RequireRateLimiting` NO estan en el endpoint ni en el EndpointGroup
- [ ] Si es listado paginado: `context.AddPaginationHeader(response)` antes de `Results.Ok`
- [ ] Si es anonimo: `[AllowAnonymous]` sobre la lambda, no en el grupo
- [ ] Si tiene cache: `SemaphoreSlim _lock` como campo estatico, variantes `Map()` / `MapCache()`
- [ ] El EndpointGroup devuelve `appEndpoints` (grupo raiz), no el sub-grupo
- [ ] El nuevo endpoint aparece en `MapEndpoints` del EndpointGroup
- [ ] Si es EndpointGroup nuevo: llamada agregada en `WebApplicationExtensions.MapEndpoints`
