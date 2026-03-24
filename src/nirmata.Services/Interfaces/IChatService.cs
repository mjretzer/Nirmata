using nirmata.Data.Dto.Models.Chat;

namespace nirmata.Services.Interfaces;

/// <summary>
/// Provides workspace-scoped chat snapshot retrieval and single-turn command processing
/// for the chat command interface.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Returns the current chat snapshot for a workspace — including the message thread,
    /// command suggestions derived from the gate state, and quick actions.
    /// Returns <see langword="null"/> when the workspace does not exist.
    /// </summary>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ChatSnapshotDto?> GetSnapshotAsync(
        Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a single chat turn for a workspace.
    /// Normalises direct <c>aos …</c> input, classifies freeform text into command or
    /// conversational mode, dispatches through the orchestrator gate pipeline, and returns
    /// a structured <see cref="OrchestratorMessageDto"/>.
    /// Returns <see langword="null"/> when the workspace does not exist.
    /// </summary>
    /// <param name="workspaceId">Workspace identifier.</param>
    /// <param name="input">Raw operator input — either a direct <c>aos</c> command or freeform text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<OrchestratorMessageDto?> ProcessTurnAsync(
        Guid workspaceId, string input, CancellationToken cancellationToken = default);
}
