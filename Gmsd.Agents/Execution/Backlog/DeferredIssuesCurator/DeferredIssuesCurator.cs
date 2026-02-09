using System.Text.Json;
using Gmsd.Aos.Public;
using Gmsd.Aos.Public.Services;

namespace Gmsd.Agents.Execution.Backlog.DeferredIssuesCurator;

/// <summary>
/// Implementation of the Deferred Issues Curator.
/// Triages issues from .aos/spec/issues/ and routes urgent items into the main execution loop.
/// </summary>
public sealed class DeferredIssuesCurator : IDeferredIssuesCurator
{
    private readonly IDeterministicJsonSerializer _jsonSerializer;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions NdjsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="DeferredIssuesCurator"/> class.
    /// </summary>
    public DeferredIssuesCurator(IDeterministicJsonSerializer jsonSerializer)
    {
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
    }

    /// <inheritdoc />
    public async Task<DeferredIssuesCurationResult> CurateAsync(DeferredIssuesCurationRequest request, CancellationToken ct = default)
    {
        try
        {
            var issuesDir = Path.Combine(request.WorkspaceRoot, ".aos", "spec", "issues");
            if (!Directory.Exists(issuesDir))
            {
                return new DeferredIssuesCurationResult
                {
                    IsSuccess = true,
                    Recommendations = Array.Empty<IssueRoutingRecommendation>()
                };
            }

            var issueFiles = Directory.GetFiles(issuesDir, "ISS-*.json");
            var recommendations = new List<IssueRoutingRecommendation>();

            foreach (var file in issueFiles)
            {
                ct.ThrowIfCancellationRequested();

                var issueId = Path.GetFileNameWithoutExtension(file);

                // Skip if specific issue IDs were requested and this isn't one of them
                if (request.IssueIds.Count > 0 && !request.IssueIds.Contains(issueId))
                {
                    continue;
                }

                var recommendation = await TriageIssueAsync(
                    file,
                    issueId,
                    request.MinimumSeverityForMainLoop,
                    request.WorkspaceRoot,
                    request.WriteEvents,
                    request.ApplyDecisions,
                    ct);

                recommendations.Add(recommendation);
            }

            return new DeferredIssuesCurationResult
            {
                IsSuccess = true,
                Recommendations = recommendations
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new DeferredIssuesCurationResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<IssueRoutingRecommendation> TriageIssueAsync(
        string filePath,
        string issueId,
        string minimumSeverityForMainLoop,
        string workspaceRoot,
        bool writeEvents,
        bool applyDecisions,
        CancellationToken ct)
    {
        var triagedAt = DateTimeOffset.UtcNow.ToString("O");

        // Read issue file
        var json = await File.ReadAllTextAsync(filePath, ct);
        var issue = JsonSerializer.Deserialize<IssueFile>(json, JsonOptions);

        if (issue == null)
        {
            return new IssueRoutingRecommendation
            {
                IssueId = issueId,
                Severity = "unknown",
                Decision = RoutingDecision.Discarded,
                Rationale = "Failed to parse issue file",
                TriagedAt = triagedAt,
                IssueFileUpdated = false,
                EventWritten = false
            };
        }

        // Assess severity and make routing decision
        var severity = issue.Severity?.ToLowerInvariant() ?? "medium";
        var (decision, rationale) = AssessRouting(severity, minimumSeverityForMainLoop, issue);

        var recommendation = new IssueRoutingRecommendation
        {
            IssueId = issueId,
            Severity = severity,
            Decision = decision,
            Rationale = rationale,
            TriagedAt = triagedAt
        };

        // Update issue file if requested
        if (applyDecisions)
        {
            recommendation = recommendation with
            {
                IssueFileUpdated = await UpdateIssueFileAsync(filePath, issue, recommendation, ct)
            };
        }

        // Write event if requested
        if (writeEvents)
        {
            recommendation = recommendation with
            {
                EventWritten = await WriteTriageEventAsync(workspaceRoot, recommendation, ct)
            };
        }

        return recommendation;
    }

    private static (RoutingDecision Decision, string Rationale) AssessRouting(
        string severity,
        string minimumSeverityForMainLoop,
        IssueFile issue)
    {
        // Severity ranking: critical > high > medium > low
        var severityRanking = new Dictionary<string, int>
        {
            ["critical"] = 4,
            ["high"] = 3,
            ["medium"] = 2,
            ["low"] = 1
        };

        var issueSeverityRank = severityRanking.GetValueOrDefault(severity, 2);
        var minSeverityRank = severityRanking.GetValueOrDefault(minimumSeverityForMainLoop.ToLowerInvariant(), 3);

        // Check if issue already has a routing decision (re-triage)
        if (!string.IsNullOrEmpty(issue.RoutingDecision))
        {
            if (issue.RoutingDecision == "discarded")
            {
                return (RoutingDecision.Discarded, "Issue previously discarded");
            }
        }

        // Check status - resolved issues can be discarded
        if (issue.Status?.ToLowerInvariant() == "resolved")
        {
            return (RoutingDecision.Discarded, "Issue already resolved");
        }

        // Route based on severity threshold
        if (issueSeverityRank >= minSeverityRank)
        {
            return (RoutingDecision.MainLoop, $"Severity '{severity}' meets or exceeds minimum threshold '{minimumSeverityForMainLoop}'");
        }

        return (RoutingDecision.Deferred, $"Severity '{severity}' below minimum threshold '{minimumSeverityForMainLoop}'");
    }

    private async Task<bool> UpdateIssueFileAsync(
        string filePath,
        IssueFile issue,
        IssueRoutingRecommendation recommendation,
        CancellationToken ct)
    {
        try
        {
            var routingDecision = recommendation.Decision switch
            {
                RoutingDecision.MainLoop => "main-loop",
                RoutingDecision.Deferred => "deferred",
                RoutingDecision.Discarded => "discarded",
                _ => "deferred"
            };

            var updatedIssue = issue with
            {
                Status = "triaged",
                Severity = recommendation.Severity,
                RoutingDecision = routingDecision,
                TriagedAt = recommendation.TriagedAt,
                Rationale = recommendation.Rationale
            };

            var json = _jsonSerializer.SerializeToString(updatedIssue, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> WriteTriageEventAsync(
        string workspaceRoot,
        IssueRoutingRecommendation recommendation,
        CancellationToken ct)
    {
        try
        {
            var eventsPath = Path.Combine(workspaceRoot, ".aos", "state", "events.ndjson");
            var eventsDir = Path.GetDirectoryName(eventsPath);

            if (!string.IsNullOrEmpty(eventsDir) && !Directory.Exists(eventsDir))
            {
                Directory.CreateDirectory(eventsDir);
            }

            var routingDecision = recommendation.Decision switch
            {
                RoutingDecision.MainLoop => "main-loop",
                RoutingDecision.Deferred => "deferred",
                RoutingDecision.Discarded => "discarded",
                _ => "deferred"
            };

            var evt = new TriageEvent
            {
                SchemaVersion = 1,
                EventType = "triage",
                TimestampUtc = recommendation.TriagedAt,
                IssueId = recommendation.IssueId,
                Severity = recommendation.Severity,
                RoutingDecision = routingDecision,
                Rationale = recommendation.Rationale
            };

            var line = JsonSerializer.Serialize(evt, NdjsonOptions);

            // Append with proper LF handling
            if (File.Exists(eventsPath))
            {
                using var stream = new FileStream(eventsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

                // Ensure trailing LF if file is not empty
                if (stream.Length > 0)
                {
                    stream.Seek(-1, SeekOrigin.End);
                    var last = stream.ReadByte();
                    if (last != '\n')
                    {
                        stream.Seek(0, SeekOrigin.End);
                        stream.WriteByte((byte)'\n');
                    }
                }

                stream.Seek(0, SeekOrigin.End);
                var lineBytes = System.Text.Encoding.UTF8.GetBytes(line + '\n');
                await stream.WriteAsync(lineBytes, ct);
            }
            else
            {
                await File.WriteAllTextAsync(eventsPath, line + '\n', ct);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed record IssueFile
    {
        public int SchemaVersion { get; init; }
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? Description { get; init; }
        public string? Severity { get; init; }
        public string? Status { get; init; }
        public string? RoutingDecision { get; init; }
        public string? TriagedAt { get; init; }
        public string? Rationale { get; init; }
    }

    private sealed record TriageEvent
    {
        public int SchemaVersion { get; init; }
        public string? EventType { get; init; }
        public string? TimestampUtc { get; init; }
        public string? IssueId { get; init; }
        public string? Severity { get; init; }
        public string? RoutingDecision { get; init; }
        public string? Rationale { get; init; }
    }
}
