using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Techsoft.Sima.Web.Api.Extensions.Endpoints;

public static partial class WebApplicationExtensions
{
    public static WebApplication AddHealthChecks(this WebApplication app, string rateLimitingPolicy)
    {
        app.MapHealthChecks("/health-check").AllowAnonymous().RequireRateLimiting(rateLimitingPolicy);
        app.HealthDetails().RequireRateLimiting(rateLimitingPolicy);
        app.AddLiveness().RequireRateLimiting(rateLimitingPolicy);
        app.AddReadiness().RequireRateLimiting(rateLimitingPolicy);
        return app;
    }

    private static IEndpointConventionBuilder HealthDetails(this WebApplication app)
        => app.MapHealthChecks("/health-details", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }).AllowAnonymous();

    private static IEndpointConventionBuilder AddLiveness(this WebApplication app)
        => app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Name == "Liveness"
        });

    private static IEndpointConventionBuilder AddReadiness(this WebApplication app)
        => app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });
}
