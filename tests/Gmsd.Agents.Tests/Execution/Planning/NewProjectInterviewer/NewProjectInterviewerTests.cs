using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.Planning;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Agents.Tests.Fakes;
using Gmsd.Aos.Public;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Planning;

using Interviewer = Gmsd.Agents.Execution.Planning.NewProjectInterviewer;

public class NewProjectInterviewerTests
{
    private readonly FakeLlmProvider _fakeLlmProvider;
    private readonly Mock<IProjectSpecGenerator> _specGeneratorMock;
    private readonly Mock<IInterviewEvidenceWriter> _evidenceWriterMock;
    private readonly Mock<ISpecStore> _specStoreMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Interviewer _sut;

    public NewProjectInterviewerTests()
    {
        _fakeLlmProvider = new FakeLlmProvider();
        _specGeneratorMock = new Mock<IProjectSpecGenerator>();
        _evidenceWriterMock = new Mock<IInterviewEvidenceWriter>();
        _specStoreMock = new Mock<ISpecStore>();
        _workspaceMock = new Mock<IWorkspace>();

        _sut = new Interviewer(
            _fakeLlmProvider,
            _specGeneratorMock.Object,
            _evidenceWriterMock.Object,
            _specStoreMock.Object,
            _workspaceMock.Object
        );
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

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

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
        await _sut.ConductInterviewAsync(session);

        // Assert
        _fakeLlmProvider.Requests.Should().HaveCount(3);
        _fakeLlmProvider.Requests[0].Messages.Should().Contain(m => m.Role == LlmMessageRole.System);
        _fakeLlmProvider.Requests[0].Messages.Should().Contain(m => m.Role == LlmMessageRole.User);
    }

    [Fact]
    public async Task ConductInterviewAsync_PopulatesQAPairsInSession()
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
        await _sut.ConductInterviewAsync(session);

        // Assert
        session.QAPairs.Should().HaveCount(5); // 2 discovery + 2 clarification + 1 confirmation
        session.QAPairs.Should().Contain(qa => qa.Phase == InterviewPhase.Discovery);
        session.QAPairs.Should().Contain(qa => qa.Phase == InterviewPhase.Clarification);
        session.QAPairs.Should().Contain(qa => qa.Phase == InterviewPhase.Confirmation);
    }

    [Fact]
    public async Task ConductInterviewAsync_PopulatesProjectDraft()
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
        await _sut.ConductInterviewAsync(session);

        // Assert
        session.ProjectDraft.Should().NotBeNull();
        session.ProjectDraft!.Name.Should().NotBeNullOrEmpty();
        session.ProjectDraft.Description.Should().NotBeNullOrEmpty();
        session.ProjectDraft.TechnologyStack.Should().NotBeNullOrEmpty();
        session.ProjectDraft.Goals.Should().NotBeEmpty();
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
            Schema = "gmsd:aos:schema:project:v1",
            Name = "Test Project",
            Description = "A test project for unit testing",
            TechnologyStack = ".NET/C#",
            Goals = ["Test goal 1", "Test goal 2"],
            TargetAudience = "Developers",
            KeyFeatures = ["Feature 1", "Feature 2"]
        };
    }
}
