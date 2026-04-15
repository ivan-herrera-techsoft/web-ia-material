namespace Techsoft.Sima.Web.Application.Dtos.SolicitudesAcceso;

public record ObtenerSolicitudResponse(
    Guid Id,
    string Email,
    string Nombre,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string? Comentario,
    string Estatus,
    DateTime FechaCreacionUtc);
