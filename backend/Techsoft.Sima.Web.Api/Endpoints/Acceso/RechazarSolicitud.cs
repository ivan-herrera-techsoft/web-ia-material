using Techsoft.Sima.Web.Application.Services;
using static Techsoft.Sima.Web.Api.Helpers.ApiConstants;

namespace Techsoft.Sima.Web.Api.Endpoints.Acceso;

public static class RechazarSolicitud
{
    private const string ENDPOINT_NAME = "Rechazar solicitud de acceso";

    public static RouteGroupBuilder MapRechazarSolicitud(this RouteGroupBuilder endpointGroup)
    {
        endpointGroup.MapPost("rechazar/{id:guid}",
            async (
                Guid id,
                SolicitudAccesoService solicitudService,
                CancellationToken ct) =>
            {
                await solicitudService.Rechazar(id, ct);
                return Results.Ok(new { Mensaje = "Solicitud rechazada correctamente." });
            })
            .AddMetadata();

        return endpointGroup;
    }

    private static RouteHandlerBuilder AddMetadata(this RouteHandlerBuilder endpoint)
    {
        return endpoint
            .HasApiVersion(VERSION_1)
            .Produces(StatusCodes.Status200OK)
            .WithDescription("Rechaza una solicitud de acceso. Solo accesible para administradores.")
            .WithSummary(ENDPOINT_NAME)
            .WithName(ENDPOINT_NAME)
            .RequireAuthorization(policy => policy.RequireRole(Roles.ADMINISTRADOR, Roles.SUPERUSUARIO));
    }
}
