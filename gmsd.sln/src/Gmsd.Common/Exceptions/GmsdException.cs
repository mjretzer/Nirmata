using Gmsd.Common.Constants;

namespace Gmsd.Common.Exceptions;

public class GmsdException : Exception
{
    public GmsdException(ErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public GmsdException(ErrorCode code, string message, Exception? innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public ErrorCode Code { get; }
}
