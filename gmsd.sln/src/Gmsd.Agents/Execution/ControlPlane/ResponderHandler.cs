using Gmsd.Aos.Contracts.Commands;

namespace Gmsd.Agents.Execution.ControlPlane;

using Chat;
using Chat.Models;

/// <summary>
/// Handler for the Responder phase that generates conversational responses using the LLM.
/// </summary>
public sealed class ResponderHandler
{
    private readonly IChatResponder _chatResponder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponderHandler"/> class.
    /// </summary>
    public ResponderHandler(IChatResponder chatResponder)
    {
        _chatResponder = chatResponder ?? throw new ArgumentNullException(nameof(chatResponder));
    }

    /// <summary>
    /// Handles the Responder phase by generating a conversational response.
    /// </summary>
    public async Task<CommandRouteResult> HandleAsync(CommandRequest request, string runId, CancellationToken ct = default)
    {
        // Extract the message from the request options, or use a default
        var message = request.Options.GetValueOrDefault("message") ?? "What can you help me with?";

        var chatRequest = new ChatRequest
        {
            Input = message,
            IncludeWorkspaceContext = true
        };

        var response = await _chatResponder.RespondAsync(chatRequest, ct);

        return response.IsSuccess
            ? CommandRouteResult.Success(response.Content)
            : CommandRouteResult.Failure(1, response.ErrorMessage ?? "Failed to generate response");
    }
}
