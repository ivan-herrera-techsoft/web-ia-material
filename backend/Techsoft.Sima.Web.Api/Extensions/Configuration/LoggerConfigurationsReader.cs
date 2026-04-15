using Serilog.Events;
using Techsoft.Sima.Web.Api.Dtos.Configurations;
using Techsoft.Sima.Web.Shared.Exceptions;

namespace Techsoft.Sima.Web.Api.Extensions.Configuration;

public static partial class ConfigurationExtensions
{
    private static LoggerConfiguration GetLoggerConfiguration(this IConfiguration configuration)
    {
        return new LoggerConfiguration
        {
            LogHttpRequests = configuration.GetValue<bool>("Logger:LogHttpRequests"),
            Sqlite = GetSqliteLoggerConfiguration(configuration),
            MainDatabase = GetSqlServerLoggerConfiguration(configuration)
        };
    }

    private static SqliteLoggerConfiguration GetSqliteLoggerConfiguration(IConfiguration configuration)
    {
        var path = Environment.GetEnvironmentVariable("LOGGER_SQLITE_PATH")
            ?? configuration["Logger:Sqlite:Path"];
        if (string.IsNullOrWhiteSpace(path))
            throw TEnvironmentException.MissingConfiguration(
                TEnvironmentException.Sources.APPSETTINGS, "La ruta del archivo SQLite de logs no fue configurada.");

        var levelStr = Environment.GetEnvironmentVariable("LOGGER_SQLITE_MINIMUM_LEVEL")
            ?? configuration["Logger:Sqlite:MinimumLevel"];

        return new SqliteLoggerConfiguration
        {
            Path = path,
            MinimumLevel = ParseLogEventLevel(levelStr, LogEventLevel.Information)
        };
    }

    private static SqlServerLoggerConfiguration GetSqlServerLoggerConfiguration(IConfiguration configuration)
    {
        var connStr = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? configuration.GetConnectionString("SimaWebtech");
        if (string.IsNullOrWhiteSpace(connStr))
            throw TEnvironmentException.MissingConfiguration(
                TEnvironmentException.Sources.APPSETTINGS, "La cadena de conexión para logs no fue configurada.");

        var levelStr = Environment.GetEnvironmentVariable("LOGGER_MAIN_MINIMUM_LEVEL")
            ?? configuration["Logger:Main:MinimumLevel"];

        return new SqlServerLoggerConfiguration
        {
            ConnectionString = connStr,
            MinimumLevel = ParseLogEventLevel(levelStr, LogEventLevel.Error)
        };
    }
}
