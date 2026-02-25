using System.Text.Json;
using Gmsd.Aos.Engine.Validation;
using Gmsd.Aos.Public;
using Gmsd.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.Validation;

/// <summary>
/// PageModel for the Validation & Maintenance dashboard.
/// Provides validation tools, repair operations, and cache management.
/// </summary>
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;
    private readonly IValidator? _validator;
    private readonly ICacheManager? _cacheManager;

    public IndexModel(
        ILogger<IndexModel> logger,
        IConfiguration configuration,
        IValidator? validator = null,
        ICacheManager? cacheManager = null)
    {
        _logger = logger;
        _configuration = configuration;
        _validator = validator;
        _cacheManager = cacheManager;
    }

    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    // Validation results
    public List<ValidationResultViewModel> ValidationResults { get; set; } = new();
    public bool HasValidationResults => ValidationResults.Count > 0;
    public int TotalIssues => ValidationResults.Sum(r => r.Issues.Count);
    public int TotalValidations => ValidationResults.Count;

    // Cache statistics
    public CacheStatsViewModel? CacheStats { get; set; }

    // Active validation type
    [BindProperty(SupportsGet = true)]
    public string? ActiveValidation { get; set; }

    public void OnGet()
    {
        LoadWorkspace();
        LoadCacheStats();
    }

    public IActionResult OnPostValidateAll()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        ActiveValidation = "all";
        RunAllValidations();
        _logger.LogInformation("Full workspace validation executed");

        return RedirectToPage(new { ActiveValidation });
    }

    public IActionResult OnPostValidateSchemas()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        ActiveValidation = "schemas";
        RunSchemaValidation();
        _logger.LogInformation("Schema validation executed");

        return RedirectToPage(new { ActiveValidation });
    }

    public IActionResult OnPostValidateSpec()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        ActiveValidation = "spec";
        RunSpecValidation();
        _logger.LogInformation("Spec validation executed");

        return RedirectToPage(new { ActiveValidation });
    }

    public IActionResult OnPostValidateState()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        ActiveValidation = "state";
        RunStateValidation();
        _logger.LogInformation("State validation executed");

        return RedirectToPage(new { ActiveValidation });
    }

    public IActionResult OnPostValidateEvidence()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        ActiveValidation = "evidence";
        RunEvidenceValidation();
        _logger.LogInformation("Evidence validation executed");

        return RedirectToPage(new { ActiveValidation });
    }

    public IActionResult OnPostValidateCodebase()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        ActiveValidation = "codebase";
        RunCodebaseValidation();
        _logger.LogInformation("Codebase validation executed");

        return RedirectToPage(new { ActiveValidation });
    }

    public IActionResult OnPostRepairIndexes()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            this.ToastError("No workspace selected");
            return RedirectToPage();
        }

        try
        {
            int repairedCount = RepairIndexes();
            this.ToastSuccess($"Index repair completed. {repairedCount} item(s) repaired.");
            _logger.LogInformation("Index repair executed. Repaired: {Count}", repairedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Index repair failed");
            this.ToastError($"Index repair failed: {ex.Message}");
        }

        return RedirectToPage();
    }

    public IActionResult OnPostClearCache()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            this.ToastError("No workspace selected");
            return RedirectToPage();
        }

        try
        {
            int removedCount;
            if (_cacheManager != null)
            {
                removedCount = _cacheManager.Clear();
            }
            else
            {
                removedCount = ClearCacheManual();
            }

            this.ToastSuccess($"Cache cleared. {removedCount} item(s) removed.");
            _logger.LogInformation("Cache cleared. Removed: {Count}", removedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache clear failed");
            this.ToastError($"Cache clear failed: {ex.Message}");
        }

        LoadCacheStats();
        return RedirectToPage();
    }

    public IActionResult OnPostPruneCache()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            this.ToastError("No workspace selected");
            return RedirectToPage();
        }

        try
        {
            int removedCount;
            if (_cacheManager != null)
            {
                // Prune entries older than 7 days
                removedCount = _cacheManager.Prune(TimeSpan.FromDays(7));
            }
            else
            {
                removedCount = PruneCacheManual(TimeSpan.FromDays(7));
            }

            this.ToastSuccess($"Cache pruned. {removedCount} stale item(s) removed.");
            _logger.LogInformation("Cache pruned. Removed: {Count}", removedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache prune failed");
            this.ToastError($"Cache prune failed: {ex.Message}");
        }

        LoadCacheStats();
        return RedirectToPage();
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

    private void LoadCacheStats()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        try
        {
            var cachePath = Path.Combine(WorkspacePath, ".aos", "cache");
            if (!Directory.Exists(cachePath))
            {
                CacheStats = new CacheStatsViewModel { Exists = false };
                return;
            }

            var fileCount = Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories).Length;
            var dirCount = Directory.GetDirectories(cachePath, "*", SearchOption.AllDirectories).Length;
            var totalSize = GetDirectorySize(cachePath);

            CacheStats = new CacheStatsViewModel
            {
                Exists = true,
                FileCount = fileCount,
                DirectoryCount = dirCount,
                TotalSizeBytes = totalSize,
                Path = cachePath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load cache stats");
        }
    }

    private void RunAllValidations()
    {
        ValidationResults.Clear();

        RunSpecValidation();
        RunStateValidation();
        RunEvidenceValidation();
        RunCodebaseValidation();
        RunSchemaValidation();
    }

    private void RunSchemaValidation()
    {
        var result = new ValidationResultViewModel
        {
            Name = "Schemas",
            Description = "JSON schema validation for all workspace artifacts"
        };

        try
        {
            // Check if schemas directory exists
            var schemasPath = Path.Combine(WorkspacePath!, ".aos", "schemas");
            if (!Directory.Exists(schemasPath))
            {
                result.Issues.Add(new ValidationIssueViewModel
                {
                    Severity = "error",
                    Message = "Schemas directory does not exist",
                    FilePath = ".aos/schemas/"
                });
                result.Status = "failed";
            }
            else
            {
                var registryPath = Path.Combine(schemasPath, "registry.json");
                if (!System.IO.File.Exists(registryPath))
                {
                    result.Issues.Add(new ValidationIssueViewModel
                    {
                        Severity = "warning",
                        Message = "Schema registry not found",
                        FilePath = ".aos/schemas/registry.json"
                    });
                }

                // Count schema files
                var schemaFiles = Directory.GetFiles(schemasPath, "*.json", SearchOption.AllDirectories);
                result.Details = $"{schemaFiles.Length} schema file(s) found";
                result.Status = result.Issues.Count == 0 ? "passed" : "warning";
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add(new ValidationIssueViewModel
            {
                Severity = "error",
                Message = $"Schema validation error: {ex.Message}",
                FilePath = ".aos/schemas/"
            });
            result.Status = "failed";
        }

        ValidationResults.Add(result);
    }

    private void RunSpecValidation()
    {
        var result = new ValidationResultViewModel
        {
            Name = "Spec",
            Description = "Project specification validation"
        };

        try
        {
            // Use the AosWorkspaceValidator for spec layer
            var report = AosWorkspaceValidator.Validate(
                WorkspacePath!,
                [AosWorkspaceLayer.Spec]
            );

            foreach (var issue in report.Issues)
            {
                result.Issues.Add(new ValidationIssueViewModel
                {
                    Severity = "error",
                    Message = issue.Message,
                    FilePath = issue.ContractPath,
                    Layer = issue.Layer?.ToString()
                });
            }

            // Check required spec files
            var requiredFiles = new[]
            {
                ".aos/spec/project.json",
                ".aos/spec/milestones/index.json",
                ".aos/spec/phases/index.json",
                ".aos/spec/tasks/index.json",
                ".aos/spec/issues/index.json",
                ".aos/spec/uat/index.json"
            };

            int foundCount = 0;
            foreach (var file in requiredFiles)
            {
                var fullPath = Path.Combine(WorkspacePath!, file);
                if (System.IO.File.Exists(fullPath))
                {
                    foundCount++;
                }
                else if (!result.Issues.Any(i => i.FilePath == file))
                {
                    result.Issues.Add(new ValidationIssueViewModel
                    {
                        Severity = "error",
                        Message = "Missing required file",
                        FilePath = file
                    });
                }
            }

            result.Details = $"{foundCount}/{requiredFiles.Length} required files found";
            result.Status = result.Issues.Count == 0 ? "passed" : "failed";
        }
        catch (Exception ex)
        {
            result.Issues.Add(new ValidationIssueViewModel
            {
                Severity = "error",
                Message = $"Spec validation error: {ex.Message}",
                FilePath = ".aos/spec/"
            });
            result.Status = "failed";
        }

        ValidationResults.Add(result);
    }

    private void RunStateValidation()
    {
        var result = new ValidationResultViewModel
        {
            Name = "State",
            Description = "State snapshot and events validation"
        };

        try
        {
            var report = AosWorkspaceValidator.Validate(
                WorkspacePath!,
                [AosWorkspaceLayer.State]
            );

            foreach (var issue in report.Issues)
            {
                result.Issues.Add(new ValidationIssueViewModel
                {
                    Severity = "error",
                    Message = issue.Message,
                    FilePath = issue.ContractPath,
                    Layer = issue.Layer?.ToString()
                });
            }

            // Check state files
            var statePath = Path.Combine(WorkspacePath!, ".aos", "state");
            var stateJsonPath = Path.Combine(statePath, "state.json");
            var eventsPath = Path.Combine(statePath, "events.ndjson");

            bool stateExists = System.IO.File.Exists(stateJsonPath);
            bool eventsExists = System.IO.File.Exists(eventsPath);

            if (!stateExists && !result.Issues.Any(i => i.FilePath.Contains("state.json")))
            {
                result.Issues.Add(new ValidationIssueViewModel
                {
                    Severity = "warning",
                    Message = "State snapshot not found",
                    FilePath = ".aos/state/state.json"
                });
            }

            if (!eventsExists && !result.Issues.Any(i => i.FilePath.Contains("events.ndjson")))
            {
                result.Issues.Add(new ValidationIssueViewModel
                {
                    Severity = "warning",
                    Message = "Events log not found",
                    FilePath = ".aos/state/events.ndjson"
                });
            }

            result.Details = $"state.json: {(stateExists ? "OK" : "Missing")}, events.ndjson: {(eventsExists ? "OK" : "Missing")}";
            result.Status = result.Issues.Count == 0 ? "passed" : (result.Issues.Any(i => i.Severity == "error") ? "failed" : "warning");
        }
        catch (Exception ex)
        {
            result.Issues.Add(new ValidationIssueViewModel
            {
                Severity = "error",
                Message = $"State validation error: {ex.Message}",
                FilePath = ".aos/state/"
            });
            result.Status = "failed";
        }

        ValidationResults.Add(result);
    }

    private void RunEvidenceValidation()
    {
        var result = new ValidationResultViewModel
        {
            Name = "Evidence",
            Description = "Run evidence and task evidence validation"
        };

        try
        {
            var report = AosWorkspaceValidator.Validate(
                WorkspacePath!,
                [AosWorkspaceLayer.Evidence]
            );

            foreach (var issue in report.Issues)
            {
                result.Issues.Add(new ValidationIssueViewModel
                {
                    Severity = "error",
                    Message = issue.Message,
                    FilePath = issue.ContractPath,
                    Layer = issue.Layer?.ToString()
                });
            }

            // Check evidence structure
            var evidencePath = Path.Combine(WorkspacePath!, ".aos", "evidence");
            var commandsLogPath = Path.Combine(evidencePath, "logs", "commands.json");
            var runsIndexPath = Path.Combine(evidencePath, "runs", "index.json");

            bool commandsExists = System.IO.File.Exists(commandsLogPath);
            bool runsIndexExists = System.IO.File.Exists(runsIndexPath);

            int runCount = 0;
            var runsPath = Path.Combine(evidencePath, "runs");
            if (Directory.Exists(runsPath))
            {
                runCount = Directory.GetDirectories(runsPath).Length;
            }

            result.Details = $"Commands log: {(commandsExists ? "OK" : "Missing")}, Runs index: {(runsIndexExists ? "OK" : "Missing")}, {runCount} run(s)";
            result.Status = result.Issues.Count == 0 ? "passed" : (result.Issues.Any(i => i.Severity == "error") ? "failed" : "warning");
        }
        catch (Exception ex)
        {
            result.Issues.Add(new ValidationIssueViewModel
            {
                Severity = "error",
                Message = $"Evidence validation error: {ex.Message}",
                FilePath = ".aos/evidence/"
            });
            result.Status = "failed";
        }

        ValidationResults.Add(result);
    }

    private void RunCodebaseValidation()
    {
        var result = new ValidationResultViewModel
        {
            Name = "Codebase",
            Description = "Codebase intelligence artifacts validation"
        };

        try
        {
            var codebasePath = Path.Combine(WorkspacePath!, ".aos", "codebase");
            if (!Directory.Exists(codebasePath))
            {
                result.Issues.Add(new ValidationIssueViewModel
                {
                    Severity = "warning",
                    Message = "Codebase directory does not exist",
                    FilePath = ".aos/codebase/"
                });
                result.Status = "warning";
            }
            else
            {
                // Check for expected artifacts
                var expectedArtifacts = new[] { "map.json", "stack.json", "structure.json" };
                int foundCount = 0;

                foreach (var artifact in expectedArtifacts)
                {
                    var artifactPath = Path.Combine(codebasePath, artifact);
                    if (System.IO.File.Exists(artifactPath))
                    {
                        foundCount++;
                        // Validate JSON
                        try
                        {
                            var content = System.IO.File.ReadAllText(artifactPath);
                            JsonSerializer.Deserialize<JsonElement>(content);
                        }
                        catch
                        {
                            result.Issues.Add(new ValidationIssueViewModel
                            {
                                Severity = "error",
                                Message = $"Invalid JSON in {artifact}",
                                FilePath = $".aos/codebase/{artifact}"
                            });
                        }
                    }
                }

                result.Details = $"{foundCount}/{expectedArtifacts.Length} expected artifacts found";
                result.Status = result.Issues.Count == 0 ? "passed" : (result.Issues.Any(i => i.Severity == "error") ? "failed" : "warning");
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add(new ValidationIssueViewModel
            {
                Severity = "error",
                Message = $"Codebase validation error: {ex.Message}",
                FilePath = ".aos/codebase/"
            });
            result.Status = "failed";
        }

        ValidationResults.Add(result);
    }

    private int RepairIndexes()
    {
        int repairedCount = 0;

        // Repair spec indexes
        var specPath = Path.Combine(WorkspacePath!, ".aos", "spec");
        if (Directory.Exists(specPath))
        {
            var indexFiles = new[] { "milestones/index.json", "phases/index.json", "tasks/index.json", "issues/index.json", "uat/index.json" };
            foreach (var indexFile in indexFiles)
            {
                var indexPath = Path.Combine(specPath, indexFile);
                var dir = Path.GetDirectoryName(indexPath);
                if (Directory.Exists(dir) && !System.IO.File.Exists(indexPath))
                {
                    // Create empty index
                    var emptyIndex = new { items = new string[] { } };
                    var json = JsonSerializer.Serialize(emptyIndex, new JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(indexPath, json);
                    repairedCount++;
                }
            }
        }

        // Repair evidence indexes
        var evidencePath = Path.Combine(WorkspacePath!, ".aos", "evidence");
        if (Directory.Exists(evidencePath))
        {
            var commandsLogPath = Path.Combine(evidencePath, "logs", "commands.json");
            if (!System.IO.File.Exists(commandsLogPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(commandsLogPath)!);
                var emptyLog = new { commands = new string[] { } };
                var json = JsonSerializer.Serialize(emptyLog, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(commandsLogPath, json);
                repairedCount++;
            }

            var runsIndexPath = Path.Combine(evidencePath, "runs", "index.json");
            if (!System.IO.File.Exists(runsIndexPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(runsIndexPath)!);
                var emptyIndex = new { runs = new string[] { } };
                var json = JsonSerializer.Serialize(emptyIndex, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(runsIndexPath, json);
                repairedCount++;
            }
        }

        return repairedCount;
    }

    private int ClearCacheManual()
    {
        var cachePath = Path.Combine(WorkspacePath!, ".aos", "cache");
        if (!Directory.Exists(cachePath))
        {
            return 0;
        }

        int count = 0;
        foreach (var file in Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories))
        {
            try
            {
                System.IO.File.Delete(file);
                count++;
            }
            catch { }
        }

        foreach (var dir in Directory.GetDirectories(cachePath, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            try
            {
                Directory.Delete(dir);
                count++;
            }
            catch { }
        }

        return count;
    }

    private int PruneCacheManual(TimeSpan ageThreshold)
    {
        var cachePath = Path.Combine(WorkspacePath!, ".aos", "cache");
        if (!Directory.Exists(cachePath))
        {
            return 0;
        }

        var cutoff = DateTime.UtcNow - ageThreshold;
        int count = 0;

        foreach (var file in Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories))
        {
            try
            {
                var lastWrite = System.IO.File.GetLastWriteTimeUtc(file);
                if (lastWrite < cutoff)
                {
                    System.IO.File.Delete(file);
                    count++;
                }
            }
            catch { }
        }

        foreach (var dir in Directory.GetDirectories(cachePath, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            try
            {
                var lastWrite = Directory.GetLastWriteTimeUtc(dir);
                if (lastWrite < cutoff && !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                    count++;
                }
            }
            catch { }
        }

        return count;
    }

    private long GetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    size += info.Length;
                }
                catch { }
            }
        }
        catch { }
        return size;
    }

    private string GetWorkspaceConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "workspace-config.json");
    }
}

public class ValidationResultViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, passed, warning, failed
    public string? Details { get; set; }
    public List<ValidationIssueViewModel> Issues { get; set; } = new();
}

public class ValidationIssueViewModel
{
    public string Severity { get; set; } = "error"; // error, warning
    public string Message { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Layer { get; set; }
}

public class CacheStatsViewModel
{
    public bool Exists { get; set; }
    public int FileCount { get; set; }
    public int DirectoryCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string? Path { get; set; }

    public string FormattedSize
    {
        get
        {
            if (TotalSizeBytes < 1024)
                return $"{TotalSizeBytes} B";
            if (TotalSizeBytes < 1024 * 1024)
                return $"{TotalSizeBytes / 1024:F1} KB";
            if (TotalSizeBytes < 1024 * 1024 * 1024)
                return $"{TotalSizeBytes / (1024 * 1024):F1} MB";
            return $"{TotalSizeBytes / (1024 * 1024 * 1024):F1} GB";
        }
    }
}

internal class WorkspaceConfig
{
    public string? SelectedWorkspacePath { get; set; }
}
