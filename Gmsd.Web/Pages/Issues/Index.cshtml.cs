using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;

namespace Gmsd.Web.Pages.Issues;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public List<IssueViewModel> Issues { get; set; } = new();
    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterStatus { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterSeverity { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterTaskId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterPhaseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterMilestoneId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchQuery { get; set; }

    public void OnGet()
    {
        LoadWorkspace();
        LoadIssues();
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

    private void LoadIssues()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var issuesPath = Path.Combine(WorkspacePath, ".aos", "spec", "issues");

        try
        {
            if (Directory.Exists(issuesPath))
            {
                var issueFiles = Directory.GetFiles(issuesPath, "*.json");
                foreach (var issueFile in issueFiles)
                {
                    var issue = ParseIssueFile(issueFile);
                    if (issue != null)
                    {
                        Issues.Add(issue);
                    }
                }
            }

            // Sort by created date descending
            Issues = Issues.OrderByDescending(i => i.CreatedAt).ToList();

            // Apply filters
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading issues");
            ErrorMessage = $"Error loading issues: {ex.Message}";
        }
    }

    private IssueViewModel? ParseIssueFile(string issueFile)
    {
        try
        {
            var json = System.IO.File.ReadAllText(issueFile);
            var issueDoc = JsonSerializer.Deserialize<JsonDocument>(json);

            if (issueDoc == null) return null;

            JsonElement root;
            if (issueDoc.RootElement.TryGetProperty("issue", out var issueProp))
            {
                root = issueProp;
            }
            else
            {
                root = issueDoc.RootElement;
            }

            var issueId = Path.GetFileNameWithoutExtension(issueFile);

            var issue = new IssueViewModel
            {
                Id = issueId,
                Title = root.TryGetProperty("title", out var title) ? title.GetString() ?? issueId : issueId,
                Description = root.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                Type = root.TryGetProperty("type", out var type) && 
                       Enum.TryParse<IssueType>(type.GetString(), out var parsedType) 
                       ? parsedType : IssueType.Bug,
                Severity = root.TryGetProperty("severity", out var severity) && 
                           Enum.TryParse<IssueSeverity>(severity.GetString(), out var parsedSeverity) 
                           ? parsedSeverity : IssueSeverity.Medium,
                Status = root.TryGetProperty("status", out var status) && 
                         Enum.TryParse<IssueStatus>(status.GetString(), out var parsedStatus) 
                         ? parsedStatus : IssueStatus.Open,
                ReproSteps = root.TryGetProperty("reproSteps", out var repro) ? repro.GetString() : null,
                ExpectedBehavior = root.TryGetProperty("expectedBehavior", out var expected) ? expected.GetString() : null,
                ActualBehavior = root.TryGetProperty("actualBehavior", out var actual) ? actual.GetString() : null,
                TaskId = root.TryGetProperty("taskId", out var taskId) ? taskId.GetString() : null,
                TaskName = root.TryGetProperty("taskName", out var taskName) ? taskName.GetString() : null,
                PhaseId = root.TryGetProperty("phaseId", out var phaseId) ? phaseId.GetString() : null,
                PhaseName = root.TryGetProperty("phaseName", out var phaseName) ? phaseName.GetString() : null,
                MilestoneId = root.TryGetProperty("milestoneId", out var milestoneId) ? milestoneId.GetString() : null,
                MilestoneName = root.TryGetProperty("milestoneName", out var milestoneName) ? milestoneName.GetString() : null,
                UatSessionId = root.TryGetProperty("uatSessionId", out var uatSession) ? uatSession.GetString() : null,
                UatCheckId = root.TryGetProperty("uatCheckId", out var uatCheck) ? uatCheck.GetString() : null,
                AssignedTo = root.TryGetProperty("assignedTo", out var assigned) ? assigned.GetString() : null,
                CreatedAt = root.TryGetProperty("createdAt", out var created) && 
                            created.ValueKind == JsonValueKind.String &&
                            DateTime.TryParse(created.GetString(), out var createdDate) 
                            ? createdDate : null,
                UpdatedAt = root.TryGetProperty("updatedAt", out var updated) && 
                            updated.ValueKind == JsonValueKind.String &&
                            DateTime.TryParse(updated.GetString(), out var updatedDate) 
                            ? updatedDate : null,
                ResolvedAt = root.TryGetProperty("resolvedAt", out var resolved) && 
                             resolved.ValueKind == JsonValueKind.String &&
                             DateTime.TryParse(resolved.GetString(), out var resolvedDate) 
                             ? resolvedDate : null,
                ResolutionNotes = root.TryGetProperty("resolutionNotes", out var notes) ? notes.GetString() : null
            };

            return issue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse issue file {IssueFile}", issueFile);
            return null;
        }
    }

    private void ApplyFilters()
    {
        if (!string.IsNullOrEmpty(FilterStatus) && 
            Enum.TryParse<IssueStatus>(FilterStatus, out var statusFilter))
        {
            Issues = Issues.Where(i => i.Status == statusFilter).ToList();
        }

        if (!string.IsNullOrEmpty(FilterSeverity) && 
            Enum.TryParse<IssueSeverity>(FilterSeverity, out var severityFilter))
        {
            Issues = Issues.Where(i => i.Severity == severityFilter).ToList();
        }

        if (!string.IsNullOrEmpty(FilterType) && 
            Enum.TryParse<IssueType>(FilterType, out var typeFilter))
        {
            Issues = Issues.Where(i => i.Type == typeFilter).ToList();
        }

        if (!string.IsNullOrEmpty(FilterTaskId))
        {
            Issues = Issues.Where(i => i.TaskId == FilterTaskId).ToList();
        }

        if (!string.IsNullOrEmpty(FilterPhaseId))
        {
            Issues = Issues.Where(i => i.PhaseId == FilterPhaseId).ToList();
        }

        if (!string.IsNullOrEmpty(FilterMilestoneId))
        {
            Issues = Issues.Where(i => i.MilestoneId == FilterMilestoneId).ToList();
        }

        if (!string.IsNullOrEmpty(SearchQuery))
        {
            var query = SearchQuery.ToLowerInvariant();
            Issues = Issues.Where(i =>
                i.Id.ToLowerInvariant().Contains(query) ||
                i.Title.ToLowerInvariant().Contains(query) ||
                i.Description?.ToLowerInvariant().Contains(query) == true ||
                i.TaskId?.ToLowerInvariant().Contains(query) == true
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
