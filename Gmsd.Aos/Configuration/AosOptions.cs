namespace Gmsd.Aos.Configuration;

/// <summary>
/// Configuration options for the GMSD AOS engine.
/// </summary>
public class AosOptions
{
    /// <summary>
    /// The root path of the repository where the AOS workspace is located.
    /// </summary>
    public string RepositoryRootPath { get; set; } = string.Empty;
}
