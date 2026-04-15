using Techsoft.Sima.Web.Domain.Entities.SolicitudesAcceso;
using Techsoft.Sima.Web.Domain.Enums;

namespace Techsoft.Sima.Web.Domain.Contracts.Repositories;

public interface ISolicitudAccesoRepository
{
    IQueryable<SolicitudAcceso> ConsultarSolicitudes();
    Task<SolicitudAcceso?> ObtenerSolicitud(IOrderedQueryable<SolicitudAcceso> query, CancellationToken ct = default);
    Task<SolicitudAcceso?> ObtenerPorId(Guid solicitudId, CancellationToken ct = default);
    Task<SolicitudAcceso?> ObtenerPorEmailYEstatus(string email, EstatusSolicitud estatus, CancellationToken ct = default);
    Task Crear(SolicitudAcceso solicitud, CancellationToken ct = default);
    Task Actualizar(SolicitudAcceso solicitud, CancellationToken ct = default);
    Task SaveChanges(Dictionary<string, string>? metadata = null, CancellationToken ct = default);
}
