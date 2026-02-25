using Gmsd.Web.Models;

namespace Gmsd.Web.Services;

public interface IWorkspaceService
{
    Task<List<WorkspaceDto>> ListWorkspacesAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceDto> OpenWorkspaceAsync(string path, CancellationToken cancellationToken = default);
    Task<WorkspaceDto> InitWorkspaceAsync(string path, string? name = null, CancellationToken cancellationToken = default);
    Task<WorkspaceValidationReport> ValidateWorkspaceAsync(string path, CancellationToken cancellationToken = default);
    Task RepairWorkspaceAsync(string path, CancellationToken cancellationToken = default);
    Task<string?> GetActiveWorkspacePathAsync();
}
