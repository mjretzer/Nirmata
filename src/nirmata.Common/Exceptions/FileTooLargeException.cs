using nirmata.Common.Constants;

namespace nirmata.Common.Exceptions;

public sealed class FileTooLargeException : nirmataException
{
    public FileTooLargeException(string message)
        : base(ErrorCode.FileTooLarge, message)
    {
    }
}
