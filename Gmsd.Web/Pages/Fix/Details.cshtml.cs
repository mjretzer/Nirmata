using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;

namespace Gmsd.Web.Pages.Fix;

/// <summary>
/// PageModel for the Fix Planning details page.
/// Shows repair loop details, fix plan tasks, and verification results.
/// Supports planning, executing, and re-verifying fixes.
/// </summary>
public class DetailsModel : PageModel
{
    private readonly ILogger<DetailsModel> _logger;
    private readonly IConfiguration _configuration;

    public DetailsModel(ILogger<DetailsModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public RepairLoopViewModel? RepairLoop { get; set; }
    public List<FixPlanTaskViewModel> FixPlanTasks { get; set; } = new();
    public List<VerificationResultViewModel> VerificationResults { get; set; } = new();
    public string? LoopStateJson { get; set; }
    public string? WorkspacePath { get; set; }
    public string? NotFoundMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public bool CanPlanFix => RepairLoop?.Status?.Equals("Active", StringComparison.OrdinalIgnoreCase) == true ||
                              RepairLoop?.Status?.Equals("Planning", StringComparison.OrdinalIgnoreCase) == true;

    public bool CanExecuteFix => RepairLoop?.Status?.Equals("Planning", StringComparison.OrdinalIgnoreCase) == true ||
                                 RepairLoop?.Status?.Equals("Executing", StringComparison.OrdinalIgnoreCase) == true;

    public bool CanReverify => RepairLoop?.Status?.Equals("Executing", StringComparison.OrdinalIgnoreCase) == true ||
                               RepairLoop?.Status?.Equals("Verifying", StringComparison.OrdinalIgnoreCase) == true;

    public bool CanAbort => RepairLoop?.Status?.Equals("Active", StringComparison.OrdinalIgnoreCase) == true ||
                            RepairLoop?.Status?.Equals("Planning", StringComparison.OrdinalIgnoreCase) == true ||
                            RepairLoop?.Status?.Equals("Executing", StringComparison.OrdinalIgnoreCase) == true ||
                            RepairLoop?.Status?.Equals("Verifying", StringComparison.OrdinalIgnoreCase) == true;

    public void OnGet(string loopId)
    {
        if (string.IsNullOrEmpty(loopId))
        {
            NotFoundMessage = "No repair loop ID specified.";
            return;
        }

        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        LoadRepairLoop(loopId);
        if (RepairLoop != null)
        {
            LoadFixPlanTasks(loopId);
            LoadVerificationResults(loopId);
            LoadLoopState(loopId);
        }
    }

    public IActionResult OnPostPlanFix(string loopId)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected.";
            return Page();
        }

        LoadRepairLoop(loopId);
        if (RepairLoop == null)
        {
            NotFoundMessage = $"Repair loop '{loopId}' was not found.";
            return Page();
        }

        try
        {
            var fixPath = Path.Combine(WorkspacePath, ".aos", "fix");
            var loopFile = Path.Combine(fixPath, $"{loopId}.json");

            if (System.IO.File.Exists(loopFile))
            {
                var json = System.IO.File.ReadAllText(loopFile);
                var loopDoc = JsonSerializer.Deserialize<JsonDocument>(json);

                if (loopDoc != null)
                {
                    var root = loopDoc.RootElement;
                    var updatedLoop = new Dictionary<string, object>();

                    foreach (var property in root.EnumerateObject())
                    {
                        updatedLoop[property.Name] = DeserializeElement(property.Value);
                    }

                    updatedLoop["status"] = "Planning";
                    updatedLoop["updatedAt"] = DateTime.UtcNow.ToString("O");

                    var updatedJson = JsonSerializer.Serialize(updatedLoop, new JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(loopFile, updatedJson);

                    RepairLoop.Status = "Planning";
                    RepairLoop.UpdatedAt = DateTime.UtcNow;

                    SuccessMessage = "Fix planning initiated. The system will analyze the issue and create a fix plan.";
                    _logger.LogInformation("Plan fix initiated for loop {LoopId}", loopId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error planning fix for loop {LoopId}", loopId);
            ErrorMessage = $"Error planning fix: {ex.Message}";
        }

        return Page();
    }

    public IActionResult OnPostExecuteFix(string loopId)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected.";
            return Page();
        }

        LoadRepairLoop(loopId);
        if (RepairLoop == null)
        {
            NotFoundMessage = $"Repair loop '{loopId}' was not found.";
            return Page();
        }

        try
        {
            var fixPath = Path.Combine(WorkspacePath, ".aos", "fix");
            var loopFile = Path.Combine(fixPath, $"{loopId}.json");

            if (System.IO.File.Exists(loopFile))
            {
                var json = System.IO.File.ReadAllText(loopFile);
                var loopDoc = JsonSerializer.Deserialize<JsonDocument>(json);

                if (loopDoc != null)
                {
                    var root = loopDoc.RootElement;
                    var updatedLoop = new Dictionary<string, object>();

                    foreach (var property in root.EnumerateObject())
                    {
                        updatedLoop[property.Name] = DeserializeElement(property.Value);
                    }

                    updatedLoop["status"] = "Executing";
                    updatedLoop["updatedAt"] = DateTime.UtcNow.ToString("O");

                    var updatedJson = JsonSerializer.Serialize(updatedLoop, new JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(loopFile, updatedJson);

                    RepairLoop.Status = "Executing";
                    RepairLoop.UpdatedAt = DateTime.UtcNow;

                    SuccessMessage = "Fix execution started. The system is applying the planned fixes.";
                    _logger.LogInformation("Fix execution started for loop {LoopId}", loopId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing fix for loop {LoopId}", loopId);
            ErrorMessage = $"Error executing fix: {ex.Message}";
        }

        return Page();
    }

    public IActionResult OnPostReverify(string loopId)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected.";
            return Page();
        }

        LoadRepairLoop(loopId);
        if (RepairLoop == null)
        {
            NotFoundMessage = $"Repair loop '{loopId}' was not found.";
            return Page();
        }

        try
        {
            var fixPath = Path.Combine(WorkspacePath, ".aos", "fix");
            var loopFile = Path.Combine(fixPath, $"{loopId}.json");

            if (System.IO.File.Exists(loopFile))
            {
                var json = System.IO.File.ReadAllText(loopFile);
                var loopDoc = JsonSerializer.Deserialize<JsonDocument>(json);

                if (loopDoc != null)
                {
                    var root = loopDoc.RootElement;
                    var updatedLoop = new Dictionary<string, object>();

                    foreach (var property in root.EnumerateObject())
                    {
                        updatedLoop[property.Name] = DeserializeElement(property.Value);
                    }

                    updatedLoop["status"] = "Verifying";
                    updatedLoop["updatedAt"] = DateTime.UtcNow.ToString("O");

                    var currentIter = RepairLoop.CurrentIteration + 1;
                    updatedLoop["currentIteration"] = currentIter;

                    var updatedJson = JsonSerializer.Serialize(updatedLoop, new JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(loopFile, updatedJson);

                    RepairLoop.Status = "Verifying";
                    RepairLoop.CurrentIteration = currentIter;
                    RepairLoop.UpdatedAt = DateTime.UtcNow;

                    SuccessMessage = $"Re-verification started (iteration {currentIter}). The system will validate the applied fixes.";
                    _logger.LogInformation("Re-verification started for loop {LoopId}, iteration {Iteration}", loopId, currentIter);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-verifying fix for loop {LoopId}", loopId);
            ErrorMessage = $"Error re-verifying fix: {ex.Message}";
        }

        return Page();
    }

    public IActionResult OnPostAbort(string loopId)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected.";
            return Page();
        }

        LoadRepairLoop(loopId);
        if (RepairLoop == null)
        {
            NotFoundMessage = $"Repair loop '{loopId}' was not found.";
            return Page();
        }

        try
        {
            var fixPath = Path.Combine(WorkspacePath, ".aos", "fix");
            var loopFile = Path.Combine(fixPath, $"{loopId}.json");

            if (System.IO.File.Exists(loopFile))
            {
                var json = System.IO.File.ReadAllText(loopFile);
                var loopDoc = JsonSerializer.Deserialize<JsonDocument>(json);

                if (loopDoc != null)
                {
                    var root = loopDoc.RootElement;
                    var updatedLoop = new Dictionary<string, object>();

                    foreach (var property in root.EnumerateObject())
                    {
                        updatedLoop[property.Name] = DeserializeElement(property.Value);
                    }

                    updatedLoop["status"] = "Failed";
                    updatedLoop["updatedAt"] = DateTime.UtcNow.ToString("O");
                    updatedLoop["abortReason"] = "Manually aborted by user";

                    var updatedJson = JsonSerializer.Serialize(updatedLoop, new JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(loopFile, updatedJson);

                    RepairLoop.Status = "Failed";
                    RepairLoop.UpdatedAt = DateTime.UtcNow;

                    SuccessMessage = "Repair loop aborted.";
                    _logger.LogInformation("Repair loop {LoopId} aborted by user", loopId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aborting repair loop {LoopId}", loopId);
            ErrorMessage = $"Error aborting repair loop: {ex.Message}";
        }

        return Page();
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

    private void LoadRepairLoop(string loopId)
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var fixPath = Path.Combine(WorkspacePath, ".aos", "fix");
        var loopFile = Path.Combine(fixPath, $"{loopId}.json");

        try
        {
            if (System.IO.File.Exists(loopFile))
            {
                var json = System.IO.File.ReadAllText(loopFile);
                var loopDoc = JsonSerializer.Deserialize<JsonDocument>(json);

                if (loopDoc == null)
                {
                    NotFoundMessage = $"Repair loop '{loopId}' could not be parsed.";
                    return;
                }

                var root = loopDoc.RootElement;

                RepairLoop = new RepairLoopViewModel
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

                LoopStateJson = json;
            }
            else
            {
                NotFoundMessage = $"Repair loop '{loopId}' was not found.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading repair loop {LoopId}", loopId);
            NotFoundMessage = $"Error loading repair loop: {ex.Message}";
        }
    }

    private void LoadFixPlanTasks(string loopId)
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var tasksPath = Path.Combine(WorkspacePath, ".aos", "fix", $"{loopId}-tasks.json");

        try
        {
            if (System.IO.File.Exists(tasksPath))
            {
                var json = System.IO.File.ReadAllText(tasksPath);
                var tasksDoc = JsonSerializer.Deserialize<JsonDocument>(json);

                if (tasksDoc != null)
                {
                    var root = tasksDoc.RootElement;

                    if (root.TryGetProperty("tasks", out var tasksArray) &&
                        tasksArray.ValueKind == JsonValueKind.Array)
                    {
                        int stepNumber = 1;
                        foreach (var taskElement in tasksArray.EnumerateArray())
                        {
                            var task = new FixPlanTaskViewModel
                            {
                                StepNumber = taskElement.TryGetProperty("stepNumber", out var step) &&
                                             step.ValueKind == JsonValueKind.Number
                                             ? step.GetInt32() : stepNumber++,
                                Title = taskElement.TryGetProperty("title", out var title)
                                        ? title.GetString() ?? "Unnamed Task" : "Unnamed Task",
                                Description = taskElement.TryGetProperty("description", out var desc)
                                              ? desc.GetString() : null,
                                Status = taskElement.TryGetProperty("status", out var status)
                                         ? status.GetString() : "Pending"
                            };

                            FixPlanTasks.Add(task);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading fix plan tasks for loop {LoopId}", loopId);
        }
    }

    private void LoadVerificationResults(string loopId)
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var resultsPath = Path.Combine(WorkspacePath, ".aos", "fix", $"{loopId}-verifications.json");

        try
        {
            if (System.IO.File.Exists(resultsPath))
            {
                var json = System.IO.File.ReadAllText(resultsPath);
                var resultsDoc = JsonSerializer.Deserialize<JsonDocument>(json);

                if (resultsDoc != null)
                {
                    var root = resultsDoc.RootElement;

                    if (root.TryGetProperty("results", out var resultsArray) &&
                        resultsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var resultElement in resultsArray.EnumerateArray())
                        {
                            var result = new VerificationResultViewModel
                            {
                                Iteration = resultElement.TryGetProperty("iteration", out var iter) &&
                                            iter.ValueKind == JsonValueKind.Number
                                            ? iter.GetInt32() : 0,
                                Status = resultElement.TryGetProperty("status", out var status)
                                         ? status.GetString() : "Unknown",
                                Timestamp = resultElement.TryGetProperty("timestamp", out var timestamp) &&
                                            timestamp.ValueKind == JsonValueKind.String &&
                                            DateTime.TryParse(timestamp.GetString(), out var ts)
                                            ? ts : null,
                                Notes = resultElement.TryGetProperty("notes", out var notes)
                                        ? notes.GetString() : null
                            };

                            VerificationResults.Add(result);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading verification results for loop {LoopId}", loopId);
        }
    }

    private void LoadLoopState(string loopId)
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var loopFile = Path.Combine(WorkspacePath, ".aos", "fix", $"{loopId}.json");

        try
        {
            if (System.IO.File.Exists(loopFile))
            {
                LoopStateJson = System.IO.File.ReadAllText(loopFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading loop state for loop {LoopId}", loopId);
        }
    }

    private object DeserializeElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object>();
                foreach (var property in element.EnumerateObject())
                {
                    dict[property.Name] = DeserializeElement(property.Value);
                }
                return dict;

            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(DeserializeElement(item));
                }
                return list;

            case JsonValueKind.String:
                return element.GetString() ?? string.Empty;

            case JsonValueKind.Number:
                if (element.TryGetInt32(out var intValue))
                    return intValue;
                if (element.TryGetInt64(out var longValue))
                    return longValue;
                if (element.TryGetDouble(out var doubleValue))
                    return doubleValue;
                return element.GetRawText();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
                return null!;

            default:
                return element.GetRawText();
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

public class FixPlanTaskViewModel
{
    public int StepNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Status { get; set; }
}

public class VerificationResultViewModel
{
    public int Iteration { get; set; }
    public string? Status { get; set; }
    public DateTime? Timestamp { get; set; }
    public string? Notes { get; set; }
}
