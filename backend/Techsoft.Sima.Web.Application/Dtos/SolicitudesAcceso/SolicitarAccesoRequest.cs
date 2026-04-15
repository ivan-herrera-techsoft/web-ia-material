namespace Techsoft.Sima.Web.Application.Dtos.SolicitudesAcceso;

public record SolicitarAccesoRequest(
    string Email,
    string Nombre,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string? Comentario);
