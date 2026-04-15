---
description: Convenciones para la página de inicio (HomeMapping) en Bisoft Atenea
globs: "**/Extensions/Endpoints/HomeMapping.cs"
---

## HomeMapping — página de bienvenida en `/`

La página de inicio vive en `Api/Extensions/Endpoints/HomeMapping.cs` como clase `partial`
de `WebApplicationExtensions`. Renderiza HTML con el nombre y versión del servicio, más
el estado actual de cada health check registrado.

## Estructura del archivo

```csharp
namespace Company.Product.Module.Api.Extensions.Endpoints;

public static partial class WebApplicationExtensions
{
    // Overload sin rate limiting
    public static WebApplication MapHome(this WebApplication app)
    {
        app.Home();
        return app;
    }

    // Overload con rate limiting
    public static WebApplication MapHome(this WebApplication app, string rateLimitingPolicy)
    {
        app.Home().RequireRateLimiting(rateLimitingPolicy);
        return app;
    }

    // Helper privado — registra el endpoint
    private static RouteHandlerBuilder Home(this WebApplication app)
    {
        return app.MapGet("/", [AllowAnonymous] async (
            HttpContext context,
            LoggerWrapper<WebApplication> logger,
            HealthCheckService healthCheckService) =>
        {
            var version  = ApiConstants.ASSEMBLY_VERSION;
            var mainText = $"Welcome {ApiConstants.APP_NAME} API";
            var content  = "";
            try
            {
                var report    = await healthCheckService.CheckHealthAsync();
                var checksHtml = string.Join("\n", report.Entries.Select(kvp =>
                {
                    var icon  = "!";
                    var style = "warning";
                    var status = kvp.Value.Status.ToString();
                    if (kvp.Value.Status == HealthStatus.Healthy)
                        { icon = "&#10003;"; style = "healthy"; }
                    else if (kvp.Value.Status == HealthStatus.Unhealthy)
                        { icon = "&#10007;"; style = "unhealthy"; }
                    return $"""
                      <div class="health-item" data-description="{kvp.Value.Description}">
                          <label>{kvp.Key}</label>
                          <span class="health-icon {style}">{icon}</span>
                      </div>
                    """;
                }));
                content = $"""
                      <div style="display:inline-block; text-align:left; margin-top:20px;">
                          {checksHtml}
                      </div>
                """;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al consultar health details desde página de bienvenida");
                content = "<div style=\"color:red;\">No se pudieron obtener los detalles de health.</div>";
            }

            var html = $"""
                <body style="background-color: aliceblue;">
                    <div style="height: 20%; width: 100%; background-color: rgb(83, 156, 227);"></div>
                    <div style="position:fixed; top:10%; left: 20%; background-color: white;
                                width: 60%; height: 60%; text-align: center; padding-top:10%;
                                font-family: 'Segoe UI', Geneva, Verdana, sans-serif;">
                        <h1>{mainText}</h1>
                        <h3>version {version}</h3>
                        {content}
                    </div>
                    <style>{HOME_STYLE}</style>
                </body>
                """;
            return Results.Content(html, "text/html");
        }).WithMetadata(new SwaggerIgnoreAttribute());
    }

    private const string HOME_STYLE = """
        .health-item { ... }
        """;
}
```

## Puntos clave

**Dos sobrecargas públicas** — `MapHome()` sin parámetros y `MapHome(string rateLimitingPolicy)`.
El helper privado `Home()` devuelve `RouteHandlerBuilder`; la sobrecarga con policy le encadena
`.RequireRateLimiting(...)` antes de retornar.

**`[AllowAnonymous]` en el lambda** — la ruta `/` debe ser accesible sin autenticación aunque el
grupo raíz tenga `RequireAuthorization`.

**`SwaggerIgnoreAttribute`** — `.WithMetadata(new SwaggerIgnoreAttribute())` excluye el endpoint
de la documentación OpenAPI. La página de inicio no debe aparecer en Swagger.

**Servicios inyectados en el lambda**

| Parámetro                         | Uso                                                  |
|-----------------------------------|------------------------------------------------------|
| `HttpContext context`             | Disponible para extensiones futuras                  |
| `LoggerWrapper<WebApplication>`   | Loguea errores al consultar los health checks        |
| `HealthCheckService`              | Ejecuta todos los health checks registrados          |

**Try/catch alrededor de health checks** — si `CheckHealthAsync()` lanza una excepción, se logea
con `LogError` y el bloque `content` muestra un mensaje de error en HTML. El endpoint devuelve
la página igualmente (nunca un 500 desde la home).

**`Results.Content(html, "text/html")`** — no `Results.Ok()`. El content-type es `text/html`
para que el browser renderice la página.

**`HOME_STYLE` como `private const string`** con raw string literal (`"""`). El CSS se mantiene
junto al archivo para que la página sea autónoma; no se referencia ningún archivo `.css` externo.

**`ApiConstants.APP_NAME` y `ApiConstants.ASSEMBLY_VERSION`** — el nombre del servicio se
actualiza en `ApiConstants.APP_NAME` al crear un nuevo proyecto. La versión se lee
automáticamente del ensamblado con `Assembly.GetExecutingAssembly().GetName().Version`.

## Registro en Program.cs

```csharp
// Siempre después de AddHealthChecks, encadenado
app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY)
   .MapHome(FIXED_RATE_LIMITING_POLICY);
```

`MapHome` retorna `WebApplication` para permitir el encadenamiento fluido desde `AddHealthChecks`.

## Iconos de estado

| Estado      | Icono HTML   | Clase CSS    |
|-------------|--------------|--------------|
| Healthy     | `&#10003;` ✓ | `healthy`    |
| Unhealthy   | `&#10007;` ✗ | `unhealthy`  |
| Degraded    | `!`          | `warning`    |

El tooltip con la descripción del check aparece al hacer hover sobre cada item, vía
`data-description` + CSS `::after`.

## Ubicación y namespace

```
Api/
└── Extensions/
    └── Endpoints/
        ├── HomeMapping.cs         ← aquí
        └── HealthChecksMapping.cs
```

Namespace: `Company.Product.Module.Api.Extensions.Endpoints`  
Clase: `public static partial class WebApplicationExtensions`
