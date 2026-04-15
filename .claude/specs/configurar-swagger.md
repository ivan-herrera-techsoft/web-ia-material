# Spec: Configurar Swagger / OpenAPI

## Contratos

---

### SC-SW-01 — SwaggerConfig como IConfigureOptions<SwaggerGenOptions>, no inline en ConfigureSwagger

**Justificación**: `SwaggerConfig` necesita inyectar `IApiVersionDescriptionProvider` para iterar las versiones registradas dinámicamente. Este servicio no está disponible en el momento en que se llama `ConfigureSwagger` (fase de registro). `IConfigureOptions<T>` se resuelve más tarde, cuando el contenedor ya está construido y el provider está disponible.

**Correcto**:
```csharp
// SwaggerConfig.cs — IConfigureOptions resuelto en tiempo de build del host
public class SwaggerConfig(IApiVersionDescriptionProvider provider)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
            options.SwaggerDoc(description.GroupName, new OpenApiInfo { ... });
    }
}

// ServiceExtensions.cs
services.ConfigureOptions<SwaggerConfig>();
```

**Incorrecto**:
```csharp
// Intentar usar IApiVersionDescriptionProvider en ConfigureSwagger directamente
services.AddSwaggerGen(options =>
{
    // IApiVersionDescriptionProvider no disponible aquí
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });
});
```

---

### SC-SW-02 — DocInclusionPredicate excluye endpoints con SwaggerIgnoreAttribute

**Justificación**: Algunos endpoints (como la home page `/`) no son parte de la API pública y no deben aparecer en la documentación. `SwaggerIgnoreAttribute` es el mecanismo explícito para marcarlos, en lugar de excluirlos por ruta o convención frágil.

**Correcto**:
```csharp
// En ConfigureSwagger:
options.DocInclusionPredicate((docName, apiDesc) =>
{
    return apiDesc.ActionDescriptor?.EndpointMetadata
        ?.OfType<SwaggerIgnoreAttribute>().Any() != true;
});

// En el endpoint excluido:
app.MapGet("/", handler).WithMetadata(new SwaggerIgnoreAttribute());
```

**Incorrecto**:
```csharp
// Excluir por ruta hardcodeada
options.DocInclusionPredicate((docName, apiDesc) =>
    !apiDesc.RelativePath?.StartsWith("/") ?? true);
```

---

### SC-SW-03 — TagActionsBy usa GroupName del EndpointGroup

**Justificación**: Los tags en Swagger deben coincidir con los grupos lógicos de negocio, que se definen con `.WithTags("Canales")` en el `EndpointGroup`. Usar `GroupName` automáticamente sincroniza los tags de Swagger con los grupos de la API sin duplicar la información.

**Correcto**:
```csharp
options.TagActionsBy(api =>
{
    if (api.GroupName != null)
        return [api.GroupName];
    return [APP_NAME];
});

// EndpointGroup — fuente del GroupName:
appEndpoints.MapGroup("canales").WithTags("Canales");
```

**Incorrecto**:
```csharp
// Hardcodear tags en SwaggerGen ignorando los WithTags de los endpoints
options.TagActionsBy(api => ["Mi API"]);
```

---

### SC-SW-04 — CustomSchemaIds con lógica completa de naming

**Justificación**: Por defecto Swashbuckle usa el nombre de tipo C# como schema ID, lo que genera conflictos con tipos genéricos y nombres largos. La lógica personalizada convierte nombres a kebab-case, resuelve genéricos y respeta el atributo `[SwaggerSchemaName]`, produciendo IDs únicos y legibles.

**Correcto** (lógica completa en orden):
```csharp
options.CustomSchemaIds(type =>
{
    // 1. Genéricos: PagedList<Canal> → paged-list_canal
    if (type.IsGenericType)
    {
        var genericTypeName = type.GetGenericTypeDefinition().Name;
        genericTypeName = genericTypeName[..genericTypeName.IndexOf('`')];
        var genericArgs = string.Join("_", type.GetGenericArguments().Select(t => t.Name));
        return $"{genericTypeName}{genericArgs}".FromPascalCase();
    }
    // 2. Atributo explícito: [SwaggerSchemaName("Crear Canal")]
    var attr = type.GetCustomAttribute<SwaggerSchemaNameAttribute>();
    if (attr != null)
        return attr.Name;
    // 3. Ends with Request: CrearCanalRequest → crear-canal
    if (type.Name.EndsWith("Request"))
        return type.Name.Replace("Request", "").FromPascalCase();
    // 4. Ends with Response: ObtenerCanalResponse → obtener-canal-response
    if (type.Name.EndsWith("Response"))
        return type.Name.FromPascalCase();
    // 5. Default
    return type.Name;
});
```

**Incorrecto**:
```csharp
// Sin lógica personalizada — genera conflictos con tipos genéricos
options.CustomSchemaIds(type => type.FullName);
```

---

### SC-SW-05 — [SwaggerSchemaName] obligatorio en DTOs Request

**Justificación**: Sin el atributo, los Requests reciben nombres kebab-case automáticos (`crear-canal`) que no son descriptivos en Swagger UI. El atributo permite nombres legibles en español con espacios (`"Crear Canal"`), mejorando la experiencia del desarrollador que consume la API.

**Correcto**:
```csharp
[SwaggerSchemaName("Crear Canal")]
public class CrearCanalRequest
{
    public required string Nombre { get; set; }
}
```

**Incorrecto**:
```csharp
// Sin atributo — nombre automático poco descriptivo
public class CrearCanalRequest
{
    public required string Nombre { get; set; }
}
```

---

### SC-SW-06 — AddOpenApi como método privado en WebApplicationExtensions con las 6 respuestas estándar

**Justificación**: Las respuestas de error (400, 401, 403, 404, 429, 500) son comunes a todos los endpoints de la API. Definirlas en un `AddOpenApiOperationTransformer` del grupo raíz garantiza que aparezcan en todos los endpoints sin que cada uno las repita manualmente.

**Correcto**:
```csharp
// Aplicado al grupo raíz:
var apiEndpoints = app.MapGroup("api")
                      .WithApiVersionSet(versionSet)
                      .AddOpenApi()   // aplica las 6 respuestas a todos
                      ...

// Método privado:
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

**Incorrecto**:
```csharp
// Declarar respuestas de error en cada endpoint individualmente
endpoint.Produces(400).Produces(401).Produces(403)...
```

---

### SC-SW-07 — UseVersionedSwagger solo en desarrollo

**Justificación**: La documentación Swagger expone la estructura completa de la API, sus modelos y operaciones. En producción esto facilita el reconocimiento para actores maliciosos. El guard de entorno es innegociable.

**Correcto**:
```csharp
if (app.Environment.IsDevelopment())
    app.UseVersionedSwagger();
```

**Incorrecto**:
```csharp
app.UseVersionedSwagger();         // sin guard de entorno
app.UseSwagger();                  // directamente sin UseVersionedSwagger
```

---

### SC-SW-08 — Cada endpoint documenta solo su respuesta exitosa en AddMetadata

**Justificación**: Las respuestas de error son responsabilidad del `AddOpenApi` del grupo. Duplicarlas en `AddMetadata` genera ruido y puede causar inconsistencias si los mensajes difieren. Cada endpoint solo agrega lo que es específico suyo: el tipo y código de respuesta exitosa.

**Correcto**:
```csharp
private static RouteHandlerBuilder AddMetadata(this RouteHandlerBuilder endpoint)
{
    return endpoint
        .HasApiVersion(ApiConstants.VERSION_1)
        .Produces<ObtenerCanalesResponse>(StatusCodes.Status200OK)  // solo éxito
        .WithDescription("Obtiene canales activos.")
        .WithSummary(ENDPOINT_NAME)
        .WithName(ENDPOINT_NAME);
}
```

**Incorrecto**:
```csharp
endpoint
    .Produces<ObtenerCanalesResponse>(200)
    .Produces(400)   // duplicado — ya lo agrega AddOpenApi
    .Produces(401)   // duplicado
    .Produces(500)   // duplicado
    ...
```
