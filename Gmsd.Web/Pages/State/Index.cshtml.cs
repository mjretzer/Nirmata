using System.Text.Json;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Public;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.State;

/// <summary>
/// PageModel for the State & Events dashboard.
/// Provides visibility into state.json and events.ndjson from .aos/state/
/// </summary>
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;
    private readonly IStateStore? _stateStore;
    private readonly IEventStore? _eventStore;

    public IndexModel(
        ILogger<IndexModel> logger,
        IConfiguration configuration,
        IStateStore? stateStore = null,
        IEventStore? eventStore = null)
    {
        _logger = logger;
        _configuration = configuration;
        _stateStore = stateStore;
        _eventStore = eventStore;
    }

    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    // State properties
    public string? StateJsonContent { get; set; }
    public StateSnapshot? StateSnapshot { get; set; }
    public bool StateExists { get; set; }

    // Event properties
    public List<StateEventViewModel> Events { get; set; } = new();
    public List<string> EventTypes { get; set; } = new();

    // History/Summary properties
    public List<HistoryEntryViewModel> HistoryEntries { get; set; } = new();

    // Query parameters
    [BindProperty(SupportsGet = true)]
    public string? EventTypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? MaxEvents { get; set; } = 50;

    [BindProperty(SupportsGet = true)]
    public bool AutoRefresh { get; set; } = false;

    public void OnGet()
    {
        LoadWorkspace();

        if (!string.IsNullOrEmpty(WorkspacePath))
        {
            LoadState();
            LoadEvents();
            LoadHistorySummary();
        }
    }

    public IActionResult OnPostRefresh()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        _logger.LogInformation("State page refresh triggered for workspace: {WorkspacePath}", WorkspacePath);
        return RedirectToPage(new { EventTypeFilter, MaxEvents, AutoRefresh });
    }

    public IActionResult OnPostFilter(string eventType)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        EventTypeFilter = eventType;
        _logger.LogInformation("Event filter applied: {EventType}", eventType);

        return RedirectToPage(new { EventTypeFilter, MaxEvents, AutoRefresh });
    }

    public IActionResult OnPostToggleAutoRefresh()
    {
        LoadWorkspace();
        AutoRefresh = !AutoRefresh;
        _logger.LogInformation("Auto-refresh toggled: {AutoRefresh}", AutoRefresh);
        return RedirectToPage(new { EventTypeFilter, MaxEvents, AutoRefresh });
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

    private void LoadState()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var statePath = Path.Combine(WorkspacePath, ".aos", "state", "state.json");

        try
        {
            StateExists = System.IO.File.Exists(statePath);

            if (StateExists)
            {
                StateJsonContent = System.IO.File.ReadAllText(statePath);

                // Try to deserialize to structured snapshot
                if (_stateStore != null)
                {
                    try
                    {
                        StateSnapshot = _stateStore.ReadSnapshot();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read structured state snapshot");
                    }
                }
                else
                {
                    // Fallback: try to deserialize directly
                    try
                    {
                        StateSnapshot = JsonSerializer.Deserialize<StateSnapshot>(StateJsonContent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize state snapshot");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading state.json");
            ErrorMessage = $"Error loading state: {ex.Message}";
        }
    }

    private void LoadEvents()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var eventsPath = Path.Combine(WorkspacePath, ".aos", "state", "events.ndjson");

        try
        {
            if (!System.IO.File.Exists(eventsPath))
            {
                return;
            }

            var maxEvents = MaxEvents ?? 50;

            if (_eventStore != null)
            {
                // Use the event store for structured access
                var request = new StateEventTailRequest
                {
                    SinceLine = 0,
                    MaxItems = maxEvents,
                    EventType = EventTypeFilter
                };

                var response = _eventStore.ListEvents(request);
                Events = response.Items.Select(e => new StateEventViewModel
                {
                    LineNumber = e.LineNumber,
                    Payload = e.Payload,
                    EventType = GetEventType(e.Payload),
                    Timestamp = GetEventTimestamp(e.Payload)
                }).ToList();
            }
            else
            {
                // Fallback: read directly from file
                var lines = System.IO.File.ReadAllLines(eventsPath);
                var events = new List<StateEventViewModel>();
                var eventTypesSet = new HashSet<string>();

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var payload = JsonSerializer.Deserialize<JsonElement>(line);
                        var eventType = GetEventType(payload);

                        eventTypesSet.Add(eventType);

                        // Apply filter
                        if (!string.IsNullOrEmpty(EventTypeFilter) && eventType != EventTypeFilter)
                        {
                            continue;
                        }

                        events.Add(new StateEventViewModel
                        {
                            LineNumber = i + 1,
                            Payload = payload,
                            EventType = eventType,
                            Timestamp = GetEventTimestamp(payload)
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse event at line {LineNumber}", i + 1);
                    }
                }

                // Take last N events
                Events = events
                    .Skip(Math.Max(0, events.Count - maxEvents))
                    .ToList();

                EventTypes = eventTypesSet.ToList();
            }

            // Extract event types from loaded events if not already populated
            if (EventTypes.Count == 0)
            {
                EventTypes = Events.Select(e => e.EventType).Distinct().ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading events.ndjson");
        }
    }

    private void LoadHistorySummary()
    {
        if (Events.Count == 0)
        {
            return;
        }

        // Group events by run/task/step for history summary
        var historyDict = new Dictionary<string, HistoryEntryViewModel>();

        foreach (var evt in Events)
        {
            var runId = GetPayloadProperty(evt.Payload, "runId");
            var taskId = GetPayloadProperty(evt.Payload, "taskId");
            var stepId = GetPayloadProperty(evt.Payload, "stepId");

            var key = $"{runId ?? "unknown"}:{taskId ?? "none"}:{stepId ?? "none"}";

            if (!historyDict.ContainsKey(key))
            {
                historyDict[key] = new HistoryEntryViewModel
                {
                    RunId = runId,
                    TaskId = taskId,
                    StepId = stepId,
                    EventCount = 0,
                    LastEventAt = evt.Timestamp,
                    EventTypes = new List<string>()
                };
            }

            var entry = historyDict[key];
            entry.EventCount++;

            if (!entry.EventTypes.Contains(evt.EventType))
            {
                entry.EventTypes.Add(evt.EventType);
            }

            // Update timestamp if this event is newer
            if (evt.Timestamp.HasValue && (!entry.LastEventAt.HasValue || evt.Timestamp.Value > entry.LastEventAt.Value))
            {
                entry.LastEventAt = evt.Timestamp;
            }
        }

        HistoryEntries = historyDict.Values
            .OrderByDescending(h => h.LastEventAt)
            .ToList();
    }

    private static string GetEventType(JsonElement payload)
    {
        return GetPayloadProperty(payload, "eventType")
            ?? GetPayloadProperty(payload, "kind")
            ?? GetPayloadProperty(payload, "type")
            ?? "unknown";
    }

    private static DateTime? GetEventTimestamp(JsonElement payload)
    {
        var timestampStr = GetPayloadProperty(payload, "timestamp")
            ?? GetPayloadProperty(payload, "occurredAt")
            ?? GetPayloadProperty(payload, "createdAt");

        if (!string.IsNullOrEmpty(timestampStr) && DateTime.TryParse(timestampStr, out var timestamp))
        {
            return timestamp;
        }

        return null;
    }

    private static string? GetPayloadProperty(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }

        return null;
    }

    private string GetWorkspaceConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "workspace-config.json");
    }
}

public class StateEventViewModel
{
    public int LineNumber { get; set; }
    public JsonElement Payload { get; set; }
    public string EventType { get; set; } = "unknown";
    public DateTime? Timestamp { get; set; }

    public string FormattedPayload => JsonSerializer.Serialize(Payload, new JsonSerializerOptions { WriteIndented = true });
}

public class HistoryEntryViewModel
{
    public string? RunId { get; set; }
    public string? TaskId { get; set; }
    public string? StepId { get; set; }
    public int EventCount { get; set; }
    public DateTime? LastEventAt { get; set; }
    public List<string> EventTypes { get; set; } = new();

    public string DisplayKey => $"{RunId ?? "unknown"} / {TaskId ?? "-"} / {StepId ?? "-"}";
}

internal class WorkspaceConfig
{
    public string? SelectedWorkspacePath { get; set; }
}
