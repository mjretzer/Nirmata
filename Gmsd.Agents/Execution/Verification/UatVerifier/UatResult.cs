using Gmsd.Aos.Public;
using Gmsd.Aos.Public.Services;
using System.Text.Json;

namespace Gmsd.Agents.Execution.Verification.UatVerifier;

/// <summary>
/// Schema for UAT result artifacts stored in evidence.
/// </summary>
public sealed record UatResult
{
    /// <summary>
    /// Schema version for the UAT result format.
    /// </summary>
    public required string SchemaVersion { get; init; }

    /// <summary>
    /// The run identifier.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// The task identifier.
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// Overall verification status (passed, failed, inconclusive).
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Timestamp when the verification was performed.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Individual check results.
    /// </summary>
    public required IReadOnlyList<UatCheckRecord> Checks { get; init; }
}

/// <summary>
/// Record of an individual UAT check result.
/// </summary>
public sealed record UatCheckRecord
{
    /// <summary>
    /// The criterion identifier.
    /// </summary>
    public required string CriterionId { get; init; }

    /// <summary>
    /// Whether the check passed.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Human-readable message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The check type.
    /// </summary>
    public required string CheckType { get; init; }

    /// <summary>
    /// The target path.
    /// </summary>
    public string? TargetPath { get; init; }

    /// <summary>
    /// Expected value.
    /// </summary>
    public string? Expected { get; init; }

    /// <summary>
    /// Actual value.
    /// </summary>
    public string? Actual { get; init; }
}

/// <summary>
/// Schema for UAT spec artifacts stored in spec layer.
/// </summary>
public sealed record UatSpec
{
    /// <summary>
    /// Schema version for the UAT spec format.
    /// </summary>
    public required string SchemaVersion { get; init; }

    /// <summary>
    /// The task identifier.
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// The acceptance criteria definitions.
    /// </summary>
    public required IReadOnlyList<UatCriterionRecord> Criteria { get; init; }

    /// <summary>
    /// Timestamp when the spec was created.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Record of an acceptance criterion definition.
/// </summary>
public sealed record UatCriterionRecord
{
    /// <summary>
    /// The criterion identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The check type.
    /// </summary>
    public required string CheckType { get; init; }

    /// <summary>
    /// The target path.
    /// </summary>
    public string? TargetPath { get; init; }

    /// <summary>
    /// Expected content pattern.
    /// </summary>
    public string? ExpectedContent { get; init; }

    /// <summary>
    /// Whether this criterion is required.
    /// </summary>
    public bool IsRequired { get; init; } = true;
}

/// <summary>
/// Implementation of the UAT result writer that writes to the evidence store.
/// </summary>
public sealed class UatResultWriter : IUatResultWriter
{
    private readonly IWorkspace _workspace;
    private readonly IDeterministicJsonSerializer _jsonSerializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UatResultWriter"/> class.
    /// </summary>
    public UatResultWriter(IWorkspace workspace, IDeterministicJsonSerializer jsonSerializer)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
    }

    /// <inheritdoc />
    public async Task<string> WriteResultAsync(string taskId, string runId, UatVerificationResult result, CancellationToken ct = default)
    {
        var artifactPath = Path.Combine(
            _workspace.AosRootPath,
            "evidence",
            "runs",
            runId,
            "artifacts",
            "uat-results.json");

        // Ensure directory exists
        var directory = Path.GetDirectoryName(artifactPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var uatResult = new UatResult
        {
            SchemaVersion = "gmsd:aos:schema:uat-result:v1",
            RunId = runId,
            TaskId = taskId,
            Status = result.IsPassed ? "passed" : "failed",
            Timestamp = DateTimeOffset.UtcNow,
            Checks = result.Checks.Select(c => new UatCheckRecord
            {
                CriterionId = c.CriterionId,
                Passed = c.Passed,
                Message = c.Message,
                CheckType = c.CheckType,
                TargetPath = c.TargetPath,
                Expected = c.Expected,
                Actual = c.Actual
            }).ToList().AsReadOnly()
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        var json = _jsonSerializer.SerializeToString(uatResult, jsonOptions);
        await File.WriteAllTextAsync(artifactPath, json, ct);

        return artifactPath;
    }
}
