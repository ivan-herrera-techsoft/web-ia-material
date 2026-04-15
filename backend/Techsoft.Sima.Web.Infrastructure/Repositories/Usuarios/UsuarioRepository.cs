using Microsoft.EntityFrameworkCore;
using Techsoft.Sima.Web.Domain.Contracts.Repositories;
using Techsoft.Sima.Web.Domain.Entities.Usuarios;
using Techsoft.Sima.Web.Infrastructure.Base;
using Techsoft.Sima.Web.Infrastructure.Contexts;
using Techsoft.Sima.Web.Shared.Logging;

namespace Techsoft.Sima.Web.Infrastructure.Repositories.Usuarios;

public class UsuarioRepository(
    SimaContext context,
    LoggerWrapper<UsuarioRepository> logger)
    : EFRepositoryBase<SimaContext, UsuarioRepository>(context, logger), IUsuarioRepository
{
    public IQueryable<Usuario> ConsultarUsuarios()
    {
        _logger.LogDebug("Creando consulta de usuarios");
        return _context.Usuarios;
    }

    public async Task<Usuario?> ObtenerUsuario(
        IOrderedQueryable<Usuario> query,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Consultando usuario");
        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<Usuario?> ObtenerPorId(Guid usuarioId, CancellationToken ct = default)
    {
        _logger.LogDebug("Consultando usuario con id: {UsuarioId}", usuarioId);
        return await _context.Usuarios
            .Where(u => u.Id == usuarioId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Usuario?> ObtenerPorEmail(string email, CancellationToken ct = default)
    {
        _logger.LogDebug("Consultando usuario con email: {Email}", email);
        return await _context.Usuarios
            .Where(u => u.Email == email)
            .FirstOrDefaultAsync(ct);
    }

    public async Task Crear(Usuario usuario, CancellationToken ct = default)
    {
        _logger.LogDebug("Creando usuario con id: {UsuarioId}", usuario.Id);
        await _context.Usuarios.AddAsync(usuario, ct);
    }

    public Task Actualizar(Usuario usuario, CancellationToken ct = default)
    {
        _logger.LogDebug("Actualizando usuario con id: {UsuarioId}", usuario.Id);
        _context.Usuarios.Update(usuario);
        return Task.CompletedTask;
    }
}
