namespace Gmsd.Common.Helpers;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
