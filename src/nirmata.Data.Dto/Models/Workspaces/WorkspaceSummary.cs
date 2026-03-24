namespace nirmata.Data.Dto.Models.Workspaces;

/// <summary>
/// Lightweight workspace summary returned from list and detail endpoints.
/// <c>Status</c> is derived from live filesystem inspection — see <see cref="WorkspaceStatus"/>.
/// </summary>
public sealed class WorkspaceSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }

    /// <summary>
    /// Derived workspace status. One of the values in <see cref="WorkspaceStatus"/>.
    /// Never persisted; computed on each read from the registered root path.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>Maps to <c>Workspace.LastOpenedAt</c>; <see cref="DateTimeOffset.MinValue"/> when null.</summary>
    public DateTimeOffset LastModified { get; init; }
}
