# Spec: Endpoints

## Propósito

Define los contratos que debe cumplir cada endpoint Minimal API y cada EndpointGroup en la arquitectura Bisoft Atenea. Los endpoints son el punto de entrada HTTP al sistema: reciben la solicitud, invocan el Application Service correspondiente y devuelven la respuesta. No contienen lógica de negocio.

> **Spec relacionado:** La lógica de negocio vive en el Application Service → [Spec: Application Service](crear-application-service.md). Los fluent de versionado requieren `ApiConstants.VERSION_1` → [Rules: api-versioning.md](../rules/api-versioning.md).

---

## Contratos del Endpoint

### SC-EP-01: Constante `ENDPOINT_NAME`

Todo endpoint debe declarar `private const string ENDPOINT_NAME` con el nombre legible en español. Esta constante se usa en `.WithSummary()` y `.WithName()`. Nunca se pasan literales de cadena directos a esos fluent.

**Justificación:** centraliza el nombre en un solo lugar; si Swagger o los logs deben mostrarlo, siempre es consistente.

✅ Correcto:
```csharp
public static class CrearCanal
{
    private const string ENDPOINT_NAME = "Crear canal";

    public static RouteGroupBuilder MapCrearCanal(this RouteGroupBuilder endpointGroup)
    {
        endpointGroup.MapPost("", async (...) => { ... })
            .WithSummary(ENDPOINT_NAME)
            .WithName(ENDPOINT_NAME);
        return endpointGroup;
    }
}
```

❌ Incorrecto:
```csharp
endpointGroup.MapPost("", async (...) => { ... })
    .WithSummary("Crear canal")        // literal repetido
    .WithName("Crear canal");          // si cambia el nombre, hay que buscarlo en dos lugares
```

---

### SC-EP-02: Handler siempre inline — nunca método separado

El handler es una lambda inline dentro de `MapGet/MapPost/MapPut/MapDelete`. No existe un método privado estático llamado `Handler` o similar.

**Justificación:** la lambda inline es la convención de Minimal API; un método `Handler` separado rompe la legibilidad del fluent chain y aleja la lógica de su ruta.

✅ Correcto:
```csharp
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
.WithSummary(ENDPOINT_NAME)
.WithName(ENDPOINT_NAME);
```

❌ Incorrecto:
```csharp
endpointGroup.MapPost("", Handler)          // Handler no existe como concepto aquí
    .HasApiVersion(ApiConstants.VERSION_1);

private static async Task<IResult> Handler(
    CanalService canalService, CancellationToken ct) { ... }
```

---

### SC-EP-03: Fluent de metadata completo en cada endpoint

Todo endpoint debe encadenar los cinco fluent de metadata: `.HasApiVersion()`, `.Produces<T>()`, `.WithDescription()`, `.WithSummary()` y `.WithName()`. Si hay variantes (cache / sin cache), los fluent se extraen a un método privado `AddMetadata(this RouteHandlerBuilder)`.

**Justificación:** Swagger no muestra correctamente el endpoint sin `WithSummary` y `WithName`; `HasApiVersion` es obligatorio para el versionado por header.

✅ Correcto — variante con `AddMetadata`:
```csharp
private static RouteHandlerBuilder AddMetadata(this RouteHandlerBuilder endpoint)
{
    return endpoint
        .HasApiVersion(ApiConstants.VERSION_1)
        .Produces<PagedList<ObtenerCanalesResponse>>(StatusCodes.Status200OK)
        .WithDescription("Consulta los canales registrados en el sistema.")
        .WithSummary(ENDPOINT_NAME)
        .WithName(ENDPOINT_NAME);
}
```

❌ Incorrecto:
```csharp
endpointGroup.MapGet("", async (...) => { ... })
    .HasApiVersion(ApiConstants.VERSION_1)
    .Produces<ObtenerCanalesResponse>(200);
// Faltan WithDescription, WithSummary, WithName
```

---

### SC-EP-04: `Operation()` para lógica extraíble de listados

Cuando un endpoint de listado tiene variantes (con cache y sin cache), la lógica de filtrado, ordenamiento y paginación se extrae a un método privado estático `Operation()`. Ambas variantes (`Map()` y `MapCache()`) llaman a `Operation()`.

**Justificación:** evita duplicar el mismo bloque de filtros/sorts en dos lambdas distintas; si cambia la lógica, se modifica en un solo lugar.

✅ Correcto:
```csharp
private static async Task<PagedList<ObtenerCanalesResponse>> Operation(
    CanalService canalService, PaginationRequest request)
{
    var query = canalService.ObtenerCanales();
    if (!string.IsNullOrEmpty(request.Filter))
        query = query.Where(c => c.Nombre.ToLower().Contains(request.Filter.ToLower()));
    // ... sorting ...
    return await PagedList<ObtenerCanalesResponse>.ToPagedList(query, request);
}

private static RouteHandlerBuilder Map(this RouteGroupBuilder g) =>
    g.MapGet("", async (HttpContext ctx, CanalService svc, [AsParameters] PaginationRequest req) =>
    {
        var response = await Operation(svc, req);
        ctx.AddPaginationHeader(response);
        return Results.Ok(response);
    });

private static RouteHandlerBuilder MapCache(this RouteGroupBuilder g) =>
    g.MapGet("", async (HttpContext ctx, CanalService svc, [AsParameters] PaginationRequest req,
        IMemoryCache cache, IOptions<ApiCacheConfiguration> cfg) =>
    {
        // ... lógica de cache ...
        var response = await Operation(svc, req);   // misma Operation
        // ...
    });
```

❌ Incorrecto:
```csharp
// Bloque de filtros y sorts duplicado dentro de Map() y dentro de MapCache()
```

---

### SC-EP-05: Cache con `SemaphoreSlim` estático

Cuando el endpoint tiene variante cache, debe declarar `private static readonly SemaphoreSlim _lock = new(1, 1)` como campo estático de la clase. El `WaitAsync` se llama antes de verificar la cache y el `Release` va en el `finally` del bloque `try/finally`.

**Justificación:** `IMemoryCache` no es thread-safe para el patrón check-then-set; el semáforo garantiza que solo un hilo ejecute la operación costosa cuando hay cache miss simultáneo.

✅ Correcto:
```csharp
private static readonly SemaphoreSlim _lock = new(1, 1);

// Dentro de MapCache:
await _lock.WaitAsync();
if (cache.TryGetValue(cacheKey, out PagedList<ObtenerCanalesResponse>? cached))
{
    _lock.Release();
    context.AddPaginationHeader(cached!);
    return Results.Ok(cached);
}
try
{
    var response = await Operation(svc, request);
    cache.Set(cacheKey, response, new MemoryCacheEntryOptions
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
```

❌ Incorrecto:
```csharp
// Sin SemaphoreSlim — condición de carrera posible
if (!cache.TryGetValue(cacheKey, out var cached))
{
    cached = await Operation(svc, request);
    cache.Set(cacheKey, cached);
}
return Results.Ok(cached);
```

---

### SC-EP-06: EndpointGroup recibe y devuelve el grupo raíz

El método público de registro `Map{Feature}Endpoints` debe recibir el grupo raíz (`RouteGroupBuilder appEndpoints`), crear un sub-grupo interno con `.MapGroup().WithTags()`, delegar a un método privado `MapEndpoints`, y **devolver `appEndpoints`** — nunca el sub-grupo.

**Justificación:** esto permite encadenar múltiples EndpointGroups con fluent: `apiEndpoints.MapCanalesEndpoints().MapUsuariosEndpoints()`.

✅ Correcto:
```csharp
public static RouteGroupBuilder MapCanalesEndpoints(
    this RouteGroupBuilder appEndpoints,
    ApiCacheConfiguration cacheConfiguration)
{
    var canalesGroup = appEndpoints.MapGroup("canales").WithTags("Canales");
    canalesGroup.MapEndpoints(cacheConfiguration);
    return appEndpoints;                        // grupo raíz, no canalesGroup
}
```

❌ Incorrecto:
```csharp
public static RouteGroupBuilder MapCanalesEndpoints(
    this RouteGroupBuilder appEndpoints,
    ApiCacheConfiguration cacheConfiguration)
{
    var canalesGroup = appEndpoints.MapGroup("canales").WithTags("Canales");
    canalesGroup.MapEndpoints(cacheConfiguration);
    return canalesGroup;    // devuelve el sub-grupo → rompe el encadenamiento
}
```

---

### SC-EP-07: `[AllowAnonymous]` sobre la lambda, nunca en el grupo

Los endpoints que no requieren autenticación deben colocar el atributo `[AllowAnonymous]` directamente sobre la lambda del handler. No se llama `.AllowAnonymous()` fluent sobre el grupo ni sobre el `RouteHandlerBuilder`.

**Justificación:** el grupo raíz ya tiene `.RequireAuthorization()` aplicado globalmente. Anularlo en el grupo o en el builder afectaría a todos los endpoints del sub-grupo. Colocarlo en la lambda es granular y explícito.

✅ Correcto:
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
.WithSummary(ENDPOINT_NAME)
.WithName(ENDPOINT_NAME);
```

❌ Incorrecto:
```csharp
// En el EndpointGroup:
var securityGroup = appEndpoints.MapGroup("auth")
    .WithTags("Security")
    .AllowAnonymous();      // anula auth en todos los endpoints del grupo

// O en el endpoint:
endpointGroup.MapPost("login", async (...) => { ... })
    .AllowAnonymous()       // el fluent no garantiza el mismo comportamiento que el atributo
    .HasApiVersion(...);
```

---

### SC-EP-08: `RequireAuthorization` y `RequireRateLimiting` solo en el grupo raíz

Ningún endpoint individual ni EndpointGroup debe llamar `.RequireAuthorization()` o `.RequireRateLimiting()`. Estas políticas se aplican una sola vez en el grupo raíz dentro de `WebApplicationExtensions.MapEndpoints`.

**Justificación:** el grupo raíz aplica las políticas a todos sus descendientes automáticamente. Repetirlas en sub-grupos o endpoints individuales provoca doble evaluación y dificulta el mantenimiento.

✅ Correcto — solo en `WebApplicationExtensions`:
```csharp
var apiEndpoints = app.MapGroup("api")
                      .WithApiVersionSet(versionSet)
                      .RequireRateLimiting(rateLimitingPolicy)
                      .AddOpenApi()
                      .RequireAuthorization();

apiEndpoints.MapCanalesEndpoints(cacheConfiguration);
```

❌ Incorrecto:
```csharp
// En el EndpointGroup:
var canalesGroup = appEndpoints.MapGroup("canales")
    .WithTags("Canales")
    .RequireAuthorization()         // ya viene del grupo raíz
    .RequireRateLimiting("Fixed");  // ya viene del grupo raíz

// O en el endpoint individual:
endpointGroup.MapPost("", async (...) => { ... })
    .RequireAuthorization();
```

---

### SC-EP-09: Parámetros del handler según tipo de entrada

Los atributos de binding son obligatorios cuando el tipo no es inferible automáticamente por Minimal API.

| Tipo de entrada   | Atributo           | Ejemplo                                  |
|-------------------|--------------------|------------------------------------------|
| Body JSON         | `[FromBody]`       | `[FromBody] CrearCanalRequest request`   |
| Query paginado    | `[AsParameters]`   | `[AsParameters] PaginationRequest req`   |
| Parámetro de ruta | ninguno            | `Guid id` en ruta `"{id}"`               |
| Servicios DI      | ninguno            | `CanalService canalService`              |
| HttpContext       | ninguno            | `HttpContext context`                    |
| CancellationToken | ninguno, al final  | `CancellationToken ct`                   |

**Justificación:** Minimal API infiere servicios DI, rutas y `HttpContext` automáticamente. `[FromBody]` y `[AsParameters]` son necesarios para que el runtime sepa que el parámetro viene del body o de múltiples query strings.

✅ Correcto:
```csharp
async (
    CanalService canalService,
    [FromBody] ActualizarCanalRequest request,
    Guid id,
    CancellationToken ct
) => { ... }
```

❌ Incorrecto:
```csharp
async (
    [FromServices] CanalService canalService,   // [FromServices] es innecesario
    [FromBody] ActualizarCanalRequest request,
    [FromRoute] Guid id,                        // [FromRoute] es innecesario, se infiere
    CancellationToken ct
) => { ... }
```

---

### SC-EP-10: El método de registro devuelve `RouteGroupBuilder`

El método público de un endpoint (`Map{Operacion}`) debe devolver `RouteGroupBuilder` — no `void` ni `RouteHandlerBuilder`. Esto permite encadenar la llamada en `MapEndpoints` del EndpointGroup.

**Justificación:** el patrón de encadenamiento `endpointGroup.MapA().MapB().MapC()` en `MapEndpoints` requiere que cada método devuelva el mismo `RouteGroupBuilder`.

✅ Correcto:
```csharp
public static RouteGroupBuilder MapCrearCanal(this RouteGroupBuilder endpointGroup)
{
    endpointGroup.MapPost("", async (...) => { ... })
        .HasApiVersion(ApiConstants.VERSION_1)
        .WithSummary(ENDPOINT_NAME)
        .WithName(ENDPOINT_NAME);
    return endpointGroup;       // devuelve el grupo para encadenar
}

// En MapEndpoints:
endpointGroup.MapObtenerCanales()
             .MapCrearCanal()
             .MapEliminarCanal();
```

❌ Incorrecto:
```csharp
public static void MapCrearCanal(this RouteGroupBuilder endpointGroup)
{
    endpointGroup.MapPost("", async (...) => { ... })
        .HasApiVersion(ApiConstants.VERSION_1);
    // void — no permite encadenar en MapEndpoints
}
```
