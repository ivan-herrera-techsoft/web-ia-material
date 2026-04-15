namespace Techsoft.Sima.Web.Api.Dtos.Configurations;

public class GeneralConfiguration
{
    public required EntraIdConfiguration EntraId { get; set; }
    public required CorsConfiguration Cors { get; set; }
    public required LoggerConfiguration Logger { get; set; }
    public required ApiCacheConfiguration Cache { get; set; }
    public required string DatabaseConnectionString { get; set; }
    public required int RateLimiterMaxCalls { get; set; }
}
