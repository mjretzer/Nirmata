using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace nirmata.Data.Dto.Models.Chat;

/// <summary>
/// Input payload for a workspace-scoped chat turn.
/// Posted to <c>POST /v1/workspaces/{workspaceId}/chat</c>.
/// </summary>
public sealed class ChatTurnRequestDto
{
    /// <summary>
    /// Freeform text or an explicit <c>aos …</c> command string submitted by the operator.
    /// The backend normalises direct <c>aos</c> input and classifies everything else
    /// into the appropriate command mode before dispatch.
    /// </summary>
    [Required]
    [JsonPropertyName("input")]
    public required string Input { get; init; }
}
