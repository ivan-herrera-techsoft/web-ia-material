namespace Techsoft.Sima.Web.Shared.Exceptions;

public class TNotFoundException : TException
{
    private const string PREFIX = "SWBNF";

    private TNotFoundException(string code, string message, Dictionary<string, object?>? args = null)
        : base(code, message, args) { }

    public static TNotFoundException EntityNotFound(string message, Dictionary<string, object?>? args = null)
        => new($"{PREFIX}0001", message, args);

    public static TNotFoundException ResourceNotFound(string message, Dictionary<string, object?>? args = null)
        => new($"{PREFIX}0002", message, args);
}
