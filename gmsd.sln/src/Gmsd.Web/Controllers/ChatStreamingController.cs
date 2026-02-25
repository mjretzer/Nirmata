using System.Runtime.CompilerServices;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Execution.Preflight;
using Gmsd.Web.Models;
using Gmsd.Web.Models.Streaming;
using Microsoft.AspNetCore.Mvc;

namespace Gmsd.Web.Controllers;

/// <summary>
/// Request to confirm a suggested command
/// </summary>
public class ConfirmSuggestionRequest
{
    /// <summary>
    /// The confirmation request ID from the command.suggested event
    /// </summary>
    public required string ConfirmationRequestId { get; set; }

    /// <summary>
    /// The formatted command to execute
    /// </summary>
    public string? FormattedCommand { get; set; }

    /// <summary>
    /// The original user input that triggered the suggestion
    /// </summary>
    public string? OriginalInput { get; set; }
}

/// <summary>
/// Request to reject/dismiss a suggested command
/// </summary>
public class RejectSuggestionRequest
{
    /// <summary>
    /// The confirmation request ID from the command.suggested event
    /// </summary>
    public required string ConfirmationRequestId { get; set; }

    /// <summary>
    /// Optional reason for rejection
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Whether to continue the conversation as chat after rejection
    /// </summary>
    public bool ContinueAsChat { get; set; } = true;
}

/// <summary>
/// API controller for chat streaming functionality using Server-Sent Events (SSE).
/// </summary>
[Route("api")]
[ApiController]
public class ChatStreamingController : ControllerBase
{
    private readonly IStreamingOrchestrator _streamingOrchestrator;
    private readonly InputClassifier _intentClassifier;
    private readonly ILogger<ChatStreamingController> _logger;
    private static readonly Dictionary<string, CancellationTokenSource> ActiveStreams = new();

        public ChatStreamingController(
        IStreamingOrchestrator streamingOrchestrator,
        InputClassifier intentClassifier,
        ILogger<ChatStreamingController> logger)
    {
        _streamingOrchestrator = streamingOrchestrator ?? throw new ArgumentNullException(nameof(streamingOrchestrator));
        _intentClassifier = intentClassifier ?? throw new ArgumentNullException(nameof(intentClassifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes a command via the agent orchestrator and streams the response.
    /// This is the main endpoint used by the chat UI.
    /// </summary>
    [HttpPost("agent/execute")]
    [Consumes("application/x-www-form-urlencoded")]
    public IAsyncEnumerable<StreamingChatEvent> ExecuteCommand(
        [FromForm] string command,
        [FromForm] string? threadId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return GetEmptyCommandError();
        }

        return ExecuteCommandInternal(command, threadId, ct);
    }

    /// <summary>
    /// Streams AI responses using Server-Sent Events (SSE).
    /// Compatible with HTMX SSE extension.
    /// </summary>
    [HttpPost("chat/stream")]
    [Consumes("application/x-www-form-urlencoded")]
    public IAsyncEnumerable<StreamingChatEvent> StreamCommand(
        [FromForm] string command,
        [FromForm] string? threadId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return GetEmptyCommandError();
        }

        return ExecuteCommandInternal(command, threadId, ct);
    }

    /// <summary>
    /// Streams AI responses using Server-Sent Events (SSE) with typed dialogue events.
    /// This is the v2 endpoint that emits structured events (intent.classified, gate.selected, etc.)
    /// instead of generic content chunks. Compatible with the dialogue streaming protocol.
    /// </summary>
    /// <remarks>
    /// <para><strong>Endpoint:</strong> POST /api/chat/stream-v2</para>
    /// <para><strong>Content-Type:</strong> application/x-www-form-urlencoded</para>
    /// 
    /// <para><strong>Request Parameters:</strong></para>
    /// <list type="bullet">
    ///   <item><term>command</term> - The user input to process (required)</item>
    ///   <item><term>threadId</term> - Optional correlation ID for conversation continuity</item>
    ///   <item><term>chatOnly</term> - Force chat mode (no write operations)</item>
    /// </list>
    /// 
    /// <para><strong>Accept Header Negotiation:</strong></para>
    /// <list type="bullet">
    ///   <item><term>application/json</term> - Returns typed StreamingEvent objects (default)</item>
    ///   <item><term>application/vnd.gmsd.legacy+json</term> - Returns legacy StreamingChatEvent format</item>
    /// </list>
    /// 
    /// <para><strong>Event Format:</strong></para>
    /// Each SSE event is a JSON object with the following structure:
    /// <code>
    /// {
    ///   "id": "event-guid",
    ///   "type": "intent.classified|gate.selected|phase.started|phase.completed|tool.call|tool.result|assistant.delta|assistant.final|run.started|run.finished|error",
    ///   "timestamp": "2024-01-01T12:00:00Z",
    ///   "correlationId": "thread-id",
    ///   "sequenceNumber": 1,
    ///   "payload": { ... }
    /// }
    /// </code>
    /// 
    /// <para><strong>Event Types:</strong></para>
    /// <list type="bullet">
    ///   <item><term>intent.classified</term> - Emitted after input classification with intent, confidence, and reasoning</item>
    ///   <item><term>gate.selected</term> - Emitted when gating engine selects target phase with reasoning</item>
    ///   <item><term>phase.started</term> - Emitted before phase execution begins</item>
    ///   <item><term>phase.completed</term> - Emitted after phase execution completes</item>
    ///   <item><term>tool.call</term> - Emitted when a tool is invoked with call ID and arguments</item>
    ///   <item><term>tool.result</term> - Emitted when tool execution completes with results</item>
    ///   <item><term>assistant.delta</term> - Streaming content chunk from AI response</item>
    ///   <item><term>assistant.final</term> - Final AI response with complete message</item>
    ///   <item><term>run.started</term> - Emitted when a write operation begins (write ops only)</item>
    ///   <item><term>run.finished</term> - Emitted when a write operation completes (write ops only)</item>
    ///   <item><term>error</term> - Emitted when an error occurs during processing</item>
    /// </list>
    /// 
    /// <para><strong>Chat Sequence Example:</strong></para>
    /// <code>
    /// intent.classified → gate.selected → assistant.delta (multiple) → assistant.final
    /// </code>
    /// 
    /// <para><strong>Workflow Sequence Example:</strong></para>
    /// <code>
    /// intent.classified → gate.selected → run.started → phase.started → tool.call → 
    /// tool.result → assistant.delta (multiple) → assistant.final → phase.completed → run.finished
    /// </code>
    /// </remarks>
    [HttpPost("chat/stream-v2")]
    [Consumes("application/x-www-form-urlencoded")]
    [Produces("application/json", "application/vnd.gmsd.legacy+json")]
    public IAsyncEnumerable<StreamingEvent> StreamCommandV2(
        [FromForm] string command,
        [FromForm] string? threadId = null,
        [FromForm] bool? chatOnly = null,
        [FromHeader(Name = "Accept")] string? acceptHeader = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return GetEmptyCommandErrorV2();
        }

        // Check if client requests legacy format
        var useLegacyFormat = acceptHeader?.Contains("vnd.gmsd.legacy") == true;

        if (useLegacyFormat)
        {
            // Return legacy format but use streaming orchestrator underneath
            _logger.LogInformation("Client requested legacy format via Accept header");
        }

        return ExecuteCommandInternalV2(command, threadId, chatOnly, ct);
    }

    /// <summary>
    /// Legacy streaming endpoint that uses the new streaming orchestrator
    /// but transforms events to legacy format for backward compatibility.
    /// </summary>
    [HttpPost("chat/stream")]
    [Consumes("application/x-www-form-urlencoded")]
    [Produces("application/json")]
    public IAsyncEnumerable<StreamingChatEvent> StreamCommandLegacy(
        [FromForm] string command,
        [FromForm] string? threadId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return GetEmptyCommandError();
        }

        return ExecuteCommandInternalLegacy(command, threadId, ct);
    }

    private async IAsyncEnumerable<StreamingChatEvent> ExecuteCommandInternalLegacy(
        string command,
        string? threadId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var correlationId = threadId ?? Guid.NewGuid().ToString("N");
        var options = StreamingOrchestrationOptions.Default;
        options.CorrelationId = correlationId;

        _logger.LogInformation("Starting legacy streaming execution for command: {Command}, correlationId: {CorrelationId}",
            command, correlationId);

        var intent = new WorkflowIntent
        {
            InputRaw = command,
            CorrelationId = correlationId
        };

        // Start with message_start event
        yield return new StreamingChatEvent
        {
            Type = "message_start",
            MessageId = Guid.NewGuid().ToString("N"),
            Content = "",
            Timestamp = DateTime.UtcNow
        };

        // Stream events from orchestrator, transformed to legacy format
        await foreach (var @event in _streamingOrchestrator.ExecuteWithEventsAsync(intent, options, ct))
        {
            var legacyEvent = LegacyEventAdapter.Transform(@event);
            yield return legacyEvent;
        }
    }

    private static async IAsyncEnumerable<StreamingEvent> GetEmptyCommandErrorV2()
    {
        yield return StreamingEvent.Create(
            StreamingEventType.Error,
            new ErrorPayload
            {
                Severity = "error",
                Code = "EMPTY_COMMAND",
                Message = "Command cannot be empty",
                Context = "ChatStreamingController"
            });
    }

    private async IAsyncEnumerable<StreamingEvent> ExecuteCommandInternalV2(
        string command,
        string? threadId,
        bool? chatOnly,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var correlationId = threadId ?? Guid.NewGuid().ToString("N");

        // Detect if this is a chat intent
        var intentClassification = _intentClassifier.Classify(command);
        var isChatIntent = intentClassification.Intent.SideEffect == SideEffect.None;

        // Handle navigation commands directly
        if (intentClassification.Intent.Kind == IntentKind.Navigation)
        {
            await foreach (var navEvent in HandleNavigationCommand(command, correlationId, ct))
            {
                yield return navEvent;
            }
            yield break;
        }

        // Use ChatOnly options for chat intents or when explicitly requested
        var useChatMode = chatOnly == true || isChatIntent;
        var options = useChatMode
            ? StreamingOrchestrationOptions.ChatOnly
            : StreamingOrchestrationOptions.Default;

        options.CorrelationId = correlationId;

        _logger.LogInformation(
            "Starting v2 streaming execution for command: {Command}, correlationId: {CorrelationId}, chatMode: {ChatMode}, intent: {Intent}",
            command, correlationId, useChatMode, intentClassification.Intent.SideEffect);

        var intent = new WorkflowIntent
        {
            InputRaw = command,
            CorrelationId = correlationId
        };

        await foreach (var @event in _streamingOrchestrator.ExecuteWithEventsAsync(intent, options, ct))
        {
            yield return @event;
        }
    }

    /// <summary>
    /// Handles navigation commands (/view, /open, /show) and emits UI navigation events
    /// </summary>
    private async IAsyncEnumerable<StreamingEvent> HandleNavigationCommand(
        string command,
        string correlationId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Parse the navigation target from the command
        var parts = command.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var navTarget = parts.Length > 1 ? string.Join(" ", parts[1..]) : "";

        _logger.LogInformation("Processing navigation command: {Command}, target: {Target}", command, navTarget);

        // Yield intent classified event
        yield return StreamingEvent.Create(
            StreamingEventType.IntentClassified,
            new Gmsd.Web.Models.Streaming.IntentClassifiedPayload
            {
                Category = "Navigation",
                Confidence = 1.0,
                Reasoning = $"Navigation command to view: {navTarget}",
                UserInput = command
            },
            correlationId,
            1);

        // Determine the navigation action based on target
        var (action, entityType, entityId, pageUrl, title) = ParseNavigationTarget(navTarget);

        // Yield UI navigation event
        yield return StreamingEvent.Create(
            StreamingEventType.UiNavigation,
            new UiNavigationPayload
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                PageUrl = pageUrl,
                Title = title,
                UseDetailPanel = true,
                Context = new Dictionary<string, object>
                {
                    ["command"] = command,
                    ["target"] = navTarget
                }
            },
            correlationId,
            2);

        // Yield final assistant response
        yield return StreamingEvent.Create(
            StreamingEventType.AssistantFinal,
            new AssistantFinalPayload
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Content = $"Opening {title} in the detail panel...",
                ContentType = "text"
            },
            correlationId,
            3);

        await Task.CompletedTask; // Required for IAsyncEnumerable method
    }

    /// <summary>
    /// Parses a navigation target string and returns navigation parameters
    /// </summary>
    private static (string action, string? entityType, string? entityId, string? pageUrl, string title) ParseNavigationTarget(string target)
    {
        var lowerTarget = target.ToLowerInvariant().Trim();

        // Handle entity references like "project:xyz" or "run:abc-123"
        if (lowerTarget.Contains(':'))
        {
            var colonIndex = lowerTarget.IndexOf(':');
            var entityType = lowerTarget[..colonIndex];
            var entityId = lowerTarget[(colonIndex + 1)..];
            return ("showEntity", entityType, entityId, null, $"{entityType} {entityId}");
        }

        // Handle page names
        return lowerTarget switch
        {
            "projects" or "project" => ("showPage", null, null, "/Projects", "Projects"),
            "runs" or "run" => ("showPage", null, null, "/Runs", "Runs"),
            "tasks" or "task" => ("showPage", null, null, "/Tasks", "Tasks"),
            "specs" or "spec" => ("showPage", null, null, "/Specs", "Specs"),
            "issues" or "issue" => ("showPage", null, null, "/Issues", "Issues"),
            "milestones" or "milestone" => ("showPage", null, null, "/Milestones", "Milestones"),
            "phases" or "phase" => ("showPage", null, null, "/Phases", "Phases"),
            "roadmap" => ("showPage", null, null, "/Roadmap", "Roadmap"),
            "workspace" => ("showPage", null, null, "/Workspace", "Workspace"),
            "settings" => ("showPage", null, null, "/Settings", "Settings"),
            "dashboard" or "home" => ("showPage", null, null, "/", "Dashboard"),
            _ => ("showPage", null, null, $"/{target}", target)
        };
    }

    private static async IAsyncEnumerable<StreamingChatEvent> GetEmptyCommandError()
    {
        yield return new StreamingChatEvent
        {
            Type = "error",
            Content = "Command cannot be empty",
            MessageId = Guid.NewGuid().ToString("N")
        };
    }

    private IAsyncEnumerable<StreamingChatEvent> ExecuteCommandInternal(
        string command,
        string? threadId,
        CancellationToken ct)
    {
        // Delegate to new streaming orchestrator with legacy format
        return ExecuteCommandInternalLegacy(command, threadId, ct);
    }

    /// <summary>
    /// Cancels an active streaming request.
    /// </summary>
    [HttpPost("chat/cancel/{streamId}")]
    public IActionResult CancelStream(string streamId)
    {
        CancellationTokenSource? cts;
        lock (ActiveStreams)
        {
            ActiveStreams.TryGetValue(streamId, out cts);
        }

        if (cts != null)
        {
            cts.Cancel();
            _logger.LogInformation("Cancelled stream {StreamId}", streamId);
            return Ok(new { cancelled = true });
        }

        return NotFound(new { error = "Stream not found or already completed" });
    }

    /// <summary>
    /// Confirms a suggested command and executes it.
    /// </summary>
    /// <remarks>
    /// After receiving a `command.suggested` event, the UI can call this endpoint
    /// to confirm and execute the suggested command.
    /// </remarks>
    [HttpPost("suggestion/confirm")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public IActionResult ConfirmSuggestion([FromBody] ConfirmSuggestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConfirmationRequestId))
        {
            return BadRequest(new { error = "ConfirmationRequestId is required" });
        }

        _logger.LogInformation(
            "Suggestion confirmed: {ConfirmationRequestId}, Command: {Command}",
            request.ConfirmationRequestId,
            request.FormattedCommand);

        // Emit the confirmation event for telemetry and auditing
        var confirmedEvent = StreamingEvent.Create(
            StreamingEventType.SuggestedCommandConfirmed,
            new SuggestedCommandConfirmedPayload
            {
                ConfirmationRequestId = request.ConfirmationRequestId,
                CommandName = request.FormattedCommand?.Split(' ').FirstOrDefault()?.TrimStart('/') ?? "unknown",
                FormattedCommand = request.FormattedCommand,
                OriginalInput = request.OriginalInput
            });

        // Return the command to execute - client should then call the execute endpoint with this command
        return Ok(new
        {
            confirmed = true,
            confirmationRequestId = request.ConfirmationRequestId,
            command = request.FormattedCommand,
            originalInput = request.OriginalInput,
            eventId = confirmedEvent.Id,
            message = "Suggestion confirmed. Execute the returned command to proceed."
        });
    }

    /// <summary>
    /// Rejects/dismisses a suggested command.
    /// </summary>
    /// <remarks>
    /// After receiving a `command.suggested` event, the UI can call this endpoint
    /// to dismiss the suggestion and optionally continue as chat.
    /// </remarks>
    [HttpPost("suggestion/dismiss")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public IActionResult DismissSuggestion([FromBody] RejectSuggestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConfirmationRequestId))
        {
            return BadRequest(new { error = "ConfirmationRequestId is required" });
        }

        _logger.LogInformation(
            "Suggestion dismissed: {ConfirmationRequestId}, ContinueAsChat: {ContinueAsChat}",
            request.ConfirmationRequestId,
            request.ContinueAsChat);

        // Emit the rejection event for telemetry and auditing
        var rejectedEvent = StreamingEvent.Create(
            StreamingEventType.SuggestedCommandRejected,
            new SuggestedCommandRejectedPayload
            {
                ConfirmationRequestId = request.ConfirmationRequestId,
                CommandName = "unknown", // Will be populated from confirmation context if available
                RejectionReason = request.Reason,
                ContinueAsChat = request.ContinueAsChat
            });

        return Ok(new
        {
            dismissed = true,
            confirmationRequestId = request.ConfirmationRequestId,
            continueAsChat = request.ContinueAsChat,
            eventId = rejectedEvent.Id,
            message = request.ContinueAsChat
                ? "Suggestion dismissed. Continuing as chat."
                : "Suggestion dismissed."
        });
    }
}

/// <summary>
/// Event structure for streaming chat responses.
/// </summary>
public class StreamingChatEvent
{
    public string Type { get; set; } = "content_chunk";
    public string MessageId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsFinal { get; set; } = false;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? Metadata { get; set; }
}
