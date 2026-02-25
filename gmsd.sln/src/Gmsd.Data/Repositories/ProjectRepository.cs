using Gmsd.Data.Context;
using Gmsd.Data.Entities.Projects;
using Microsoft.EntityFrameworkCore;

namespace Gmsd.Data.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly GmsdDbContext _dbContext;

    public ProjectRepository(GmsdDbContext dbContext)
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
