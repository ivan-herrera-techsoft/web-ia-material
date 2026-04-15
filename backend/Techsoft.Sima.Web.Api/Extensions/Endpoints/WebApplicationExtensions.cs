using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.OpenApi;
using Techsoft.Sima.Web.Api.Dtos.Configurations;
using Techsoft.Sima.Web.Api.Endpoints.Acceso;
using Techsoft.Sima.Web.Api.Endpoints.Auth;
using static Techsoft.Sima.Web.Api.Helpers.ApiConstants;

namespace Techsoft.Sima.Web.Api.Extensions.Endpoints;

public static partial class WebApplicationExtensions
{
    public static WebApplication MapEndpoints(
        this WebApplication app,
        string rateLimitingPolicy,
        ApiCacheConfiguration cacheConfiguration)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(VERSION_1).Build();
        var apiEndpoints = app.MapGroup("api")
            .WithApiVersionSet(versionSet)
            .RequireRateLimiting(rateLimitingPolicy)
            .AddOpenApiResponses()
            .RequireAuthorization();

        apiEndpoints
            .MapAuthEndpoints()
            .MapAccesoEndpoints();

        return app;
    }

    public static WebApplication UseVersionedSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            foreach (var groupName in provider.ApiVersionDescriptions.Select(d => d.GroupName))
            {
                options.SwaggerEndpoint($"{groupName}/swagger.json", groupName.ToUpperInvariant());
            }
        });
        return app;
    }

    private static RouteGroupBuilder AddOpenApiResponses(this RouteGroupBuilder group)
    {
        return group.AddOpenApiOperationTransformer((operation, context, ct) =>
        {
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["400"] = new OpenApiResponse { Description = "Solicitud incorrecta" };
            operation.Responses["401"] = new OpenApiResponse { Description = "No autorizado" };
            operation.Responses["403"] = new OpenApiResponse { Description = "Acceso no concedido" };
            operation.Responses["404"] = new OpenApiResponse { Description = "No encontrado" };
            operation.Responses["429"] = new OpenApiResponse { Description = "Se ha excedido la cantidad de peticiones" };
            operation.Responses["500"] = new OpenApiResponse { Description = "Error interno del servidor" };
            return Task.CompletedTask;
        });
    }
}
