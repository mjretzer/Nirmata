using System.Text.Json.Serialization;

namespace nirmata.Data.Dto.Models.Chat;

/// <summary>
/// Full chat snapshot for a workspace.
/// Returned by <c>GET /v1/workspaces/{workspaceId}/chat</c>.
/// Contains the current message thread, available command suggestions, and quick actions.
/// </summary>
public sealed class ChatSnapshotDto
{
    /// <summary>
    /// Ordered message thread for the workspace (oldest first).
    /// May be empty when the workspace has no chat history.
    /// </summary>
    [JsonPropertyName("messages")]
    public required IReadOnlyList<OrchestratorMessageDto> Messages { get; init; }

    /// <summary>
    /// Autocomplete suggestions sourced from the current workspace state.
    /// Used to populate the chat input as the operator types.
    /// </summary>
    [JsonPropertyName("commandSuggestions")]
    public required IReadOnlyList<CommandSuggestionDto> CommandSuggestions { get; init; }

    /// <summary>
    /// One-click quick actions relevant to the current workspace state.
    /// Each action maps to an <c>aos</c> command that will be submitted via the chat endpoint.
    /// </summary>
    [JsonPropertyName("quickActions")]
    public required IReadOnlyList<QuickActionDto> QuickActions { get; init; }
}
