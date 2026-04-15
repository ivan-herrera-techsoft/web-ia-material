using Techsoft.Sima.Web.Domain.Contracts.Repositories;
using Techsoft.Sima.Web.Domain.Entities.Usuarios;
using Techsoft.Sima.Web.Domain.Enums;
using Techsoft.Sima.Web.Shared.Exceptions;
using Techsoft.Sima.Web.Shared.Logging;
using static Techsoft.Sima.Web.Domain.DomainConstants;

namespace Techsoft.Sima.Web.Domain.Services;

public class UsuarioDomainService(
    IUsuarioRepository repositorioUsuario,
    LoggerWrapper<UsuarioDomainService> logger)
{
    private readonly IUsuarioRepository _repositorioUsuario = repositorioUsuario;
    private readonly LoggerWrapper<UsuarioDomainService> _logger = logger;

    public IQueryable<Usuario> ConsultarUsuarios()
        => _repositorioUsuario.ConsultarUsuarios();

    public async Task<Usuario> ObtenerPorId(Guid usuarioId, CancellationToken ct = default)
    {
        _logger.LogDebug("Obteniendo usuario con id: {UsuarioId}", usuarioId);
        return await _repositorioUsuario.ObtenerPorId(usuarioId, ct)
            ?? throw TNotFoundException.EntityNotFound(
                "No existe un usuario con id {UsuarioId}",
                new Dictionary<string, object?> { ["UsuarioId"] = usuarioId });
    }

    public async Task<Usuario?> ObtenerPorEmail(string email, CancellationToken ct = default)
    {
        _logger.LogDebug("Obteniendo usuario con email: {Email}", email);
        return await _repositorioUsuario.ObtenerPorEmail(email, ct);
    }

    public async Task<Usuario> Guardar(
        string email,
        string nombre,
        string apellidoPaterno,
        string? apellidoMaterno,
        string rol,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Validando unicidad de email: {Email}", email);
        await ValidarEmailUnico(email, ct);

        var usuario = new Usuario(email, nombre, apellidoPaterno, apellidoMaterno, rol);
        usuario.Activar();

        await _repositorioUsuario.Crear(usuario, ct);
        await _repositorioUsuario.SaveChanges(
            new Dictionary<string, string> { ["UsuarioId"] = usuario.Id.ToString() }, ct);

        _logger.LogInformation("Usuario creado con id: {UsuarioId}", usuario.Id);
        return usuario;
    }

    public async Task Activar(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await ObtenerPorId(usuarioId, ct);
        usuario.Activar();
        await _repositorioUsuario.Actualizar(usuario, ct);
        await _repositorioUsuario.SaveChanges(
            new Dictionary<string, string> { ["UsuarioId"] = usuario.Id.ToString() }, ct);
        _logger.LogInformation("Usuario activado con id: {UsuarioId}", usuarioId);
    }

    public async Task Desactivar(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await ObtenerPorId(usuarioId, ct);
        usuario.Desactivar();
        await _repositorioUsuario.Actualizar(usuario, ct);
        await _repositorioUsuario.SaveChanges(
            new Dictionary<string, string> { ["UsuarioId"] = usuario.Id.ToString() }, ct);
        _logger.LogInformation("Usuario desactivado con id: {UsuarioId}", usuarioId);
    }

    private async Task ValidarEmailUnico(string email, CancellationToken ct)
    {
        _logger.LogDebug("Validando unicidad de email: {Email}", email);
        var existente = await _repositorioUsuario.ObtenerPorEmail(email, ct);
        if (existente is not null)
            throw new TInvalidOperationException(
                ExceptionCodes.Operation.USUARIO_YA_EXISTE,
                "Ya existe un usuario registrado con el email {Email}",
                new Dictionary<string, object?> { ["Email"] = email });
    }
}
