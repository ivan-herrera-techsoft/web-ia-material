using Mapster;
using Techsoft.Sima.Web.Application.Dtos.Usuarios;
using Techsoft.Sima.Web.Domain.Contracts.Repositories;
using Techsoft.Sima.Web.Domain.Services;
using Techsoft.Sima.Web.Shared.Exceptions;
using Techsoft.Sima.Web.Shared.Logging;

namespace Techsoft.Sima.Web.Application.Services;

public class UsuarioService(
    UsuarioDomainService servicioDominio,
    IUsuarioRepository repositorioUsuario,
    LoggerWrapper<UsuarioService> logger)
{
    private readonly UsuarioDomainService _servicioDominio = servicioDominio;
    private readonly IUsuarioRepository _repositorioUsuario = repositorioUsuario;
    private readonly LoggerWrapper<UsuarioService> _logger = logger;

    public async Task<PerfilUsuarioResponse> ObtenerPerfil(string email, CancellationToken ct = default)
    {
        _logger.LogDebug("Obteniendo perfil para email: {Email}", email);
        var usuario = await _servicioDominio.ObtenerPorEmail(email, ct)
            ?? throw TNotFoundException.EntityNotFound(
                "No existe un usuario registrado con el email {Email}",
                new Dictionary<string, object?> { ["Email"] = email });

        return new PerfilUsuarioResponse(
            usuario.Id,
            usuario.Email,
            usuario.Nombre,
            usuario.ApellidoPaterno,
            usuario.ApellidoMaterno,
            usuario.Rol,
            usuario.Estatus.ToString());
    }

    public IQueryable<ObtenerUsuarioResponse> ObtenerUsuarios()
        => _repositorioUsuario.ConsultarUsuarios()
            .Select(u => new ObtenerUsuarioResponse(
                u.Id,
                u.Email,
                u.Nombre,
                u.ApellidoPaterno,
                u.ApellidoMaterno,
                u.Rol,
                u.Estatus.ToString(),
                u.FechaCreacionUtc,
                u.FechaActualizacionUtc));
}
