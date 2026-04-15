namespace Techsoft.Sima.Web.Shared.Exceptions;

public class TInvalidOperationException : TException
{
    public TInvalidOperationException(int code, string message, Dictionary<string, object?>? args = null)
        : base($"SWBOP{code:0000}", message, args) { }

    public TInvalidOperationException(string code, string message, Dictionary<string, object?>? args = null)
        : base(code, message, args) { }
}
