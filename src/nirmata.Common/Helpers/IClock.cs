namespace nirmata.Common.Helpers;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
