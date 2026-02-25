using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.Context;

/// <summary>
/// PageModel for the Context Packs list page.
/// Displays context packs by task/phase with budget information.
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

    public List<ContextPackViewModel> ContextPacks { get; set; } = new();
    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterDrivingId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchQuery { get; set; }

    public void OnGet()
    {
        LoadWorkspace();
        LoadContextPacks();
    }

    public IActionResult OnPostBuildPack(string drivingId)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected.";
            return RedirectToPage();
        }

        SuccessMessage = $"Build Pack action triggered for {drivingId}. This would integrate with the pack builder service.";
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

    private void LoadContextPacks()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var packsPath = Path.Combine(WorkspacePath, ".aos", "context", "packs");

        try
        {
            if (Directory.Exists(packsPath))
            {
                var packFiles = Directory.GetFiles(packsPath, "PCK-*.json");
                foreach (var packFile in packFiles)
                {
                    var pack = ParseContextPackFile(packFile);
                    if (pack != null)
                    {
                        ContextPacks.Add(pack);
                    }
                }
            }

            ContextPacks = ContextPacks.OrderByDescending(p => p.PackId).ToList();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading context packs");
            ErrorMessage = $"Error loading context packs: {ex.Message}";
        }
    }

    private ContextPackViewModel? ParseContextPackFile(string packFile)
    {
        try
        {
            var json = System.IO.File.ReadAllText(packFile);
            var packDoc = JsonSerializer.Deserialize<JsonDocument>(json);

            if (packDoc == null) return null;

            var root = packDoc.RootElement;

            var packId = Path.GetFileNameWithoutExtension(packFile);

            var pack = new ContextPackViewModel
            {
                PackId = packId,
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
                EntryCount = root.TryGetProperty("entries", out var entries) &&
                             entries.ValueKind == JsonValueKind.Array
                             ? entries.GetArrayLength() : 0
            };

            if (pack.MaxBytes > 0)
            {
                pack.UtilizationPercent = (int)((pack.TotalBytes / (double)pack.MaxBytes) * 100);
            }

            return pack;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse context pack file {PackFile}", packFile);
            return null;
        }
    }

    private void ApplyFilters()
    {
        if (!string.IsNullOrEmpty(FilterDrivingId))
        {
            ContextPacks = ContextPacks.Where(p =>
                p.DrivingId?.Equals(FilterDrivingId, StringComparison.OrdinalIgnoreCase) == true
            ).ToList();
        }

        if (!string.IsNullOrEmpty(SearchQuery))
        {
            var query = SearchQuery.ToLowerInvariant();
            ContextPacks = ContextPacks.Where(p =>
                p.PackId.ToLowerInvariant().Contains(query) ||
                p.DrivingId?.ToLowerInvariant().Contains(query) == true ||
                p.Mode?.ToLowerInvariant().Contains(query) == true
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

public class ContextPackViewModel
{
    public string PackId { get; set; } = string.Empty;
    public string? Mode { get; set; }
    public string? DrivingId { get; set; }
    public int SchemaVersion { get; set; }
    public int MaxBytes { get; set; }
    public int MaxItems { get; set; }
    public int TotalBytes { get; set; }
    public int TotalItems { get; set; }
    public int EntryCount { get; set; }
    public int UtilizationPercent { get; set; }
}
