using Gmsd.Agents.Execution.Verification.UatVerifier;
using Gmsd.Aos.Public;
using Gmsd.Aos.Public.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Gmsd.Agents.Execution.Verification.Issues;

/// <summary>
/// Schema for issue artifacts stored in spec layer.
/// </summary>
public sealed record Issue
{
    /// <summary>
    /// Schema version for the issue format.
    /// </summary>
    public required string SchemaVersion { get; init; }

    /// <summary>
    /// The issue identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The scope (files/areas affected).
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Steps to reproduce the failure.
    /// </summary>
    public required string Repro { get; init; }

    /// <summary>
    /// Expected behavior.
    /// </summary>
    public required string Expected { get; init; }

    /// <summary>
    /// Actual behavior observed.
    /// </summary>
    public required string Actual { get; init; }

    /// <summary>
    /// Severity level (critical, high, medium, low).
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// Parent UAT identifier that triggered this issue.
    /// </summary>
    public required string ParentUatId { get; init; }

    /// <summary>
    /// The task identifier.
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// The run identifier.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Timestamp when the issue was created.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Hash for de-duplication.
    /// </summary>
    public required string DedupHash { get; init; }

    /// <summary>
    /// The file path where the issue is stored (not serialized).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? FilePath { get; init; }
}

/// <summary>
/// Implementation of the issue writer that writes to the spec store.
/// </summary>
public sealed class IssueWriter : IIssueWriter
{
    private readonly IWorkspace _workspace;
    private readonly IDeterministicJsonSerializer _jsonSerializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="IssueWriter"/> class.
    /// </summary>
    public IssueWriter(IWorkspace workspace, IDeterministicJsonSerializer jsonSerializer)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
    }

    /// <inheritdoc />
    public async Task<IssueCreated> CreateIssueAsync(string taskId, string runId, UatCheckResult checkResult, CancellationToken ct = default)
    {
        var issuesDir = Path.Combine(_workspace.AosRootPath, "spec", "issues");
        if (!Directory.Exists(issuesDir))
        {
            Directory.CreateDirectory(issuesDir);
        }

        // Get next issue number
        var existingIssues = Directory.GetFiles(issuesDir, "ISS-*.json");
        var nextNumber = existingIssues.Length + 1;
        var issueId = $"ISS-{nextNumber:D4}";

        // Generate dedup hash based on scope + expected
        var dedupHash = ComputeDedupHash(checkResult.TargetPath ?? "", checkResult.Expected ?? "");

        // Check for existing issue with same hash (update if found)
        var existingIssue = FindExistingIssue(existingIssues, dedupHash);
        if (existingIssue != null)
        {
            // Update existing issue
            var updatedIssue = existingIssue with
            {
                RunId = runId,
                Timestamp = DateTimeOffset.UtcNow,
                Actual = checkResult.Actual ?? ""
            };

            var updateOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
            var json = _jsonSerializer.SerializeToString(updatedIssue, updateOptions);
            await File.WriteAllTextAsync(updatedIssue.FilePath!, json, ct);

            return new IssueCreated
            {
                Id = updatedIssue.Id,
                FilePath = updatedIssue.FilePath!
            };
        }

        var issuePath = Path.Combine(issuesDir, $"{issueId}.json");

        var severity = checkResult.IsRequired ? "high" : "medium";

        var issue = new Issue
        {
            SchemaVersion = "gmsd:aos:schema:issue:v1",
            Id = issueId,
            Scope = checkResult.TargetPath ?? "",
            Repro = $"Run UAT verification for task {taskId}. Check '{checkResult.CriterionId}' failed.",
            Expected = checkResult.Expected ?? checkResult.Message,
            Actual = checkResult.Actual ?? "failed",
            Severity = severity,
            ParentUatId = checkResult.CriterionId,
            TaskId = taskId,
            RunId = runId,
            Timestamp = DateTimeOffset.UtcNow,
            DedupHash = dedupHash
        };

        var serializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        var issueJson = _jsonSerializer.SerializeToString(issue, serializerOptions);
        await File.WriteAllTextAsync(issuePath, issueJson, ct);

        return new IssueCreated
        {
            Id = issueId,
            FilePath = issuePath
        };
    }

    private static string ComputeDedupHash(string scope, string expected)
    {
        var input = $"{scope}:{expected}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }

    private Issue? FindExistingIssue(string[] existingFiles, string dedupHash)
    {
        foreach (var file in existingFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var issue = JsonSerializer.Deserialize<Issue>(json);
                if (issue?.DedupHash == dedupHash)
                {
                    return issue with { FilePath = file };
                }
            }
            catch
            {
                // Skip files that can't be read
                continue;
            }
        }

        return null;
    }
}
