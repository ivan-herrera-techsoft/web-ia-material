namespace Techsoft.Sima.Web.Api.Dtos.Configurations;

public class ApiCacheConfiguration
{
    public bool CacheEnabled { get; set; }
    public TimeSpan CacheSlidingDuration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan CacheAbsoluteDuration { get; set; } = TimeSpan.FromMinutes(30);
}
