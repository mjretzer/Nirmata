using nirmata.Common.Constants;

namespace nirmata.Common.Exceptions;

public class nirmataException : Exception
{
    public nirmataException(ErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public nirmataException(ErrorCode code, string message, Exception? innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public ErrorCode Code { get; }
}
