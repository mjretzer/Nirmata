using nirmata.Data.Context;
using nirmata.Data.Entities.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace nirmata.Data.Repositories;

public sealed class WorkspaceRepository : IWorkspaceRepository
{
    private readonly nirmataDbContext _dbContext;

    public WorkspaceRepository(nirmataDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<Workspace>()
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<Workspace?> GetByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<Workspace>()
            .FirstOrDefaultAsync(w => w.Path == path, cancellationToken);
    }

    public async Task<List<Workspace>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<Workspace>()
            .OrderByDescending(w => w.LastOpenedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Workspace>> GetByHealthStatusAsync(string healthStatus, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<Workspace>()
            .Where(w => w.HealthStatus == healthStatus)
            .OrderByDescending(w => w.LastValidatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Workspace>> GetRecentlyValidatedAsync(int days, CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-days);
        return await _dbContext.Set<Workspace>()
            .Where(w => w.LastValidatedAt >= cutoffDate)
            .OrderByDescending(w => w.LastValidatedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(Workspace workspace)
    {
        _dbContext.Set<Workspace>().Add(workspace);
    }

    public void Update(Workspace workspace)
    {
        _dbContext.Set<Workspace>().Update(workspace);
    }

    public void Delete(Workspace workspace)
    {
        _dbContext.Set<Workspace>().Remove(workspace);
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
