using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using static Techsoft.Sima.Web.Api.Helpers.ApiConstants;

namespace Techsoft.Sima.Web.Api.Helpers.Swagger;

public class SwaggerConfig(IApiVersionDescriptionProvider provider)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = APP_NAME,
                Version = description.ApiVersion.ToString()
            });
        }
    }
}
