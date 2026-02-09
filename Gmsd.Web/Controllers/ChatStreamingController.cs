using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Web.AgentRunner;
using Gmsd.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Gmsd.Web.Controllers;

/// <summary>
/// API controller for chat streaming functionality using Server-Sent Events (SSE).
/// </summary>
[Route("api")]
[ApiController]
public class ChatStreamingController : ControllerBase
{
    private readonly WorkflowClassifier _agentRunner;
    private readonly ILogger<ChatStreamingController> _logger;
    private static readonly Dictionary<string, CancellationTokenSource> ActiveStreams = new();

        public ChatStreamingController(
        WorkflowClassifier agentRunner,
        ILogger<ChatStreamingController> logger)
    {
        _agentRunner = agentRunner ?? throw new ArgumentNullException(nameof(agentRunner));
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
        var channel = Channel.CreateUnbounded<StreamingChatEvent>();

        _ = Task.Run(async () =>
        {
            var messageId = Guid.NewGuid().ToString("N");
            var streamId = Guid.NewGuid().ToString("N");
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            
            lock (ActiveStreams)
            {
                ActiveStreams[streamId] = cts;
            }

            try
            {
                await channel.Writer.WriteAsync(new StreamingChatEvent
                {
                    Type = "message_start",
                    MessageId = messageId,
                    Content = "",
                    Timestamp = DateTime.UtcNow
                }, cts.Token);

                _logger.LogInformation("Starting agent execution for command: {Command}", command);

                // Stream initial "thinking" message
                await channel.Writer.WriteAsync(new StreamingChatEvent
                {
                    Type = "thinking",
                    MessageId = messageId,
                    Content = "Processing your request...",
                    IsFinal = false
                }, cts.Token);

                // Execute via the actual orchestrator
                OrchestratorResult result;
                try
                {
                    result = await _agentRunner.ExecuteAsync(command, cts.Token);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Provider") || ex.Message.Contains("API key"))
                {
                    _logger.LogError(ex, "LLM provider not configured");
                    await channel.Writer.WriteAsync(new StreamingChatEvent
                    {
                        Type = "error",
                        MessageId = messageId,
                        Content = "LLM provider not configured. Please configure your API key in Settings > LLM."
                    }, CancellationToken.None);
                    return;
                }

                // Stream the result based on success/failure
                if (result.IsSuccess)
                {
                    var responseText = BuildSuccessResponse(result, command);
                    var chunks = SplitIntoChunks(responseText, 8);

                    foreach (var chunk in chunks)
                    {
                        if (cts.Token.IsCancellationRequested)
                        {
                            await channel.Writer.WriteAsync(new StreamingChatEvent
                            {
                                Type = "cancelled",
                                MessageId = messageId,
                                Content = ""
                            }, CancellationToken.None);
                            return;
                        }

                        await channel.Writer.WriteAsync(new StreamingChatEvent
                        {
                            Type = "content_chunk",
                            MessageId = messageId,
                            Content = chunk,
                            IsFinal = false
                        }, cts.Token);

                        await Task.Delay(30, cts.Token);
                    }
                }
                else
                {
                    var errorText = BuildErrorResponse(result);
                    await channel.Writer.WriteAsync(new StreamingChatEvent
                    {
                        Type = "content_chunk",
                        MessageId = messageId,
                        Content = errorText,
                        IsFinal = false
                    }, cts.Token);
                }

                // Send completion event
                await channel.Writer.WriteAsync(new StreamingChatEvent
                {
                    Type = "message_complete",
                    MessageId = messageId,
                    Content = "",
                    Timestamp = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        ["streamId"] = streamId,
                        ["command"] = command,
                        ["runId"] = result.RunId ?? "unknown",
                        ["isSuccess"] = result.IsSuccess,
                        ["finalPhase"] = result.FinalPhase ?? "unknown"
                    }
                }, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Streaming cancelled for message {MessageId}", messageId);
                await channel.Writer.WriteAsync(new StreamingChatEvent
                {
                    Type = "cancelled",
                    MessageId = messageId,
                    Content = ""
                }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during streaming for message {MessageId}", messageId);
                await channel.Writer.WriteAsync(new StreamingChatEvent
                {
                    Type = "error",
                    MessageId = messageId,
                    Content = $"Error: {ex.Message}"
                }, CancellationToken.None);
            }
            finally
            {
                lock (ActiveStreams)
                {
                    ActiveStreams.Remove(streamId);
                }
                cts.Dispose();
                channel.Writer.Complete();
            }
        }, ct);

        return channel.Reader.ReadAllAsync(ct);
    }

    private static string BuildSuccessResponse(OrchestratorResult result, string command)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"✅ **Execution Complete**");
        sb.AppendLine();
        sb.AppendLine($"**Command:** `{command}`");
        sb.AppendLine($"**Run ID:** `{result.RunId}`");
        sb.AppendLine($"**Phase:** {result.FinalPhase}");
        sb.AppendLine();

        if (result.Artifacts?.Count > 0)
        {
            sb.AppendLine("**Artifacts:**");
            foreach (var artifact in result.Artifacts.Take(5))
            {
                sb.AppendLine($"- {artifact.Key}: {artifact.Value}");
            }
            if (result.Artifacts.Count > 5)
            {
                sb.AppendLine($"- ... and {result.Artifacts.Count - 5} more");
            }
            sb.AppendLine();
        }

        sb.AppendLine("The workflow has been executed successfully. You can view details in the detail panel.");

        return sb.ToString();
    }

    private static string BuildErrorResponse(OrchestratorResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"❌ **Execution Failed**");
        sb.AppendLine();
        sb.AppendLine($"**Run ID:** `{result.RunId}`");
        if (!string.IsNullOrEmpty(result.FinalPhase))
        {
            sb.AppendLine($"**Phase:** {result.FinalPhase}");
        }
        sb.AppendLine();

        if (result.Artifacts?.TryGetValue("error", out var error) == true)
        {
            sb.AppendLine($"**Error:** {error}");
        }
        else
        {
            sb.AppendLine("An error occurred during execution. Please check the logs for details.");
        }

        return sb.ToString();
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

    private static List<string> SplitIntoChunks(string text, int chunkSize)
    {
        var chunks = new List<string>();
        var words = text.Split(' ');
        var currentChunk = new System.Text.StringBuilder();

        foreach (var word in words)
        {
            if (currentChunk.Length + word.Length + 1 > chunkSize * 5)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
            }
            currentChunk.Append(word + " ");
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks;
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
