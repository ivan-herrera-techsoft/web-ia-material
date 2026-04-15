using Techsoft.Sima.Web.Domain.Entities.Usuarios;

namespace Techsoft.Sima.Web.Domain.Contracts.Repositories;

public interface IUsuarioRepository
{
    IQueryable<Usuario> ConsultarUsuarios();
    Task<Usuario?> ObtenerUsuario(IOrderedQueryable<Usuario> query, CancellationToken ct = default);
    Task<Usuario?> ObtenerPorId(Guid usuarioId, CancellationToken ct = default);
    Task<Usuario?> ObtenerPorEmail(string email, CancellationToken ct = default);
    Task Crear(Usuario usuario, CancellationToken ct = default);
    Task Actualizar(Usuario usuario, CancellationToken ct = default);
    Task SaveChanges(Dictionary<string, string>? metadata = null, CancellationToken ct = default);
}
