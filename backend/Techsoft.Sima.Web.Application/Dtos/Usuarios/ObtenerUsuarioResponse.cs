namespace Techsoft.Sima.Web.Application.Dtos.Usuarios;

public record ObtenerUsuarioResponse(
    Guid Id,
    string Email,
    string Nombre,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string Rol,
    string Estatus,
    DateTime FechaCreacionUtc,
    DateTime FechaActualizacionUtc);
