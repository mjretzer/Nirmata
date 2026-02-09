using Gmsd.Aos.Public;
using System.Text;

namespace Gmsd.Agents.Execution.Planning;

/// <summary>
/// Writes interview evidence files to the workspace.
/// </summary>
public interface IInterviewEvidenceWriter
{
    /// <summary>
    /// Writes the interview transcript to the evidence directory.
    /// </summary>
    /// <param name="session">The interview session.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The absolute path to the written file.</returns>
    Task<string> WriteTranscriptAsync(InterviewSession session, CancellationToken ct = default);

    /// <summary>
    /// Writes the interview summary to the evidence directory.
    /// </summary>
    /// <param name="session">The interview session.</param>
    /// <param name="spec">The generated project specification.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The absolute path to the written file.</returns>
    Task<string> WriteSummaryAsync(InterviewSession session, ProjectSpecification spec, CancellationToken ct = default);

    /// <summary>
    /// Writes the project specification to the spec directory.
    /// </summary>
    /// <param name="spec">The project specification.</param>
    /// <param name="specJson">The JSON representation of the spec.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The absolute path to the written file.</returns>
    Task<string> WriteProjectSpecAsync(ProjectSpecification spec, string specJson, CancellationToken ct = default);
}

/// <summary>
/// Default implementation of the interview evidence writer.
/// </summary>
public sealed class InterviewEvidenceWriter : IInterviewEvidenceWriter
{
    private readonly IWorkspace _workspace;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterviewEvidenceWriter"/> class.
    /// </summary>
    public InterviewEvidenceWriter(IWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    /// <inheritdoc />
    public async Task<string> WriteTranscriptAsync(InterviewSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var runId = session.RunId ?? "unknown";
        var artifactsDir = GetArtifactsDirectory(runId);

        if (!Directory.Exists(artifactsDir))
        {
            Directory.CreateDirectory(artifactsDir);
        }

        var filePath = Path.Combine(artifactsDir, "interview.transcript.md");
        var content = GenerateTranscriptMarkdown(session);

        await File.WriteAllTextAsync(filePath, content, ct);

        return filePath;
    }

    /// <inheritdoc />
    public async Task<string> WriteSummaryAsync(InterviewSession session, ProjectSpecification spec, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(spec);

        var runId = session.RunId ?? "unknown";
        var artifactsDir = GetArtifactsDirectory(runId);

        if (!Directory.Exists(artifactsDir))
        {
            Directory.CreateDirectory(artifactsDir);
        }

        var filePath = Path.Combine(artifactsDir, "interview.summary.md");
        var content = GenerateSummaryMarkdown(session, spec);

        await File.WriteAllTextAsync(filePath, content, ct);

        return filePath;
    }

    /// <inheritdoc />
    public async Task<string> WriteProjectSpecAsync(ProjectSpecification spec, string specJson, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        // Use workspace to get the spec directory path
        var specPath = _workspace.GetAbsolutePathForArtifactId("project");
        var specDir = Path.GetDirectoryName(specPath);

        if (!string.IsNullOrEmpty(specDir) && !Directory.Exists(specDir))
        {
            Directory.CreateDirectory(specDir);
        }

        // Ensure LF line endings
        var normalizedJson = specJson.Replace("\r\n", "\n");
        await File.WriteAllTextAsync(specPath, normalizedJson, ct);

        return specPath;
    }

    private string GetArtifactsDirectory(string runId)
    {
        // Get the workspace root path
        var workspaceRoot = _workspace.GetAbsolutePathForArtifactId("workspace");
        return Path.Combine(workspaceRoot, ".aos", "evidence", "runs", runId, "artifacts");
    }

    private static string GenerateTranscriptMarkdown(InterviewSession session)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Interview Transcript");
        builder.AppendLine();
        builder.AppendLine($"- **Session ID:** {session.SessionId}");
        builder.AppendLine($"- **Started:** {session.StartedAt:yyyy-MM-dd HH:mm:ss UTC}");
        if (session.CompletedAt.HasValue)
        {
            builder.AppendLine($"- **Completed:** {session.CompletedAt.Value:yyyy-MM-dd HH:mm:ss UTC}");
        }
        builder.AppendLine($"- **Run ID:** {session.RunId ?? "N/A"}");
        builder.AppendLine($"- **Total Q&A Pairs:** {session.QAPairs.Count}");
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine();

        var qaByPhase = session.QAPairs.GroupBy(qa => qa.Phase);

        foreach (var phaseGroup in qaByPhase)
        {
            builder.AppendLine($"## {phaseGroup.Key} Phase");
            builder.AppendLine();

            var index = 1;
            foreach (var qa in phaseGroup)
            {
                builder.AppendLine($"### Q{index}: {qa.Question}");
                builder.AppendLine();
                builder.AppendLine($"> {qa.Answer}");
                builder.AppendLine();
                builder.AppendLine($"*Timestamp: {qa.Timestamp:yyyy-MM-dd HH:mm:ss UTC}*");
                builder.AppendLine();
                index++;
            }

            builder.AppendLine("---");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string GenerateSummaryMarkdown(InterviewSession session, ProjectSpecification spec)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Interview Summary");
        builder.AppendLine();
        builder.AppendLine($"- **Session ID:** {session.SessionId}");
        builder.AppendLine($"- **Run ID:** {session.RunId ?? "N/A"}");
        builder.AppendLine($"- **Project:** {spec.Name}");
        builder.AppendLine($"- **Completed:** {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        builder.AppendLine();

        builder.AppendLine("## Key Decisions");
        builder.AppendLine();
        builder.AppendLine($"- **Technology Stack:** {spec.TechnologyStack ?? "Not specified"}");
        builder.AppendLine($"- **Target Audience:** {spec.TargetAudience ?? "Not specified"}");
        builder.AppendLine();

        if (spec.Goals.Count > 0)
        {
            builder.AppendLine("## Project Goals");
            builder.AppendLine();
            foreach (var goal in spec.Goals)
            {
                builder.AppendLine($"- {goal}");
            }
            builder.AppendLine();
        }

        if (spec.KeyFeatures.Count > 0)
        {
            builder.AppendLine("## Key Features");
            builder.AppendLine();
            foreach (var feature in spec.KeyFeatures)
            {
                builder.AppendLine($"- {feature}");
            }
            builder.AppendLine();
        }

        if (spec.Constraints.Count > 0)
        {
            builder.AppendLine("## Constraints");
            builder.AppendLine();
            foreach (var constraint in spec.Constraints)
            {
                builder.AppendLine($"- {constraint}");
            }
            builder.AppendLine();
        }

        if (spec.Assumptions.Count > 0)
        {
            builder.AppendLine("## Assumptions");
            builder.AppendLine();
            foreach (var assumption in spec.Assumptions)
            {
                builder.AppendLine($"- {assumption}");
            }
            builder.AppendLine();
        }

        builder.AppendLine("## Project Description");
        builder.AppendLine();
        builder.AppendLine(spec.Description);
        builder.AppendLine();

        builder.AppendLine("## Schema Information");
        builder.AppendLine();
        builder.AppendLine($"- **Schema Version:** {spec.Schema}");
        builder.AppendLine($"- **Generated At:** {spec.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}");
        builder.AppendLine();

        return builder.ToString();
    }
}
