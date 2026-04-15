---
name: configurar-home
description: Crea la página de inicio (HomeMapping) con health checks embebidos en Bisoft Atenea
---

Configurar la página de bienvenida en `/` para el proyecto actual.

## 1. Actualizar APP_NAME en ApiConstants

En `Api/Helpers/ApiConstants.cs`, cambiar el valor de `APP_NAME` al nombre real del servicio:

```csharp
public const string APP_NAME = "NombreDelServicio";
```

> Este valor aparece como título en la página (`Welcome NombreDelServicio API`).

## 2. Crear HomeMapping.cs

Crear `Api/Extensions/Endpoints/HomeMapping.cs`:

```csharp
using Bisoft.Logging.Util;
using Company.Product.Module.Api.Helpers;
using Company.Product.Module.Api.Helpers.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Reflection;

namespace Company.Product.Module.Api.Extensions.Endpoints;

public static partial class WebApplicationExtensions
{
    public static WebApplication MapHome(this WebApplication app)
    {
        app.Home();
        return app;
    }

    public static WebApplication MapHome(this WebApplication app, string rateLimitingPolicy)
    {
        app.Home().RequireRateLimiting(rateLimitingPolicy);
        return app;
    }

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
                var report     = await healthCheckService.CheckHealthAsync();
                var checksHtml = string.Join("\n", report.Entries.Select(kvp =>
                {
                    var icon  = "!";
                    var style = "warning";
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

    private const string HOME_STYLE =
        """
            .health-item {
                position: relative;
                display: flex;
                align-items: center;
                gap: 10px;
                padding: 10px 14px;
                border-radius: 8px;
                background: #f8f9fa;
                font-family: "Segoe UI", sans-serif;
                font-size: 15px;
                font-weight: 500;
                color: #333;
                box-shadow: 0 2px 6px rgba(0,0,0,0.08);
                transition: transform 0.2s ease, box-shadow 0.2s ease;
                margin-bottom: 20px;
                justify-content: space-between;
            }

            .health-item:hover {
                transform: translateY(-2px);
                box-shadow: 0 4px 12px rgba(0,0,0,0.12);
            }

            .health-icon {
                width: 24px;
                height: 24px;
                border-radius: 50%;
                display: flex;
                align-items: center;
                justify-content: center;
                font-size: 14px;
                font-weight: bold;
                color: white;
                flex-shrink: 0;
                animation: pop 0.3s ease;
                user-select: none;
                pointer-events: none;
            }

            .healthy   { background-color: #4caf50; }
            .unhealthy { background-color: #e53935; }
            .warning   { background-color: #ffb300; color: black; }

            @keyframes pop {
                0%   { transform: scale(0.7); opacity: 0; }
                100% { transform: scale(1);   opacity: 1; }
            }

            .health-item::after {
                content: attr(data-description);
                position: absolute;
                bottom: 120%;
                left: 50%;
                transform: translateX(-50%);
                background-color: #222;
                color: #fff;
                padding: 6px 10px;
                border-radius: 6px;
                font-size: 13px;
                white-space: nowrap;
                opacity: 0;
                pointer-events: none;
                transition: opacity 0.3s ease;
                transition-delay: 0.3s;
                user-select: none;
                z-index: 10;
            }

            .health-item:hover::after { opacity: 1; }
        """;
}
```

## 3. Registrar en Program.cs

Encadenar `MapHome` inmediatamente después de `AddHealthChecks`:

```csharp
app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY)
   .MapHome(FIXED_RATE_LIMITING_POLICY);
```

Si el proyecto no usa rate limiting:

```csharp
app.AddHealthChecks()
   .MapHome();
```

## Checklist

- [ ] `APP_NAME` actualizado en `ApiConstants` con el nombre del servicio
- [ ] Archivo en `Api/Extensions/Endpoints/HomeMapping.cs`
- [ ] Clase `public static partial class WebApplicationExtensions` en namespace `...Extensions.Endpoints`
- [ ] Dos sobrecargas públicas de `MapHome` (con y sin `rateLimitingPolicy`)
- [ ] Helper privado `Home()` devuelve `RouteHandlerBuilder`
- [ ] `[AllowAnonymous]` en el lambda
- [ ] `.WithMetadata(new SwaggerIgnoreAttribute())` al final del MapGet
- [ ] Try/catch alrededor de `healthCheckService.CheckHealthAsync()`
- [ ] `Results.Content(html, "text/html")` — no `Results.Ok()`
- [ ] `HOME_STYLE` como `private const string` con raw string literal
- [ ] `MapHome` encadenado después de `AddHealthChecks` en Program.cs
