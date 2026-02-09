using Gmsd.Aos.Engine.Stores;

namespace Gmsd.Aos.Public;

/// <summary>
/// Public spec store implementation backed by the internal engine store.
/// </summary>
public sealed class SpecStore : ISpecStore
{
    private readonly AosSpecStore _inner;

    private SpecStore(string aosRootPath)
    {
        if (string.IsNullOrWhiteSpace(aosRootPath))
        {
            throw new ArgumentException("Missing AOS root path.", nameof(aosRootPath));
        }

        _inner = new AosSpecStore(aosRootPath);
    }

    /// <summary>
    /// Creates a spec store for an explicit <c>.aos</c> root path.
    /// </summary>
    public static SpecStore FromAosRoot(string aosRootPath) => new(aosRootPath);

    /// <summary>
    /// Creates a spec store for a workspace's <c>.aos</c> root.
    /// </summary>
    public static SpecStore FromWorkspace(IWorkspace workspace)
    {
        if (workspace is null) throw new ArgumentNullException(nameof(workspace));
        return new SpecStore(workspace.AosRootPath);
    }

    /// <summary>
    /// Gets the internal store for advanced operations.
    /// </summary>
    internal AosSpecStore Inner => _inner;
}
