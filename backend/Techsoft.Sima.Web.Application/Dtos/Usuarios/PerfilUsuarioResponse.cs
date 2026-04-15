namespace Techsoft.Sima.Web.Application.Dtos.Usuarios;

public record PerfilUsuarioResponse(
    Guid Id,
    string Email,
    string Nombre,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string Rol,
    string Estatus);
