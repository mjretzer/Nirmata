using Gmsd.Agents.Execution.Planning;

namespace Gmsd.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of IInterviewEvidenceWriter for testing.
/// Tracks what would be written without actually writing to disk.
/// </summary>
public sealed class FakeInterviewEvidenceWriter : IInterviewEvidenceWriter
{
    /// <summary>
    /// The last transcript path that was "written".
    /// </summary>
    public string? LastTranscriptPath { get; private set; }

    /// <summary>
    /// The last summary path that was "written".
    /// </summary>
    public string? LastSummaryPath { get; private set; }

    /// <summary>
    /// The last project spec path that was "written".
    /// </summary>
    public string? LastProjectSpecPath { get; private set; }

    /// <summary>
    /// The last session passed to WriteTranscriptAsync.
    /// </summary>
    public InterviewSession? LastTranscriptSession { get; private set; }

    /// <summary>
    /// The last session passed to WriteSummaryAsync.
    /// </summary>
    public InterviewSession? LastSummarySession { get; private set; }

    /// <summary>
    /// The last spec passed to WriteSummaryAsync.
    /// </summary>
    public ProjectSpecification? LastSummarySpec { get; private set; }

    /// <summary>
    /// The last spec passed to WriteProjectSpecAsync.
    /// </summary>
    public ProjectSpecification? LastProjectSpec { get; private set; }

    /// <summary>
    /// The last spec JSON passed to WriteProjectSpecAsync.
    /// </summary>
    public string? LastProjectSpecJson { get; private set; }

    /// <summary>
    /// Simulates writing a transcript and returns a fake path.
    /// </summary>
    public Task<string> WriteTranscriptAsync(InterviewSession session, CancellationToken ct = default)
    {
        LastTranscriptSession = session;
        var runId = session.RunId ?? "unknown";
        LastTranscriptPath = $"/test/.aos/evidence/runs/{runId}/artifacts/interview.transcript.md";
        return Task.FromResult(LastTranscriptPath);
    }

    /// <summary>
    /// Simulates writing a summary and returns a fake path.
    /// </summary>
    public Task<string> WriteSummaryAsync(InterviewSession session, ProjectSpecification spec, CancellationToken ct = default)
    {
        LastSummarySession = session;
        LastSummarySpec = spec;
        var runId = session.RunId ?? "unknown";
        LastSummaryPath = $"/test/.aos/evidence/runs/{runId}/artifacts/interview.summary.md";
        return Task.FromResult(LastSummaryPath);
    }

    /// <summary>
    /// Simulates writing a project spec and returns a fake path.
    /// </summary>
    public Task<string> WriteProjectSpecAsync(ProjectSpecification spec, string specJson, CancellationToken ct = default)
    {
        LastProjectSpec = spec;
        LastProjectSpecJson = specJson;
        LastProjectSpecPath = "/test/.aos/spec/project.json";
        return Task.FromResult(LastProjectSpecPath);
    }
}
