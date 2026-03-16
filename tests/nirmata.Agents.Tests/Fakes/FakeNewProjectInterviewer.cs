using nirmata.Agents.Execution.Planning;

namespace nirmata.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of INewProjectInterviewer for testing.
/// Returns a canned interview result for testing purposes.
/// </summary>
public sealed class FakeNewProjectInterviewer : INewProjectInterviewer
{
    /// <summary>
    /// The canned interview result to return. Configure this before testing.
    /// </summary>
    public InterviewResult? CannedResult { get; set; }

    /// <summary>
    /// Gets the last interview session that was passed to ConductInterviewAsync.
    /// Useful for test assertions.
    /// </summary>
    public InterviewSession? LastSession { get; private set; }

    /// <summary>
    /// Conducts an interview session and returns a canned result.
    /// </summary>
    public Task<InterviewResult> ConductInterviewAsync(InterviewSession session, CancellationToken ct = default)
    {
        LastSession = session;

        if (CannedResult != null)
        {
            return Task.FromResult(CannedResult);
        }

        // Return default success result
        var projectSpec = new ProjectSpecification
        {
            Name = "Test Project",
            Description = "A test project for E2E testing"
        };

        return Task.FromResult(new InterviewResult
        {
            Success = true,
            ProjectSpec = projectSpec,
            ProjectSpecJson = """{"name":"Test Project","description":"A test project for E2E testing"}""",
            TranscriptMarkdown = "# Interview Transcript\n\nQ: What is the project name?\nA: Test Project",
            SummaryMarkdown = "## Summary\n\nCreated a test project specification.",
            Session = session,
            Artifacts = new[]
            {
                new InterviewArtifact
                {
                    ArtifactId = "project.json",
                    FileName = "project.json",
                    FilePath = "/test/project.json",
                    ContentType = "application/json",
                    Content = """{"name":"Test Project"}"""
                }
            }
        });
    }
}
