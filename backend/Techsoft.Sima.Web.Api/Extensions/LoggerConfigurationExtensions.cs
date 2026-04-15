using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using Techsoft.Sima.Web.Api.Dtos.Configurations;

namespace Techsoft.Sima.Web.Api.Extensions;

public static class LoggerConfigurationExtensions
{
    private const string TABLE_NAME = "SimaWebLogs";

    public static Serilog.LoggerConfiguration AddConfiguration(
        this Serilog.LoggerConfiguration loggerConfiguration,
        GeneralConfiguration configuration)
    {
        // SQLite sink
        loggerConfiguration.WriteTo.SQLite(
            sqliteDbPath: configuration.Logger.Sqlite.Path,
            restrictedToMinimumLevel: configuration.Logger.Sqlite.MinimumLevel,
            tableName: TABLE_NAME);

        // SQL Server sink
        var sinkOpts = new MSSqlServerSinkOptions
        {
            TableName = TABLE_NAME,
            AutoCreateSqlTable = true
        };
        loggerConfiguration.WriteTo.MSSqlServer(
            connectionString: configuration.Logger.MainDatabase.ConnectionString,
            sinkOptions: sinkOpts,
            restrictedToMinimumLevel: configuration.Logger.MainDatabase.MinimumLevel);

        // Filter HTTP requests if not needed
        if (!configuration.Logger.LogHttpRequests)
            loggerConfiguration.Filter.ByExcluding(
                Serilog.Filters.Matching.FromSource("Microsoft.AspNetCore"));

        // Always filter EF Core command logs
        loggerConfiguration.Filter.ByExcluding(
            Serilog.Filters.Matching.FromSource("Microsoft.EntityFrameworkCore.Database.Command"));

        // Console only in debug
#if DEBUG
        loggerConfiguration.WriteTo.Console();
#endif

        loggerConfiguration.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                           .MinimumLevel.Override("System", LogEventLevel.Warning);

        return loggerConfiguration;
    }
}
