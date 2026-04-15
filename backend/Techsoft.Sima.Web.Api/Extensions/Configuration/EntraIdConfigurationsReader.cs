using Techsoft.Sima.Web.Api.Dtos.Configurations;
using Techsoft.Sima.Web.Shared.Exceptions;

namespace Techsoft.Sima.Web.Api.Extensions.Configuration;

public static partial class ConfigurationExtensions
{
    private static EntraIdConfiguration GetEntraIdConfiguration(this IConfiguration configuration)
    {
        var authority = Environment.GetEnvironmentVariable("ENTRA_AUTHORITY")
            ?? configuration["EntraId:Authority"];
        if (string.IsNullOrWhiteSpace(authority))
            throw TEnvironmentException.MissingConfiguration(
                TEnvironmentException.Sources.APPSETTINGS, "La Authority de Azure Entra ID no fue configurada.");

        var audience = Environment.GetEnvironmentVariable("ENTRA_AUDIENCE")
            ?? configuration["EntraId:Audience"];
        if (string.IsNullOrWhiteSpace(audience))
            throw TEnvironmentException.MissingConfiguration(
                TEnvironmentException.Sources.APPSETTINGS, "El Audience de Azure Entra ID no fue configurado.");

        return new EntraIdConfiguration
        {
            Authority = authority,
            Audience = audience
        };
    }
}
