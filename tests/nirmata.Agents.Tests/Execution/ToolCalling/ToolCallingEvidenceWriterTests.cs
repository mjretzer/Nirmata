using nirmata.Agents.Execution.ToolCalling;
using nirmata.Aos.Public;
using NSubstitute;
using Xunit;

namespace nirmata.Agents.Tests.Execution.ToolCalling;

/// <summary>
/// Unit tests for the ToolCallingEvidenceWriter class.
/// </summary>
public class ToolCallingEvidenceWriterTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IWorkspace _workspace;
    private readonly ToolCallingEvidenceWriter _writer;

    public ToolCallingEvidenceWriterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        _workspace = Substitute.For<IWorkspace>();
        _workspace.GetAbsolutePathForArtifactId("workspace").Returns(_tempDirectory);

        _writer = new ToolCallingEvidenceWriter(_workspace);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task WriteAsync_CreatesEvidenceFileInCorrectDirectory()
    {
        // Arrange
        var evidence = new ToolCallingConversationEvidence
        {
            CallId = "test-call-123",
            RunId = "test-run-456",
            StartedAt = DateTimeOffset.UtcNow,
            CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
            ConversationHistory = new[]
            {
                new ToolCallingConversationMessage
                {
                    Role = "user",
                    Content = "Hello",
                    Timestamp = DateTimeOffset.UtcNow
                },
                new ToolCallingConversationMessage
                {
                    Role = "assistant",
                    Content = "Hi there!",
                    Timestamp = DateTimeOffset.UtcNow
                }
            }
        };

        // Act
        var filePath = await _writer.WriteAsync(evidence);

        // Assert
        Assert.True(File.Exists(filePath));
        var expectedDir = Path.Combine(_tempDirectory, ".aos", "evidence", "runs", "test-run-456", "tool-calling");
        Assert.Equal(expectedDir, Path.GetDirectoryName(filePath));
        Assert.Equal("test-call-123.json", Path.GetFileName(filePath));
    }

    [Fact]
    public async Task WriteAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var evidence = new ToolCallingConversationEvidence
        {
            CallId = "test-call-789",
            RunId = "new-run",
            StartedAt = DateTimeOffset.UtcNow,
            CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
            ConversationHistory = Array.Empty<ToolCallingConversationMessage>()
        };

        var expectedDir = Path.Combine(_tempDirectory, ".aos", "evidence", "runs", "new-run", "tool-calling");
        Assert.False(Directory.Exists(expectedDir));

        // Act
        await _writer.WriteAsync(evidence);

        // Assert
        Assert.True(Directory.Exists(expectedDir));
    }

    [Fact]
    public async Task WriteAsync_SerializesEvidenceCorrectly()
    {
        // Arrange
        var startedAt = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var evidence = new ToolCallingConversationEvidence
        {
            SchemaVersion = 1,
            CallId = "call-001",
            RunId = "run-001",
            CorrelationId = "corr-001",
            StartedAt = startedAt,
            CompletedAt = startedAt.AddSeconds(5),
            CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
            ConversationHistory = new[]
            {
                new ToolCallingConversationMessage
                {
                    Role = "system",
                    Content = "You are a helpful assistant.",
                    Timestamp = startedAt
                },
                new ToolCallingConversationMessage
                {
                    Role = "user",
                    Content = "What's the weather?",
                    Timestamp = startedAt.AddSeconds(1)
                },
                new ToolCallingConversationMessage
                {
                    Role = "assistant",
                    Content = null,
                    ToolCalls = new[]
                    {
                        new ToolCallingRequestEvidence
                        {
                            Id = "call_1",
                            Name = "get_weather",
                            ArgumentsJson = "{\"location\":\"NYC\"}"
                        }
                    },
                    Timestamp = startedAt.AddSeconds(2)
                },
                new ToolCallingConversationMessage
                {
                    Role = "tool",
                    ToolCallId = "call_1",
                    ToolName = "get_weather",
                    Content = "{\"temperature\":72,\"condition\":\"sunny\"}",
                    Timestamp = startedAt.AddSeconds(3)
                },
                new ToolCallingConversationMessage
                {
                    Role = "assistant",
                    Content = "It's sunny and 72 degrees in NYC!",
                    Timestamp = startedAt.AddSeconds(4)
                }
            },
            ToolExecutions = new[]
            {
                new ToolCallingExecutionEvidence
                {
                    Iteration = 1,
                    ToolCallId = "call_1",
                    ToolName = "get_weather",
                    ArgumentsJson = "{\"location\":\"NYC\"}",
                    IsSuccess = true,
                    ResultContent = "{\"temperature\":72,\"condition\":\"sunny\"}",
                    StartedAt = startedAt.AddSeconds(2),
                    CompletedAt = startedAt.AddSeconds(3)
                }
            },
            Usage = new ToolCallingUsageEvidence
            {
                TotalPromptTokens = 150,
                TotalCompletionTokens = 50,
                IterationCount = 2,
                TotalToolCalls = 1
            },
            Options = new ToolCallingOptionsEvidence
            {
                MaxIterations = 10,
                Timeout = TimeSpan.FromMinutes(5),
                Model = "gpt-4",
                EnableParallelToolExecution = true
            },
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "test"
            }
        };

        // Act
        var filePath = await _writer.WriteAsync(evidence);
        var json = await File.ReadAllTextAsync(filePath);

        // Assert
        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"callId\": \"call-001\"", json);
        Assert.Contains("\"runId\": \"run-001\"", json);
        Assert.Contains("\"correlationId\": \"corr-001\"", json);
        Assert.Contains("\"role\": \"user\"", json);
        Assert.Contains("\"role\": \"assistant\"", json);
        Assert.Contains("\"role\": \"tool\"", json);
        Assert.Contains("\"toolCalls\"", json);
        Assert.Contains("\"get_weather\"", json);
        Assert.Contains("\"completionReason\": \"completedNaturally\"", json);
        Assert.Contains("\"totalPromptTokens\": 150", json);
        Assert.Contains("\"maxIterations\": 10", json);
    }

    [Fact]
    public async Task WriteAsync_WithError_IncludeErrorDetails()
    {
        // Arrange
        var evidence = new ToolCallingConversationEvidence
        {
            CallId = "error-call",
            RunId = "error-run",
            StartedAt = DateTimeOffset.UtcNow,
            CompletionReason = ToolCallingCompletionReason.Error,
            ConversationHistory = Array.Empty<ToolCallingConversationMessage>(),
            Error = new ToolCallingErrorEvidence
            {
                Code = "ToolExecutionFailed",
                Message = "Tool execution failed with error",
                ExceptionDetails = "Stack trace here"
            }
        };

        // Act
        var filePath = await _writer.WriteAsync(evidence);
        var json = await File.ReadAllTextAsync(filePath);

        // Assert
        Assert.Contains("\"error\":", json);
        Assert.Contains("\"code\": \"ToolExecutionFailed\"", json);
        Assert.Contains("\"exceptionDetails\": \"Stack trace here\"", json);
        Assert.Contains("\"completionReason\": \"error\"", json);
    }

    [Fact]
    public async Task WriteAsync_WithNullEvidence_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _writer.WriteAsync(null!));
    }

    [Fact]
    public void Constructor_WithNullWorkspace_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ToolCallingEvidenceWriter(null!));
    }

    [Fact]
    public async Task WriteAsync_UsesLfLineEndings()
    {
        // Arrange
        var evidence = new ToolCallingConversationEvidence
        {
            CallId = "lf-test",
            RunId = "lf-run",
            StartedAt = DateTimeOffset.UtcNow,
            CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
            ConversationHistory = Array.Empty<ToolCallingConversationMessage>()
        };

        // Act
        var filePath = await _writer.WriteAsync(evidence);
        var content = await File.ReadAllTextAsync(filePath);

        // Assert
        Assert.DoesNotContain("\r\n", content);
        Assert.Contains("\n", content);
    }
}
