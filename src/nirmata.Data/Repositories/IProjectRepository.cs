using nirmata.Data.Entities.Projects;

namespace nirmata.Data.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(string projectId, CancellationToken cancellationToken = default);
    void Add(Project project);
}
