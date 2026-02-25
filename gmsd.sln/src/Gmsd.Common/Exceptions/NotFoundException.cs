using Gmsd.Common.Constants;

namespace Gmsd.Common.Exceptions;

public sealed class NotFoundException : GmsdException
{
    public NotFoundException(string message)
        : base(ErrorCode.NotFound, message)
    {
    }
}
