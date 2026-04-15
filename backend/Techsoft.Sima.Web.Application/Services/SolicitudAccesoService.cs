using Techsoft.Sima.Web.Application.Dtos.SolicitudesAcceso;
using Techsoft.Sima.Web.Domain.Contracts.Repositories;
using Techsoft.Sima.Web.Domain.Services;
using Techsoft.Sima.Web.Shared.Logging;

namespace Techsoft.Sima.Web.Application.Services;

public class SolicitudAccesoService(
    SolicitudAccesoDomainService servicioDominio,
    UsuarioDomainService servicioUsuario,
    ISolicitudAccesoRepository repositorioSolicitud,
    LoggerWrapper<SolicitudAccesoService> logger)
{
    private readonly SolicitudAccesoDomainService _servicioDominio = servicioDominio;
    private readonly UsuarioDomainService _servicioUsuario = servicioUsuario;
    private readonly ISolicitudAccesoRepository _repositorioSolicitud = repositorioSolicitud;
    private readonly LoggerWrapper<SolicitudAccesoService> _logger = logger;

    public IQueryable<ObtenerSolicitudResponse> ObtenerSolicitudesPendientes()
    {
        _logger.LogDebug("Consultando solicitudes de acceso pendientes");
        return _repositorioSolicitud.ConsultarSolicitudes()
            .Where(s => s.Estatus == Domain.Enums.EstatusSolicitud.Pendiente)
            .OrderBy(s => s.FechaCreacionUtc)
            .Select(s => new ObtenerSolicitudResponse(
                s.Id,
                s.Email,
                s.Nombre,
                s.ApellidoPaterno,
                s.ApellidoMaterno,
                s.Comentario,
                s.Estatus.ToString(),
                s.FechaCreacionUtc));
    }

    public async Task<SolicitarAccesoResponse> Solicitar(
        SolicitarAccesoRequest solicitud,
        CancellationToken ct = default)
    {
        var entidad = await _servicioDominio.Guardar(
            solicitud.Email,
            solicitud.Nombre,
            solicitud.ApellidoPaterno,
            solicitud.ApellidoMaterno,
            solicitud.Comentario,
            ct);

        _logger.LogInformation("Solicitud de acceso registrada con id: {SolicitudId}", entidad.Id);

        return new SolicitarAccesoResponse(
            entidad.Id,
            entidad.Email,
            "Tu solicitud de acceso ha sido registrada correctamente. Recibirás una notificación cuando sea procesada.");
    }

    public async Task Aceptar(Guid solicitudId, string rol, CancellationToken ct = default)
    {
        var solicitud = await _servicioDominio.AceptarYObtener(solicitudId, ct);

        await _servicioUsuario.Guardar(
            solicitud.Email,
            solicitud.Nombre,
            solicitud.ApellidoPaterno,
            solicitud.ApellidoMaterno,
            rol,
            ct);

        _logger.LogInformation("Solicitud de acceso aceptada y usuario creado. SolicitudId: {SolicitudId}", solicitudId);
    }

    public async Task Rechazar(Guid solicitudId, CancellationToken ct = default)
    {
        await _servicioDominio.Rechazar(solicitudId, ct);
        _logger.LogInformation("Solicitud de acceso rechazada. SolicitudId: {SolicitudId}", solicitudId);
    }
}
