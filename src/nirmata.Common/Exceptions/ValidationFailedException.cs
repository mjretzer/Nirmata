using nirmata.Common.Constants;

namespace nirmata.Common.Exceptions;

public sealed class ValidationFailedException : nirmataException
{
    public ValidationFailedException(string message)
        : base(ErrorCode.ValidationFailed, message)
    {
    }
}
