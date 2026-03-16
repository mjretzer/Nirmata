namespace nirmata.Aos.Engine.Stores;

/// <summary>
/// Base contract for interacting with the AOS workspace rooted at <c>.aos/</c>.
/// </summary>
internal interface IAosStore
{
    /// <summary>
    /// Absolute filesystem path to the <c>.aos</c> root directory.
    /// </summary>
    string AosRootPath { get; }
}

