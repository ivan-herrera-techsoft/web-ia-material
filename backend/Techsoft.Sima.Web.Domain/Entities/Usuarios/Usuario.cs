using Techsoft.Sima.Web.Domain.Enums;
using Techsoft.Sima.Web.Domain.Validators.Entities;

namespace Techsoft.Sima.Web.Domain.Entities.Usuarios;

public class Usuario
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string Nombre { get; private set; } = null!;
    public string ApellidoPaterno { get; private set; } = null!;
    public string? ApellidoMaterno { get; private set; }
    public string Rol { get; private set; } = null!;
    public EstatusEntidad Estatus { get; private set; }
    public DateTime FechaCreacionUtc { get; private set; }
    public DateTime FechaActualizacionUtc { get; private set; }

    protected Usuario() { }

    public Usuario(
        string email,
        string nombre,
        string apellidoPaterno,
        string? apellidoMaterno,
        string rol)
    {
        Id = Guid.NewGuid();
        Email = email.FormatearEmail();
        Nombre = nombre.FormatearNombre();
        ApellidoPaterno = apellidoPaterno.FormatearApellido();
        ApellidoMaterno = apellidoMaterno?.FormatearApellidoOpcional();
        Rol = rol.FormatearRol();
        Estatus = EstatusEntidad.Borrador;
        FechaCreacionUtc = DateTime.UtcNow;
        FechaActualizacionUtc = FechaCreacionUtc;
    }

    public void Activar()
    {
        Estatus = EstatusEntidad.Activo;
        FechaActualizacionUtc = DateTime.UtcNow;
    }

    public void Desactivar()
    {
        Estatus = EstatusEntidad.Inactivo;
        FechaActualizacionUtc = DateTime.UtcNow;
    }

    public void Eliminar()
    {
        Estatus = EstatusEntidad.Eliminado;
        FechaActualizacionUtc = DateTime.UtcNow;
    }

    public void ActualizarRol(string rol)
    {
        Rol = rol.FormatearRol();
        FechaActualizacionUtc = DateTime.UtcNow;
    }
}
