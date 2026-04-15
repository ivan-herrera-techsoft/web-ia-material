using Techsoft.Sima.Web.Shared.Exceptions;
using static Techsoft.Sima.Web.Domain.DomainConstants;

namespace Techsoft.Sima.Web.Domain.Validators.Entities;

public static class UsuarioValidator
{
    private static readonly string[] _rolesValidos = ["superusuario", "administrador", "lider", "usuario"];

    public static string FormatearEmail(this string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw TArgumentException.NullOrEmpty("email");

        var trimmed = email.Trim().ToLowerInvariant();

        if (trimmed.Length > Values.MAX_LENGTH_EMAIL)
            throw TArgumentException.OutOfRange("email", Values.MAX_LENGTH_EMAIL);

        if (!trimmed.Contains('@') || !trimmed.Contains('.'))
            throw TArgumentException.InvalidFormat("El email no tiene un formato válido");

        return trimmed;
    }

    public static string FormatearNombre(this string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw TArgumentException.NullOrEmpty("nombre");

        var trimmed = nombre.Trim();

        if (trimmed.Length > Values.MAX_LENGTH_NOMBRE)
            throw TArgumentException.OutOfRange("nombre", Values.MAX_LENGTH_NOMBRE);

        return trimmed;
    }

    public static string FormatearApellido(this string? apellido)
    {
        if (string.IsNullOrWhiteSpace(apellido))
            throw TArgumentException.NullOrEmpty("apellidoPaterno");

        var trimmed = apellido.Trim();

        if (trimmed.Length > Values.MAX_LENGTH_APELLIDO)
            throw TArgumentException.OutOfRange("apellidoPaterno", Values.MAX_LENGTH_APELLIDO);

        return trimmed;
    }

    public static string FormatearApellidoOpcional(this string apellido)
    {
        var trimmed = apellido.Trim();

        if (trimmed.Length > Values.MAX_LENGTH_APELLIDO)
            throw TArgumentException.OutOfRange("apellidoMaterno", Values.MAX_LENGTH_APELLIDO);

        return trimmed;
    }

    public static string FormatearRol(this string? rol)
    {
        if (string.IsNullOrWhiteSpace(rol))
            throw TArgumentException.NullOrEmpty("rol");

        var normalizado = rol.Trim().ToLowerInvariant();

        if (!_rolesValidos.Contains(normalizado))
            throw TArgumentException.NotSupported($"El rol '{rol}' no es válido. Los roles válidos son: {string.Join(", ", _rolesValidos)}");

        return normalizado;
    }

    public static string FormatearComentario(this string comentario)
    {
        var trimmed = comentario.Trim();

        if (trimmed.Length > Values.MAX_LENGTH_COMENTARIO)
            throw TArgumentException.OutOfRange("comentario", Values.MAX_LENGTH_COMENTARIO);

        return trimmed;
    }
}
