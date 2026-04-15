using Serilog.Events;
using Techsoft.Sima.Web.Api.Dtos.Configurations;
using Techsoft.Sima.Web.Shared.Exceptions;

namespace Techsoft.Sima.Web.Api.Extensions.Configuration;

public static partial class ConfigurationExtensions
{
    public static GeneralConfiguration GetConfiguration(this IConfiguration configuration)
    {
        return new GeneralConfiguration
        {
            EntraId = configuration.GetEntraIdConfiguration(),
            Cors = configuration.GetCorsConfiguration(),
            Logger = configuration.GetLoggerConfiguration(),
            Cache = configuration.GetCacheConfiguration(),
            DatabaseConnectionString = configuration.GetDatabaseConnectionString(),
            RateLimiterMaxCalls = configuration.GetRateLimiterMaxCalls()
        };
    }

    private static string GetDatabaseConnectionString(this IConfiguration configuration)
    {
        var value = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? configuration.GetConnectionString("SimaWebtech");
        if (string.IsNullOrWhiteSpace(value))
            throw TEnvironmentException.MissingConfiguration(
                TEnvironmentException.Sources.APPSETTINGS,
                "La cadena de conexión 'SimaWebtech' no fue encontrada.");
        return value;
    }

    private static int GetRateLimiterMaxCalls(this IConfiguration configuration)
    {
        var value = Environment.GetEnvironmentVariable("MAX_CALLS")
            ?? configuration["MaxCallsPerMinute"];
        if (string.IsNullOrWhiteSpace(value) || !int.TryParse(value, out var result))
            throw TEnvironmentException.InvalidConfiguration(
                TEnvironmentException.Sources.APPSETTINGS,
                "La configuración 'MaxCallsPerMinute' es inválida o no fue encontrada.");
        return result;
    }

    private static ApiCacheConfiguration GetCacheConfiguration(this IConfiguration configuration)
    {
        return new ApiCacheConfiguration
        {
            CacheEnabled = configuration.GetValue<bool>("Cache:CacheEnabled"),
            CacheSlidingDuration = configuration.GetValue<TimeSpan?>("Cache:SlidingDuration") ?? TimeSpan.FromMinutes(5),
            CacheAbsoluteDuration = configuration.GetValue<TimeSpan?>("Cache:AbsoluteDuration") ?? TimeSpan.FromMinutes(30)
        };
    }

    internal static LogEventLevel ParseLogEventLevel(string? value, LogEventLevel defaultLevel = LogEventLevel.Information)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultLevel;
        return Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level) ? level : defaultLevel;
    }
}
