using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using System.Runtime.CompilerServices;

namespace Gmsd.Agents.Tests.Fakes;

/// <summary>
/// Fake LLM provider for unit testing. Returns configurable responses.
/// </summary>
public sealed class FakeLlmProvider : ILlmProvider
{
    private readonly Queue<ResponseOrException> _responses = new();
    private readonly List<LlmCompletionRequest> _requests = new();

    /// <inheritdoc />
    public string ProviderName => "fake";

    /// <summary>
    /// Gets all requests made to this provider.
    /// </summary>
    public IReadOnlyList<LlmCompletionRequest> Requests => _requests.AsReadOnly();

    /// <summary>
    /// Configures a response to be returned on the next call.
    /// </summary>
    public FakeLlmProvider EnqueueResponse(LlmCompletionResponse response)
    {
        _responses.Enqueue(new ResponseOrException(response));
        return this;
    }

    /// <summary>
    /// Configures a simple text response.
    /// </summary>
    public FakeLlmProvider EnqueueTextResponse(string content, string model = "fake-model")
    {
        _responses.Enqueue(new ResponseOrException(new LlmCompletionResponse
        {
            Message = LlmMessage.Assistant(content),
            Model = model,
            Provider = ProviderName,
            FinishReason = "stop"
        }));
        return this;
    }

    /// <summary>
    /// Configures a tool call response.
    /// </summary>
    public FakeLlmProvider EnqueueToolCallResponse(
        string toolName,
        object arguments,
        string model = "fake-model")
    {
        var toolCall = new LlmToolCall
        {
            Id = $"call_{Guid.NewGuid():N}",
            Name = toolName,
            ArgumentsJson = System.Text.Json.JsonSerializer.Serialize(arguments)
        };

        _responses.Enqueue(new ResponseOrException(new LlmCompletionResponse
        {
            Message = LlmMessage.Assistant(toolCalls: new[] { toolCall }),
            Model = model,
            Provider = ProviderName,
            FinishReason = "tool_calls"
        }));
        return this;
    }

    /// <summary>
    /// Configures an exception to be thrown on the next call.
    /// </summary>
    public FakeLlmProvider EnqueueException(LlmProviderException exception)
    {
        _responses.Enqueue(new ResponseOrException(exception));
        return this;
    }

    /// <inheritdoc />
    public Task<LlmCompletionResponse> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        _requests.Add(request);

        if (!_responses.TryDequeue(out var item))
        {
            throw new InvalidOperationException(
                "No more responses configured for FakeLlmProvider. " +
                "Call EnqueueResponse before invoking CompleteAsync.");
        }

        if (item.Exception is not null)
        {
            throw item.Exception;
        }

        return Task.FromResult(item.Response!);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LlmDelta> StreamCompletionAsync(
        LlmCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _requests.Add(request);

        if (!_responses.TryDequeue(out var item))
        {
            throw new InvalidOperationException(
                "No more responses configured for FakeLlmProvider. " +
                "Call EnqueueResponse before invoking StreamCompletionAsync.");
        }

        if (item.Exception is not null)
        {
            throw item.Exception;
        }

        // Simulate streaming by yielding the response content in chunks
        var response = item.Response!;
        var content = response.Message.Content ?? string.Empty;
        var chunkSize = 10;

        for (var i = 0; i < content.Length; i += chunkSize)
        {
            var chunk = content.Substring(i, Math.Min(chunkSize, content.Length - i));
            var isFinal = i + chunkSize >= content.Length;

            yield return new LlmDelta
            {
                Content = chunk,
                FinishReason = isFinal ? response.FinishReason : null
            };

            // Small delay to simulate streaming
            await Task.Delay(1, cancellationToken);
        }
    }

    /// <summary>
    /// Clears all recorded requests.
    /// </summary>
    public void ClearRequests() => _requests.Clear();

    /// <summary>
    /// Resets the provider, clearing all queued responses and recorded requests.
    /// </summary>
    public void Reset()
    {
        _responses.Clear();
        _requests.Clear();
    }

    private sealed class ResponseOrException
    {
        public LlmCompletionResponse? Response { get; }
        public LlmProviderException? Exception { get; }

        public ResponseOrException(LlmCompletionResponse response)
        {
            Response = response;
        }

        public ResponseOrException(LlmProviderException exception)
        {
            Exception = exception;
        }
    }
}
