using nirmata.Common.Constants;

namespace nirmata.Common.Exceptions;

public sealed class NotFoundException : nirmataException
{
    public NotFoundException(string message)
        : base(ErrorCode.NotFound, message)
    {
    }
}
