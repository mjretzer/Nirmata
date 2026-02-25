#pragma warning disable CS0618 // ILlmProvider is obsolete during migration period - will be removed in future release

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.Preflight;
using Gmsd.Agents.Execution.Preflight.CommandSuggestion;
using Gmsd.Aos.Contracts.Tools;
using Gmsd.Web.AgentRunner;
using Microsoft.Extensions.Options;

namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Streaming wrapper around the existing orchestrator that emits typed dialogue events
/// during workflow execution. Wraps <see cref="IOrchestrator"/> and exposes the agent's
/// reasoning, decisions, and conversational turns as a stream of events.
/// </summary>
public sealed class StreamingOrchestrator : IStreamingOrchestrator
{
    private readonly IOrchestrator _innerOrchestrator;
    private readonly IGatingEngine _gatingEngine;
    private readonly InputClassifier _inputClassifier;
    private readonly ILlmProvider _llmProvider;
    private readonly ICommandSuggester _commandSuggester;
    private readonly CommandSuggestionOptions _suggestionOptions;

    /// <summary>
    /// Creates a new streaming orchestrator wrapping the provided orchestrator.
    /// </summary>
    /// <param name="innerOrchestrator">The underlying orchestrator to wrap</param>
    /// <param name="gatingEngine">The gating engine for evaluating workspace state</param>
    /// <param name="inputClassifier">The input classifier for intent classification</param>
    /// <param name="llmProvider">The LLM provider for streaming assistant responses</param>
    /// <param name="commandSuggester">The command suggester for natural language input</param>
    /// <param name="suggestionOptions">Options for command suggestion behavior</param>
    public StreamingOrchestrator(
        IOrchestrator innerOrchestrator,
        IGatingEngine gatingEngine,
        InputClassifier inputClassifier,
        ILlmProvider llmProvider,
        ICommandSuggester commandSuggester,
        IOptions<CommandSuggestionOptions> suggestionOptions)
    {
        _innerOrchestrator = innerOrchestrator ?? throw new ArgumentNullException(nameof(innerOrchestrator));
        _gatingEngine = gatingEngine ?? throw new ArgumentNullException(nameof(gatingEngine));
        _inputClassifier = inputClassifier ?? throw new ArgumentNullException(nameof(inputClassifier));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _commandSuggester = commandSuggester ?? throw new ArgumentNullException(nameof(commandSuggester));
        _suggestionOptions = suggestionOptions?.Value ?? throw new ArgumentNullException(nameof(suggestionOptions));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamingEvent> ExecuteWithEventsAsync(
        WorkflowIntent intent,
        StreamingOrchestrationOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(intent);

        options ??= StreamingOrchestrationOptions.Default;
        var correlationId = options.CorrelationId ?? intent.CorrelationId ?? Guid.NewGuid().ToString("N");
        var sequenceGen = new SequenceGenerator(0);

        // Create channel for event streaming
        var channel = Channel.CreateUnbounded<StreamingEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        var sink = new ChannelEventSink(channel, completeOnWriterClose: true);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Classify input early to determine if this is a chat-only interaction
        var classification = _inputClassifier.Classify(intent.InputRaw);
        var category = MapIntentKindToCategory(classification.Intent.Kind);
        var isChatOnly = category == "Chat";

        // Start the orchestration in a background task to allow streaming events immediately
        var producerTask = Task.Run(async () =>
        {
            string? runId = null;
            OrchestratorResult? result = null;
            GatingResult? gatingResult = null;
            Exception? caughtException = null;

            try
            {
                // Emit intent.classified event before execution
                if (options.EmitIntentClassified)
                {
                    await EmitClassificationEventAsync(sink, classification, intent, correlationId, options, sequenceGen, ct);
                }

                // Attempt command suggestion for chat inputs when suggestion mode is enabled
                if (_suggestionOptions.EnableSuggestionMode && isChatOnly)
                {
                    var suggestion = await _commandSuggester.SuggestAsync(intent.InputRaw, ct);
                    if (suggestion != null)
                    {
                        await sink.EmitCommandSuggestedAsync(
                            commandName: suggestion.CommandName,
                            arguments: suggestion.Arguments,
                            formattedCommand: suggestion.FormattedCommand,
                            confidence: suggestion.Confidence,
                            reasoning: suggestion.Reasoning,
                            originalInput: intent.InputRaw,
                            correlationId: correlationId,
                            sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                            cancellationToken: ct);
                    }
                }

                // For chat-only interactions, skip gating and execute directly
                if (isChatOnly)
                {
                    result = await _innerOrchestrator.ExecuteAsync(intent, ct);
                    
                    // Emit assistant dialogue events
                    var messageId = Guid.NewGuid().ToString("N");
                    await EmitAssistantFromResultAsync(sink, messageId, result, correlationId, options, sequenceGen, ct);
                    
                    sink.Complete();
                }
                else
                {
                    // Build gating context and evaluate gates BEFORE run starts
                    var gatingContext = BuildGatingContextFromIntent(intent, classification);
                    gatingResult = await _gatingEngine.EvaluateAsync(gatingContext, ct);

                    // Emit gate.selected event BEFORE phase dispatch with full reasoning
                    if (options.EmitGateSelected)
                    {
                        await EmitGateSelectedFromResultAsync(sink, gatingResult, correlationId, options, sequenceGen, ct);
                    }

                    // Handle confirmation requirement from gating result
                    if (gatingResult.RequiresConfirmation)
                    {
                        // Stop here and wait for user confirmation
                        // The client should handle the confirmation and call back
                        sink.Complete();
                    }
                    else
                    {
                        // Now that we've passed the gate, emit run.started
                        if (options.EmitRunLifecycle)
                        {
                            await sink.EmitRunLifecycleAsync(
                                status: "started",
                                correlationId: correlationId,
                                sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                                cancellationToken: ct);
                        }

                        // Create tool event sink for instrumenting tool calls
                        IToolEventSink toolEventSink = new StreamingToolEventSink(sink, sequenceGen.GetNext());

                        // Execute the underlying orchestrator within the ToolEventSinkContext
                        try
                        {
                            // Emit phase.started event before execution
                            if (options.EmitPhaseLifecycle)
                            {
                                await sink.EmitPhaseLifecycleAsync(
                                    phase: gatingResult.TargetPhase,
                                    status: "started",
                                    context: new Dictionary<string, object>
                                    {
                                        ["input"] = intent.InputRaw,
                                        ["correlationId"] = correlationId,
                                        ["reason"] = gatingResult.Reason
                                    },
                                    correlationId: correlationId,
                                    sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                                    cancellationToken: ct);
                            }

                            // Execute orchestrator with tool event sink context
                            result = await ToolEventSinkContext.ExecuteWithContextAsync(
                                toolEventSink,
                                correlationId,
                                gatingResult.TargetPhase,
                                () => _innerOrchestrator.ExecuteAsync(intent, ct));

                            runId = result.RunId;

                            // Emit phase.completed event after successful execution
                            if (options.EmitPhaseLifecycle)
                            {
                                var artifacts = BuildPhaseArtifacts(result);
                                await sink.EmitPhaseLifecycleAsync(
                                    phase: result.FinalPhase ?? gatingResult.TargetPhase,
                                    status: "completed",
                                    context: new Dictionary<string, object>
                                    {
                                        ["runId"] = runId ?? "",
                                        ["isSuccess"] = result.IsSuccess
                                    },
                                    artifacts: artifacts,
                                    correlationId: correlationId,
                                    sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                                    cancellationToken: ct);
                            }
                        }
                        catch (Exception ex)
                        {
                            caughtException = ex;

                            // Emit phase.completed with error if phase lifecycle is enabled
                            if (options.EmitPhaseLifecycle)
                            {
                                var phaseError = new PhaseError
                                {
                                    Code = "PHASE_EXECUTION_FAILED",
                                    Message = ex.Message,
                                    Details = ex.StackTrace
                                };

                                await sink.EmitPhaseLifecycleAsync(
                                    phase: gatingResult?.TargetPhase ?? "Unknown",
                                    status: "completed",
                                    context: new Dictionary<string, object>
                                    {
                                        ["runId"] = runId ?? "",
                                        ["isSuccess"] = false,
                                        ["errorType"] = ex.GetType().Name
                                    },
                                    error: phaseError,
                                    correlationId: correlationId,
                                    sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                                    cancellationToken: ct);
                            }

                            // Emit error event on exception
                            await sink.EmitErrorAsync(
                                severity: "error",
                                code: "ORCHESTRATION_FAILED",
                                message: ex.Message,
                                context: "StreamingOrchestrator",
                                recoverable: false,
                                correlationId: correlationId,
                                sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                                cancellationToken: ct);

                            // Emit run.finished with failure status
                            if (options.EmitRunLifecycle)
                            {
                                await sink.EmitRunLifecycleAsync(
                                    status: "finished",
                                    runId: runId,
                                    durationMs: stopwatch.ElapsedMilliseconds,
                                    success: false,
                                    correlationId: correlationId,
                                    sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                                    cancellationToken: ct);
                            }
                        }

                        // Emit assistant dialogue events (only if no exception)
                        if (caughtException == null)
                        {
                            var messageId = Guid.NewGuid().ToString("N");
                            await EmitAssistantFromResultAsync(
                                sink,
                                messageId,
                                result!,
                                correlationId,
                                options,
                                sequenceGen,
                                ct);
                        }

                        // Emit run.finished event (only if no exception occurred)
                        if (caughtException == null && options.EmitRunLifecycle)
                        {
                            var artifactRefs = result!.Artifacts?.Keys.ToList();
                            await sink.EmitRunLifecycleAsync(
                                status: "finished",
                                runId: runId ?? result!.RunId,
                                durationMs: stopwatch.ElapsedMilliseconds,
                                success: result!.IsSuccess,
                                artifactReferences: artifactRefs,
                                correlationId: correlationId,
                                sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                                cancellationToken: ct);
                        }

                        // Complete the sink successfully
                        sink.Complete();
                    }
                }
            }
            catch (Exception ex)
            {
                sink.Complete(ex);
            }
        }, ct);

        // Yield all events from the channel
        await foreach (var @event in channel.Reader.ReadAllAsync(ct))
        {
            yield return @event;
        }

        // Await the producer task to ensure any unhandled exceptions are observed
        // (though sink.Complete(ex) should handle propagation via channel)
        try
        {
            await producerTask;
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation of producer task
        }
        catch (Exception)
        {
            // Ignore other exceptions as they are propagated via channel
        }
    }

    /// <summary>
    /// Builds a gating context from the workflow intent and classification.
    /// </summary>
    private static GatingContext BuildGatingContextFromIntent(WorkflowIntent intent, IntentClassificationResult classification)
    {
        // Build context from intent and classification
        // For now, use defaults - the actual workspace state check happens in the orchestrator
        return new GatingContext
        {
            HasProject = false,  // Will be determined by gating engine from workspace state
            HasRoadmap = false,
            HasPlan = false,
            CurrentCursor = null,
            LastExecutionStatus = null,
            LastVerificationStatus = null,
            StateData = new Dictionary<string, object>
            {
                ["input"] = intent.InputRaw,
                ["intentKind"] = classification.Intent.Kind.ToString()
            }
        };
    }

    private static async Task EmitGateSelectedFromResultAsync(
        IEventSink sink,
        GatingResult gatingResult,
        string correlationId,
        StreamingOrchestrationOptions options,
        SequenceGenerator sequenceGen,
        CancellationToken ct)
    {
        // Build proposed action payload from gating result
        ProposedAction? proposedAction = null;
        if (gatingResult.ProposedAction != null)
        {
            proposedAction = new ProposedAction
            {
                Description = gatingResult.ProposedAction.Description,
                ActionType = gatingResult.ProposedAction.Phase,
                Parameters = new Dictionary<string, object>
                {
                    ["riskLevel"] = gatingResult.ProposedAction.RiskLevel.ToString(),
                    ["sideEffects"] = gatingResult.ProposedAction.SideEffects,
                    ["affectedResources"] = gatingResult.ProposedAction.AffectedResources
                }
            };
        }

        await sink.EmitGateSelectedAsync(
            phase: gatingResult.TargetPhase,
            reasoning: gatingResult.Reasoning ?? gatingResult.Reason,
            requiresConfirmation: gatingResult.RequiresConfirmation,
            proposedAction: proposedAction,
            correlationId: correlationId,
            sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
            cancellationToken: ct);
    }

    private class SequenceGenerator
    {
        private long _sequenceNumber;

        public SequenceGenerator(long start = 0)
        {
            _sequenceNumber = start;
        }

        public long GetNext()
        {
            return ++_sequenceNumber;
        }

        public long? GetNextNullable(bool enabled)
        {
            return enabled ? ++_sequenceNumber : null;
        }
    }

    private async Task<bool> EmitClassificationEventAsync(
        IEventSink sink,
        IntentClassificationResult classification,
        WorkflowIntent intent,
        string correlationId,
        StreamingOrchestrationOptions options,
        SequenceGenerator sequenceGen,
        CancellationToken ct)
    {
        try
        {
            // Map IntentKind to category string for event payload
            var category = MapIntentKindToCategory(classification.Intent.Kind);

            // Build extended reasoning with command info if available
            var reasoning = classification.Intent.Reasoning;
            if (classification.ParsedCommand != null)
            {
                reasoning = $"{reasoning} (Detected command: /{classification.ParsedCommand.CommandName})";
            }

            await sink.EmitIntentClassifiedAsync(
                category: category,
                confidence: classification.Intent.Confidence,
                reasoning: reasoning,
                userInput: intent.InputRaw,
                correlationId: correlationId,
                sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                cancellationToken: ct);

            return true;
        }
        catch (Exception ex)
        {
            // Emit error event on classification failure
            await sink.EmitErrorAsync(
                severity: "error",
                code: "CLASSIFICATION_FAILED",
                message: $"Failed to classify intent: {ex.Message}",
                context: "InputClassifier",
                recoverable: true,
                correlationId: correlationId,
                sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                cancellationToken: ct);

            // Still emit a fallback classification event
            await sink.EmitIntentClassifiedAsync(
                category: "Unknown",
                confidence: 0.0,
                reasoning: "Classification failed due to error",
                userInput: intent.InputRaw,
                correlationId: correlationId,
                sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                cancellationToken: ct);

            return false;
        }
    }

    private static async Task EmitConfirmationRequiredEventAsync(
        IEventSink sink,
        IntentClassificationResult classification,
        string correlationId,
        StreamingOrchestrationOptions options,
        SequenceGenerator sequenceGen,
        CancellationToken ct)
    {
        var actionDescription = classification.ParsedCommand != null
            ? $"Execute '{classification.ParsedCommand.CommandName}' command"
            : "Execute workflow action";

        await sink.EmitGateSelectedAsync(
            phase: "ConfirmationRequired",
            reasoning: classification.Intent.Reasoning,
            requiresConfirmation: true,
            proposedAction: new ProposedAction
            {
                Description = actionDescription,
                ActionType = classification.Intent.SideEffect.ToString(),
                Parameters = new Dictionary<string, object>
                {
                    ["confidence"] = classification.Intent.Confidence,
                    ["threshold"] = classification.ConfirmationThreshold,
                    ["command"] = classification.ParsedCommand?.CommandName ?? "unknown"
                }
            },
            correlationId: correlationId,
            sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
            cancellationToken: ct);
    }

    private static string MapIntentKindToCategory(IntentKind kind)
    {
        return kind switch
        {
            IntentKind.SmallTalk => "Chat",
            IntentKind.Help => "Chat",
            IntentKind.Status => "ReadOnly",
            IntentKind.Explain => "Chat",
            IntentKind.WorkflowCommand => "Write",
            IntentKind.WorkflowFreeform => "Write",
            IntentKind.Unknown => "Chat",
            _ => "Chat"
        };
    }

    private static async Task EmitGateSelectedEventAsync(
        IEventSink sink,
        OrchestratorResult result,
        string correlationId,
        StreamingOrchestrationOptions options,
        SequenceGenerator sequenceGen,
        CancellationToken ct)
    {
        var phase = result.FinalPhase ?? "Unknown";

        // Extract reasoning from artifacts if available
        var reasoning = result.Artifacts.TryGetValue("reason", out var reasonValue)
            ? reasonValue?.ToString()
            : $"Phase '{phase}' selected based on workspace state and input classification";

        await sink.EmitGateSelectedAsync(
            phase: phase,
            reasoning: reasoning,
            requiresConfirmation: false,
            proposedAction: null,
            correlationId: correlationId,
            sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
            cancellationToken: ct);
    }

    /// <summary>
    /// Builds phase artifacts from the orchestrator result for phase.completed events.
    /// </summary>
    private static List<PhaseArtifact>? BuildPhaseArtifacts(OrchestratorResult result)
    {
        if (result.Artifacts == null || result.Artifacts.Count == 0)
            return null;

        var artifacts = new List<PhaseArtifact>();

        foreach (var (key, value) in result.Artifacts)
        {
            if (value == null)
                continue;

            artifacts.Add(new PhaseArtifact
            {
                Type = value.GetType().Name,
                Name = key,
                Reference = value?.ToString(),
                Summary = key switch
                {
                    "output" => "Phase execution output",
                    "hasPlan" => "Task plan created",
                    "hasRoadmap" => "Roadmap generated",
                    "executed" => "Task executed",
                    "verified" => "Verification completed",
                    _ => null
                }
            });
        }

        return artifacts.Count > 0 ? artifacts : null;
    }

    /// <summary>
    /// Emits assistant dialogue events by streaming from the LLM provider.
    /// Emits assistant.delta for each chunk and assistant.final at completion.
    /// </summary>
    private async Task<string?> EmitAssistantStreamingAsync(
        IEventSink sink,
        string messageId,
        LlmCompletionRequest request,
        string correlationId,
        StreamingOrchestrationOptions options,
        SequenceGenerator sequenceGen,
        CancellationToken ct)
    {
        if (!options.EmitAssistantDeltas)
        {
            // Non-streaming: use CompleteAsync and emit single final event
            var response = await _llmProvider.CompleteAsync(request, ct);
            var content = response.Message.Content ?? string.Empty;

            await sink.EmitAssistantFinalAsync(
                messageId: messageId,
                content: content,
                contentType: "text/plain",
                correlationId: correlationId,
                sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                cancellationToken: ct);

            return content;
        }

        // Streaming: use StreamCompletionAsync and emit delta events
        var completeContent = new System.Text.StringBuilder();
        var index = 0;

        try
        {
            await foreach (var delta in _llmProvider.StreamCompletionAsync(request, ct))
            {
                if (!string.IsNullOrEmpty(delta.Content))
                {
                    completeContent.Append(delta.Content);

                    await sink.EmitAssistantDeltaAsync(
                        messageId: messageId,
                        content: delta.Content,
                        index: index++,
                        correlationId: correlationId,
                        sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                        cancellationToken: ct);
                }

                // Check if this is the final delta
                if (delta.IsFinal)
                {
                    break;
                }
            }

            // Emit final event
            var finalContent = completeContent.ToString();
            await sink.EmitAssistantFinalAsync(
                messageId: messageId,
                content: finalContent,
                contentType: "text/plain",
                correlationId: correlationId,
                sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                cancellationToken: ct);

            return finalContent;
        }
        catch (Exception ex)
        {
            // Emit error event on streaming failure
            await sink.EmitErrorAsync(
                severity: "error",
                code: "ASSISTANT_STREAMING_FAILED",
                message: $"Failed to stream assistant response: {ex.Message}",
                context: "StreamingOrchestrator",
                recoverable: true,
                correlationId: correlationId,
                sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                cancellationToken: ct);

            // Still emit final event with what we have so far
            var partialContent = completeContent.ToString();
            await sink.EmitAssistantFinalAsync(
                messageId: messageId,
                content: partialContent,
                contentType: "text/plain",
                correlationId: correlationId,
                sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                cancellationToken: ct);

            return partialContent;
        }
    }

    /// <summary>
    /// Emits assistant response from a pre-completed result (non-streaming fallback).
    /// </summary>
    private async Task EmitAssistantFromResultAsync(
        IEventSink sink,
        string messageId,
        OrchestratorResult result,
        string correlationId,
        StreamingOrchestrationOptions options,
        SequenceGenerator sequenceGen,
        CancellationToken ct)
    {
        // Extract response content from result artifacts
        var content = result.Artifacts.TryGetValue("response", out var responseValue)
            ? responseValue?.ToString() ?? string.Empty
            : string.Empty;

        if (!options.EmitAssistantDeltas)
        {
            // Direct final event for non-streaming mode
            await sink.EmitAssistantFinalAsync(
                messageId: messageId,
                content: content,
                contentType: "text/plain",
                correlationId: correlationId,
                sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                cancellationToken: ct);
            return;
        }

        // Simulate streaming by chunking the content
        var chunks = ChunkContent(content, chunkSize: 10);
        var index = 0;

        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();

            await sink.EmitAssistantDeltaAsync(
                messageId: messageId,
                content: chunk,
                index: index++,
                correlationId: correlationId,
                sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
                cancellationToken: ct);
        }

        // Emit final event
        await sink.EmitAssistantFinalAsync(
            messageId: messageId,
            content: content,
            contentType: "text/plain",
            correlationId: correlationId,
            sequenceNumber: sequenceGen.GetNextNullable(options.EnableSequenceNumbers),
            cancellationToken: ct);
    }

    /// <summary>
    /// Chunks content into smaller pieces for simulated streaming.
    /// </summary>
    private static IEnumerable<string> ChunkContent(string content, int chunkSize)
    {
        if (string.IsNullOrEmpty(content))
        {
            yield return string.Empty;
            yield break;
        }

        for (var i = 0; i < content.Length; i += chunkSize)
        {
            yield return content.Substring(i, Math.Min(chunkSize, content.Length - i));
        }
    }
}

#pragma warning restore CS0618
