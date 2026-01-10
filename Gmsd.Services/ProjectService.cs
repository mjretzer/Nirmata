using Gmsd.Data;
using Gmsd.Data.Model.Projects;
using Gmsd.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Gmsd.Services;

public class ProjectService : IProjectService
{
    private readonly GmsdDbContext _dbContext;

    public ProjectService(GmsdDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        return await _dbContext.Projects.ToListAsync();
    }

    public async Task<List<Project>> SearchProjectsAsync(string query)
    {
        return await _dbContext.Projects
            .Where(p => EF.Functions.Like(p.Name, $"%{query}%"))
            .ToListAsync();
    }
}
