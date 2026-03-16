namespace nirmata.Aos.Public.Configuration;

/// <summary>
/// Configuration options for the nirmata AOS engine.
/// </summary>
public class AosOptions
{
    /// <summary>
    /// The root path of the repository where the AOS workspace is located.
    /// </summary>
    public string RepositoryRootPath { get; set; } = string.Empty;
}
