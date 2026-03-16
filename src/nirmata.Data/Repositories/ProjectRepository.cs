using nirmata.Data.Context;
using nirmata.Data.Entities.Projects;
using Microsoft.EntityFrameworkCore;

namespace nirmata.Data.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly nirmataDbContext _dbContext;

    public ProjectRepository(nirmataDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Project?> GetByIdAsync(string projectId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects
            .AsNoTracking()
            .Include(project => project.Steps)
            .FirstOrDefaultAsync(project => project.ProjectId == projectId, cancellationToken);
    }

    public void Add(Project project)
    {
        _dbContext.Projects.Add(project);
    }
}
