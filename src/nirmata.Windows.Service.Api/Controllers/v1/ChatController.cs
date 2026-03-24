using Microsoft.AspNetCore.Mvc;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class ChatArtifactRef
{
    public string Path { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty; // "created" | "updated" | "deleted" | "referenced"
}

public sealed class ChatMessage
{
    public string Id { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty; // "user" | "assistant" | "system" | "result"
    public string Content { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string? Agent { get; init; }
    public string? Gate { get; init; }
    public string? Command { get; init; }
    public IReadOnlyList<ChatArtifactRef>? Artifacts { get; init; }
    public IReadOnlyList<OrchestratorTimelineStep>? Timeline { get; init; }
    public string? RunId { get; init; }
    public bool? Streaming { get; init; }
    public IReadOnlyList<string>? Logs { get; init; }
}

public sealed class CommandSuggestion
{
    public string Command { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
}

public sealed class QuickAction
{
    public string Label { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string Variant { get; init; } = string.Empty; // "default" | "primary" | "destructive"
}

public sealed class ChatSnapshot
{
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];
    public IReadOnlyList<CommandSuggestion> Suggestions { get; init; } = [];
    public IReadOnlyList<QuickAction> QuickActions { get; init; } = [];
}

[ApiController]
[Route("api/v1/chat")]
public class ChatController : ControllerBase
{
    /// <summary>
    /// Returns the chat snapshot: message history, command suggestions, and quick actions.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ChatSnapshot), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var snapshot = new ChatSnapshot
        {
            Messages = [],
            Suggestions = [],
            QuickActions = []
        };

        return Ok(snapshot);
    }
}
