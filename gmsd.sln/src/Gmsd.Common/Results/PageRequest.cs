namespace Gmsd.Common.Results;

public sealed class PageRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;

    public int Offset => (PageNumber - 1) * PageSize;
}
