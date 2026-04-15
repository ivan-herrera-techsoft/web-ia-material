namespace Techsoft.Sima.Web.Shared.Exceptions;

public class TUnauthorizedAccessException : TException
{
    private const string PREFIX = "SWBUA";

    private TUnauthorizedAccessException(string code, string message, Dictionary<string, object?>? args = null)
        : base(code, message, args) { }

    public static TUnauthorizedAccessException IncorrectCredentials(string message)
        => new($"{PREFIX}0001", message);

    public static TUnauthorizedAccessException InsufficientPermissions(string message)
        => new($"{PREFIX}0002", message);

    public static TUnauthorizedAccessException InvalidToken(string message)
        => new($"{PREFIX}0003", message);
}
