using System.Text.Json;
using Gmsd.Aos.Engine.Workspace;
using Gmsd.Data.Entities.Workspaces;
using Gmsd.Data.Repositories;
using Gmsd.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Gmsd.Web.Services;

public sealed class WorkspaceService : IWorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<WorkspaceService>? _logger;
    private const string SelectedWorkspacePathConfigKey = "selectedWorkspacePath";

    public WorkspaceService(
        IWorkspaceRepository workspaceRepository,
        IHttpContextAccessor httpContextAccessor,
        ILogger<WorkspaceService>? logger = null)
    {
        _workspaceRepository = workspaceRepository;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<List<WorkspaceDto>> ListWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        var workspaces = await _workspaceRepository.GetAllAsync(cancellationToken);
        return workspaces.Select(MapToDto).ToList();
    }

    public async Task<WorkspaceDto> OpenWorkspaceAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(path);
        var workspace = await _workspaceRepository.GetByPathAsync(normalizedPath, cancellationToken);

        if (workspace == null)
        {
            workspace = new Workspace
            {
                Id = Guid.NewGuid(),
                Path = normalizedPath,
                Name = Path.GetFileName(normalizedPath) ?? normalizedPath
            };
            _workspaceRepository.Add(workspace);
        }

        workspace.LastOpenedAt = DateTimeOffset.UtcNow;
        _workspaceRepository.Update(workspace);

        SaveSelectedWorkspacePath(normalizedPath);

        return MapToDto(workspace);
    }

    public async Task<WorkspaceDto> InitWorkspaceAsync(string path, string? name = null, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(path);
        
        _logger?.LogInformation("Initializing workspace at {Path}", normalizedPath);

        // AOS Engine Bootstrap
        var result = AosWorkspaceBootstrapper.EnsureInitialized(normalizedPath);

        var workspace = await _workspaceRepository.GetByPathAsync(normalizedPath, cancellationToken);
        if (workspace == null)
        {
            workspace = new Workspace
            {
                Id = Guid.NewGuid(),
                Path = normalizedPath,
                Name = name ?? Path.GetFileName(normalizedPath) ?? normalizedPath
            };
            _workspaceRepository.Add(workspace);
        }

        workspace.LastOpenedAt = DateTimeOffset.UtcNow;
        workspace.LastValidatedAt = DateTimeOffset.UtcNow;
        workspace.HealthStatus = "Healthy";
        _workspaceRepository.Update(workspace);
        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        // Migrate config from global to workspace-specific if needed
        await MigrateWorkspaceConfigAsync(normalizedPath, cancellationToken);

        SaveSelectedWorkspacePath(normalizedPath);

        _logger?.LogInformation("Workspace initialized successfully at {Path}", normalizedPath);

        return MapToDto(workspace);
    }

    public async Task<WorkspaceValidationReport> ValidateWorkspaceAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(path);
        var report = AosWorkspaceBootstrapper.CheckCompliance(normalizedPath);

        var workspace = await _workspaceRepository.GetByPathAsync(normalizedPath, cancellationToken);
        if (workspace != null)
        {
            workspace.HealthStatus = report.IsCompliant ? "Healthy" : "Unhealthy";
            workspace.LastValidatedAt = DateTimeOffset.UtcNow;
            _workspaceRepository.Update(workspace);
            await _workspaceRepository.SaveChangesAsync(cancellationToken);
        }

        _logger?.LogInformation("Workspace validation completed for {Path}: {IsCompliant}", normalizedPath, report.IsCompliant);

        return new WorkspaceValidationReport(
            normalizedPath,
            report.IsCompliant,
            report.MissingDirectories.Concat(report.InvalidDirectories).Concat(report.MissingFiles).Concat(report.InvalidFiles).ToList(),
            report.ExtraTopLevelEntries.ToList(),
            DateTimeOffset.UtcNow
        );
    }

    public async Task RepairWorkspaceAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(path);
        
        _logger?.LogInformation("Starting workspace repair for {Path}", normalizedPath);

        var repairResult = AosWorkspaceBootstrapper.Repair(normalizedPath);
        
        if (repairResult.Outcome != AosWorkspaceRepairOutcome.Success)
        {
            _logger?.LogError("Workspace repair failed for {Path}: {Outcome}", normalizedPath, repairResult.Outcome);
            throw new InvalidOperationException($"Workspace repair failed: {repairResult.Outcome}");
        }

        if (repairResult.SchemaValidationIssues.Count > 0)
        {
            _logger?.LogWarning("Workspace repair completed with {IssueCount} schema validation issues for {Path}", 
                repairResult.SchemaValidationIssues.Count, normalizedPath);
            foreach (var issue in repairResult.SchemaValidationIssues)
            {
                _logger?.LogWarning("Schema validation issue: {Issue}", issue);
            }
        }

        var workspace = await _workspaceRepository.GetByPathAsync(normalizedPath, cancellationToken);
        if (workspace != null)
        {
            workspace.HealthStatus = "Healthy";
            workspace.LastValidatedAt = DateTimeOffset.UtcNow;
            _workspaceRepository.Update(workspace);
            await _workspaceRepository.SaveChangesAsync(cancellationToken);
        }

        if (repairResult.Duration.HasValue)
        {
            _logger?.LogInformation("Workspace repair completed for {Path} in {Duration}ms", 
                normalizedPath, repairResult.Duration.Value.TotalMilliseconds);
        }
        else
        {
            _logger?.LogInformation("Workspace repair completed for {Path}", normalizedPath);
        }
    }

    public Task<string?> GetActiveWorkspacePathAsync()
    {
        return Task.FromResult(TryGetSelectedWorkspacePath());
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty", nameof(path));

        return Path.GetFullPath(path);
    }

    private WorkspaceDto MapToDto(Workspace workspace) =>
        new WorkspaceDto(workspace.Id, workspace.Path, workspace.Name, workspace.LastOpenedAt, workspace.HealthStatus);

    private void SaveSelectedWorkspacePath(string path)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var configDir = Path.Combine(appData, "Gmsd");
            Directory.CreateDirectory(configDir);
            var configPath = Path.Combine(configDir, "workspace-config.json");

            var config = new { selectedWorkspacePath = path };
            File.WriteAllText(configPath, JsonSerializer.Serialize(config));
        }
        catch
        {
            // Best effort
        }
    }

    private string? TryGetSelectedWorkspacePath()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var configPath = Path.Combine(appData, "Gmsd", "workspace-config.json");
            if (!File.Exists(configPath)) return null;

            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(SelectedWorkspacePathConfigKey, out var prop))
            {
                return prop.GetString();
            }
        }
        catch
        {
            // Best effort
        }
        return null;
    }

    private async Task MigrateWorkspaceConfigAsync(string repositoryRootPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var globalConfigPath = Path.Combine(appData, "Gmsd", "workspace-config.json");

            if (!File.Exists(globalConfigPath))
            {
                return;
            }

            var json = File.ReadAllText(globalConfigPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var agentPrefs = new Dictionary<string, object>();
            var engineOverrides = new Dictionary<string, object>();
            var excludedPaths = new List<string>();

            if (root.TryGetProperty("agentPreferences", out var agentPrefsElement))
            {
                foreach (var prop in agentPrefsElement.EnumerateObject())
                {
                    agentPrefs[prop.Name] = prop.Value.GetRawText();
                }
            }

            if (root.TryGetProperty("engineOverrides", out var engineOverridesElement))
            {
                foreach (var prop in engineOverridesElement.EnumerateObject())
                {
                    engineOverrides[prop.Name] = prop.Value.GetRawText();
                }
            }

            if (root.TryGetProperty("excludedPaths", out var excludedPathsElement))
            {
                foreach (var item in excludedPathsElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var path = item.GetString();
                        if (path != null)
                        {
                            excludedPaths.Add(path);
                        }
                    }
                }
            }

            var workspaceConfig = new AosWorkspaceConfigDocument(
                SchemaVersion: 1,
                AgentPreferences: agentPrefs,
                EngineOverrides: engineOverrides,
                ExcludedPaths: excludedPaths
            );

            var success = AosWorkspaceBootstrapper.WriteWorkspaceConfig(repositoryRootPath, workspaceConfig);
            if (success)
            {
                _logger?.LogInformation("Migrated workspace config from global to {Path}", repositoryRootPath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to migrate workspace config for {Path}", repositoryRootPath);
        }
    }
}
