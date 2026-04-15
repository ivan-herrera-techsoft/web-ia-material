using Microsoft.EntityFrameworkCore;
using Techsoft.Sima.Web.Domain.Contracts.Repositories;
using Techsoft.Sima.Web.Domain.Entities.SolicitudesAcceso;
using Techsoft.Sima.Web.Domain.Enums;
using Techsoft.Sima.Web.Infrastructure.Base;
using Techsoft.Sima.Web.Infrastructure.Contexts;
using Techsoft.Sima.Web.Shared.Logging;

namespace Techsoft.Sima.Web.Infrastructure.Repositories.SolicitudesAcceso;

public class SolicitudAccesoRepository(
    SimaContext context,
    LoggerWrapper<SolicitudAccesoRepository> logger)
    : EFRepositoryBase<SimaContext, SolicitudAccesoRepository>(context, logger), ISolicitudAccesoRepository
{
    public IQueryable<SolicitudAcceso> ConsultarSolicitudes()
    {
        _logger.LogDebug("Creando consulta de solicitudes de acceso");
        return _context.SolicitudesAcceso;
    }

    public async Task<SolicitudAcceso?> ObtenerSolicitud(
        IOrderedQueryable<SolicitudAcceso> query,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Consultando solicitud de acceso");
        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<SolicitudAcceso?> ObtenerPorId(Guid solicitudId, CancellationToken ct = default)
    {
        _logger.LogDebug("Consultando solicitud de acceso con id: {SolicitudId}", solicitudId);
        return await _context.SolicitudesAcceso
            .Where(s => s.Id == solicitudId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<SolicitudAcceso?> ObtenerPorEmailYEstatus(
        string email,
        EstatusSolicitud estatus,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Consultando solicitud de acceso para email: {Email} con estatus: {Estatus}", email, estatus);
        return await _context.SolicitudesAcceso
            .Where(s => s.Email == email && s.Estatus == estatus)
            .FirstOrDefaultAsync(ct);
    }

    public async Task Crear(SolicitudAcceso solicitud, CancellationToken ct = default)
    {
        _logger.LogDebug("Creando solicitud de acceso con id: {SolicitudId}", solicitud.Id);
        await _context.SolicitudesAcceso.AddAsync(solicitud, ct);
    }

    public Task Actualizar(SolicitudAcceso solicitud, CancellationToken ct = default)
    {
        _logger.LogDebug("Actualizando solicitud de acceso con id: {SolicitudId}", solicitud.Id);
        _context.SolicitudesAcceso.Update(solicitud);
        return Task.CompletedTask;
    }
}
