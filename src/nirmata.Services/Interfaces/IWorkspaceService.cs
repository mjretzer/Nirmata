using nirmata.Data.Dto.Models.Workspaces;

namespace nirmata.Services.Interfaces;

public interface IWorkspaceService
{
    /// <summary>Returns all registered workspaces with live-derived status.</summary>
    Task<List<WorkspaceSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the workspace with the given ID, or <c>null</c> if not found.</summary>
    Task<WorkspaceSummary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Registers a new workspace root path and returns its summary.</summary>
    Task<WorkspaceSummary> RegisterAsync(string name, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the registered root path for a workspace.
    /// Returns the updated summary, or <c>null</c> if the workspace does not exist.
    /// </summary>
    Task<WorkspaceSummary?> UpdatePathAsync(Guid id, string newPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deregisters a workspace.
    /// Returns <c>true</c> if the workspace existed and was removed; <c>false</c> if not found.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the registered root path for a workspace.
    /// Returns the path string, or <c>null</c> if the workspace does not exist.
    /// Used by downstream services (<c>ISpecService</c>, <c>IFileSystemService</c>) to obtain the workspace root.
    /// </summary>
    Task<string?> ResolveRootAsync(Guid id, CancellationToken cancellationToken = default);
}
