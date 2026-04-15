using Techsoft.Sima.Web.Api.Dtos.Pagination;
using Techsoft.Sima.Web.Api.Helpers;
using Techsoft.Sima.Web.Application.Dtos.SolicitudesAcceso;
using Techsoft.Sima.Web.Application.Services;
using static Techsoft.Sima.Web.Api.Helpers.ApiConstants;

namespace Techsoft.Sima.Web.Api.Endpoints.Acceso;

public static class ObtenerSolicitudes
{
    private const string ENDPOINT_NAME = "Obtener solicitudes de acceso";

    public static RouteGroupBuilder MapObtenerSolicitudes(this RouteGroupBuilder endpointGroup)
    {
        endpointGroup.MapGet("solicitudes",
            async (
                HttpContext context,
                SolicitudAccesoService solicitudService,
                [AsParameters] PaginationRequest request) =>
            {
                var query = solicitudService.ObtenerSolicitudesPendientes();
                var response = await PagedList<ObtenerSolicitudResponse>.ToPagedList(query, request);
                context.AddPaginationHeader(response);
                return Results.Ok(response);
            })
            .AddMetadata();

        return endpointGroup;
    }

    private static RouteHandlerBuilder AddMetadata(this RouteHandlerBuilder endpoint)
    {
        return endpoint
            .HasApiVersion(VERSION_1)
            .Produces<PagedList<ObtenerSolicitudResponse>>(StatusCodes.Status200OK)
            .WithDescription("Obtiene las solicitudes de acceso pendientes. Solo accesible para administradores.")
            .WithSummary(ENDPOINT_NAME)
            .WithName(ENDPOINT_NAME)
            .RequireAuthorization(policy => policy.RequireRole(Roles.ADMINISTRADOR, Roles.SUPERUSUARIO));
    }
}
