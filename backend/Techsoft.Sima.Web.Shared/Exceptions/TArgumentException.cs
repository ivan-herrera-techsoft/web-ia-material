namespace Techsoft.Sima.Web.Shared.Exceptions;

public class TArgumentException : TException
{
    private const string PREFIX = "SWBARG";

    private TArgumentException(string code, string message, Dictionary<string, object?>? args = null)
        : base(code, message, args) { }

    public static TArgumentException NullOrEmpty(string fieldName)
        => new($"{PREFIX}0001", $"El campo '{fieldName}' no puede ser nulo o vacío.",
            new Dictionary<string, object?> { ["Campo"] = fieldName });

    public static TArgumentException OutOfRange(string fieldName, int maxLength)
        => new($"{PREFIX}0002", $"El campo '{fieldName}' excede la longitud máxima de {maxLength} caracteres.",
            new Dictionary<string, object?> { ["Campo"] = fieldName, ["MaxLength"] = maxLength });

    public static TArgumentException InvalidFormat(string message)
        => new($"{PREFIX}0003", message);

    public static TArgumentException NotSupported(string message)
        => new($"{PREFIX}0004", message);
}
