using Microsoft.AspNetCore.Mvc;
using Techsoft.Sima.Web.Application.Dtos.SolicitudesAcceso;
using Techsoft.Sima.Web.Application.Services;
using static Techsoft.Sima.Web.Api.Helpers.ApiConstants;

namespace Techsoft.Sima.Web.Api.Endpoints.Acceso;

public static class AceptarSolicitud
{
    private const string ENDPOINT_NAME = "Aceptar solicitud de acceso";

    public static RouteGroupBuilder MapAceptarSolicitud(this RouteGroupBuilder endpointGroup)
    {
        endpointGroup.MapPost("aceptar/{id:guid}",
            async (
                Guid id,
                [FromBody] AceptarSolicitudRequest request,
                SolicitudAccesoService solicitudService,
                CancellationToken ct) =>
            {
                await solicitudService.Aceptar(id, request.Rol, ct);
                return Results.Ok(new { Mensaje = "Solicitud aceptada y usuario creado correctamente." });
            })
            .AddMetadata();

        return endpointGroup;
    }

    private static RouteHandlerBuilder AddMetadata(this RouteHandlerBuilder endpoint)
    {
        return endpoint
            .HasApiVersion(VERSION_1)
            .Produces(StatusCodes.Status200OK)
            .WithDescription("Acepta una solicitud de acceso y crea el usuario en el sistema. Solo accesible para administradores.")
            .WithSummary(ENDPOINT_NAME)
            .WithName(ENDPOINT_NAME)
            .RequireAuthorization(policy => policy.RequireRole(Roles.ADMINISTRADOR, Roles.SUPERUSUARIO));
    }
}
