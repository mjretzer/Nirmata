using nirmata.Data.Dto.Models.Chat;
using nirmata.Data.Dto.Models.OrchestratorGate;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

/// <summary>
/// Validates workspace identity, normalises and classifies operator input, evaluates
/// the orchestrator gate and timeline, and maps results into chat DTOs.
/// </summary>
public sealed class ChatService : IChatService
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IOrchestratorGateService _orchestratorGateService;

    // Recognised workflow command tokens (case-insensitive). Input that starts with one
    // of these — with or without a leading "aos " prefix — is treated as command mode.
    private static readonly string[] _commandPrefixes =
    [
        "status",
        "new-project", "create-roadmap",
        "plan-phase", "execute-plan", "verify-work", "plan-fix",
        "map-codebase", "pause-work", "resume-work", "resume-task",
        "add-phase", "insert-phase", "remove-phase",
        "discuss-phase", "list-phase-assumptions", "research-phase",
        "discuss-milestone", "complete-milestone", "new-milestone",
        "add-todo", "check-todos", "consider-issues",
        "validate", "init", "help",
    ];

    private static readonly IReadOnlyList<CommandSuggestionDto> _standardSuggestions =
    [
        new() { Command = "aos status",       Description = "Show current workspace status" },
        new() { Command = "plan-phase",        Description = "Generate task plans for the next phase" },
        new() { Command = "execute-plan",      Description = "Execute the current task plan" },
        new() { Command = "verify-work",       Description = "Run acceptance verification for the current phase" },
        new() { Command = "plan-fix",          Description = "Plan fixes for failing verification" },
        new() { Command = "create-roadmap",    Description = "Generate the project roadmap" },
        new() { Command = "new-project",       Description = "Start a new project interview" },
        new() { Command = "map-codebase",      Description = "Scan and map the existing codebase" },
    ];

    public ChatService(
        IWorkspaceService workspaceService,
        IOrchestratorGateService orchestratorGateService)
    {
        _workspaceService = workspaceService;
        _orchestratorGateService = orchestratorGateService;
    }

    // ── IChatService ──────────────────────────────────────────────────────────

    public async Task<ChatSnapshotDto?> GetSnapshotAsync(
        Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return null;

        var gate = await _orchestratorGateService.GetGateAsync(root, cancellationToken);

        return new ChatSnapshotDto
        {
            Messages = [],
            CommandSuggestions = BuildSuggestions(gate),
            QuickActions = BuildQuickActions(gate),
        };
    }

    public async Task<OrchestratorMessageDto?> ProcessTurnAsync(
        Guid workspaceId, string input, CancellationToken cancellationToken = default)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return null;

        var normalized = NormalizeInput(input);
        var isCommand = IsCommandInput(normalized);

        var gate = await _orchestratorGateService.GetGateAsync(root, cancellationToken);
        var timeline = await _orchestratorGateService.GetTimelineAsync(root, cancellationToken);

        var content = isCommand
            ? BuildCommandContent(normalized, gate)
            : BuildConversationalContent(input.Trim(), gate);

        return new OrchestratorMessageDto
        {
            Role = "assistant",
            Content = content,
            Gate = gate,
            Timeline = timeline,
            NextCommand = gate.RecommendedAction,
            Timestamp = DateTimeOffset.UtcNow,
            AgentId = "orchestrator",
        };
    }

    // ── Input normalisation & classification ──────────────────────────────────

    /// <summary>
    /// Strips a leading "aos " prefix (case-insensitive) so the logical command verb is
    /// the first token — e.g. "aos status" → "status", "aos plan-phase PH-0001" → "plan-phase PH-0001".
    /// </summary>
    private static string NormalizeInput(string input)
    {
        var trimmed = input.Trim();
        const string prefix = "aos ";
        return trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[prefix.Length..].TrimStart()
            : trimmed;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the normalised input begins with a recognised
    /// workflow command token, indicating it should be handled as a command dispatch.
    /// </summary>
    private static bool IsCommandInput(string normalized) =>
        !string.IsNullOrWhiteSpace(normalized) &&
        _commandPrefixes.Any(prefix =>
            normalized.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase));

    // ── Response content ──────────────────────────────────────────────────────

    private static string BuildCommandContent(string normalized, OrchestratorGateDto gate)
    {
        var firstToken = normalized
            .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0]
            .ToLowerInvariant();

        if (firstToken == "status")
            return BuildStatusContent(gate);

        var action = gate.RecommendedAction;
        return action is not null
            ? $"Command received: `{normalized}`.\n\n" +
              $"Workspace state: {GateSummary(gate)}.\n\n" +
              $"Recommended next step: `{action}`."
            : $"Command received: `{normalized}`.\n\n" +
              (gate.Runnable
                  ? "The workspace is ready to proceed."
                  : "No further recommended actions at this time.");
    }

    private static string BuildStatusContent(OrchestratorGateDto gate)
    {
        var taskLine = gate.TaskId is not null
            ? $"Active task: `{gate.TaskId}`" +
              (gate.TaskTitle is not null ? $" — {gate.TaskTitle}" : string.Empty)
            : "No active task.";

        var actionLine = gate.RecommendedAction is not null
            ? $"Recommended action: `{gate.RecommendedAction}`"
            : "All checks passed — workspace is in a clean state.";

        return $"**Workspace Status**\n\n{taskLine}\nGate runnable: {(gate.Runnable ? "Yes" : "No")}\n{actionLine}";
    }

    private static string BuildConversationalContent(string input, OrchestratorGateDto gate) =>
        gate.RecommendedAction is not null
            ? $"Understood: \"{input}\".\n\n" +
              $"Based on the current workspace state, the recommended next step is " +
              $"`{gate.RecommendedAction}`. You can run this command or use a quick action below."
            : $"Understood: \"{input}\".\n\n" +
              "The workspace is in a clean state with no pending actions. " +
              "Type an `aos` command or select a quick action to continue.";

    private static string GateSummary(OrchestratorGateDto gate)
    {
        var failCount = gate.Checks.Count(c => c.Status == GateCheckStatus.Fail);
        return failCount == 0
            ? "all gate checks pass"
            : $"{failCount} gate check{(failCount == 1 ? "" : "s")} failing";
    }

    // ── Suggestion & quick-action builders ────────────────────────────────────

    private static IReadOnlyList<CommandSuggestionDto> BuildSuggestions(OrchestratorGateDto gate)
    {
        if (gate.RecommendedAction is null)
            return _standardSuggestions;

        var recommended = new CommandSuggestionDto
        {
            Command = gate.RecommendedAction,
            Description = $"Recommended: {gate.RecommendedAction}",
        };

        // Put the recommended action first; omit its duplicate from the standard list.
        var rest = _standardSuggestions
            .Where(s => !s.Command.Equals(gate.RecommendedAction, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return [recommended, .. rest];
    }

    private static IReadOnlyList<QuickActionDto> BuildQuickActions(OrchestratorGateDto gate)
    {
        var actions = new List<QuickActionDto>();

        if (gate.RecommendedAction is not null)
        {
            actions.Add(new QuickActionDto
            {
                Label = gate.RecommendedAction,
                Command = gate.RecommendedAction,
                Icon = ActionIcon(gate.RecommendedAction),
            });
        }

        // Always include a status check unless it is already the primary recommendation.
        if (!string.Equals(gate.RecommendedAction, "aos status", StringComparison.OrdinalIgnoreCase))
        {
            actions.Add(new QuickActionDto
            {
                Label = "Check Status",
                Command = "aos status",
                Icon = "info",
            });
        }

        return actions;
    }

    private static string? ActionIcon(string action) => action.ToLowerInvariant() switch
    {
        var a when a.StartsWith("execute-plan") => "play",
        var a when a.StartsWith("verify-work")  => "check",
        var a when a.StartsWith("plan-")        => "list",
        var a when a.StartsWith("new-project")  => "folder-plus",
        var a when a.StartsWith("create-roadmap") => "map",
        var a when a.StartsWith("map-codebase") => "search",
        var a when a.StartsWith("plan-fix")     => "wrench",
        _ => null,
    };
}
