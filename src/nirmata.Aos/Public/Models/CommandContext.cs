using nirmata.Aos.Public;

namespace nirmata.Aos.Public.Models;

/// <summary>
/// Context provided to command handlers during execution.
/// </summary>
public sealed class CommandContext
{
    /// <summary>
    /// The workspace for path resolution.
    /// </summary>
    public required IWorkspace Workspace { get; init; }

    /// <summary>
    /// The evidence store (optional, when evidence is enabled).
    /// </summary>
    public IEvidenceStore? EvidenceStore { get; init; }

    /// <summary>
    /// Cancellation token for the command execution.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Command arguments from the request.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Command options from the request.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Options { get; init; } = new Dictionary<string, string?>();

    /// <summary>
    /// Creates a new context for the given workspace.
    /// </summary>
    public static CommandContext Create(IWorkspace workspace, CancellationToken ct = default) =>
        new() { Workspace = workspace, CancellationToken = ct };

    /// <summary>
    /// Creates a new context with evidence store.
    /// </summary>
    public static CommandContext Create(IWorkspace workspace, IEvidenceStore? evidenceStore, CancellationToken ct = default) =>
        new() { Workspace = workspace, EvidenceStore = evidenceStore, CancellationToken = ct };
}
