---
description: Convenciones para Swagger/OpenAPI — solo en Development, soporte multi-versión con IApiVersionDescriptionProvider
globs: "**/Extensions/**,**/Api/**"
---

# Reglas de Swagger / OpenAPI

## Principio general

Swagger se configura en dos partes: el registro de servicios (`ConfigureSwagger` en `ServiceExtensions`) y la documentación por versión (`SwaggerConfig` vía `IConfigureOptions<SwaggerGenOptions>`). Solo se activa en desarrollo. La generación de docs por versión se delega a `IApiVersionDescriptionProvider`.

---

## Helpers en Api/Helpers/Swagger/

### SwaggerConfig — Documentación por versión

```csharp
// Api/Helpers/Swagger/SwaggerConfig.cs
namespace Company.Product.Module.Api.Helpers.Swagger;

public class SwaggerConfig(IApiVersionDescriptionProvider provider)
    : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider provider = provider;

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = ApiConstants.APP_NAME,
                Version = description.ApiVersion.ToString()
            });
        }
    }
}
```

Itera las versiones registradas por `ConfigureApiVersioning` y crea un documento Swagger por cada una.

### SwaggerIgnoreAttribute — Excluir endpoints

```csharp
// Api/Helpers/Swagger/SwaggerIgnoreAttribute.cs
namespace Company.Product.Module.Api.Helpers.Swagger;

public class SwaggerIgnoreAttribute : Attribute { }
```

Se aplica con `.WithMetadata(new SwaggerIgnoreAttribute())` en el endpoint. Uso típico: endpoint `/` (home page).

### SwaggerSchemaNameAttribute — Nombre de schema personalizado

```csharp
// Api/Helpers/Swagger/SwaggerSchemaNameAttribute.cs
namespace Company.Product.Module.Api.Helpers.Swagger;

[AttributeUsage(AttributeTargets.Class)]
public class SwaggerSchemaNameAttribute : Attribute
{
    public string Name { get; }
    public SwaggerSchemaNameAttribute(string name) => Name = name;
}
```

Se aplica en el DTO para personalizar el nombre del schema en Swagger:

```csharp
[SwaggerSchemaName("Crear Canal")]
public class CrearCanalRequest { ... }
```

---

## ConfigureSwagger en ServiceExtensions

```csharp
// Api/Extensions/ServiceExtensions.cs
public static IServiceCollection ConfigureSwagger(this IServiceCollection services)
{
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(options =>
    {
        // Excluir endpoints marcados con [SwaggerIgnore]
        options.DocInclusionPredicate((docName, apiDesc) =>
        {
            return apiDesc.ActionDescriptor?.EndpointMetadata
                ?.OfType<SwaggerIgnoreAttribute>().Any() != true;
        });

        // Tags por GroupName (.WithTags en el EndpointGroup)
        options.TagActionsBy(api =>
        {
            if (api.GroupName != null)
                return [api.GroupName];
            return [APP_NAME];
        });

        // IDs de schema personalizados
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

`ConfigureSwagger` no recibe parámetros. La documentación por versión la inyecta `SwaggerConfig` a través de `services.ConfigureOptions<SwaggerConfig>()`.

---

## CustomSchemaIds — Lógica de nombres de schema

| Tipo de clase               | Resultado                                    |
|-----------------------------|----------------------------------------------|
| Genérico (`PagedList<T>`)   | `pagedList_T` (nombre genérico + arg, kebab) |
| Con `[SwaggerSchemaName]`   | Valor del atributo literal                   |
| Termina en `Request`        | Sin "Request", kebab-case                    |
| Termina en `Response`       | Nombre completo kebab-case                   |
| Cualquier otro              | Nombre de tipo tal cual                      |

Ejemplo: `CrearCanalRequest` → `crear-canal`, `ObtenerCanalResponse` → `obtener-canal-response`.

---

## Metadata en endpoints (AddMetadata)

Cada endpoint declara su documentación en el método `AddMetadata()`:

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

- `.WithSummary` — título corto en Swagger UI
- `.WithDescription` — descripción larga
- `.WithName` — operationId en el documento OpenAPI
- `.Produces<T>` — documenta el tipo de respuesta exitosa
- Los errores estándar (400/401/403/404/429/500) se agregan globalmente en `AddOpenApi` del grupo raíz

---

## Respuestas estándar del grupo raíz (AddOpenApi)

El método privado `AddOpenApi` en `WebApplicationExtensions` agrega respuestas de error comunes a todos los endpoints del grupo `api` vía `AddOpenApiOperationTransformer`:

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

---

## UseVersionedSwagger (solo en desarrollo)

```csharp
// Program.cs
if (app.Environment.IsDevelopment())
    app.UseVersionedSwagger();
```

```csharp
// WebApplicationExtensions.cs
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
