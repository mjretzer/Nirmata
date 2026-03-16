using System.Text.Json;
using System.Text.Json.Serialization;

namespace nirmata.Agents.Execution.Evidence;

/// <summary>
/// Writes execution summary JSON containing task metadata, timing, and results.
/// </summary>
public sealed class ExecutionSummaryWriter
{
    /// <summary>
    /// Writes an execution summary to a JSON file.
    /// </summary>
    public void WriteExecutionSummary(string outputPath, ExecutionSummary summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(summary);

        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(summary, options);
            File.WriteAllText(outputPath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to write execution summary to {outputPath}: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Represents a complete execution summary.
/// </summary>
public sealed class ExecutionSummary
{
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("runId")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime EndTime { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan Duration => EndTime - StartTime;

    [JsonPropertyName("iterations")]
    public int Iterations { get; set; }

    [JsonPropertyName("filesModified")]
    public List<string> FilesModified { get; set; } = new();

    [JsonPropertyName("toolCallsCount")]
    public int ToolCallsCount { get; set; }

    [JsonPropertyName("buildResult")]
    public BuildResult? BuildResult { get; set; }

    [JsonPropertyName("testResult")]
    public TestResult? TestResult { get; set; }

    [JsonPropertyName("completionStatus")]
    public string CompletionStatus { get; set; } = "unknown";

    [JsonPropertyName("deterministicHash")]
    public string? DeterministicHash { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Represents build execution results.
/// </summary>
public sealed class BuildResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; }

    [JsonPropertyName("exitCode")]
    public int ExitCode { get; set; }
}

/// <summary>
/// Represents test execution results.
/// </summary>
public sealed class TestResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("totalTests")]
    public int TotalTests { get; set; }

    [JsonPropertyName("passed")]
    public int Passed { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; }
}
