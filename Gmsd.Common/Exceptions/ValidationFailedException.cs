using Gmsd.Common.Constants;

namespace Gmsd.Common.Exceptions;

public sealed class ValidationFailedException : GmsdException
{
    public ValidationFailedException(string message)
        : base(ErrorCode.ValidationFailed, message)
    {
    }
}
