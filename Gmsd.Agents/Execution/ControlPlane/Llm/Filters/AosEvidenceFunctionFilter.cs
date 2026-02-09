using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gmsd.Aos.Engine;
using Gmsd.Aos.Engine.Evidence;
using Gmsd.Aos.Engine.Paths;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Gmsd.Agents.Execution.ControlPlane.Llm.Filters;

/// <summary>
/// Semantic Kernel function invocation filter that captures LLM call evidence.
/// Implements <see cref="IFunctionInvocationFilter"/> to intercept chat completion calls
/// and write <see cref="LlmCallEnvelope"/> records to the evidence store.
/// </summary>
internal sealed class AosEvidenceFunctionFilter : IFunctionInvocationFilter
{
    private readonly ILlmEvidenceWriter _evidenceWriter;
    private readonly string _runId;
    private readonly string _provider;
    private readonly string? _model;

    // Dictionary to track call start times by function invocation context
    private readonly Dictionary<string, CallTrackingInfo> _trackingInfo = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AosEvidenceFunctionFilter"/> class.
    /// </summary>
    /// <param name="evidenceWriter">The evidence writer for persisting call envelopes.</param>
    /// <param name="runId">The run identifier for grouping evidence.</param>
    /// <param name="provider">The LLM provider identifier (e.g., "openai", "azure-openai").</param>
    /// <param name="model">The model identifier (optional).</param>
    public AosEvidenceFunctionFilter(
        ILlmEvidenceWriter evidenceWriter,
        string runId,
        string provider,
        string? model = null)
    {
        _evidenceWriter = evidenceWriter ?? throw new ArgumentNullException(nameof(evidenceWriter));
        _runId = runId ?? throw new ArgumentNullException(nameof(runId));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _model = model;
    }

    /// <inheritdoc />
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var callId = GenerateCallId(context);
        var startTime = Stopwatch.GetTimestamp();

        // Capture pre-invocation state
        var requestSummary = BuildRequestSummary(context);
        var trackingInfo = new CallTrackingInfo
        {
            CallId = callId,
            StartTimestamp = startTime,
            RequestSummary = requestSummary
        };
        _trackingInfo[callId] = trackingInfo;

        Exception? capturedException = null;
        FunctionResult? result = null;

        try
        {
            // Proceed with the function invocation
            await next(context);
            result = context.Result;
        }
        catch (Exception ex)
        {
            capturedException = ex;
            throw;
        }
        finally
        {
            // Capture post-invocation state and write evidence
            var endTime = Stopwatch.GetTimestamp();
            var durationMs = (long)Stopwatch.GetElapsedTime(startTime, endTime).TotalMilliseconds;

            LlmCallEnvelope envelope;

            if (capturedException != null)
            {
                envelope = LlmCallEnvelope.FromError(
                    runId: _runId,
                    callId: callId,
                    provider: _provider,
                    model: _model,
                    request: requestSummary,
                    exception: capturedException,
                    durationMs: durationMs
                );
            }
            else
            {
                var (responseSummary, tokens, finishReason) = BuildResponseSummary(result);

                envelope = LlmCallEnvelope.FromResponse(
                    runId: _runId,
                    callId: callId,
                    provider: _provider,
                    model: _model,
                    request: requestSummary,
                    response: responseSummary,
                    tokens: tokens,
                    finishReason: finishReason,
                    durationMs: durationMs
                );
            }

            _evidenceWriter.Write(envelope);
            _trackingInfo.Remove(callId);
        }
    }

    private static string GenerateCallId(FunctionInvocationContext context)
    {
        // Generate a unique call ID based on function name and timestamp
        var functionName = context.Function?.Name ?? "unknown";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"{functionName}-{timestamp}-{random}";
    }

    private static object BuildRequestSummary(FunctionInvocationContext context)
    {
        var arguments = new Dictionary<string, object?>();

        if (context.Arguments != null)
        {
            foreach (var arg in context.Arguments)
            {
                // Skip sensitive data and large objects
                var value = arg.Value?.GetType().Name switch
                {
                    "String" => arg.Value,
                    "Int32" => arg.Value,
                    "Int64" => arg.Value,
                    "Boolean" => arg.Value,
                    "Double" => arg.Value,
                    _ => $"[{arg.Value?.GetType().Name}]"
                };
                arguments[arg.Key] = value;
            }
        }

        return new
        {
            FunctionName = context.Function?.Name,
            FunctionDescription = context.Function?.Description,
            PluginName = context.Function?.PluginName,
            Arguments = arguments,
            Timestamp = DateTimeOffset.UtcNow.ToString("O")
        };
    }

    private static (object Summary, LlmCallEnvelope.TokenUsage? Tokens, string? FinishReason) BuildResponseSummary(FunctionResult? result)
    {
        if (result == null)
        {
            return (new { Status = "no_result" }, null, null);
        }

        // Build a summary from the result - use ToString() for content since FunctionResult
        // may not expose a direct Value property in this version of Semantic Kernel
        var resultType = result.GetType();
        
        // Try to get Value property via reflection if it exists
        object? rawValue = null;
        try
        {
            var valueProperty = resultType.GetProperty("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            rawValue = valueProperty?.GetValue(result);
        }
        catch
        {
            // Ignore reflection errors
        }

        // If we got a ChatMessageContent, extract detailed info
        if (rawValue is ChatMessageContent chatMessageContent)
        {
            var tokens = ExtractTokenUsage(chatMessageContent);
            var finishReason = ExtractFinishReason(chatMessageContent);

            var summary = new
            {
                Content = chatMessageContent.Content,
                Role = chatMessageContent.Role.Label,
                Model = chatMessageContent.ModelId,
                FinishReason = finishReason,
                TokenUsage = tokens != null ? new { tokens.PromptTokens, tokens.CompletionTokens, tokens.TotalTokens } : null
            };

            return (summary, tokens, finishReason);
        }

        // Fall back to generic result info
        return (new 
        { 
            ResultType = resultType.Name, 
            Value = rawValue?.ToString() ?? result.ToString() ?? "null"
        }, null, null);
    }

    private static LlmCallEnvelope.TokenUsage? ExtractTokenUsage(ChatMessageContent message)
    {
        if (message.Metadata == null)
        {
            return null;
        }

        // Try to extract token usage from metadata (provider-specific)
        int? promptTokens = null;
        int? completionTokens = null;

        // OpenAI format
        if (message.Metadata.TryGetValue("PromptTokens", out var ptValue) && ptValue is int pt)
        {
            promptTokens = pt;
        }
        else if (message.Metadata.TryGetValue("prompt_tokens", out var ptObj) && ptObj is int pt2)
        {
            promptTokens = pt2;
        }

        if (message.Metadata.TryGetValue("CompletionTokens", out var ctValue) && ctValue is int ct)
        {
            completionTokens = ct;
        }
        else if (message.Metadata.TryGetValue("completion_tokens", out var ctObj) && ctObj is int ct2)
        {
            completionTokens = ct2;
        }

        if (promptTokens.HasValue && completionTokens.HasValue)
        {
            return new LlmCallEnvelope.TokenUsage
            {
                PromptTokens = promptTokens.Value,
                CompletionTokens = completionTokens.Value
            };
        }

        return null;
    }

    private static string? ExtractFinishReason(ChatMessageContent message)
    {
        if (message.Metadata == null)
        {
            return null;
        }

        // Try to extract finish reason from metadata (provider-specific)
        if (message.Metadata.TryGetValue("FinishReason", out var frValue) && frValue is string fr)
        {
            return fr;
        }
        if (message.Metadata.TryGetValue("finish_reason", out var frObj) && frObj is string fr2)
        {
            return fr2;
        }

        return null;
    }

    private sealed class CallTrackingInfo
    {
        public required string CallId { get; init; }
        public required long StartTimestamp { get; init; }
        public required object RequestSummary { get; init; }
    }
}
