using System.Text.Json;
using Gmsd.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Gmsd.Web.Controllers;

/// <summary>
/// API controller for sidebar context data
/// </summary>
[Route("api/sidebar")]
[ApiController]
public class SidebarController : ControllerBase
{
    private readonly ILogger<SidebarController> _logger;
    private readonly IConfiguration _configuration;

    public SidebarController(
        ILogger<SidebarController> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Gets the current sidebar context data including workspace status and recent runs
    /// </summary>
    [HttpGet("context")]
    public IActionResult GetContext()
    {
        try
        {
            var workspacePath = GetSelectedWorkspacePath();
            var model = new ContextSidebarViewModel
            {
                Workspace = GetWorkspaceStatus(workspacePath),
                RecentRuns = GetRecentRuns(workspacePath),
                QuickActions = GetQuickActions()
            };

            return Ok(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching sidebar context");
            return StatusCode(500, new { error = "Failed to load sidebar context" });
        }
    }

    /// <summary>
    /// Refreshes the sidebar context data (for HTMX polling)
    /// </summary>
    [HttpGet("refresh")]
    public IActionResult RefreshContext()
    {
        return GetContext();
    }

    /// <summary>
    /// Gets workspace status from session or file system
    /// </summary>
    private WorkspaceStatusModel? GetWorkspaceStatus(string? workspacePath)
    {
        if (string.IsNullOrEmpty(workspacePath))
        {
            return null;
        }

        try
        {
            var aosPath = Path.Combine(workspacePath, ".aos");
            var isInitialized = Directory.Exists(aosPath);

            // Count projects
            var projectsPath = Path.Combine(aosPath, "projects");
            var projectCount = Directory.Exists(projectsPath)
                ? Directory.GetDirectories(projectsPath).Length
                : 0;

            // Count open issues
            var issuesPath = Path.Combine(aosPath, "spec", "issues");
            var issueCount = Directory.Exists(issuesPath)
                ? Directory.GetFiles(issuesPath, "*.json").Length
                : 0;

            // Get last activity
            var lastActivity = isInitialized ? Directory.GetLastWriteTimeUtc(aosPath) : (DateTime?)null;

            // Try to read state for cursor
            string? cursor = null;
            var statePath = Path.Combine(aosPath, "state.json");
            if (System.IO.File.Exists(statePath))
            {
                try
                {
                    var stateJson = System.IO.File.ReadAllText(statePath);
                    using var doc = System.Text.Json.JsonDocument.Parse(stateJson);
                    if (doc.RootElement.TryGetProperty("cursor", out var cursorElement))
                    {
                        cursor = cursorElement.GetString();
                    }
                }
                catch { }
            }

            return new WorkspaceStatusModel
            {
                Id = Path.GetFileName(workspacePath),
                Name = Path.GetFileName(workspacePath),
                Path = workspacePath,
                Status = isInitialized ? "active" : "uninitialized",
                ProjectCount = projectCount,
                OpenIssueCount = issueCount,
                LastActivityAt = lastActivity,
                Cursor = cursor
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read workspace status for {Path}", workspacePath);
            return new WorkspaceStatusModel
            {
                Id = Path.GetFileName(workspacePath),
                Name = Path.GetFileName(workspacePath),
                Path = workspacePath,
                Status = "error"
            };
        }
    }

    /// <summary>
    /// Gets recent runs from the workspace
    /// </summary>
    private List<RecentRunModel> GetRecentRuns(string? workspacePath)
    {
        var runs = new List<RecentRunModel>();

        if (string.IsNullOrEmpty(workspacePath))
        {
            return runs;
        }

        try
        {
            var runsPath = Path.Combine(workspacePath, ".aos", "runs");
            if (!Directory.Exists(runsPath))
            {
                return runs;
            }

            var runDirs = Directory.GetDirectories(runsPath)
                .Select(d => new DirectoryInfo(d))
                .OrderByDescending(d => d.LastWriteTimeUtc)
                .Take(5);

            foreach (var runDir in runDirs)
            {
                var runId = runDir.Name;
                var status = "unknown";
                var description = $"Run {runId[..8]}...";
                DateTime startedAt = runDir.CreationTimeUtc;
                TimeSpan? duration = null;

                // Try to read run manifest
                var manifestPath = Path.Combine(runDir.FullName, "manifest.json");
                if (System.IO.File.Exists(manifestPath))
                {
                    try
                    {
                        var manifestJson = System.IO.File.ReadAllText(manifestPath);
                        using var doc = System.Text.Json.JsonDocument.Parse(manifestJson);

                        if (doc.RootElement.TryGetProperty("status", out var statusElement))
                        {
                            status = statusElement.GetString() ?? "unknown";
                        }

                        if (doc.RootElement.TryGetProperty("description", out var descElement))
                        {
                            description = descElement.GetString() ?? description;
                        }

                        if (doc.RootElement.TryGetProperty("startedAt", out var startedElement) &&
                            startedElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                            DateTime.TryParse(startedElement.GetString(), out var parsedStarted))
                        {
                            startedAt = parsedStarted.ToUniversalTime();
                        }

                        if (doc.RootElement.TryGetProperty("completedAt", out var completedElement) &&
                            completedElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                            DateTime.TryParse(completedElement.GetString(), out var parsedCompleted))
                        {
                            duration = parsedCompleted.ToUniversalTime() - startedAt;
                        }
                    }
                    catch { }
                }

                runs.Add(new RecentRunModel
                {
                    RunId = runId,
                    Status = status,
                    Description = description,
                    StartedAt = startedAt,
                    Duration = duration
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read recent runs for {Path}", workspacePath);
        }

        return runs;
    }

    /// <summary>
    /// Gets predefined quick actions
    /// </summary>
    private List<QuickActionModel> GetQuickActions()
    {
        return new List<QuickActionModel>
        {
            new() { Command = "/status", Label = "Status", Icon = "📊", Category = "status" },
            new() { Command = "/list projects", Label = "Projects", Icon = "📁", Category = "navigation" },
            new() { Command = "/list runs", Label = "Runs", Icon = "▶", Category = "navigation" },
            new() { Command = "/help", Label = "Help", Icon = "❓", Category = "help" },
            new() { Command = "/validate", Label = "Validate", Icon = "✓", Category = "action" },
            new() { Command = "/init", Label = "Initialize", Icon = "🚀", Category = "action" }
        };
    }

    private string? GetSelectedWorkspacePath()
    {
        try
        {
            var configPath = GetWorkspaceConfigPath();
            if (System.IO.File.Exists(configPath))
            {
                var json = System.IO.File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("selectedWorkspacePath", out var pathProp))
                {
                    return pathProp.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load selected workspace configuration");
        }
        return null;
    }

    private string GetWorkspaceConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "workspace-config.json");
    }
}
