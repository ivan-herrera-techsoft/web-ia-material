namespace Techsoft.Sima.Web.Api.Endpoints.Auth;

public static class AuthEndpointGroup
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder appEndpoints)
    {
        var group = appEndpoints.MapGroup("auth").WithTags("Auth");
        group.MapEndpoints();
        return appEndpoints;
    }

    private static RouteGroupBuilder MapEndpoints(this RouteGroupBuilder endpointGroup)
    {
        endpointGroup.MapObtenerPerfil();
        return endpointGroup;
    }
}
