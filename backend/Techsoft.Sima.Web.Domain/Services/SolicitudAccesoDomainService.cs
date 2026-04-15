using Techsoft.Sima.Web.Domain.Contracts.Repositories;
using Techsoft.Sima.Web.Domain.Entities.SolicitudesAcceso;
using Techsoft.Sima.Web.Domain.Enums;
using Techsoft.Sima.Web.Shared.Exceptions;
using Techsoft.Sima.Web.Shared.Logging;
using static Techsoft.Sima.Web.Domain.DomainConstants;

namespace Techsoft.Sima.Web.Domain.Services;

public class SolicitudAccesoDomainService(
    ISolicitudAccesoRepository repositorioSolicitud,
    LoggerWrapper<SolicitudAccesoDomainService> logger)
{
    private readonly ISolicitudAccesoRepository _repositorioSolicitud = repositorioSolicitud;
    private readonly LoggerWrapper<SolicitudAccesoDomainService> _logger = logger;

    public IQueryable<SolicitudAcceso> ConsultarSolicitudes()
        => _repositorioSolicitud.ConsultarSolicitudes();

    public async Task<SolicitudAcceso> ObtenerPorId(Guid solicitudId, CancellationToken ct = default)
    {
        _logger.LogDebug("Obteniendo solicitud de acceso con id: {SolicitudId}", solicitudId);
        return await _repositorioSolicitud.ObtenerPorId(solicitudId, ct)
            ?? throw TNotFoundException.EntityNotFound(
                "No existe una solicitud de acceso con id {SolicitudId}",
                new Dictionary<string, object?> { ["SolicitudId"] = solicitudId });
    }

    public async Task<SolicitudAcceso> Guardar(
        string email,
        string nombre,
        string apellidoPaterno,
        string? apellidoMaterno,
        string? comentario,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Validando solicitud pendiente para email: {Email}", email);
        await ValidarSinSolicitudPendiente(email, ct);

        var solicitud = new SolicitudAcceso(email, nombre, apellidoPaterno, apellidoMaterno, comentario);
        await _repositorioSolicitud.Crear(solicitud, ct);
        await _repositorioSolicitud.SaveChanges(
            new Dictionary<string, string> { ["SolicitudId"] = solicitud.Id.ToString() }, ct);

        _logger.LogInformation("Solicitud de acceso creada con id: {SolicitudId}", solicitud.Id);
        return solicitud;
    }

    public async Task Aceptar(Guid solicitudId, CancellationToken ct = default)
    {
        var solicitud = await ObtenerPorId(solicitudId, ct);
        ValidarSolicitudPendiente(solicitud);
        solicitud.Aceptar();
        await _repositorioSolicitud.Actualizar(solicitud, ct);
        await _repositorioSolicitud.SaveChanges(
            new Dictionary<string, string> { ["SolicitudId"] = solicitud.Id.ToString() }, ct);
        _logger.LogInformation("Solicitud de acceso aceptada con id: {SolicitudId}", solicitudId);
    }

    public async Task<SolicitudAcceso> AceptarYObtener(Guid solicitudId, CancellationToken ct = default)
    {
        var solicitud = await ObtenerPorId(solicitudId, ct);
        ValidarSolicitudPendiente(solicitud);
        solicitud.Aceptar();
        await _repositorioSolicitud.Actualizar(solicitud, ct);
        await _repositorioSolicitud.SaveChanges(
            new Dictionary<string, string> { ["SolicitudId"] = solicitud.Id.ToString() }, ct);
        _logger.LogInformation("Solicitud de acceso aceptada con id: {SolicitudId}", solicitudId);
        return solicitud;
    }

    public async Task Rechazar(Guid solicitudId, CancellationToken ct = default)
    {
        var solicitud = await ObtenerPorId(solicitudId, ct);
        ValidarSolicitudPendiente(solicitud);
        solicitud.Rechazar();
        await _repositorioSolicitud.Actualizar(solicitud, ct);
        await _repositorioSolicitud.SaveChanges(
            new Dictionary<string, string> { ["SolicitudId"] = solicitud.Id.ToString() }, ct);
        _logger.LogInformation("Solicitud de acceso rechazada con id: {SolicitudId}", solicitudId);
    }

    private async Task ValidarSinSolicitudPendiente(string email, CancellationToken ct)
    {
        _logger.LogDebug("Validando que no exista solicitud pendiente para email: {Email}", email);
        var existente = await _repositorioSolicitud.ObtenerPorEmailYEstatus(email, EstatusSolicitud.Pendiente, ct);
        if (existente is not null)
            throw new TInvalidOperationException(
                ExceptionCodes.Operation.SOLICITUD_YA_EXISTE,
                "Ya existe una solicitud de acceso pendiente para el email {Email}",
                new Dictionary<string, object?> { ["Email"] = email });
    }

    private static void ValidarSolicitudPendiente(SolicitudAcceso solicitud)
    {
        if (solicitud.Estatus != EstatusSolicitud.Pendiente)
            throw new TInvalidOperationException(
                ExceptionCodes.Operation.SOLICITUD_NO_PENDIENTE,
                "La solicitud de acceso con id {SolicitudId} no está en estado pendiente",
                new Dictionary<string, object?> { ["SolicitudId"] = solicitud.Id });
    }
}
