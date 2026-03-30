using Microsoft.Extensions.Logging;
using nirmata.Common.Exceptions;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Data.Entities.Workspaces;
using nirmata.Data.Repositories;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

public sealed class WorkspaceService : IWorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly ILogger<WorkspaceService> _logger;

    public WorkspaceService(IWorkspaceRepository workspaceRepository, ILogger<WorkspaceService> logger)
    {
        _workspaceRepository = workspaceRepository;
        _logger = logger;
    }

    public async Task<List<WorkspaceSummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var workspaces = await _workspaceRepository.GetAllAsync(cancellationToken);
        return workspaces.Select(ToSummary).ToList();
    }

    public async Task<WorkspaceSummary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(id, cancellationToken);
        return workspace is null ? null : ToSummary(workspace);
    }

    public async Task<WorkspaceSummary> RegisterAsync(string name, string path, CancellationToken cancellationToken = default)
    {
        string normalizedPath;
        try
        {
            normalizedPath = NormalizeAndValidatePath(path);
        }
        catch (ValidationFailedException ex)
        {
            _logger.LogWarning("Workspace registration rejected — invalid path '{Path}': {Reason}", path, ex.Message);
            throw;
        }

        try
        {
            ValidateGitBacked(normalizedPath);
        }
        catch (ValidationFailedException ex)
        {
            _logger.LogWarning("Workspace registration rejected — path is not git-backed '{Path}': {Reason}", normalizedPath, ex.Message);
            throw;
        }

        var existing = await _workspaceRepository.GetByPathAsync(normalizedPath, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation("Workspace registration reused existing entry for path '{Path}'", normalizedPath);
            return ToSummary(existing);
        }

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = name,
            Path = normalizedPath,
            LastOpenedAt = DateTimeOffset.UtcNow,
        };

        _workspaceRepository.Add(workspace);
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        return ToSummary(workspace);
    }

    public async Task<WorkspaceSummary?> UpdatePathAsync(Guid id, string newPath, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(id, cancellationToken);
        if (workspace is null)
            return null;

        string normalizedPath;
        try
        {
            normalizedPath = NormalizeAndValidatePath(newPath);
        }
        catch (ValidationFailedException ex)
        {
            _logger.LogWarning("Workspace path update rejected for {WorkspaceId} — invalid path '{Path}': {Reason}", id, newPath, ex.Message);
            throw;
        }

        try
        {
            ValidateGitBacked(normalizedPath);
        }
        catch (ValidationFailedException ex)
        {
            _logger.LogWarning("Workspace path update rejected for {WorkspaceId} — path is not git-backed '{Path}': {Reason}", id, normalizedPath, ex.Message);
            throw;
        }

        workspace.Path = normalizedPath;
        _workspaceRepository.Update(workspace);
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        return ToSummary(workspace);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(id, cancellationToken);
        if (workspace is null)
            return false;

        _workspaceRepository.Delete(workspace);
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<string?> ResolveRootAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(id, cancellationToken);
        if (workspace is null)
        {
            _logger.LogWarning("Workspace resolution failed: workspace {WorkspaceId} is not registered", id);
            return null;
        }

        return workspace.Path;
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Validates and normalizes a workspace path.
    /// Throws <see cref="ValidationFailedException"/> if the path is missing or not absolute.
    /// Returns the normalized absolute path.
    /// </summary>
    private static string NormalizeAndValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ValidationFailedException("Workspace path is required.");

        if (!Path.IsPathRooted(path))
            throw new ValidationFailedException("Workspace path must be an absolute path.");

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            throw new ValidationFailedException($"Workspace path is not valid: {ex.Message}");
        }
    }

    private static WorkspaceSummary ToSummary(Workspace workspace) => new()
    {
        Id = workspace.Id,
        Name = workspace.Name,
        Path = workspace.Path,
        Status = DeriveStatus(workspace.Path),
        LastModified = workspace.LastOpenedAt ?? DateTimeOffset.MinValue,
    };

    /// <summary>
    /// Derives workspace status from live filesystem inspection.
    /// Never reads from <c>Workspace.HealthStatus</c> — that column is not used for status.
    /// </summary>
    private static string DeriveStatus(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return WorkspaceStatus.Missing;

            var gitPath = Path.Combine(path, ".git");
            var aosPath = Path.Combine(path, ".aos");
            return Directory.Exists(gitPath) && Directory.Exists(aosPath)
                ? WorkspaceStatus.Initialized
                : WorkspaceStatus.NotInitialized;
        }
        catch (UnauthorizedAccessException)
        {
            return WorkspaceStatus.Inaccessible;
        }
        catch (IOException)
        {
            return WorkspaceStatus.Inaccessible;
        }
    }

    /// <summary>
    /// Throws <see cref="ValidationFailedException"/> if <paramref name="normalizedPath"/>
    /// exists on disk but does not contain a <c>.git/</c> directory.
    /// Paths that do not exist yet are allowed — they will appear as <see cref="WorkspaceStatus.Missing"/>
    /// until the folder is bootstrapped.
    /// </summary>
    private static void ValidateGitBacked(string normalizedPath)
    {
        if (!Directory.Exists(normalizedPath))
            return;

        var gitPath = Path.Combine(normalizedPath, ".git");
        if (!Directory.Exists(gitPath))
            throw new ValidationFailedException(
                $"The path '{normalizedPath}' is not a git repository. " +
                "Run bootstrap to initialize git and AOS workspace scaffolding before registering a workspace.");
    }
}
