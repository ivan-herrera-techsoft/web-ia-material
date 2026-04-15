# Skill: Configurar API Versioning

Configura el versionado de API por header en un proyecto Atenea Minimal API.

---

## Paso 1 — Constante VERSION_1 en ApiConstants

Verificar que `Api/Helpers/ApiConstants.cs` contiene la versión como propiedad:

```csharp
private static readonly ApiVersion _version1 = new(1, 0);
public static ApiVersion VERSION_1 => _version1;
```

Si no existe, agregarla. No declarar como `readonly field` directamente — usar el patrón `private field + property`.

---

## Paso 2 — ConfigureApiVersioning en ServiceExtensions

En `Api/Extensions/ServiceExtensions.cs` agregar:

```csharp
public static IServiceCollection ConfigureApiVersioning(this IServiceCollection services)
{
    services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = VERSION_1;
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = ApiVersionReader.Combine(
            new HeaderApiVersionReader("X-Version")
        );
    }).AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });
    return services;
}
```

Encadenar en `Program.cs`:

```csharp
builder.Services
    ...
    .ConfigureApiVersioning()
    ...
```

---

## Paso 3 — GetVersionSet y MapEndpoints en WebApplicationExtensions

En `Api/Extensions/WebApplicationExtensions.cs`, agregar el método privado y usarlo en `MapEndpoints`:

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

    // Registrar grupos de endpoints del dominio
    apiEndpoints.Map{Entidad}Endpoints(cacheConfiguration);

    return app;
}

private static ApiVersionSet GetVersionSet(this WebApplication app)
{
    return app.NewApiVersionSet()
              .HasApiVersion(VERSION_1)
              .Build();
}
```

---

## Paso 4 — UseVersionedSwagger en WebApplicationExtensions

Agregar el método de Swagger versionado:

```csharp
public static WebApplication UseVersionedSwagger(this WebApplication app)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        if (provider?.ApiVersionDescriptions == null || provider.ApiVersionDescriptions.Count == 0)
            return;
        foreach (var groupName in provider.ApiVersionDescriptions.Select(d => d.GroupName))
        {
            options.SwaggerEndpoint(
                $"{groupName}/swagger.json",
                groupName.ToUpperInvariant()
            );
        }
    });
    return app;
}
```

Activar en `Program.cs` solo en desarrollo:

```csharp
if (app.Environment.IsDevelopment())
    app.UseVersionedSwagger();
```

---

## Paso 5 — HasApiVersion en cada endpoint

Cada endpoint individual declara su versión en el método `AddMetadata()`. El método se encadena después del `MapGet/MapPost/...`:

```csharp
public static class ObtenerCanales
{
    private const string ENDPOINT_NAME = "Obtener canales";

    public static RouteGroupBuilder MapObtenerCanales(this RouteGroupBuilder endpointGroup)
    {
        endpointGroup.MapGet("", async (...) => { ... })
                     .AddMetadata();
        return endpointGroup;
    }

    private static RouteHandlerBuilder AddMetadata(this RouteHandlerBuilder endpoint)
    {
        return endpoint
            .HasApiVersion(ApiConstants.VERSION_1)
            .Produces<IEnumerable<ObtenerCanalesResponse>>(StatusCodes.Status200OK)
            .WithDescription("Descripción del endpoint.")
            .WithSummary(ENDPOINT_NAME)
            .WithName(ENDPOINT_NAME);
    }
}
```

---

## Paso 6 — Grupos de endpoints por dominio

Cada dominio tiene un `EndpointGroup` que crea su sub-grupo con tag. El group **no** llama a `HasApiVersion` — eso es responsabilidad de cada endpoint individual:

```csharp
// Api/Endpoints/Canales/CanalesEndpointGroup.cs
public static class CanalesEndpointGroup
{
    public static RouteGroupBuilder MapCanalesEndpoints(this RouteGroupBuilder appEndpoints)
    {
        var group = appEndpoints.MapGroup("canales").WithTags("Canales");
        group.MapEndpoints();
        return appEndpoints;
    }

    private static RouteGroupBuilder MapEndpoints(this RouteGroupBuilder endpointGroup)
    {
        endpointGroup.MapObtenerCanales()
                     .MapCrearCanal();
        return endpointGroup;
    }
}
```

Registrar en `MapEndpoints` de `WebApplicationExtensions`:

```csharp
apiEndpoints.MapCanalesEndpoints();
```

---

## Verificación

- [ ] `VERSION_1` existe en `ApiConstants` como propiedad (no campo directo)
- [ ] `ConfigureApiVersioning` usa `ApiVersionReader.Combine(new HeaderApiVersionReader("X-Version"))`
- [ ] `DefaultApiVersion = VERSION_1` y `AssumeDefaultVersionWhenUnspecified = true`
- [ ] `GroupNameFormat = "'v'VVV"` en `AddApiExplorer`
- [ ] `GetVersionSet()` usa `NewApiVersionSet().HasApiVersion(VERSION_1).Build()`
- [ ] `WithApiVersionSet(versionSet)` aplicado al grupo `api` raíz
- [ ] Cada endpoint tiene `.HasApiVersion(ApiConstants.VERSION_1)` en `AddMetadata`
- [ ] `UseVersionedSwagger` solo se activa si `app.Environment.IsDevelopment()`
