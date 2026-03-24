using nirmata.Data.Dto.Models.Chat;
using nirmata.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace nirmata.Api.Controllers.V1;

[Route("v1/workspaces/{workspaceId:guid}/chat")]
public class ChatController : nirmataController
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>
    /// Returns the current chat snapshot for a workspace — including the message thread,
    /// command suggestions, and quick actions derived from the current gate state.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ChatSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSnapshot(Guid workspaceId, CancellationToken cancellationToken)
    {
        var snapshot = await _chatService.GetSnapshotAsync(workspaceId, cancellationToken);
        if (snapshot is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        return Ok(snapshot);
    }

    /// <summary>
    /// Processes a single chat turn for a workspace.
    /// Accepts freeform text or an explicit <c>aos …</c> command, classifies and dispatches
    /// through the orchestrator pipeline, and returns a structured <see cref="OrchestratorMessageDto"/>.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrchestratorMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostTurn(
        Guid workspaceId,
        [FromBody] ChatTurnRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationErrorResult();

        var message = await _chatService.ProcessTurnAsync(workspaceId, request.Input, cancellationToken);
        if (message is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        return Ok(message);
    }
}
