namespace nirmata.Aos.Public;

/// <summary>
/// Stable compile-against entry-point for the AOS engine.
/// Consumers SHOULD reference only types rooted under <c>nirmata.Aos.Public.*</c> and <c>nirmata.Aos.Contracts.*</c>.
/// </summary>
public static class AosPublicApi
{
    /// <summary>
    /// Anchor type for the stable contracts namespace.
    /// </summary>
    public static Type ContractsAnchorType => typeof(Contracts.AosContracts);
}
