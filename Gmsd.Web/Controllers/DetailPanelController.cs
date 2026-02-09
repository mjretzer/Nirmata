using System.Text.Json;
using Gmsd.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Gmsd.Web.Controllers;

/// <summary>
/// API controller for the detail panel entity views
/// </summary>
[Route("api")]
public class DetailPanelController : Controller
{
    private readonly ILogger<DetailPanelController> _logger;
    private readonly IConfiguration _configuration;

    public DetailPanelController(
        ILogger<DetailPanelController> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Gets entity details by type and ID
    /// </summary>
    [HttpGet("entities/{entityType}/{entityId}")]
    public IActionResult GetEntity(string entityType, string entityId)
    {
        try
        {
            var entity = LoadEntity(entityType.ToLowerInvariant(), entityId);
            if (entity == null)
            {
                return NotFound(new { error = $"{entityType} not found: {entityId}" });
            }

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading entity {EntityType}/{EntityId}", entityType, entityId);
            return StatusCode(500, new { error = "Failed to load entity details" });
        }
    }

    /// <summary>
    /// Gets the detail panel content partial view (for HTMX)
    /// </summary>
    [HttpGet("detailpanel/content")]
    public IActionResult GetContent([FromQuery] string? entityType = null, [FromQuery] string? entityId = null, [FromQuery] string? activeTab = null)
    {
        try
        {
            var model = new DetailPanelViewModel
            {
                ActiveTab = activeTab ?? "properties"
            };

            if (!string.IsNullOrEmpty(entityType) && !string.IsNullOrEmpty(entityId))
            {
                model.Entity = LoadEntity(entityType.ToLowerInvariant(), entityId);
            }

            // Return partial view for HTMX requests
            if (Request.Headers.ContainsKey("HX-Request"))
            {
                return PartialView("_DetailPanel", model);
            }

            // Return JSON for API requests
            return Ok(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading detail panel content");
            return StatusCode(500, new { error = "Failed to load detail panel" });
        }
    }

    /// <summary>
    /// Refreshes entity data
    /// </summary>
    [HttpPost("entities/{entityType}/{entityId}/refresh")]
    public IActionResult RefreshEntity(string entityType, string entityId)
    {
        return GetEntity(entityType, entityId);
    }

    /// <summary>
    /// Loads entity details from the workspace
    /// </summary>
    private EntityDetailModel? LoadEntity(string entityType, string entityId)
    {
        var workspacePath = GetSelectedWorkspacePath();

        return entityType switch
        {
            "project" => LoadProject(entityId, workspacePath),
            "run" => LoadRun(entityId, workspacePath),
            "task" => LoadTask(entityId, workspacePath),
            "spec" => LoadSpec(entityId, workspacePath),
            "issue" => LoadIssue(entityId, workspacePath),
            "milestone" => LoadMilestone(entityId, workspacePath),
            "phase" => LoadPhase(entityId, workspacePath),
            _ => CreateMockEntity(entityType, entityId)
        };
    }

    /// <summary>
    /// Loads project details
    /// </summary>
    private EntityDetailModel? LoadProject(string projectId, string? workspacePath)
    {
        // Try to load from workspace if available
        if (!string.IsNullOrEmpty(workspacePath))
        {
            var projectPath = Path.Combine(workspacePath, ".aos", "projects", projectId);
            if (Directory.Exists(projectPath) || System.IO.File.Exists(projectPath + ".json"))
            {
                return LoadProjectFromDisk(projectId, projectPath, workspacePath);
            }
        }

        // Return mock data for demonstration
        return CreateMockEntity("project", projectId, name: $"Project {projectId}");
    }

    /// <summary>
    /// Loads project data from disk
    /// </summary>
    private EntityDetailModel LoadProjectFromDisk(string projectId, string projectPath, string workspacePath)
    {
        var jsonPath = projectPath.EndsWith(".json") ? projectPath : projectPath + ".json";
        var properties = new Dictionary<string, PropertyValueModel>();
        var status = "active";

        if (System.IO.File.Exists(jsonPath))
        {
            try
            {
                var json = System.IO.File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("name", out var nameEl))
                    properties["Name"] = new PropertyValueModel { Value = nameEl.GetString() ?? projectId };
                if (doc.RootElement.TryGetProperty("description", out var descEl))
                    properties["Description"] = new PropertyValueModel { Value = descEl.GetString() ?? "" };
                if (doc.RootElement.TryGetProperty("status", out var statusEl))
                    status = statusEl.GetString() ?? "active";
                if (doc.RootElement.TryGetProperty("version", out var verEl))
                    properties["Version"] = new PropertyValueModel { Value = verEl.GetString() ?? "1.0.0" };
                if (doc.RootElement.TryGetProperty("priority", out var priEl))
                    properties["Priority"] = new PropertyValueModel { Value = priEl.GetString() ?? "medium" };
                if (doc.RootElement.TryGetProperty("owner", out var ownerEl))
                    properties["Owner"] = new PropertyValueModel { Value = ownerEl.GetString() ?? "Unassigned" };
            }
            catch { }
        }

        // Add system properties
        properties["ID"] = new PropertyValueModel { Value = projectId, IsCopyable = true, Format = "code" };
        properties["Path"] = new PropertyValueModel { Value = projectPath, Format = "code" };
        properties["Created"] = new PropertyValueModel { Value = Directory.Exists(projectPath) ? Directory.GetCreationTimeUtc(projectPath).ToString("g") : "Unknown", Format = "date" };
        properties["Modified"] = new PropertyValueModel { Value = Directory.Exists(projectPath) ? Directory.GetLastWriteTimeUtc(projectPath).ToString("g") : "Unknown", Format = "date" };

        return new EntityDetailModel
        {
            Id = projectId,
            Type = "project",
            Name = properties.TryGetValue("Name", out var n) ? n.Value : projectId,
            Status = status,
            Properties = properties,
            FullPageUrl = $"/Projects/Details?id={Uri.EscapeDataString(projectId)}",
            RawData = System.IO.File.Exists(jsonPath) ? System.IO.File.ReadAllText(jsonPath) : "{}"
        };
    }

    /// <summary>
    /// Loads run details
    /// </summary>
    private EntityDetailModel? LoadRun(string runId, string? workspacePath)
    {
        if (!string.IsNullOrEmpty(workspacePath))
        {
            var runsPath = Path.Combine(workspacePath, ".aos", "runs", runId);
            if (Directory.Exists(runsPath))
            {
                return LoadRunFromDisk(runId, runsPath);
            }
        }

        return CreateMockEntity("run", runId, name: $"Run {runId[..Math.Min(8, runId.Length)]}");
    }

    /// <summary>
    /// Loads run data from disk
    /// </summary>
    private EntityDetailModel LoadRunFromDisk(string runId, string runPath)
    {
        var manifestPath = Path.Combine(runPath, "manifest.json");
        var properties = new Dictionary<string, PropertyValueModel>();
        var evidence = new List<EvidenceItemModel>();
        var status = "unknown";
        var rawData = "{}";

        if (System.IO.File.Exists(manifestPath))
        {
            try
            {
                rawData = System.IO.File.ReadAllText(manifestPath);
                using var doc = JsonDocument.Parse(rawData);

                if (doc.RootElement.TryGetProperty("status", out var statusEl))
                    status = statusEl.GetString() ?? "unknown";
                if (doc.RootElement.TryGetProperty("description", out var descEl))
                    properties["Description"] = new PropertyValueModel { Value = descEl.GetString() ?? "" };
                if (doc.RootElement.TryGetProperty("startedAt", out var startedEl))
                    properties["Started"] = new PropertyValueModel { Value = startedEl.GetString() ?? "", Format = "date" };
                if (doc.RootElement.TryGetProperty("completedAt", out var completedEl))
                    properties["Completed"] = new PropertyValueModel { Value = completedEl.GetString() ?? "", Format = "date" };
                if (doc.RootElement.TryGetProperty("specId", out var specEl))
                    properties["Spec"] = new PropertyValueModel { Value = specEl.GetString() ?? "", LinkUrl = $"/Specs/Details?id={specEl.GetString()}" };
            }
            catch { }
        }

        // Add system properties
        properties["Run ID"] = new PropertyValueModel { Value = runId, IsCopyable = true, Format = "code" };
        properties["Path"] = new PropertyValueModel { Value = runPath, Format = "code" };
        properties["Created"] = new PropertyValueModel { Value = Directory.GetCreationTimeUtc(runPath).ToString("g"), Format = "date" };

        // Look for log files as evidence
        var logsPath = Path.Combine(runPath, "logs");
        if (Directory.Exists(logsPath))
        {
            foreach (var logFile in Directory.GetFiles(logsPath, "*.log").Take(5))
            {
                evidence.Add(new EvidenceItemModel
                {
                    Id = Path.GetFileNameWithoutExtension(logFile),
                    Type = "log",
                    Title = Path.GetFileName(logFile),
                    FileUrl = $"/api/runs/{runId}/logs/{Path.GetFileName(logFile)}",
                    CreatedAt = System.IO.File.GetLastWriteTimeUtc(logFile)
                });
            }
        }

        return new EntityDetailModel
        {
            Id = runId,
            Type = "run",
            Name = properties.TryGetValue("Description", out var d) && !string.IsNullOrEmpty(d.Value) ? d.Value : $"Run {runId[..8]}...",
            Status = status,
            Properties = properties,
            Evidence = evidence,
            FullPageUrl = $"/Runs/Details?id={Uri.EscapeDataString(runId)}",
            RawData = rawData
        };
    }

    /// <summary>
    /// Loads task details
    /// </summary>
    private EntityDetailModel? LoadTask(string taskId, string? workspacePath)
    {
        return CreateMockEntity("task", taskId, name: $"Task {taskId}");
    }

    /// <summary>
    /// Loads spec details
    /// </summary>
    private EntityDetailModel? LoadSpec(string specId, string? workspacePath)
    {
        return CreateMockEntity("spec", specId, name: $"Spec {specId}");
    }

    /// <summary>
    /// Loads issue details
    /// </summary>
    private EntityDetailModel? LoadIssue(string issueId, string? workspacePath)
    {
        return CreateMockEntity("issue", issueId, name: $"Issue {issueId}");
    }

    /// <summary>
    /// Loads milestone details
    /// </summary>
    private EntityDetailModel? LoadMilestone(string milestoneId, string? workspacePath)
    {
        return CreateMockEntity("milestone", milestoneId, name: $"Milestone {milestoneId}");
    }

    /// <summary>
    /// Loads phase details
    /// </summary>
    private EntityDetailModel? LoadPhase(string phaseId, string? workspacePath)
    {
        return CreateMockEntity("phase", phaseId, name: $"Phase {phaseId}");
    }

    /// <summary>
    /// Creates a mock entity for demonstration when real data isn't available
    /// </summary>
    private EntityDetailModel CreateMockEntity(string entityType, string entityId, string? name = null)
    {
        var displayName = name ?? $"{entityType} {entityId}";
        var status = "active";

        var properties = new Dictionary<string, PropertyValueModel>
        {
            ["ID"] = new() { Value = entityId, IsCopyable = true, Format = "code" },
            ["Type"] = new() { Value = entityType },
            ["Status"] = new() { Value = status },
            ["Created"] = new() { Value = DateTime.UtcNow.AddDays(-7).ToString("g"), Format = "date" },
            ["Updated"] = new() { Value = DateTime.UtcNow.AddDays(-1).ToString("g"), Format = "date" }
        };

        // Add type-specific properties
        switch (entityType)
        {
            case "project":
                properties["Version"] = new PropertyValueModel { Value = "1.0.0" };
                properties["Priority"] = new PropertyValueModel { Value = "High" };
                properties["Owner"] = new PropertyValueModel { Value = "Team Alpha" };
                break;
            case "run":
                properties["Duration"] = new PropertyValueModel { Value = "2m 34s", Format = "duration" };
                properties["Spec"] = new PropertyValueModel { Value = "spec-123", LinkUrl = "/Specs/Details?id=spec-123" };
                break;
            case "task":
                properties["Assignee"] = new PropertyValueModel { Value = "Developer A" };
                properties["Due Date"] = new PropertyValueModel { Value = DateTime.UtcNow.AddDays(3).ToString("g"), Format = "date" };
                break;
        }

        var mockEvidence = new List<EvidenceItemModel>();
        if (entityType == "run")
        {
            mockEvidence.Add(new EvidenceItemModel
            {
                Id = "log-1",
                Type = "log",
                Title = "execution.log",
                Description = "Full execution log from the run",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
        }

        return new EntityDetailModel
        {
            Id = entityId,
            Type = entityType,
            Name = displayName,
            Status = status,
            Properties = properties,
            Evidence = mockEvidence,
            FullPageUrl = $"/{(entityType == "project" ? "Projects" : char.ToUpperInvariant(entityType[0]) + entityType[1..])}/Details?id={Uri.EscapeDataString(entityId)}",
            RawData = JsonSerializer.Serialize(new { id = entityId, type = entityType, name = displayName, status, properties = properties.ToDictionary(p => p.Key, p => p.Value.Value) }, new JsonSerializerOptions { WriteIndented = true })
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
