using FluentAssertions;
using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using nirmata.Agents.Execution.Planning;
using nirmata.Agents.Persistence.Runs;
using nirmata.Agents.Tests.Fakes;
using nirmata.Aos.Public;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Planning;

using Interviewer = nirmata.Agents.Execution.Planning.NewProjectInterviewer;

public class NewProjectInterviewerTests
{
    private readonly FakeLlmProvider _fakeLlmProvider;
    private readonly Mock<IProjectSpecGenerator> _specGeneratorMock;
    private readonly Mock<IInterviewEvidenceWriter> _evidenceWriterMock;
    private readonly Mock<ISpecStore> _specStoreMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Interviewer _sut;

    // Structured JSON responses matching the prompts in InterviewPrompts.cs
    private const string DiscoveryJsonResponse = """
    {
      "qaPairs": [
        { "question": "What problem does this project solve?", "answer": "It automates software project management and orchestration." },
        { "question": "Who is the target audience?", "answer": "Software developers and development teams." },
        { "question": "What are the primary goals?", "answer": "Improve project organization and streamline development workflow." }
      ],
      "draft": {
        "name": "Test Project",
        "description": "A software development project for managing and organizing projects efficiently",
        "technologyStack": ".NET/C#",
        "goals": ["Improve project organization", "Streamline development workflow"],
        "targetAudience": "Software developers and development teams",
        "keyFeatures": ["Project organization", "Workflow management"],
        "constraints": ["Must run on Windows"],
        "assumptions": ["Team is familiar with .NET"]
      }
    }
    """;

    private const string ClarificationJsonResponse = """
    {
      "qaPairs": [
        { "question": "What integration boundaries exist?", "answer": "Integration with LLM providers and MCP servers." },
        { "question": "Are there security or compliance requirements?", "answer": "API keys must not be logged." }
      ],
      "draftUpdates": {
        "keyFeatures": ["LLM integration", "MCP server support"],
        "constraints": ["API keys must not be logged"],
        "assumptions": ["LLM providers are available via HTTP"]
      }
    }
    """;

    private const string ConfirmationJsonResponse = """
    {
      "qaPairs": [
        { "question": "Do these requirements accurately capture the project needs?", "answer": "Yes, this covers the main requirements." }
      ],
      "confirmedDraft": {
        "name": "Test Project",
        "description": "A software development project for managing and organizing projects efficiently",
        "technologyStack": ".NET/C#",
        "goals": ["Improve project organization", "Streamline development workflow"],
        "targetAudience": "Software developers and development teams",
        "keyFeatures": ["Project organization", "Workflow management", "LLM integration", "MCP server support"],
        "constraints": ["Must run on Windows", "API keys must not be logged"],
        "assumptions": ["Team is familiar with .NET", "LLM providers are available via HTTP"]
      }
    }
    """;

    public NewProjectInterviewerTests()
    {
        _fakeLlmProvider = new FakeLlmProvider();
        _specGeneratorMock = new Mock<IProjectSpecGenerator>();
        _evidenceWriterMock = new Mock<IInterviewEvidenceWriter>();
        _specStoreMock = new Mock<ISpecStore>();
        _workspaceMock = new Mock<IWorkspace>();

        // AosRootPath is required for session persistence between phases
        _workspaceMock.Setup(x => x.AosRootPath).Returns(Path.Combine(Path.GetTempPath(), "nirmata-test-" + Guid.NewGuid().ToString("N")));

        _sut = new Interviewer(
            _fakeLlmProvider,
            _specGeneratorMock.Object,
            _evidenceWriterMock.Object,
            _specStoreMock.Object,
            _workspaceMock.Object
        );
    }

    private void EnqueueStructuredResponses()
    {
        _fakeLlmProvider
            .EnqueueTextResponse(DiscoveryJsonResponse)
            .EnqueueTextResponse(ClarificationJsonResponse)
            .EnqueueTextResponse(ConfirmationJsonResponse);
    }

    [Fact]
    public async Task ConductInterviewAsync_WithValidSession_CompletesSuccessfully()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-001" };
        var expectedSpec = CreateValidProjectSpec();
        var expectedArtifacts = new List<InterviewArtifact>
        {
            new()
            {
                ArtifactId = "interview-transcript",
                FileName = "interview.transcript.md",
                FilePath = "/tmp/transcript.md",
                ContentType = "text/markdown",
                Content = "transcript"
            }
        };

        EnqueueStructuredResponses();

        _specGeneratorMock
            .Setup(x => x.GenerateFromSession(session))
            .Returns(expectedSpec);

        _specGeneratorMock
            .Setup(x => x.Validate(expectedSpec))
            .Returns(SpecValidationResult.Success());

        _specGeneratorMock
            .Setup(x => x.SerializeToJson(expectedSpec))
            .Returns("{ \"name\": \"Test Project\" }");

        _evidenceWriterMock
            .Setup(x => x.WriteTranscriptAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.transcript.md");

        _evidenceWriterMock
            .Setup(x => x.WriteSummaryAsync(session, expectedSpec, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.summary.md");

        _workspaceMock
            .Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns("/tmp/project.json");

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        result.Success.Should().BeTrue();
        result.ProjectSpec.Should().Be(expectedSpec);
        result.Session.Should().Be(session);
        session.State.Should().Be(InterviewState.Complete);
        session.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ConductInterviewAsync_SessionIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.ConductInterviewAsync(null!));
    }

    [Fact]
    public async Task ConductInterviewAsync_WhenSpecValidationFails_ReturnsFailureResult()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-001" };
        var expectedSpec = CreateValidProjectSpec();

        EnqueueStructuredResponses();

        _specGeneratorMock
            .Setup(x => x.GenerateFromSession(session))
            .Returns(expectedSpec);

        _specGeneratorMock
            .Setup(x => x.Validate(expectedSpec))
            .Returns(SpecValidationResult.Failure(["Project name is required"]));

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Project name is required");
        session.State.Should().Be(InterviewState.Failed);
    }

    [Fact]
    public async Task ConductInterviewAsync_WhenLlmThrowsException_ReturnsFailureResult()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-001" };

        _fakeLlmProvider.EnqueueException(new LlmProviderException("fake", "LLM service unavailable"));

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("LLM service unavailable");
        session.State.Should().Be(InterviewState.Failed);
    }

    [Fact]
    public async Task ConductInterviewAsync_CallsLlmProviderForEachPhase()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-001" };
        var expectedSpec = CreateValidProjectSpec();

        EnqueueStructuredResponses();

        _specGeneratorMock
            .Setup(x => x.GenerateFromSession(session))
            .Returns(expectedSpec);

        _specGeneratorMock
            .Setup(x => x.Validate(expectedSpec))
            .Returns(SpecValidationResult.Success());

        _specGeneratorMock
            .Setup(x => x.SerializeToJson(expectedSpec))
            .Returns("{}");

        _evidenceWriterMock
            .Setup(x => x.WriteTranscriptAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.transcript.md");

        _evidenceWriterMock
            .Setup(x => x.WriteSummaryAsync(session, expectedSpec, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.summary.md");

        _workspaceMock
            .Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns("/tmp/project.json");

        // Act
        await _sut.ConductInterviewAsync(session);

        // Assert
        _fakeLlmProvider.Requests.Should().HaveCount(3);
        _fakeLlmProvider.Requests[0].Messages.Should().Contain(m => m.Role == LlmMessageRole.System);
        _fakeLlmProvider.Requests[0].Messages.Should().Contain(m => m.Role == LlmMessageRole.User);
    }

    [Fact]
    public async Task ConductInterviewAsync_PopulatesQAPairsFromLlmResponse()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-001" };
        var expectedSpec = CreateValidProjectSpec();

        EnqueueStructuredResponses();

        _specGeneratorMock
            .Setup(x => x.GenerateFromSession(session))
            .Returns(expectedSpec);

        _specGeneratorMock
            .Setup(x => x.Validate(expectedSpec))
            .Returns(SpecValidationResult.Success());

        _specGeneratorMock
            .Setup(x => x.SerializeToJson(expectedSpec))
            .Returns("{}");

        _evidenceWriterMock
            .Setup(x => x.WriteTranscriptAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.transcript.md");

        _evidenceWriterMock
            .Setup(x => x.WriteSummaryAsync(session, expectedSpec, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.summary.md");

        _workspaceMock
            .Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns("/tmp/project.json");

        // Act
        await _sut.ConductInterviewAsync(session);

        // Assert — 3 discovery + 2 clarification + 1 confirmation = 6 Q&A pairs from structured JSON
        session.QAPairs.Should().HaveCount(6);
        session.QAPairs.Should().Contain(qa => qa.Phase == InterviewPhase.Discovery);
        session.QAPairs.Should().Contain(qa => qa.Phase == InterviewPhase.Clarification);
        session.QAPairs.Should().Contain(qa => qa.Phase == InterviewPhase.Confirmation);
    }

    [Fact]
    public async Task ConductInterviewAsync_PopulatesProjectDraftFromLlmResponse()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-001" };
        var expectedSpec = CreateValidProjectSpec();

        EnqueueStructuredResponses();

        _specGeneratorMock
            .Setup(x => x.GenerateFromSession(session))
            .Returns(expectedSpec);

        _specGeneratorMock
            .Setup(x => x.Validate(expectedSpec))
            .Returns(SpecValidationResult.Success());

        _specGeneratorMock
            .Setup(x => x.SerializeToJson(expectedSpec))
            .Returns("{}");

        _evidenceWriterMock
            .Setup(x => x.WriteTranscriptAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.transcript.md");

        _evidenceWriterMock
            .Setup(x => x.WriteSummaryAsync(session, expectedSpec, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.summary.md");

        _workspaceMock
            .Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns("/tmp/project.json");

        // Act
        await _sut.ConductInterviewAsync(session);

        // Assert — draft is populated from the confirmed draft in the confirmation phase
        session.ProjectDraft.Should().NotBeNull();
        session.ProjectDraft!.Name.Should().Be("Test Project");
        session.ProjectDraft.Description.Should().Contain("software development project");
        session.ProjectDraft.TechnologyStack.Should().Be(".NET/C#");
        session.ProjectDraft.Goals.Should().HaveCountGreaterOrEqualTo(2);
        session.ProjectDraft.KeyFeatures.Should().Contain("LLM integration");
        session.ProjectDraft.Constraints.Should().NotBeEmpty();
        session.ProjectDraft.Assumptions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ConductInterviewAsync_WithNonJsonLlmResponse_UsesFallbackQAPairs()
    {
        // Arrange — LLM returns plain text instead of structured JSON
        var session = new InterviewSession { RunId = "RUN-001" };
        var expectedSpec = CreateValidProjectSpec();

        _fakeLlmProvider
            .EnqueueTextResponse("Just a plain text discovery response")
            .EnqueueTextResponse("Just a plain text clarification response")
            .EnqueueTextResponse("Just a plain text confirmation response");

        _specGeneratorMock
            .Setup(x => x.GenerateFromSession(session))
            .Returns(expectedSpec);

        _specGeneratorMock
            .Setup(x => x.Validate(expectedSpec))
            .Returns(SpecValidationResult.Success());

        _specGeneratorMock
            .Setup(x => x.SerializeToJson(expectedSpec))
            .Returns("{}");

        _evidenceWriterMock
            .Setup(x => x.WriteTranscriptAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.transcript.md");

        _evidenceWriterMock
            .Setup(x => x.WriteSummaryAsync(session, expectedSpec, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.summary.md");

        _workspaceMock
            .Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns("/tmp/project.json");

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert — fallback creates 1 Q&A pair per phase (3 total)
        result.Success.Should().BeTrue();
        session.QAPairs.Should().HaveCount(3);
        session.QAPairs[0].Answer.Should().Contain("plain text discovery");
    }

    [Fact]
    public async Task ConductInterviewAsync_WritesEvidenceArtifacts()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-001" };
        var expectedSpec = CreateValidProjectSpec();

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        _specGeneratorMock
            .Setup(x => x.GenerateFromSession(session))
            .Returns(expectedSpec);

        _specGeneratorMock
            .Setup(x => x.Validate(expectedSpec))
            .Returns(SpecValidationResult.Success());

        _specGeneratorMock
            .Setup(x => x.SerializeToJson(expectedSpec))
            .Returns("{}");

        _evidenceWriterMock
            .Setup(x => x.WriteTranscriptAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.transcript.md");

        _evidenceWriterMock
            .Setup(x => x.WriteSummaryAsync(session, expectedSpec, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.summary.md");

        _workspaceMock
            .Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns("/tmp/project.json");

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        _evidenceWriterMock.Verify(x => x.WriteTranscriptAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _evidenceWriterMock.Verify(x => x.WriteSummaryAsync(session, expectedSpec, It.IsAny<CancellationToken>()), Times.Once);
        result.Artifacts.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConductInterviewAsync_GeneratesTranscriptMarkdown()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-001" };
        var expectedSpec = CreateValidProjectSpec();

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        _specGeneratorMock
            .Setup(x => x.GenerateFromSession(session))
            .Returns(expectedSpec);

        _specGeneratorMock
            .Setup(x => x.Validate(expectedSpec))
            .Returns(SpecValidationResult.Success());

        _specGeneratorMock
            .Setup(x => x.SerializeToJson(expectedSpec))
            .Returns("{}");

        _evidenceWriterMock
            .Setup(x => x.WriteTranscriptAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.transcript.md");

        _evidenceWriterMock
            .Setup(x => x.WriteSummaryAsync(session, expectedSpec, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.summary.md");

        _workspaceMock
            .Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns("/tmp/project.json");

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        result.TranscriptMarkdown.Should().NotBeNullOrEmpty();
        result.TranscriptMarkdown.Should().Contain("# Interview Transcript");
        result.TranscriptMarkdown.Should().Contain(session.SessionId);
    }

    [Fact]
    public async Task ConductInterviewAsync_GeneratesSummaryMarkdown()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-001" };
        var expectedSpec = CreateValidProjectSpec();

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        _specGeneratorMock
            .Setup(x => x.GenerateFromSession(session))
            .Returns(expectedSpec);

        _specGeneratorMock
            .Setup(x => x.Validate(expectedSpec))
            .Returns(SpecValidationResult.Success());

        _specGeneratorMock
            .Setup(x => x.SerializeToJson(expectedSpec))
            .Returns("{}");

        _evidenceWriterMock
            .Setup(x => x.WriteTranscriptAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.transcript.md");

        _evidenceWriterMock
            .Setup(x => x.WriteSummaryAsync(session, expectedSpec, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.summary.md");

        _workspaceMock
            .Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns("/tmp/project.json");

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        result.SummaryMarkdown.Should().NotBeNullOrEmpty();
        result.SummaryMarkdown.Should().Contain("# Interview Summary");
        result.SummaryMarkdown.Should().Contain(expectedSpec.Name);
    }

    [Fact]
    public async Task ConductInterviewAsync_UpdatesSessionStateThroughPhases()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-001" };
        var expectedSpec = CreateValidProjectSpec();
        var stateTransitions = new List<InterviewState>();

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        _specGeneratorMock
            .Setup(x => x.GenerateFromSession(session))
            .Returns(expectedSpec)
            .Callback(() => stateTransitions.Add(session.State));

        _specGeneratorMock
            .Setup(x => x.Validate(expectedSpec))
            .Returns(SpecValidationResult.Success());

        _specGeneratorMock
            .Setup(x => x.SerializeToJson(expectedSpec))
            .Returns("{}");

        _evidenceWriterMock
            .Setup(x => x.WriteTranscriptAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.transcript.md");

        _evidenceWriterMock
            .Setup(x => x.WriteSummaryAsync(session, expectedSpec, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/interview.summary.md");

        _workspaceMock
            .Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns("/tmp/project.json");

        // Act
        await _sut.ConductInterviewAsync(session);

        // Assert
        session.State.Should().Be(InterviewState.Complete);
        session.CurrentPhase.Should().Be(InterviewPhase.Confirmation);
    }

    private static ProjectSpecification CreateValidProjectSpec()
    {
        return new ProjectSpecification
        {
            Schema = "nirmata:aos:schema:project:v1",
            Name = "Test Project",
            Description = "A test project for unit testing",
            TechnologyStack = ".NET/C#",
            Goals = ["Test goal 1", "Test goal 2"],
            TargetAudience = "Developers",
            KeyFeatures = ["Feature 1", "Feature 2"]
        };
    }
}
