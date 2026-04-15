---
description: Convenciones para el versionado de API via header X-Version con Asp.Versioning
globs: "**/Extensions/**,**/Endpoints/**,**/Api/**"
---

# Reglas de API Versioning

## Principio general

El versionado se realiza **vía header `X-Version`**. No se usa query string ni segmento de URL. La versión por defecto es `1.0`. Cada endpoint declara explícitamente su versión con `.HasApiVersion()`. El conjunto de versiones (`ApiVersionSet`) se construye una vez en `WebApplicationExtensions` y se aplica al grupo raíz de endpoints.

---

## Constantes en ApiConstants

```csharp
// Api/Helpers/ApiConstants.cs
private static readonly ApiVersion _version1 = new(1, 0);
public static ApiVersion VERSION_1 => _version1;
```

---

## Registro en ServiceExtensions

```csharp
// Api/Extensions/ServiceExtensions.cs
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

Puntos clave:
- `ApiVersionReader.Combine(...)` aunque solo haya un reader — permite agregar más en el futuro sin cambiar la firma.
- `GroupNameFormat = "'v'VVV"` genera nombres de grupo como `v1`, `v1.0`, usados por Swagger.
- No recibe `GeneralConfiguration` ni `IConfiguration` — no necesita configuración externa.

---

## VersionSet en WebApplicationExtensions

El conjunto de versiones se construye con `NewApiVersionSet()` y se aplica al grupo raíz de la API:

```csharp
// Api/Extensions/WebApplicationExtensions.cs
public static WebApplication MapEndpoints(this WebApplication app, string rateLimitingPolicy, ApiCacheConfiguration cacheConfiguration)
{
    var versionSet = app.GetVersionSet();
    var apiEndpoints = app.MapGroup("api")
                          .WithApiVersionSet(versionSet)
                          .RequireRateLimiting(rateLimitingPolicy)
                          .AddOpenApi()
                          .RequireAuthorization();
    apiEndpoints.MapSecurityEndpoints()
                .MapUsuariosEndpoints(cacheConfiguration);
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

## Estructura de grupos de endpoints

Cada dominio de negocio tiene su propio `EndpointGroup` que crea un sub-grupo con tag:

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
                     .MapCrearCanal()
                     .MapEliminarCanal();
        return endpointGroup;
    }
}
```

El grupo de negocio **no** llama a `.WithApiVersionSet()` ni `.HasApiVersion()` — eso ocurre a nivel del grupo raíz `api` y de cada endpoint individual respectivamente.

---

## HasApiVersion en cada endpoint

Cada endpoint declara su versión en el método `AddMetadata()`:

```csharp
// Api/Endpoints/Canales/ObtenerCanales.cs
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
            .WithDescription("Obtiene el listado de canales activos.")
            .WithSummary(ENDPOINT_NAME)
            .WithName(ENDPOINT_NAME);
    }
}
```

---

## Swagger por versión (solo desarrollo)

`UseVersionedSwagger` lee las versiones desde `IApiVersionDescriptionProvider` y registra un endpoint Swagger por cada versión:

```csharp
// Api/Extensions/WebApplicationExtensions.cs
public static WebApplication UseVersionedSwagger(this WebApplication app)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
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

Solo se activa en desarrollo:

```csharp
// Program.cs
if (app.Environment.IsDevelopment())
    app.UseVersionedSwagger();
```

---

## Header de request

```http
GET /api/canales
X-Version: 1.0
Authorization: Bearer {token}
```

Si no se envía `X-Version`, se usa la versión por defecto (`1.0`) gracias a `AssumeDefaultVersionWhenUnspecified = true`.
