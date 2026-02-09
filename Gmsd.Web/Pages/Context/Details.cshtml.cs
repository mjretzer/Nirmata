using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;

namespace Gmsd.Web.Pages.Context;

/// <summary>
/// PageModel for the Context Pack details page.
/// Displays pack content, entries, and budget information.
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

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    public ContextPackDetailViewModel? Pack { get; set; }
    public List<ContextPackEntryViewModel> Entries { get; set; } = new();
    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public void OnGet()
    {
        LoadWorkspace();
        LoadPack();
    }

    public IActionResult OnPostRebuildPack()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected.";
            return RedirectToPage();
        }

        SuccessMessage = $"Rebuild Pack action triggered for {Id}. This would integrate with the pack builder service.";
        return RedirectToPage(new { id = Id });
    }

    public IActionResult OnPostDiffWithRun(string runId)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected.";
            return RedirectToPage();
        }

        SuccessMessage = $"Diff Pack since RUN {runId} action triggered for {Id}. This would compare the current pack with the state at run {runId}.";
        return RedirectToPage(new { id = Id });
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

    private void LoadPack()
    {
        if (string.IsNullOrEmpty(WorkspacePath) || string.IsNullOrEmpty(Id))
        {
            return;
        }

        var packPath = Path.Combine(WorkspacePath, ".aos", "context", "packs", $"{Id}.json");

        if (!System.IO.File.Exists(packPath))
        {
            ErrorMessage = $"Context pack '{Id}' not found.";
            return;
        }

        try
        {
            var json = System.IO.File.ReadAllText(packPath);
            var packDoc = JsonSerializer.Deserialize<JsonDocument>(json);

            if (packDoc == null)
            {
                ErrorMessage = "Failed to parse context pack.";
                return;
            }

            var root = packDoc.RootElement;

            Pack = new ContextPackDetailViewModel
            {
                PackId = Id,
                Mode = root.TryGetProperty("mode", out var mode) ? mode.GetString() : "unknown",
                DrivingId = root.TryGetProperty("drivingId", out var drivingId) ? drivingId.GetString() : null,
                SchemaVersion = root.TryGetProperty("schemaVersion", out var schemaVer) &&
                                schemaVer.ValueKind == JsonValueKind.Number
                                ? schemaVer.GetInt32() : 1,
                MaxBytes = root.TryGetProperty("budget", out var budget) &&
                           budget.TryGetProperty("maxBytes", out var maxBytes) &&
                           maxBytes.ValueKind == JsonValueKind.Number
                           ? maxBytes.GetInt32() : 0,
                MaxItems = root.TryGetProperty("budget", out var budget2) &&
                           budget2.TryGetProperty("maxItems", out var maxItems) &&
                           maxItems.ValueKind == JsonValueKind.Number
                           ? maxItems.GetInt32() : 0,
                TotalBytes = root.TryGetProperty("summary", out var summary) &&
                             summary.TryGetProperty("totalBytes", out var totalBytes) &&
                             totalBytes.ValueKind == JsonValueKind.Number
                             ? totalBytes.GetInt32() : 0,
                TotalItems = root.TryGetProperty("summary", out var summary2) &&
                             summary2.TryGetProperty("totalItems", out var totalItems) &&
                             totalItems.ValueKind == JsonValueKind.Number
                             ? totalItems.GetInt32() : 0,
                RawJson = json
            };

            if (Pack.MaxBytes > 0)
            {
                Pack.UtilizationPercent = (int)((Pack.TotalBytes / (double)Pack.MaxBytes) * 100);
            }

            if (root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    var entryVm = new ContextPackEntryViewModel
                    {
                        ContractPath = entry.TryGetProperty("contractPath", out var contractPath)
                            ? contractPath.GetString() : null,
                        ContentType = entry.TryGetProperty("contentType", out var contentType)
                            ? contentType.GetString() : null,
                        Sha256 = entry.TryGetProperty("sha256", out var sha256)
                            ? sha256.GetString() : null,
                        Bytes = entry.TryGetProperty("bytes", out var bytes) &&
                                bytes.ValueKind == JsonValueKind.Number
                                ? bytes.GetInt32() : 0
                    };

                    if (entry.TryGetProperty("content", out var content))
                    {
                        entryVm.Content = content.GetRawText();
                        entryVm.ContentPreview = content.ValueKind == JsonValueKind.String
                            ? content.GetString()?.Substring(0, Math.Min(200, content.GetString()?.Length ?? 0))
                            : content.GetRawText().Substring(0, Math.Min(200, content.GetRawText().Length));
                    }

                    Entries.Add(entryVm);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading context pack {PackId}", Id);
            ErrorMessage = $"Error loading context pack: {ex.Message}";
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

public class ContextPackDetailViewModel
{
    public string PackId { get; set; } = string.Empty;
    public string? Mode { get; set; }
    public string? DrivingId { get; set; }
    public int SchemaVersion { get; set; }
    public int MaxBytes { get; set; }
    public int MaxItems { get; set; }
    public int TotalBytes { get; set; }
    public int TotalItems { get; set; }
    public int UtilizationPercent { get; set; }
    public string? RawJson { get; set; }
}

public class ContextPackEntryViewModel
{
    public string? ContractPath { get; set; }
    public string? ContentType { get; set; }
    public string? Sha256 { get; set; }
    public int Bytes { get; set; }
    public string? Content { get; set; }
    public string? ContentPreview { get; set; }
}
