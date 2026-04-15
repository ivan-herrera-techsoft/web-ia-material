namespace Techsoft.Sima.Web.Api.Endpoints.Acceso;

public static class AccesoEndpointGroup
{
    public static RouteGroupBuilder MapAccesoEndpoints(this RouteGroupBuilder appEndpoints)
    {
        var group = appEndpoints.MapGroup("acceso").WithTags("Acceso");
        group.MapEndpoints();
        return appEndpoints;
    }

    private static RouteGroupBuilder MapEndpoints(this RouteGroupBuilder endpointGroup)
    {
        endpointGroup
            .MapSolicitarAcceso()
            .MapObtenerSolicitudes()
            .MapAceptarSolicitud()
            .MapRechazarSolicitud();

        return endpointGroup;
    }
}
