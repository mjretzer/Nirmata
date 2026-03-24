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

    public async Task<(IReadOnlyList<Project> Items, int TotalCount)> GetAllAsync(
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Projects
            .AsNoTracking()
            .Include(p => p.Steps)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(p => p.Name.Contains(searchTerm));

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(Project project)
    {
        _dbContext.Projects.Add(project);
    }

    public void Update(Project project)
    {
        _dbContext.Projects.Update(project);
    }

    public async Task<bool> DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects
            .FindAsync(new object[] { projectId }, cancellationToken);

        if (project is null)
            return false;

        _dbContext.Projects.Remove(project);
        return true;
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }
        catch
        {
            return false;
        }
    }
}
