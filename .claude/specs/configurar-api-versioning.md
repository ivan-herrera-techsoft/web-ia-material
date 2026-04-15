# Spec: Configurar API Versioning

## Contratos

---

### SC-AV-01 — Versionado exclusivamente vía header X-Version

**Justificación**: El header `X-Version` mantiene la URL limpia y semánticamente estable entre versiones. El versionado por query string (`?api-version=1.0`) contamina las URLs y dificulta el caché. El versionado por segmento de URL (`/v1/canales`) requiere duplicar rutas. El header es la convención Bisoft para todos los proyectos Atenea.

**Correcto**:
```csharp
options.ApiVersionReader = ApiVersionReader.Combine(
    new HeaderApiVersionReader("X-Version")
);
```

```http
GET /api/canales
X-Version: 1.0
```

**Incorrecto**:
```csharp
options.ApiVersionReader = new QueryStringApiVersionReader("api-version");
// o
options.ApiVersionReader = new UrlSegmentApiVersionReader();
```

---

### SC-AV-02 — VERSION_1 como propiedad en ApiConstants, no campo directo

**Justificación**: `ApiVersion` es una clase y `readonly` en un campo estático puede generar problemas con inicialización estática en algunos escenarios. El patrón de campo privado + propiedad pública es el estándar del template y garantiza consistencia de acceso.

**Correcto**:
```csharp
// ApiConstants.cs
private static readonly ApiVersion _version1 = new(1, 0);
public static ApiVersion VERSION_1 => _version1;
```

**Incorrecto**:
```csharp
public static readonly ApiVersion VERSION_1 = new(1, 0);
// o
public const string VERSION_1 = "1.0";
```

---

### SC-AV-03 — ApiVersionReader.Combine aunque haya un solo reader

**Justificación**: `ApiVersionReader.Combine` permite agregar readers adicionales en el futuro (por ejemplo, un `QueryStringApiVersionReader` de compatibilidad) sin cambiar la firma del método. Usar un reader directamente requiere refactorización cuando se agrega el segundo.

**Correcto**:
```csharp
options.ApiVersionReader = ApiVersionReader.Combine(
    new HeaderApiVersionReader("X-Version")
);
```

**Incorrecto**:
```csharp
options.ApiVersionReader = new HeaderApiVersionReader("X-Version");
```

---

### SC-AV-04 — WithApiVersionSet en el grupo raíz, HasApiVersion en cada endpoint

**Justificación**: `WithApiVersionSet` en el grupo `api` asocia el conjunto de versiones al router. `HasApiVersion` en cada endpoint declara qué versiones soporta ese endpoint específico. Aplicar `HasApiVersion` al grupo en lugar de al endpoint individual impediría tener endpoints con versiones distintas dentro del mismo grupo.

**Correcto**:
```csharp
// WebApplicationExtensions — grupo raíz
var apiEndpoints = app.MapGroup("api")
                      .WithApiVersionSet(versionSet)
                      ...

// Endpoint individual — AddMetadata
endpoint.HasApiVersion(ApiConstants.VERSION_1)
        .Produces<...>(200)
        ...
```

**Incorrecto**:
```csharp
// Aplicar HasApiVersion al grupo en lugar de a cada endpoint
var group = appEndpoints.MapGroup("canales")
                        .HasApiVersion(ApiConstants.VERSION_1);   // incorrecto
```

---

### SC-AV-05 — GetVersionSet como método privado en WebApplicationExtensions

**Justificación**: La construcción del `ApiVersionSet` es un detalle de implementación del mapeo de endpoints. Encapsularla en un método privado de `WebApplicationExtensions` evita que se llame desde `Program.cs` u otras extensiones, manteniendo la responsabilidad en la capa correcta.

**Correcto**:
```csharp
// WebApplicationExtensions.cs — método privado
private static ApiVersionSet GetVersionSet(this WebApplication app)
{
    return app.NewApiVersionSet()
              .HasApiVersion(VERSION_1)
              .Build();
}
```

**Incorrecto**:
```csharp
// En Program.cs directamente
var versionSet = app.NewApiVersionSet()
                    .HasApiVersion(new ApiVersion(1, 0))
                    .Build();
```

---

### SC-AV-06 — AddMetadata como método privado por endpoint

**Justificación**: Separar la declaración de metadatos (versión, respuestas, nombre, descripción) del handler del endpoint mejora la legibilidad. `AddMetadata` agrupa todo lo declarativo en un solo lugar, y el handler se enfoca solo en la lógica.

**Correcto**:
```csharp
public static RouteGroupBuilder MapObtenerCanales(this RouteGroupBuilder endpointGroup)
{
    endpointGroup.MapGet("", async (...) =>
    {
        // lógica del handler
    }).AddMetadata();
    return endpointGroup;
}

private static RouteHandlerBuilder AddMetadata(this RouteHandlerBuilder endpoint)
{
    return endpoint
        .HasApiVersion(ApiConstants.VERSION_1)
        .Produces<IEnumerable<ObtenerCanalesResponse>>(StatusCodes.Status200OK)
        .WithDescription("Descripción.")
        .WithSummary(ENDPOINT_NAME)
        .WithName(ENDPOINT_NAME);
}
```

**Incorrecto**:
```csharp
// Metadatos mezclados con el handler
endpointGroup.MapGet("", async (...) => { ... })
             .HasApiVersion(ApiConstants.VERSION_1)
             .Produces<...>(200)
             .WithSummary("Obtener canales")
             .WithName("Obtener canales");
```

---

### SC-AV-07 — UseVersionedSwagger solo en desarrollo

**Justificación**: Swagger expone la documentación completa de la API, incluyendo estructura de requests y responses. En producción esto sería un vector de reconocimiento para atacantes. El entorno de desarrollo es el único contexto donde la documentación interactiva agrega valor sin riesgo.

**Correcto**:
```csharp
if (app.Environment.IsDevelopment())
    app.UseVersionedSwagger();
```

**Incorrecto**:
```csharp
// Sin guardia de entorno
app.UseVersionedSwagger();
// o
app.UseSwagger();
app.UseSwaggerUI();
```

---

### SC-AV-08 — UseVersionedSwagger lee versiones desde IApiVersionDescriptionProvider

**Justificación**: Hardcodear los endpoints de Swagger (`/swagger/v1/swagger.json`) requiere actualizarlos manualmente cada vez que se agrega una versión. Leer desde `IApiVersionDescriptionProvider` genera los endpoints automáticamente para cada versión registrada.

**Correcto**:
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

**Incorrecto**:
```csharp
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");  // hardcodeado
});
```
