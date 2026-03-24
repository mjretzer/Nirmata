using nirmata.Data.Entities.Projects;

namespace nirmata.Data.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(string projectId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Project> Items, int TotalCount)> GetAllAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    void Add(Project project);
    void Update(Project project);
    Task<bool> DeleteAsync(string projectId, CancellationToken cancellationToken = default);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}
