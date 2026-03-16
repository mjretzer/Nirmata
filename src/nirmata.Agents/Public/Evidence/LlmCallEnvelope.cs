using System.Text.Json.Serialization;

namespace nirmata.Aos.Engine.Evidence;

/// <summary>
/// Evidence envelope for LLM provider calls.
/// Mirrors the call-envelope schema with LLM-specific metadata.
/// Uses primitive types to remain LLM-agnostic.
/// </summary>
internal sealed record LlmCallEnvelope
{
    public LlmCallEnvelope(
        int schemaVersion,
        string runId,
        string callId,
        string provider,
        string status)
    {
        SchemaVersion = schemaVersion;
        RunId = runId;
        CallId = callId;
        Provider = provider;
        Status = status;
        Tool = "llm";
    }

    public int SchemaVersion { get; init; }
    public string RunId { get; init; }
    public string CallId { get; init; }
    public string Provider { get; init; }
    public string Tool { get; init; }
    public string Status { get; init; }

    /// <summary>
    /// The model identifier used for the completion.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Token usage statistics for the call.
    /// </summary>
    public TokenUsage? Tokens { get; init; }

    /// <summary>
    /// Duration of the call in milliseconds.
    /// </summary>
    public long? DurationMs { get; init; }

    /// <summary>
    /// The finish reason (e.g., "stop", "length", "tool_calls").
    /// </summary>
    public string? FinishReason { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Request { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Response { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Error { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Meta { get; init; }

    /// <summary>
    /// Token usage breakdown.
    /// </summary>
    public sealed record TokenUsage
    {
        public int PromptTokens { get; init; }
        public int CompletionTokens { get; init; }
        public int TotalTokens => PromptTokens + CompletionTokens;
    }

    /// <summary>
    /// Creates an LlmCallEnvelope from primitive parameters.
    /// Callers should construct the request/response objects from their LLM types.
    /// </summary>
    public static LlmCallEnvelope FromResponse(
        string runId,
        string callId,
        string provider,
        string? model,
        object request,
        object response,
        TokenUsage? tokens,
        string? finishReason,
        long durationMs)
    {
        return new LlmCallEnvelope(
            schemaVersion: 1,
            runId: runId,
            callId: callId,
            provider: provider,
            status: "success")
        {
            Model = model,
            DurationMs = durationMs,
            FinishReason = finishReason,
            Tokens = tokens,
            Request = request,
            Response = response
        };
    }

    /// <summary>
    /// Creates an LlmCallEnvelope for a failed call.
    /// </summary>
    public static LlmCallEnvelope FromError(
        string runId,
        string callId,
        string provider,
        string? model,
        object request,
        Exception exception,
        long durationMs)
    {
        return new LlmCallEnvelope(
            schemaVersion: 1,
            runId: runId,
            callId: callId,
            provider: provider,
            status: "error")
        {
            Model = model,
            DurationMs = durationMs,
            Request = request,
            Error = new
            {
                Type = exception.GetType().Name,
                Message = exception.Message
            }
        };
    }
}
