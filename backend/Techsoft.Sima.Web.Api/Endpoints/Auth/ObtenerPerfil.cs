using Techsoft.Sima.Web.Api.Helpers;
using Techsoft.Sima.Web.Application.Dtos.Usuarios;
using Techsoft.Sima.Web.Application.Services;
using static Techsoft.Sima.Web.Api.Helpers.ApiConstants;

namespace Techsoft.Sima.Web.Api.Endpoints.Auth;

public static class ObtenerPerfil
{
    private const string ENDPOINT_NAME = "Obtener perfil";

    public static RouteGroupBuilder MapObtenerPerfil(this RouteGroupBuilder endpointGroup)
    {
        endpointGroup.MapGet("me",
            async (
                HttpContext context,
                UsuarioService usuarioService,
                CancellationToken ct) =>
            {
                var email = context.User.Claims
                    .FirstOrDefault(c => c.Type == Claims.EMAIL)?.Value
                    ?? throw new UnauthorizedAccessException("Token sin claim de email");

                var response = await usuarioService.ObtenerPerfil(email, ct);
                return Results.Ok(response);
            })
            .AddMetadata();

        return endpointGroup;
    }

    private static RouteHandlerBuilder AddMetadata(this RouteHandlerBuilder endpoint)
    {
        return endpoint
            .HasApiVersion(VERSION_1)
            .Produces<PerfilUsuarioResponse>(StatusCodes.Status200OK)
            .WithDescription("Obtiene el perfil del usuario autenticado según su token de Azure Entra ID.")
            .WithSummary(ENDPOINT_NAME)
            .WithName(ENDPOINT_NAME);
    }
}
