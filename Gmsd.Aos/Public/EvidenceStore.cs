using Gmsd.Aos.Engine.Stores;

namespace Gmsd.Aos.Public;

/// <summary>
/// Public evidence store implementation backed by the internal engine store.
/// </summary>
public sealed class EvidenceStore : IEvidenceStore
{
    private readonly AosEvidenceStore _inner;

    private EvidenceStore(string aosRootPath)
    {
        if (string.IsNullOrWhiteSpace(aosRootPath))
        {
            throw new ArgumentException("Missing AOS root path.", nameof(aosRootPath));
        }

        _inner = new AosEvidenceStore(aosRootPath);
    }

    /// <summary>
    /// Creates an evidence store for an explicit <c>.aos</c> root path.
    /// </summary>
    public static EvidenceStore FromAosRoot(string aosRootPath) => new(aosRootPath);

    /// <summary>
    /// Creates an evidence store for a workspace's <c>.aos</c> root.
    /// </summary>
    public static EvidenceStore FromWorkspace(IWorkspace workspace)
    {
        if (workspace is null) throw new ArgumentNullException(nameof(workspace));
        return new EvidenceStore(workspace.AosRootPath);
    }

    /// <summary>
    /// Gets the internal store for advanced operations.
    /// </summary>
    internal AosEvidenceStore Inner => _inner;
}
