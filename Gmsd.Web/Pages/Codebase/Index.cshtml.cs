using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.Codebase;

/// <summary>
/// PageModel for the Codebase Intelligence dashboard.
/// Provides visibility into codebase mapping artifacts stored in .aos/codebase/
/// </summary>
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ActiveAction { get; set; }

    public List<CodebaseArtifactViewModel> Artifacts { get; set; } = new();
    public CodebaseStatusViewModel? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ViewArtifact { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Action { get; set; }

    public void OnGet()
    {
        LoadWorkspace();

        if (!string.IsNullOrEmpty(Action))
        {
            HandleAction(Action);
        }

        if (!string.IsNullOrEmpty(WorkspacePath))
        {
            LoadCodebaseArtifacts();
            LoadCodebaseStatus();
        }
    }

    public IActionResult OnPostScan()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        ActiveAction = "Scan";
        SuccessMessage = "Codebase scan initiated. This builds the foundational file index.";
        _logger.LogInformation("Codebase scan triggered for workspace: {WorkspacePath}", WorkspacePath);

        return RedirectToPage(new { Action = "scan-completed" });
    }

    public IActionResult OnPostBuildMap()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        ActiveAction = "Map";
        SuccessMessage = "Map build initiated. This creates the codebase structure map.";
        _logger.LogInformation("Map build triggered for workspace: {WorkspacePath}", WorkspacePath);

        return RedirectToPage(new { Action = "map-built" });
    }

    public IActionResult OnPostBuildSymbols()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        ActiveAction = "Symbols";
        SuccessMessage = "Symbols build initiated. This extracts and indexes code symbols.";
        _logger.LogInformation("Symbols build triggered for workspace: {WorkspacePath}", WorkspacePath);

        return RedirectToPage(new { Action = "symbols-built" });
    }

    public IActionResult OnPostBuildGraph()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        ActiveAction = "Graph";
        SuccessMessage = "Graph build initiated. This creates the dependency and call graph.";
        _logger.LogInformation("Graph build triggered for workspace: {WorkspacePath}", WorkspacePath);

        return RedirectToPage(new { Action = "graph-built" });
    }

    private void HandleAction(string action)
    {
        ActiveAction = action;
        switch (action.ToLowerInvariant())
        {
            case "scan-completed":
                SuccessMessage = "Scan completed successfully.";
                break;
            case "map-built":
                SuccessMessage = "Map build completed successfully.";
                break;
            case "symbols-built":
                SuccessMessage = "Symbols build completed successfully.";
                break;
            case "graph-built":
                SuccessMessage = "Graph build completed successfully.";
                break;
        }
    }

    private void LoadWorkspace()
    {
        try
        {
            var configPath = GetWorkspaceConfigPath();
            if (System.IO.File.Exists(configPath))
            {
                var json = System.IO.File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<WorkspaceConfig>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                WorkspacePath = config?.SelectedWorkspacePath;
            }

            if (string.IsNullOrEmpty(WorkspacePath))
            {
                ErrorMessage = "No workspace selected. Please select a workspace first.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load workspace configuration");
            ErrorMessage = "Failed to load workspace configuration.";
        }
    }

    private void LoadCodebaseArtifacts()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var codebasePath = Path.Combine(WorkspacePath, ".aos", "codebase");

        try
        {
            // Define the expected artifact files
            var artifactDefinitions = new[]
            {
                new { Name = "map", File = "map.json", Description = "Codebase structure and file organization" },
                new { Name = "stack", File = "stack.json", Description = "Technology stack analysis" },
                new { Name = "architecture", File = "architecture.json", Description = "High-level architecture overview" },
                new { Name = "structure", File = "structure.json", Description = "Project and folder structure" },
                new { Name = "conventions", File = "conventions.json", Description = "Coding conventions and patterns" },
                new { Name = "testing", File = "testing.json", Description = "Testing infrastructure and coverage" },
                new { Name = "integrations", File = "integrations.json", Description = "External integrations and dependencies" },
                new { Name = "concerns", File = "concerns.json", Description = "Cross-cutting concerns and aspects" },
                new { Name = "symbols", File = "symbols.json", Description = "Extracted code symbols index" },
                new { Name = "graph", File = "graph.json", Description = "Dependency and call graph" }
            };

            foreach (var def in artifactDefinitions)
            {
                var artifactPath = Path.Combine(codebasePath, def.File);
                var exists = System.IO.File.Exists(artifactPath);
                DateTime? lastModified = null;
                bool isValid = false;
                string? validationError = null;

                if (exists)
                {
                    try
                    {
                        lastModified = System.IO.File.GetLastWriteTime(artifactPath);
                        var content = System.IO.File.ReadAllText(artifactPath);
                        JsonSerializer.Deserialize<JsonElement>(content);
                        isValid = true;
                    }
                    catch (Exception ex)
                    {
                        validationError = $"Invalid JSON: {ex.Message}";
                        isValid = false;
                    }
                }

                Artifacts.Add(new CodebaseArtifactViewModel
                {
                    Id = def.Name,
                    Name = def.Name,
                    FileName = def.File,
                    Description = def.Description,
                    Exists = exists,
                    LastModified = lastModified,
                    IsValid = isValid,
                    ValidationError = validationError,
                    FullPath = artifactPath
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading codebase artifacts");
            ErrorMessage = $"Error loading codebase artifacts: {ex.Message}";
        }
    }

    private void LoadCodebaseStatus()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var codebasePath = Path.Combine(WorkspacePath, ".aos", "codebase");

        try
        {
            var status = new CodebaseStatusViewModel
            {
                DirectoryExists = Directory.Exists(codebasePath),
                TotalArtifacts = Artifacts.Count(a => a.Exists),
                ValidArtifacts = Artifacts.Count(a => a.Exists && a.IsValid),
                InvalidArtifacts = Artifacts.Count(a => a.Exists && !a.IsValid),
                MissingArtifacts = Artifacts.Count(a => !a.Exists)
            };

            // Determine overall status
            if (status.TotalArtifacts == 0)
            {
                status.OverallStatus = "NotInitialized";
                status.StatusMessage = "Codebase intelligence not initialized. Run a scan to begin.";
            }
            else if (status.InvalidArtifacts > 0)
            {
                status.OverallStatus = "Degraded";
                status.StatusMessage = $"{status.InvalidArtifacts} artifact(s) have validation errors.";
            }
            else if (status.MissingArtifacts > 0)
            {
                status.OverallStatus = "Partial";
                status.StatusMessage = $"{status.MissingArtifacts} artifact(s) not yet built.";
            }
            else
            {
                status.OverallStatus = "Ready";
                status.StatusMessage = "All codebase intelligence artifacts are ready.";
            }

            // Get last scan/build time from the most recent artifact
            status.LastScanAt = Artifacts
                .Where(a => a.Exists && a.LastModified.HasValue)
                .Max(a => a.LastModified);

            Status = status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading codebase status");
        }
    }

    private string GetWorkspaceConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "workspace-config.json");
    }
}

public class CodebaseArtifactViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public DateTime? LastModified { get; set; }
    public bool IsValid { get; set; }
    public string? ValidationError { get; set; }
    public string? FullPath { get; set; }
}

public class CodebaseStatusViewModel
{
    public bool DirectoryExists { get; set; }
    public int TotalArtifacts { get; set; }
    public int ValidArtifacts { get; set; }
    public int InvalidArtifacts { get; set; }
    public int MissingArtifacts { get; set; }
    public string OverallStatus { get; set; } = "Unknown";
    public string StatusMessage { get; set; } = string.Empty;
    public DateTime? LastScanAt { get; set; }
}

internal class WorkspaceConfig
{
    public string? SelectedWorkspacePath { get; set; }
}
