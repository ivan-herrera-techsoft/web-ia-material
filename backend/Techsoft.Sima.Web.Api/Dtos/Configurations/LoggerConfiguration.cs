using Serilog.Events;

namespace Techsoft.Sima.Web.Api.Dtos.Configurations;

public class LoggerConfiguration
{
    public bool LogHttpRequests { get; set; }
    public required SqliteLoggerConfiguration Sqlite { get; set; }
    public required SqlServerLoggerConfiguration MainDatabase { get; set; }
}

public class SqliteLoggerConfiguration
{
    public required string Path { get; set; }
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;
}

public class SqlServerLoggerConfiguration
{
    public required string ConnectionString { get; set; }
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Error;
}
