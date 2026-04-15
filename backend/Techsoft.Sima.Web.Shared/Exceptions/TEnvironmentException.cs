namespace Techsoft.Sima.Web.Shared.Exceptions;

public class TEnvironmentException : TException
{
    private const string PREFIX = "SWBENV";

    public enum Sources { APPSETTINGS, ENVIRONMENT_VARIABLE, DATABASE }

    private TEnvironmentException(string code, string message, Dictionary<string, object?>? args = null)
        : base(code, message, args) { }

    public static TEnvironmentException MissingConfiguration(Sources source, string message)
        => new($"{PREFIX}0001", message, new Dictionary<string, object?> { ["Source"] = source.ToString() });

    public static TEnvironmentException InvalidConfiguration(Sources source, string message)
        => new($"{PREFIX}0002", message, new Dictionary<string, object?> { ["Source"] = source.ToString() });
}
