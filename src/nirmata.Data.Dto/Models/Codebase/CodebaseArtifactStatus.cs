namespace nirmata.Data.Dto.Models.Codebase;

/// <summary>
/// Well-known freshness status values for codebase artifacts.
/// Derived from file presence and manifest hash verification on each read — never stored.
/// </summary>
public static class CodebaseArtifactStatus
{
    /// <summary>Artifact file exists and its hash matches the stored manifest entry.</summary>
    public const string Ready = "ready";

    /// <summary>Artifact file exists but its hash does not match the manifest, or no manifest is present.</summary>
    public const string Stale = "stale";

    /// <summary>Recognized artifact file is absent from the workspace codebase directory.</summary>
    public const string Missing = "missing";

    /// <summary>Artifact file exists but is unreadable or cannot be parsed as valid JSON.</summary>
    public const string Error = "error";
}
