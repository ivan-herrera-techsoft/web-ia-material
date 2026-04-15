using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Techsoft.Sima.Web.Application.Dtos.SolicitudesAcceso;
using Techsoft.Sima.Web.Application.Services;
using static Techsoft.Sima.Web.Api.Helpers.ApiConstants;

namespace Techsoft.Sima.Web.Api.Endpoints.Acceso;

public static class SolicitarAcceso
{
    private const string ENDPOINT_NAME = "Solicitar acceso";

    public static RouteGroupBuilder MapSolicitarAcceso(this RouteGroupBuilder endpointGroup)
    {
        endpointGroup.MapPost("solicitar",
            [AllowAnonymous] async (
                [FromBody] SolicitarAccesoRequest request,
                SolicitudAccesoService solicitudService,
                CancellationToken ct) =>
            {
                var response = await solicitudService.Solicitar(request, ct);
                return Results.Created("", response);
            })
            .AddMetadata();

        return endpointGroup;
    }

    private static RouteHandlerBuilder AddMetadata(this RouteHandlerBuilder endpoint)
    {
        return endpoint
            .HasApiVersion(VERSION_1)
            .Produces<SolicitarAccesoResponse>(StatusCodes.Status201Created)
            .WithDescription("Registra una solicitud de acceso al sistema. No requiere autenticación.")
            .WithSummary(ENDPOINT_NAME)
            .WithName(ENDPOINT_NAME);
    }
}
