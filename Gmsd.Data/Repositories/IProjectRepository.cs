using Gmsd.Data.Entities.Projects;

namespace Gmsd.Data.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(string projectId, CancellationToken cancellationToken = default);
    void Add(Project project);
}
