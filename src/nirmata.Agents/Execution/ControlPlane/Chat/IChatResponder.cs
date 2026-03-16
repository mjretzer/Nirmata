namespace nirmata.Agents.Execution.ControlPlane.Chat;

using Models;

/// <summary>
/// Interface for the chat responder service that generates LLM-backed conversational responses.
/// </summary>
public interface IChatResponder
{
    /// <summary>
    /// Generates a chat response using the LLM provider (blocking mode).
    /// </summary>
    /// <param name="request">The chat request containing user input and options.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation with the chat response.</returns>
    Task<ChatResponse> RespondAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a chat response token-by-token from the LLM provider.
    /// </summary>
    /// <param name="request">The chat request containing user input and options.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An async enumerable of chat delta chunks containing streaming content.</returns>
    IAsyncEnumerable<ChatDelta> StreamResponseAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
