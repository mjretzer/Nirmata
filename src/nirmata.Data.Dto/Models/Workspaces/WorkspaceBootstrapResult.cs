namespace nirmata.Data.Dto.Models.Workspaces;

/// <summary>Categorizes why a workspace bootstrap operation failed.</summary>
public enum BootstrapFailureKind
{
    /// <summary>No failure; bootstrap succeeded.</summary>
    None = 0,

    /// <summary>The provided path was null, empty, relative, or structurally invalid.</summary>
    InvalidPath,

    /// <summary>The workspace root directory does not exist on disk.</summary>
    DirectoryNotFound,

    /// <summary>The <c>git</c> executable was not found on PATH.</summary>
    GitNotFound,

    /// <summary><c>git init</c> was found but exited with a non-zero exit code.</summary>
    GitCommandFailed,

    /// <summary>A filesystem error prevented the AOS scaffold from being created or seeded.</summary>
    FileSystemError,
}

/// <summary>
/// Result returned from a workspace bootstrap operation.
/// Indicates whether git initialization and AOS scaffolding succeeded.
/// </summary>
public sealed class WorkspaceBootstrapResult
{
    /// <summary><c>true</c> when bootstrap completed without errors.</summary>
    public required bool Success { get; init; }

    /// <summary>
    /// <c>true</c> when a new git repository was created; <c>false</c> when one already existed.
    /// Only meaningful when <see cref="Success"/> is <c>true</c>.
    /// </summary>
    public bool GitRepositoryCreated { get; init; }

    /// <summary>
    /// <c>true</c> when the AOS scaffold was created or partially seeded;
    /// <c>false</c> when the scaffold already existed in full.
    /// Only meaningful when <see cref="Success"/> is <c>true</c>.
    /// </summary>
    public bool AosScaffoldCreated { get; init; }

    /// <summary>
    /// <c>true</c> when an <c>origin</c> remote was created or updated during bootstrap.
    /// <c>false</c> when no remote URL was provided or the remote already matched the requested URL.
    /// Only meaningful when <see cref="Success"/> is <c>true</c>.
    /// </summary>
    public bool OriginConfigured { get; init; }

    /// <summary>Human-readable failure reason. <c>null</c> when <see cref="Success"/> is <c>true</c>.</summary>
    public string? Error { get; init; }

    /// <summary>
    /// Categorizes why bootstrap failed.
    /// <see cref="BootstrapFailureKind.None"/> when <see cref="Success"/> is <c>true</c>.
    /// </summary>
    public BootstrapFailureKind FailureKind { get; init; }
}
