namespace nirmata.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Normalized request for LLM completion.
/// </summary>
[Obsolete("Use Microsoft.SemanticKernel.ChatCompletion.ChatHistory and PromptExecutionSettings directly. " +
          "This abstraction will be removed in a future release.", false)]
public sealed record LlmCompletionRequest
{
    /// <summary>
    /// The conversation messages to send to the LLM.
    /// </summary>
    [Obsolete("Use Microsoft.SemanticKernel.ChatCompletion.ChatMessageContent directly. " +
              "This abstraction will be removed in a future release.", false)]
    public required IReadOnlyList<LlmMessage> Messages { get; init; }

    /// <summary>
    /// Optional model identifier override. If null, the provider uses its configured default.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Provider options including temperature, max tokens, etc.
    /// </summary>
    public LlmProviderOptions Options { get; init; } = new();

    /// <summary>
    /// Tool definitions available for this completion request.
    /// </summary>
    public IReadOnlyList<LlmToolDefinition>? Tools { get; init; }

    /// <summary>
    /// When set, forces the model to use this specific tool.
    /// </summary>
    public string? ToolChoice { get; init; }

    /// <summary>
    /// Optional structured output schema that the provider should enforce.
    /// </summary>
    public LlmStructuredOutputSchema? StructuredOutputSchema { get; init; }

    /// <summary>
    /// Creates a new request with an additional message appended to the conversation.
    /// Useful for incremental message accumulation during tool calling loops.
    /// </summary>
    /// <param name="message">The message to append.</param>
    /// <returns>A new request with the message added.</returns>
    public LlmCompletionRequest WithMessage(LlmMessage message)
    {
        var newMessages = Messages.ToList();
        newMessages.Add(message);

        return this with { Messages = newMessages };
    }

    /// <summary>
    /// Creates a new request with multiple messages appended to the conversation.
    /// Useful for adding tool results after execution.
    /// </summary>
    /// <param name="messages">The messages to append.</param>
    /// <returns>A new request with the messages added.</returns>
    public LlmCompletionRequest WithMessages(IEnumerable<LlmMessage> messages)
    {
        var newMessages = Messages.ToList();
        newMessages.AddRange(messages);

        return this with { Messages = newMessages };
    }

    /// <summary>
    /// Creates a new request with the message at the specified index replaced.
    /// Useful for updating existing messages in the conversation.
    /// </summary>
    /// <param name="index">The index of the message to replace.</param>
    /// <param name="message">The replacement message.</param>
    /// <returns>A new request with the message replaced.</returns>
    public LlmCompletionRequest WithMessageReplaced(int index, LlmMessage message)
    {
        var newMessages = Messages.ToList();
        if (index < 0 || index >= newMessages.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        newMessages[index] = message;

        return this with { Messages = newMessages };
    }

    /// <summary>
    /// Creates a builder for constructing a request incrementally.
    /// </summary>
    /// <param name="initialMessage">Optional initial message to start the conversation.</param>
    /// <returns>A new builder instance.</returns>
    public static Builder CreateBuilder(LlmMessage? initialMessage = null)
    {
        var builder = new Builder();
        if (initialMessage != null)
        {
            builder.WithMessage(initialMessage);
        }
        return builder;
    }

    /// <summary>
    /// Builder for constructing LlmCompletionRequest instances incrementally.
    /// </summary>
    public sealed class Builder
    {
        private readonly List<LlmMessage> _messages = new();
        private string? _model;
        private LlmProviderOptions _options = new();
        private List<LlmToolDefinition>? _tools;
        private string? _toolChoice;
        private LlmStructuredOutputSchema? _structuredOutputSchema;

        /// <summary>
        /// Adds a message to the conversation.
        /// </summary>
        public Builder WithMessage(LlmMessage message)
        {
            _messages.Add(message);
            return this;
        }

        /// <summary>
        /// Adds multiple messages to the conversation.
        /// </summary>
        public Builder WithMessages(IEnumerable<LlmMessage> messages)
        {
            _messages.AddRange(messages);
            return this;
        }

        /// <summary>
        /// Sets the model identifier.
        /// </summary>
        public Builder WithModel(string? model)
        {
            _model = model;
            return this;
        }

        /// <summary>
        /// Sets the provider options.
        /// </summary>
        public Builder WithOptions(LlmProviderOptions options)
        {
            _options = options;
            return this;
        }

        /// <summary>
        /// Adds a tool definition to the available tools.
        /// </summary>
        public Builder WithTool(LlmToolDefinition tool)
        {
            _tools ??= new List<LlmToolDefinition>();
            _tools.Add(tool);
            return this;
        }

        /// <summary>
        /// Sets the available tools.
        /// </summary>
        public Builder WithTools(IEnumerable<LlmToolDefinition>? tools)
        {
            _tools = tools?.ToList();
            return this;
        }

        /// <summary>
        /// Sets the tool choice directive.
        /// </summary>
        public Builder WithToolChoice(string? toolChoice)
        {
            _toolChoice = toolChoice;
            return this;
        }

        /// <summary>
        /// Sets the structured output schema to enforce.
        /// </summary>
        public Builder WithStructuredOutputSchema(LlmStructuredOutputSchema? schema)
        {
            _structuredOutputSchema = schema;
            return this;
        }

        /// <summary>
        /// Builds the LlmCompletionRequest.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no messages have been added.</exception>
        public LlmCompletionRequest Build()
        {
            if (_messages.Count == 0)
                throw new InvalidOperationException("At least one message is required to build a completion request.");

            return new LlmCompletionRequest
            {
                Messages = _messages.ToList(),
                Model = _model,
                Options = _options,
                Tools = _tools?.ToList(),
                ToolChoice = _toolChoice,
                StructuredOutputSchema = _structuredOutputSchema
            };
        }
    }
}
