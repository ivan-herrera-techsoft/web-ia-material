---
description: Convenciones para Minimal API Endpoints y EndpointGroups
globs: "**/Endpoints/**"
---

## Anatomia de un endpoint

Cada endpoint es una **clase estatica** con tres elementos fijos:

1. `private const string ENDPOINT_NAME` — nombre legible para Swagger.
2. Metodo publico `Map{Operacion}(this RouteGroupBuilder)` — registra la ruta y devuelve `RouteGroupBuilder`.
3. Handler **inline** como lambda dentro de `MapGet/MapPost/MapPut/MapDelete`.

No existe un metodo separado llamado `Handler`. El handler es siempre una lambda.

### Endpoint simple (POST / PUT / DELETE / GET por id)

```csharp
namespace Company.Product.Module.Api.Endpoints.Canales;

public static class CrearCanal
{
    private const string ENDPOINT_NAME = "Crear canal";

    public static RouteGroupBuilder MapCrearCanal(this RouteGroupBuilder endpointGroup)
    {
        endpointGroup.MapPost("",
            async (
                CanalService canalService,
                [FromBody] CrearCanalRequest request,
                CancellationToken ct
            ) =>
            {
                var response = await canalService.Guardar(request, ct);
                return Results.Created("", response);
            }
        )
        .HasApiVersion(ApiConstants.VERSION_1)
        .Produces<CrearCanalResponse>(StatusCodes.Status201Created)
        .WithDescription("Crea un nuevo canal en el sistema.")
        .WithSummary(ENDPOINT_NAME)
        .WithName(ENDPOINT_NAME);
        return endpointGroup;
    }
}
```

### Endpoint con metadatos extraidos (`AddMetadata`)

Cuando el endpoint tiene variantes (por ejemplo, con y sin cache) se usa un metodo privado
`AddMetadata(this RouteHandlerBuilder)` para centralizar los fluent de Swagger y version:

```csharp
namespace Company.Product.Module.Api.Endpoints.Canales;

public static class ObtenerCanales
{
    private const string ENDPOINT_NAME = "Obtener canales";

    public static RouteGroupBuilder MapObtenerCanales(this RouteGroupBuilder endpointGroup,
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
                CanalService canalService,
                [AsParameters] PaginationRequest request
            ) =>
            {
                var response = await Operation(canalService, request);
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
                CanalService canalService,
                [AsParameters] PaginationRequest request,
                IMemoryCache cache,
                IOptions<ApiCacheConfiguration> cacheConfiguration
            ) =>
            {
                var cacheKey = $"ObtenerCanales-{request.PageNumber}-{request.PageSize}-{request.Sort}-{request.Filter}";
                await _lock.WaitAsync();
                if (cache.TryGetValue(cacheKey, out PagedList<ObtenerCanalesResponse>? cachedResponse))
                {
                    _lock.Release();
                    context.AddPaginationHeader(cachedResponse!);
                    return Results.Ok(cachedResponse);
                }
                try
                {
                    var response = await Operation(canalService, request);
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

    private static IReadOnlyDictionary<string, IOrderedQueryable<ObtenerCanalesResponse>>
        ObtenerCanalesSorts(IQueryable<ObtenerCanalesResponse> query) =>
        new Dictionary<string, IOrderedQueryable<ObtenerCanalesResponse>>
        {
            { "id",       query.OrderBy(a => a.Id) },
            { "id_desc",  query.OrderByDescending(a => a.Id) },
            { "nombre",   query.OrderBy(a => a.Nombre) },
            { "nombre_desc", query.OrderByDescending(a => a.Nombre) }
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
                throw TArgumentException.NotSupported("Parametro de ordenamiento no soportado");
            query = sorted;
        }
        return await PagedList<ObtenerCanalesResponse>.ToPagedList(query, request);
    }

    private static RouteHandlerBuilder AddMetadata(this RouteHandlerBuilder endpoint)
    {
        return endpoint
            .HasApiVersion(ApiConstants.VERSION_1)
            .Produces<PagedList<ObtenerCanalesResponse>>(StatusCodes.Status200OK)
            .WithDescription("Consulta los canales registrados en el sistema.")
            .WithSummary(ENDPOINT_NAME)
            .WithName(ENDPOINT_NAME);
    }
}
```

### Endpoint anonimo (`[AllowAnonymous]`)

El atributo `[AllowAnonymous]` se coloca sobre la **lambda**, no sobre el grupo.
Nunca se pone `AllowAnonymous()` fluent en el grupo completo.

```csharp
endpointGroup.MapPost("login", [AllowAnonymous]
    async (
        UsuarioService usuarioService,
        [FromBody] IniciarSesionRequest request,
        CancellationToken ct
    ) =>
    {
        var response = await usuarioService.IniciarSesion(request, ct);
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

## EndpointGroup

El EndpointGroup es el equivalente a un Controller. Es una clase estatica que:

1. Recibe el **grupo raiz** (`RouteGroupBuilder appEndpoints`) — que ya tiene rate limiting, auth y version set aplicados desde `WebApplicationExtensions`.
2. Crea un **sub-grupo** con ruta y tags propios.
3. Delega el registro de endpoints a un metodo privado `MapEndpoints`.
4. **Devuelve `appEndpoints`** (el grupo raiz), no el sub-grupo.

```csharp
namespace Company.Product.Module.Api.Endpoints.Canales;

public static class CanalesEndpointGroup
{
    public static RouteGroupBuilder MapCanalesEndpoints(
        this RouteGroupBuilder appEndpoints,
        ApiCacheConfiguration cacheConfiguration)
    {
        var canalesGroup = appEndpoints.MapGroup("canales").WithTags("Canales");
        canalesGroup.MapEndpoints(cacheConfiguration);
        return appEndpoints;                        // siempre el grupo raiz
    }

    private static RouteGroupBuilder MapEndpoints(
        this RouteGroupBuilder endpointGroup,
        ApiCacheConfiguration cacheConfiguration)
    {
        endpointGroup.MapObtenerCanales(cacheConfiguration)
                     .MapCrearCanal()
                     .MapActualizarCanal()
                     .MapEliminarCanal();
        return endpointGroup;
    }
}
```

> El sub-grupo NO hereda `RequireAuthorization` ni `RequireRateLimiting` — esos ya vienen del
> grupo raiz configurado en `WebApplicationExtensions`. No se deben repetir aqui.

## Registro raiz en WebApplicationExtensions

El metodo `MapEndpoints` en `WebApplicationExtensions` crea el grupo raiz con todas las politicas
transversales y llama a cada EndpointGroup:

```csharp
public static WebApplication MapEndpoints(
    this WebApplication app,
    string rateLimitingPolicy,
    ApiCacheConfiguration cacheConfiguration)
{
    var versionSet = app.GetVersionSet();
    var apiEndpoints = app.MapGroup("api")
                          .WithApiVersionSet(versionSet)
                          .RequireRateLimiting(rateLimitingPolicy)
                          .AddOpenApi()
                          .RequireAuthorization();

    apiEndpoints.MapSecurityEndpoints()
                .MapCanalesEndpoints(cacheConfiguration);

    return app;
}
```

`AddOpenApi()` es un metodo privado de extension en el mismo archivo que agrega los 6 responses
estandar (400, 401, 403, 404, 429, 500) mediante `AddOpenApiOperationTransformer`.

## Estructura de carpetas

```
Endpoints/
├── Security/
│   ├── SecurityEndpointGroup.cs
│   ├── IniciarSesion.cs
│   └── RefrescarToken.cs
└── Canales/
    ├── CanalesEndpointGroup.cs
    ├── ObtenerCanales.cs
    ├── CrearCanal.cs
    ├── ActualizarCanal.cs
    └── EliminarCanal.cs
```

## Parametros del handler

| Tipo de parametro | Convencion                          | Ejemplo                              |
|-------------------|-------------------------------------|--------------------------------------|
| Body              | `[FromBody] CrearCanalRequest`      | POST / PUT                           |
| Query paginado    | `[AsParameters] PaginationRequest`  | GET listado                          |
| Ruta              | sin atributo, mismo nombre          | `Guid id` en ruta `"{id}"`           |
| HttpContext       | `HttpContext context`               | pagination header, leer cookies      |
| CancellationToken | `CancellationToken ct`              | ultimo parametro en async            |
| Services          | sin atributo (DI automatico)        | `CanalService canalService`          |

## Fluent de metadata obligatorios

Siempre, ya sea en cadena inline o en `AddMetadata()`:

```csharp
.HasApiVersion(ApiConstants.VERSION_1)
.Produces<TipoRespuesta>(StatusCodes.Status200OK)   // o 201 Created
.WithDescription("Descripcion completa.")
.WithSummary(ENDPOINT_NAME)
.WithName(ENDPOINT_NAME)
```

`RequireAuthorization` y `RequireRateLimiting` **no** se ponen en endpoints individuales ni en
EndpointGroups; ya vienen del grupo raiz.

## Verbo HTTP y respuesta exitosa

| Operacion    | Verbo    | Respuesta                             |
|--------------|----------|---------------------------------------|
| Listado      | GET      | `Results.Ok(pageList)`               |
| Detalle      | GET      | `Results.Ok(response)`               |
| Crear        | POST     | `Results.Created("", response)`      |
| Actualizar   | PUT      | `Results.Ok(response)`               |
| Eliminar     | DELETE   | `Results.NoContent()`                |
| Toggle       | PATCH    | `Results.Ok(response)`               |
