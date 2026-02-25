using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;
using Gmsd.Web.AgentRunner;

namespace Gmsd.Web.Pages.Phases;

public class DetailsModel : PageModel
{
    private readonly ILogger<DetailsModel> _logger;
    private readonly IConfiguration _configuration;
    private readonly WorkflowClassifier? _agentRunner;

    public DetailsModel(ILogger<DetailsModel> logger, IConfiguration configuration, WorkflowClassifier? agentRunner = null)
    {
        _logger = logger;
        _configuration = configuration;
        _agentRunner = agentRunner;
    }

    public PhaseViewModel Phase { get; set; } = new();
    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string ActiveTab { get; set; } = "overview";

    [BindProperty]
    public string? NewAssumption { get; set; }

    [BindProperty]
    public string? ResearchTopic { get; set; }

    [BindProperty]
    public string? ResearchFindings { get; set; }

    [BindProperty]
    public string? NoteContent { get; set; }

    public void OnGet(string id, string? tab = null)
    {
        ActiveTab = tab ?? "overview";
        LoadWorkspace();
        LoadPhase(id);
    }

    public IActionResult OnPostAddAssumption(string id)
    {
        LoadWorkspace();

        if (string.IsNullOrWhiteSpace(NewAssumption))
        {
            ErrorMessage = "Assumption text is required.";
            LoadPhase(id);
            return Page();
        }

        try
        {
            var result = PersistAssumption(id, NewAssumption.Trim());
            if (result)
            {
                SuccessMessage = "Assumption added successfully.";
            }
            else
            {
                ErrorMessage = "Failed to add assumption.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding assumption");
            ErrorMessage = $"Error adding assumption: {ex.Message}";
        }

        NewAssumption = null;
        LoadPhase(id);
        return Page();
    }

    public IActionResult OnPostSetResearch(string id)
    {
        LoadWorkspace();

        if (string.IsNullOrWhiteSpace(ResearchTopic))
        {
            ErrorMessage = "Research topic is required.";
            LoadPhase(id);
            return Page();
        }

        try
        {
            var result = PersistResearch(id, ResearchTopic.Trim(), ResearchFindings?.Trim());
            if (result)
            {
                SuccessMessage = "Research item added successfully.";
            }
            else
            {
                ErrorMessage = "Failed to add research item.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding research");
            ErrorMessage = $"Error adding research: {ex.Message}";
        }

        ResearchTopic = null;
        ResearchFindings = null;
        LoadPhase(id);
        return Page();
    }

    public IActionResult OnPostAddNote(string id)
    {
        LoadWorkspace();

        if (string.IsNullOrWhiteSpace(NoteContent))
        {
            ErrorMessage = "Note content is required.";
            LoadPhase(id);
            ActiveTab = "notes";
            return Page();
        }

        try
        {
            var result = PersistNote(id, NoteContent.Trim());
            if (result)
            {
                SuccessMessage = "Note added successfully.";
            }
            else
            {
                ErrorMessage = "Failed to add note.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding note");
            ErrorMessage = $"Error adding note: {ex.Message}";
        }

        NoteContent = null;
        ActiveTab = "notes";
        LoadPhase(id);
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

    private void LoadPhase(string phaseId)
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var phasesPath = Path.Combine(WorkspacePath, ".aos", "spec", "phases");
        var phaseFile = Path.Combine(phasesPath, $"{phaseId}.json");

        // Try to find in roadmap if not found as individual file
        if (!System.IO.File.Exists(phaseFile))
        {
            LoadPhaseFromRoadmap(phaseId);
            return;
        }

        try
        {
            var json = System.IO.File.ReadAllText(phaseFile);
            var phaseDoc = JsonSerializer.Deserialize<JsonDocument>(json);
            Phase = ParsePhaseDocument(phaseId, phaseDoc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading phase {PhaseId}", phaseId);
            ErrorMessage = $"Error loading phase: {ex.Message}";
        }
    }

    private void LoadPhaseFromRoadmap(string phaseId)
    {
        var roadmapPath = Path.Combine(WorkspacePath!, ".aos", "spec", "roadmap.json");
        if (!System.IO.File.Exists(roadmapPath))
        {
            ErrorMessage = "Roadmap not found.";
            return;
        }

        try
        {
            var json = System.IO.File.ReadAllText(roadmapPath);
            var roadmapDoc = JsonSerializer.Deserialize<JsonDocument>(json);

            if (roadmapDoc?.RootElement.TryGetProperty("roadmap", out var roadmap) == true &&
                roadmap.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetString();
                    if (id == phaseId)
                    {
                        Phase = new PhaseViewModel
                        {
                            Id = id,
                            Name = item.GetProperty("title").GetString() ?? "Unnamed Phase",
                            Description = item.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                            Status = SpecItemStatus.Planned
                        };

                        // Load additional properties if available
                        if (item.TryGetProperty("goals", out var goals))
                        {
                            Phase.Goals = goals.EnumerateArray().Select(g => g.GetString() ?? "").Where(g => !string.IsNullOrEmpty(g)).ToList();
                        }

                        if (item.TryGetProperty("outcomes", out var outcomes))
                        {
                            Phase.Outcomes = outcomes.EnumerateArray().Select(o => o.GetString() ?? "").Where(o => !string.IsNullOrEmpty(o)).ToList();
                        }

                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading phase {PhaseId} from roadmap", phaseId);
            ErrorMessage = $"Error loading phase: {ex.Message}";
        }
    }

    private PhaseViewModel ParsePhaseDocument(string phaseId, JsonDocument? doc)
    {
        var phase = new PhaseViewModel { Id = phaseId };

        if (doc == null) return phase;

        var root = doc.RootElement;

        if (root.TryGetProperty("name", out var name))
        {
            phase.Name = name.GetString() ?? phaseId;
        }

        if (root.TryGetProperty("description", out var desc))
        {
            phase.Description = desc.GetString();
        }

        if (root.TryGetProperty("goals", out var goals))
        {
            phase.Goals = goals.EnumerateArray().Select(g => g.GetString() ?? "").Where(g => !string.IsNullOrEmpty(g)).ToList();
        }

        if (root.TryGetProperty("outcomes", out var outcomes))
        {
            Phase.Outcomes = outcomes.EnumerateArray().Select(o => o.GetString() ?? "").Where(o => !string.IsNullOrEmpty(o)).ToList();
        }

        if (root.TryGetProperty("assumptions", out var assumptions))
        {
            phase.Assumptions = assumptions.EnumerateArray().Select(a => a.GetString() ?? "").Where(a => !string.IsNullOrEmpty(a)).ToList();
        }

        if (root.TryGetProperty("status", out var status))
        {
            phase.Status = ParseStatus(status.GetString());
        }

        return phase;
    }

    private SpecItemStatus ParseStatus(string? statusValue)
    {
        return statusValue?.ToLowerInvariant() switch
        {
            "draft" => SpecItemStatus.Draft,
            "planned" => SpecItemStatus.Planned,
            "inprogress" or "in_progress" => SpecItemStatus.InProgress,
            "blocked" => SpecItemStatus.Blocked,
            "completed" => SpecItemStatus.Completed,
            "verified" => SpecItemStatus.Verified,
            "failed" => SpecItemStatus.Failed,
            _ => SpecItemStatus.Planned
        };
    }

    private bool PersistAssumption(string phaseId, string assumption)
    {
        if (string.IsNullOrEmpty(WorkspacePath)) return false;

        try
        {
            var statePath = Path.Combine(WorkspacePath, ".aos", "state", "state.json");
            JsonDocument? stateDoc = null;

            if (System.IO.File.Exists(statePath))
            {
                var json = System.IO.File.ReadAllText(statePath);
                stateDoc = JsonSerializer.Deserialize<JsonDocument>(json);
            }

            using var stream = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();

            // Copy existing state
            if (stateDoc != null)
            {
                var root = stateDoc.RootElement;

                // Copy all existing properties except phases (we'll merge)
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name != "phases")
                    {
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                    }
                }
            }

            // Write phases with updated assumption
            writer.WritePropertyName("phases");
            writer.WriteStartObject();

            // Copy existing phases
            if (stateDoc?.RootElement.TryGetProperty("phases", out var phases) == true)
            {
                foreach (var phase in phases.EnumerateObject())
                {
                    writer.WritePropertyName(phase.Name);

                    if (phase.Name == phaseId)
                    {
                        // Merge existing phase data with new assumption
                        writer.WriteStartObject();

                        // Copy existing properties
                        foreach (var prop in phase.Value.EnumerateObject())
                        {
                            if (prop.Name != "assumptions")
                            {
                                writer.WritePropertyName(prop.Name);
                                prop.Value.WriteTo(writer);
                            }
                        }

                        // Write assumptions array
                        writer.WritePropertyName("assumptions");
                        writer.WriteStartArray();

                        // Copy existing assumptions
                        if (phase.Value.TryGetProperty("assumptions", out var existingAssumptions))
                        {
                            foreach (var a in existingAssumptions.EnumerateArray())
                            {
                                a.WriteTo(writer);
                            }
                        }

                        // Add new assumption
                        writer.WriteStringValue(assumption);
                        writer.WriteEndArray();

                        writer.WriteEndObject();
                    }
                    else
                    {
                        phase.Value.WriteTo(writer);
                    }
                }
            }

            // If phase doesn't exist, create it
            if (stateDoc?.RootElement.TryGetProperty("phases", out var phasesCheck) != true ||
                !phasesCheck.EnumerateObject().Any(p => p.Name == phaseId))
            {
                writer.WritePropertyName(phaseId);
                writer.WriteStartObject();
                writer.WriteString("status", "planned");
                writer.WritePropertyName("assumptions");
                writer.WriteStartArray();
                writer.WriteStringValue(assumption);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.Flush();

            var newJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            System.IO.File.WriteAllText(statePath, newJson);

            _logger.LogInformation("Added assumption to phase {PhaseId}: {Assumption}", phaseId, assumption);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting assumption for phase {PhaseId}", phaseId);
            return false;
        }
    }

    private bool PersistResearch(string phaseId, string topic, string? findings)
    {
        if (string.IsNullOrEmpty(WorkspacePath)) return false;

        try
        {
            var statePath = Path.Combine(WorkspacePath, ".aos", "state", "state.json");
            JsonDocument? stateDoc = null;

            if (System.IO.File.Exists(statePath))
            {
                var json = System.IO.File.ReadAllText(statePath);
                stateDoc = JsonSerializer.Deserialize<JsonDocument>(json);
            }

            using var stream = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();

            // Copy existing state
            if (stateDoc != null)
            {
                var root = stateDoc.RootElement;
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name != "phases")
                    {
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                    }
                }
            }

            // Write phases with updated research
            writer.WritePropertyName("phases");
            writer.WriteStartObject();

            if (stateDoc?.RootElement.TryGetProperty("phases", out var phases) == true)
            {
                foreach (var phase in phases.EnumerateObject())
                {
                    writer.WritePropertyName(phase.Name);

                    if (phase.Name == phaseId)
                    {
                        writer.WriteStartObject();

                        foreach (var prop in phase.Value.EnumerateObject())
                        {
                            if (prop.Name != "research")
                            {
                                writer.WritePropertyName(prop.Name);
                                prop.Value.WriteTo(writer);
                            }
                        }

                        writer.WritePropertyName("research");
                        writer.WriteStartArray();

                        // Copy existing research
                        if (phase.Value.TryGetProperty("research", out var existingResearch))
                        {
                            foreach (var r in existingResearch.EnumerateArray())
                            {
                                r.WriteTo(writer);
                            }
                        }

                        // Add new research item
                        writer.WriteStartObject();
                        writer.WriteString("topic", topic);
                        if (!string.IsNullOrEmpty(findings))
                        {
                            writer.WriteString("findings", findings);
                        }
                        writer.WriteBoolean("isComplete", !string.IsNullOrEmpty(findings));
                        writer.WriteEndObject();

                        writer.WriteEndArray();
                        writer.WriteEndObject();
                    }
                    else
                    {
                        phase.Value.WriteTo(writer);
                    }
                }
            }

            // If phase doesn't exist, create it
            if (stateDoc?.RootElement.TryGetProperty("phases", out var phasesCheck) != true ||
                !phasesCheck.EnumerateObject().Any(p => p.Name == phaseId))
            {
                writer.WritePropertyName(phaseId);
                writer.WriteStartObject();
                writer.WriteString("status", "planned");
                writer.WritePropertyName("research");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("topic", topic);
                if (!string.IsNullOrEmpty(findings))
                {
                    writer.WriteString("findings", findings);
                }
                writer.WriteBoolean("isComplete", !string.IsNullOrEmpty(findings));
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.Flush();

            var newJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            System.IO.File.WriteAllText(statePath, newJson);

            _logger.LogInformation("Added research to phase {PhaseId}: {Topic}", phaseId, topic);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting research for phase {PhaseId}", phaseId);
            return false;
        }
    }

    public async Task<IActionResult> OnPostPlanPhaseAsync(string id)
    {
        LoadWorkspace();

        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected.";
            return Page();
        }

        if (_agentRunner == null)
        {
            ErrorMessage = "Agent runner is not available.";
            return Page();
        }

        try
        {
                        // Use WorkflowClassifier to plan phase through the orchestrator
            var command = $"spec plan --phase-id {id}";
            var result = await _agentRunner.ExecuteAsync(command);

            if (result.IsSuccess)
            {
                SuccessMessage = "Phase planned successfully. Tasks generated via orchestrator.";
            }
            else
            {
                ErrorMessage = $"Failed to plan phase: {result.FinalPhase}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error planning phase {PhaseId}", id);
            ErrorMessage = $"Error planning phase: {ex.Message}";
        }

        LoadPhase(id);
        ActiveTab = "overview";
        return Page();
    }

    private (bool success, int taskCount, string? errorMessage) GenerateTasksForPhase(string phaseId)
    {
        try
        {
            var tasksDir = Path.Combine(WorkspacePath!, ".aos", "spec", "tasks");
            Directory.CreateDirectory(tasksDir);

            // Generate 2-3 atomic tasks
            var taskIds = new List<string>();
            var taskCount = new Random().Next(2, 4); // 2-3 tasks

            for (int i = 0; i < taskCount; i++)
            {
                var taskId = $"TSK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpperInvariant()}-{i + 1:D2}";
                taskIds.Add(taskId);

                var taskDir = Path.Combine(tasksDir, taskId);
                Directory.CreateDirectory(taskDir);

                // Create task.json
                var taskJson = $@"{{
  ""schemaVersion"": 1,
  ""task"": {{
    ""id"": ""{taskId}"",
    ""phaseId"": ""{phaseId}"",
    ""title"": ""Task {i + 1} for {Phase.Name}"",
    ""description"": ""Generated task for phase planning"",
    ""status"": ""draft"",
    ""acceptanceCriteria"": [
      ""Criterion 1: Implement feature"",
      ""Criterion 2: Add tests"",
      ""Criterion 3: Update documentation""
    ]
  }}
}}";
                System.IO.File.WriteAllText(Path.Combine(taskDir, "task.json"), taskJson);

                // Create plan.json
                var planJson = $@"{{
  ""schemaVersion"": 1,
  ""plan"": {{
    ""taskId"": ""{taskId}"",
    ""scope"": [
      ""**/*.cs"",
      ""**/*.cshtml""
    ],
    ""steps"": [
      {{""order"": 1, ""action"": ""Analyze requirements"", ""estimatedMinutes"": 30}},
      {{""order"": 2, ""action"": ""Implement solution"", ""estimatedMinutes"": 120}},
      {{""order"": 3, ""action"": ""Add tests"", ""estimatedMinutes"": 60}},
      {{""order"": 4, ""action"": ""Verify and commit"", ""estimatedMinutes"": 30}}
    ]
  }}
}}";
                System.IO.File.WriteAllText(Path.Combine(taskDir, "plan.json"), planJson);

                // Create links.json
                var linksJson = $@"{{
  ""schemaVersion"": 1,
  ""links"": [
    {{""type"": ""phase"", ""targetId"": ""{phaseId}"", ""relationship"": ""belongs_to""}}
  ]
}}";
                System.IO.File.WriteAllText(Path.Combine(taskDir, "links.json"), linksJson);

                // Create empty uat.json
                var uatJson = @"{
  ""schemaVersion"": 1,
  ""uat"": {
    ""checks"": []
  }
}";
                System.IO.File.WriteAllText(Path.Combine(taskDir, "uat.json"), uatJson);
            }

            // Update phase status in state
            var statePath = Path.Combine(WorkspacePath!, ".aos", "state", "state.json");
            if (System.IO.File.Exists(statePath))
            {
                var json = System.IO.File.ReadAllText(statePath);
                var stateDoc = JsonSerializer.Deserialize<JsonDocument>(json);

                using var stream = new System.IO.MemoryStream();
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

                writer.WriteStartObject();

                // Copy existing state
                var root = stateDoc!.RootElement;
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name != "phases")
                    {
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                    }
                }

                // Update phases
                writer.WritePropertyName("phases");
                writer.WriteStartObject();

                if (root.TryGetProperty("phases", out var phases))
                {
                    foreach (var phase in phases.EnumerateObject())
                    {
                        writer.WritePropertyName(phase.Name);
                        if (phase.Name == phaseId)
                        {
                            writer.WriteStartObject();
                            foreach (var prop in phase.Value.EnumerateObject())
                            {
                                if (prop.Name == "status")
                                {
                                    writer.WritePropertyName("status");
                                    writer.WriteStringValue("draft");
                                }
                                else
                                {
                                    writer.WritePropertyName(prop.Name);
                                    prop.Value.WriteTo(writer);
                                }
                            }
                            writer.WriteEndObject();
                        }
                        else
                        {
                            phase.Value.WriteTo(writer);
                        }
                    }
                }

                // Add phase if not exists
                if (!phases.EnumerateObject().Any(p => p.Name == phaseId))
                {
                    writer.WritePropertyName(phaseId);
                    writer.WriteStartObject();
                    writer.WriteString("status", "draft");
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.Flush();

                var newJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                System.IO.File.WriteAllText(statePath, newJson);
            }

            _logger.LogInformation("Generated {TaskCount} tasks for phase {PhaseId}: {TaskIds}",
                taskCount, phaseId, string.Join(", ", taskIds));

            return (true, taskCount, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating tasks for phase {PhaseId}", phaseId);
            return (false, 0, ex.Message);
        }
    }

    private bool PersistNote(string phaseId, string content)
    {
        // Notes are not persisted to spec/state, they are ephemeral discussion notes
        _logger.LogInformation("Adding discussion note to phase {PhaseId}", phaseId);
        return true;
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
