using nirmata.Common.Constants;

namespace nirmata.Common.Exceptions;

public sealed class ForbiddenException : nirmataException
{
    public ForbiddenException(string message)
        : base(ErrorCode.Forbidden, message)
    {
    }
}
