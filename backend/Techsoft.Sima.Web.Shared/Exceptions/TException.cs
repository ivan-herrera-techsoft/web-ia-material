namespace Techsoft.Sima.Web.Shared.Exceptions;

public abstract class TException : Exception
{
    public string Code { get; }
    public new string Message { get; }
    public Dictionary<string, object?>? Args { get; }

    protected TException(string code, string message, Dictionary<string, object?>? args = null)
        : base(message)
    {
        Code = code;
        Message = message;
        Args = args;
    }
}
