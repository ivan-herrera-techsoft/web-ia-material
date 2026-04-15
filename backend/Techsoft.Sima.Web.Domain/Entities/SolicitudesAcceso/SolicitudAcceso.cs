using Techsoft.Sima.Web.Domain.Enums;
using Techsoft.Sima.Web.Domain.Validators.Entities;

namespace Techsoft.Sima.Web.Domain.Entities.SolicitudesAcceso;

public class SolicitudAcceso
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string Nombre { get; private set; } = null!;
    public string ApellidoPaterno { get; private set; } = null!;
    public string? ApellidoMaterno { get; private set; }
    public string? Comentario { get; private set; }
    public EstatusSolicitud Estatus { get; private set; }
    public DateTime FechaCreacionUtc { get; private set; }
    public DateTime FechaActualizacionUtc { get; private set; }

    protected SolicitudAcceso() { }

    public SolicitudAcceso(
        string email,
        string nombre,
        string apellidoPaterno,
        string? apellidoMaterno,
        string? comentario)
    {
        Id = Guid.NewGuid();
        Email = email.FormatearEmail();
        Nombre = nombre.FormatearNombre();
        ApellidoPaterno = apellidoPaterno.FormatearApellido();
        ApellidoMaterno = apellidoMaterno?.FormatearApellidoOpcional();
        Comentario = comentario?.FormatearComentario();
        Estatus = EstatusSolicitud.Pendiente;
        FechaCreacionUtc = DateTime.UtcNow;
        FechaActualizacionUtc = FechaCreacionUtc;
    }

    public void Aceptar()
    {
        Estatus = EstatusSolicitud.Aceptada;
        FechaActualizacionUtc = DateTime.UtcNow;
    }

    public void Rechazar()
    {
        Estatus = EstatusSolicitud.Rechazada;
        FechaActualizacionUtc = DateTime.UtcNow;
    }
}
