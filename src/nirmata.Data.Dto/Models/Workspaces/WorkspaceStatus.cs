namespace nirmata.Data.Dto.Models.Workspaces;

/// <summary>
/// Well-known status values for <see cref="WorkspaceSummary.Status"/>.
/// Derived from filesystem inspection on each read — never stored.
/// </summary>
public static class WorkspaceStatus
{
    /// <summary>Workspace root exists and contains both a <c>.git/</c> and a <c>.aos/</c> directory.</summary>
    public const string Initialized = "initialized";

    /// <summary>Workspace root exists but is missing <c>.git/</c> or <c>.aos/</c> (not yet bootstrapped).</summary>
    public const string NotInitialized = "not-initialized";

    /// <summary>Workspace root path does not exist on disk.</summary>
    public const string Missing = "missing";

    /// <summary>Workspace root path exists but cannot be accessed (permission denied or I/O error).</summary>
    public const string Inaccessible = "inaccessible";
}
