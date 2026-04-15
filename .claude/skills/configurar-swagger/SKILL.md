# Skill: Configurar Swagger / OpenAPI

Configura la documentación OpenAPI (Swagger) en un proyecto Atenea Minimal API. Requiere que `ConfigureApiVersioning` ya esté configurado, ya que `SwaggerConfig` depende de `IApiVersionDescriptionProvider`.

---

## Paso 1 — Crear SwaggerConfig

Crear `Api/Helpers/Swagger/SwaggerConfig.cs`:

```csharp
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Company.Product.Module.Api.Helpers.Swagger;

public class SwaggerConfig(IApiVersionDescriptionProvider provider)
    : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider provider = provider;

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = ApiConstants.APP_NAME,
                Version = description.ApiVersion.ToString()
            });
        }
    }
}
```

---

## Paso 2 — Crear SwaggerIgnoreAttribute

Crear `Api/Helpers/Swagger/SwaggerIgnoreAttribute.cs`:

```csharp
namespace Company.Product.Module.Api.Helpers.Swagger;

public class SwaggerIgnoreAttribute : Attribute { }
```

Se usa con `.WithMetadata(new SwaggerIgnoreAttribute())` en endpoints que no deben aparecer en Swagger (por ejemplo, la home page `/`).

---

## Paso 3 — Crear SwaggerSchemaNameAttribute

Crear `Api/Helpers/Swagger/SwaggerSchemaNameAttribute.cs`:

```csharp
namespace Company.Product.Module.Api.Helpers.Swagger;

[AttributeUsage(AttributeTargets.Class)]
public class SwaggerSchemaNameAttribute : Attribute
{
    public string Name { get; }
    public SwaggerSchemaNameAttribute(string name) => Name = name;
}
```

Se aplica en DTOs Request para personalizar el nombre del schema en Swagger:

```csharp
[SwaggerSchemaName("Crear Canal")]
public class CrearCanalRequest { ... }
```

---

## Paso 4 — ConfigureSwagger en ServiceExtensions

En `Api/Extensions/ServiceExtensions.cs` agregar:

```csharp
public static IServiceCollection ConfigureSwagger(this IServiceCollection services)
{
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(options =>
    {
        options.DocInclusionPredicate((docName, apiDesc) =>
        {
            return apiDesc.ActionDescriptor?.EndpointMetadata
                ?.OfType<SwaggerIgnoreAttribute>().Any() != true;
        });

        options.TagActionsBy(api =>
        {
            if (api.GroupName != null)
                return [api.GroupName];
            return [APP_NAME];
        });

        options.CustomSchemaIds(type =>
        {
            if (type.IsGenericType)
            {
                var genericTypeName = type.GetGenericTypeDefinition().Name;
                genericTypeName = genericTypeName[..genericTypeName.IndexOf('`')];
                var genericArgs = string.Join("_", type.GetGenericArguments().Select(t => t.Name));
                return $"{genericTypeName}{genericArgs}".FromPascalCase();
            }
            var attr = type.GetCustomAttribute<SwaggerSchemaNameAttribute>();
            if (attr != null)
                return attr.Name;

            if (type.Name.EndsWith("Request"))
                return type.Name.Replace("Request", "").FromPascalCase();

            if (type.Name.EndsWith("Response"))
                return type.Name.FromPascalCase();

            return type.Name;
        });
    });

    services.ConfigureOptions<SwaggerConfig>();
    return services;
}
```

Encadenar en `Program.cs`:

```csharp
builder.Services
    ...
    .ConfigureSwagger()
    ...
```

---

## Paso 5 — AddOpenApi en WebApplicationExtensions

En `Api/Extensions/WebApplicationExtensions.cs` agregar el método privado que aplica respuestas estándar a todo el grupo API:

```csharp
private static RouteGroupBuilder AddOpenApi(this RouteGroupBuilder app)
{
    return app.AddOpenApiOperationTransformer((options, context, ct) =>
    {
        options.Responses["400"] = new OpenApiResponse { Description = "Solicitud incorrecta" };
        options.Responses["401"] = new OpenApiResponse { Description = "No autorizado" };
        options.Responses["403"] = new OpenApiResponse { Description = "Acceso no concedido" };
        options.Responses["404"] = new OpenApiResponse { Description = "No encontrado" };
        options.Responses["429"] = new OpenApiResponse { Description = "Se ha excedido la cantidad de peticiones" };
        options.Responses["500"] = new OpenApiResponse { Description = "Error interno del servidor" };
        return Task.CompletedTask;
    });
}
```

Invocar desde `MapEndpoints` en el grupo raíz:

```csharp
var apiEndpoints = app.MapGroup("api")
                      .WithApiVersionSet(versionSet)
                      .RequireRateLimiting(rateLimitingPolicy)
                      .AddOpenApi()           // <-- aquí
                      .RequireAuthorization();
```

---

## Paso 6 — UseVersionedSwagger en WebApplicationExtensions

Agregar en `Api/Extensions/WebApplicationExtensions.cs`:

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
            options.SwaggerEndpoint($"{groupName}/swagger.json", groupName.ToUpperInvariant());
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

## Paso 7 — Metadata en cada endpoint

Cada endpoint documenta sus respuestas en `AddMetadata()`. Los errores estándar vienen del grupo, solo documentar la respuesta exitosa:

```csharp
private static RouteHandlerBuilder AddMetadata(this RouteHandlerBuilder endpoint)
{
    return endpoint
        .HasApiVersion(ApiConstants.VERSION_1)
        .Produces<ObtenerCanalesResponse>(StatusCodes.Status200OK)
        .WithDescription("Descripción detallada del endpoint.")
        .WithSummary(ENDPOINT_NAME)
        .WithName(ENDPOINT_NAME);
}
```

Para endpoints sin cuerpo de respuesta exitosa (ej. DELETE):

```csharp
.Produces(StatusCodes.Status204NoContent)
```

---

## Verificación

- [ ] `SwaggerConfig.cs` implementa `IConfigureOptions<SwaggerGenOptions>` con constructor principal
- [ ] `SwaggerIgnoreAttribute.cs` y `SwaggerSchemaNameAttribute.cs` existen en `Helpers/Swagger/`
- [ ] `ConfigureSwagger` llama a `services.ConfigureOptions<SwaggerConfig>()` (no `AddSingleton`)
- [ ] `DocInclusionPredicate` excluye endpoints con `SwaggerIgnoreAttribute`
- [ ] `TagActionsBy` usa `api.GroupName` (que viene de `.WithTags()` en el EndpointGroup)
- [ ] `CustomSchemaIds` tiene la lógica completa: genérico → atributo → Request → Response → default
- [ ] `AddOpenApi()` privado en `WebApplicationExtensions` con las 6 respuestas de error
- [ ] `UseVersionedSwagger` solo se activa con `app.Environment.IsDevelopment()`
- [ ] Los DTOs Request tienen `[SwaggerSchemaName]` con nombre legible en español
