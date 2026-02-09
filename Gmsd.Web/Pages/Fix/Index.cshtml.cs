using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;

namespace Gmsd.Web.Pages.Fix;

/// <summary>
/// PageModel for the Fix Planning list page.
/// Displays repair loops with filtering and search capabilities.
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

    public List<RepairLoopViewModel> RepairLoops { get; set; } = new();
    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterStatus { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchQuery { get; set; }

    public void OnGet()
    {
        LoadWorkspace();
        LoadRepairLoops();
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

    private void LoadRepairLoops()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var fixPath = Path.Combine(WorkspacePath, ".aos", "fix");

        try
        {
            if (Directory.Exists(fixPath))
            {
                var loopFiles = Directory.GetFiles(fixPath, "*.json");
                foreach (var loopFile in loopFiles)
                {
                    var loop = ParseRepairLoopFile(loopFile);
                    if (loop != null)
                    {
                        RepairLoops.Add(loop);
                    }
                }
            }

            RepairLoops = RepairLoops.OrderByDescending(l => l.CreatedAt).ToList();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading repair loops");
            ErrorMessage = $"Error loading repair loops: {ex.Message}";
        }
    }

    private RepairLoopViewModel? ParseRepairLoopFile(string loopFile)
    {
        try
        {
            var json = System.IO.File.ReadAllText(loopFile);
            var loopDoc = JsonSerializer.Deserialize<JsonDocument>(json);

            if (loopDoc == null) return null;

            var root = loopDoc.RootElement;

            var loopId = Path.GetFileNameWithoutExtension(loopFile);

            var loop = new RepairLoopViewModel
            {
                Id = loopId,
                IssueId = root.TryGetProperty("issueId", out var issueId) ? issueId.GetString() : null,
                IssueTitle = root.TryGetProperty("issueTitle", out var issueTitle) ? issueTitle.GetString() : loopId,
                Status = root.TryGetProperty("status", out var status) ? status.GetString() : "Active",
                CurrentIteration = root.TryGetProperty("currentIteration", out var currentIter) &&
                                   currentIter.ValueKind == JsonValueKind.Number
                                   ? currentIter.GetInt32() : 0,
                MaxIterations = root.TryGetProperty("maxIterations", out var maxIter) &&
                                maxIter.ValueKind == JsonValueKind.Number
                                ? maxIter.GetInt32() : 5,
                CreatedAt = root.TryGetProperty("createdAt", out var created) &&
                            created.ValueKind == JsonValueKind.String &&
                            DateTime.TryParse(created.GetString(), out var createdDate)
                            ? createdDate : null,
                UpdatedAt = root.TryGetProperty("updatedAt", out var updated) &&
                            updated.ValueKind == JsonValueKind.String &&
                            DateTime.TryParse(updated.GetString(), out var updatedDate)
                            ? updatedDate : null,
                RunId = root.TryGetProperty("runId", out var runId) ? runId.GetString() : null
            };

            return loop;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse repair loop file {LoopFile}", loopFile);
            return null;
        }
    }

    private void ApplyFilters()
    {
        if (!string.IsNullOrEmpty(FilterStatus))
        {
            RepairLoops = RepairLoops.Where(l =>
                l.Status?.Equals(FilterStatus, StringComparison.OrdinalIgnoreCase) == true
            ).ToList();
        }

        if (!string.IsNullOrEmpty(SearchQuery))
        {
            var query = SearchQuery.ToLowerInvariant();
            RepairLoops = RepairLoops.Where(l =>
                l.Id.ToLowerInvariant().Contains(query) ||
                l.IssueId?.ToLowerInvariant().Contains(query) == true ||
                l.IssueTitle?.ToLowerInvariant().Contains(query) == true ||
                l.RunId?.ToLowerInvariant().Contains(query) == true
            ).ToList();
        }
    }

    private string GetWorkspaceConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "workspace-config.json");
    }

    private class WorkspaceConfig
    {
        public string? SelectedWorkspacePath { get; set; }
    }
}

public class RepairLoopViewModel
{
    public string Id { get; set; } = string.Empty;
    public string? IssueId { get; set; }
    public string? IssueTitle { get; set; }
    public string? Status { get; set; }
    public int CurrentIteration { get; set; }
    public int MaxIterations { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? RunId { get; set; }
}
