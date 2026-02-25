using Gmsd.Data.Entities.Workspaces;

namespace Gmsd.Data.Repositories;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Workspace?> GetByPathAsync(string path, CancellationToken cancellationToken = default);
    Task<List<Workspace>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<Workspace>> GetByHealthStatusAsync(string healthStatus, CancellationToken cancellationToken = default);
    Task<List<Workspace>> GetRecentlyValidatedAsync(int days, CancellationToken cancellationToken = default);
    void Add(Workspace workspace);
    void Update(Workspace workspace);
    void Delete(Workspace workspace);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}
