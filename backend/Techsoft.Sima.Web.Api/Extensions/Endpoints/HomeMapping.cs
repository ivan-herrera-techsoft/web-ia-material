using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Techsoft.Sima.Web.Api.Helpers;
using Techsoft.Sima.Web.Api.Helpers.Swagger;
using Techsoft.Sima.Web.Shared.Logging;

namespace Techsoft.Sima.Web.Api.Extensions.Endpoints;

public static partial class WebApplicationExtensions
{
    public static WebApplication MapHome(this WebApplication app, string rateLimitingPolicy)
    {
        app.Home().RequireRateLimiting(rateLimitingPolicy);
        return app;
    }

    private static RouteHandlerBuilder Home(this WebApplication app)
    {
        return app.MapGet("/", [AllowAnonymous] async (
            LoggerWrapper<WebApplication> logger,
            HealthCheckService healthCheckService) =>
        {
            var version = ApiConstants.ASSEMBLY_VERSION;
            var mainText = $"Welcome {ApiConstants.APP_NAME} API";
            var content = "";

            try
            {
                var report = await healthCheckService.CheckHealthAsync();
                var checksHtml = string.Join("\n", report.Entries.Select(kvp =>
                {
                    var icon = "!";
                    var style = "warning";
                    if (kvp.Value.Status == HealthStatus.Healthy) { icon = "&#10003;"; style = "healthy"; }
                    else if (kvp.Value.Status == HealthStatus.Unhealthy) { icon = "&#10007;"; style = "unhealthy"; }
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
                logger.LogError(ex, "Error al consultar health checks desde página de bienvenida");
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
        .health-item { display: flex; align-items: center; gap: 12px; padding: 6px 0; }
        .health-icon { font-size: 1.2rem; }
        .health-icon.healthy { color: green; }
        .health-icon.unhealthy { color: red; }
        .health-icon.warning { color: orange; }
        """;
}
