using Techsoft.Sima.Web.Api.Dtos.Configurations;
using Techsoft.Sima.Web.Shared.Exceptions;

namespace Techsoft.Sima.Web.Api.Extensions.Configuration;

public static partial class ConfigurationExtensions
{
    private static CorsConfiguration GetCorsConfiguration(this IConfiguration configuration)
    {
        return new CorsConfiguration(
            origins: GetCorsOrigins(configuration),
            headers: GetCorsHeaders(configuration)
        );
    }

    private static string[] GetCorsOrigins(IConfiguration configuration)
    {
        var envValue = Environment.GetEnvironmentVariable("CORS_ORIGINS");
        if (!string.IsNullOrWhiteSpace(envValue))
            return envValue.Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var origins = configuration.GetSection("Cors:Origins").Get<string[]>();
        if (origins == null || origins.Length == 0)
            throw TEnvironmentException.MissingConfiguration(
                TEnvironmentException.Sources.APPSETTINGS, "La configuración 'Cors:Origins' es obligatoria.");
        return origins;
    }

    private static string[] GetCorsHeaders(IConfiguration configuration)
    {
        var envValue = Environment.GetEnvironmentVariable("CORS_HEADERS");
        if (!string.IsNullOrWhiteSpace(envValue))
            return envValue.Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var headers = configuration.GetSection("Cors:Headers").Get<string[]>();
        if (headers == null || headers.Length == 0)
            throw TEnvironmentException.MissingConfiguration(
                TEnvironmentException.Sources.APPSETTINGS, "La configuración 'Cors:Headers' es obligatoria.");
        return headers;
    }
}
