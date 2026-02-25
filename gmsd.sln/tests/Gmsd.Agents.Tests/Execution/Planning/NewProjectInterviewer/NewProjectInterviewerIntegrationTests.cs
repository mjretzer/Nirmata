using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.Planning;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Agents.Tests.Fakes;
using Gmsd.Aos.Engine.Validation;
using Gmsd.Aos.Engine.Workspace;
using Gmsd.Aos.Public;
using Moq;
using System.Text.Json;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Planning;

using Interviewer = Gmsd.Agents.Execution.Planning.NewProjectInterviewer;

/// <summary>
/// Integration tests for the NewProjectInterviewer that verify end-to-end interview flow
/// with actual file system operations.
/// </summary>
public class NewProjectInterviewerIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _aosDirectory;
    private readonly FakeLlmProvider _fakeLlmProvider;
    private readonly ProjectSpecGenerator _specGenerator;
    private readonly InterviewEvidenceWriter _evidenceWriter;
    private readonly Mock<ISpecStore> _specStoreMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Interviewer _sut;

    public NewProjectInterviewerIntegrationTests()
    {
        // Create temp directory for workspace simulation
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"gmsd-interview-test-{Guid.NewGuid():N}");
        _aosDirectory = Path.Combine(_tempDirectory, ".aos");
        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_aosDirectory);

        _fakeLlmProvider = new FakeLlmProvider();
        _specGenerator = new ProjectSpecGenerator();
        _specStoreMock = new Mock<ISpecStore>();

        // Setup workspace mock to resolve paths to temp directory
        _workspaceMock = new Mock<IWorkspace>();
        _workspaceMock.Setup(x => x.RepositoryRootPath).Returns(_tempDirectory);
        _workspaceMock.Setup(x => x.AosRootPath).Returns(_aosDirectory);
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId(It.IsAny<string>()))
            .Returns<string>(artifactId => Path.Combine(_aosDirectory, $"{artifactId}.json"));

        // Use real evidence writer for actual file system tests
        _evidenceWriter = new InterviewEvidenceWriter(_workspaceMock.Object);

        _sut = new Interviewer(
            _fakeLlmProvider,
            _specGenerator,
            _evidenceWriter,
            _specStoreMock.Object,
            _workspaceMock.Object
        );
    }

    public void Dispose()
    {
        // Cleanup temp directory
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    #region 6.3 Integration test: full interview flow writes valid project.json

    [Fact]
    public async Task FullInterviewFlow_WritesValidProjectJson_ToSpecDirectory()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-INT-001" };

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        var expectedSpecPath = Path.Combine(_aosDirectory, "spec", "project.json");
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns(expectedSpecPath);

        // Ensure spec directory exists
        Directory.CreateDirectory(Path.Combine(_aosDirectory, "spec"));

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        result.Success.Should().BeTrue();
        result.ProjectSpecJson.Should().NotBeNullOrEmpty();

        // Verify file was written
        File.Exists(expectedSpecPath).Should().BeTrue($"project.json should be written to {expectedSpecPath}");

        // Read and verify the file content
        var fileContent = await File.ReadAllTextAsync(expectedSpecPath);
        fileContent.Should().NotBeNullOrEmpty();

        // Verify it's valid JSON
        var jsonDoc = JsonDocument.Parse(fileContent);
        jsonDoc.Should().NotBeNull();

        // Verify schema compliance
        var root = jsonDoc.RootElement;
        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        root.GetProperty("project").GetProperty("name").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("project").GetProperty("description").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FullInterviewFlow_GeneratedSpec_MeetsSchemaRequirements()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-INT-002" };

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        var expectedSpecPath = Path.Combine(_aosDirectory, "spec", "project.json");
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns(expectedSpecPath);

        Directory.CreateDirectory(Path.Combine(_aosDirectory, "spec"));

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        result.Success.Should().BeTrue();
        result.ProjectSpec.Should().NotBeNull();

        // Validate the spec directly
        var validationResult = _specGenerator.Validate(result.ProjectSpec!);
        validationResult.IsValid.Should().BeTrue(
            $"Spec validation failed: {string.Join(", ", validationResult.Errors)}");

        // Verify all required fields are present
        var spec = result.ProjectSpec!;
        spec.Schema.Should().Be("gmsd:aos:schema:project:v1");
        spec.Name.Should().NotBeNullOrEmpty();
        spec.Description.Should().NotBeNullOrEmpty();
        spec.GeneratedAt.Should().BeWithin(TimeSpan.FromSeconds(1)).Before(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task FullInterviewFlow_UsesDeterministicLfLineEndings()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-INT-003" };

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        var expectedSpecPath = Path.Combine(_aosDirectory, "spec", "project.json");
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns(expectedSpecPath);

        Directory.CreateDirectory(Path.Combine(_aosDirectory, "spec"));

        // Act
        await _sut.ConductInterviewAsync(session);

        // Assert
        var fileContent = await File.ReadAllTextAsync(expectedSpecPath);

        // Should use LF only, not CRLF
        fileContent.Should().NotContain("\r\n", "JSON should use LF line endings only");
        fileContent.Should().Contain("\n");
    }

    [Fact]
    public async Task FullInterviewFlow_UpdatesExistingProjectJson_WhenReRunning()
    {
        // Arrange
        var session = new InterviewSession { RunId = "RUN-INT-004" };
        var specPath = Path.Combine(_aosDirectory, "spec", "project.json");

        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns(specPath);

        Directory.CreateDirectory(Path.Combine(_aosDirectory, "spec"));

        // Pre-create an existing project.json
        var existingContent = "{ \"name\": \"Old Project\", \"schema\": \"gmsd:aos:schema:project:v1\" }";
        await File.WriteAllTextAsync(specPath, existingContent);

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(specPath).Should().BeTrue();

        // Verify file was updated (should not be the old content)
        var newContent = await File.ReadAllTextAsync(specPath);
        newContent.Should().NotBe(existingContent);
        newContent.Should().Contain("\"schemaVersion\": 1");
    }

    #endregion

    #region 6.4 Integration test: interview evidence attached to run artifacts

    [Fact]
    public async Task InterviewEvidence_WritesTranscript_ToRunArtifactsDirectory()
    {
        // Arrange
        var runId = "RUN-EVD-001";
        var session = new InterviewSession { RunId = runId };

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        var expectedSpecPath = Path.Combine(_aosDirectory, "spec", "project.json");
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns(expectedSpecPath);

        Directory.CreateDirectory(Path.Combine(_aosDirectory, "spec"));

        // Setup workspace for evidence directory resolution
        var workspaceRoot = _tempDirectory;
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("workspace"))
            .Returns(workspaceRoot);

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        result.Success.Should().BeTrue();
        result.Artifacts.Should().NotBeEmpty();

        // Verify transcript artifact
        var transcriptArtifact = result.Artifacts.FirstOrDefault(a => a.ArtifactId == "interview-transcript");
        transcriptArtifact.Should().NotBeNull();
        transcriptArtifact!.FileName.Should().Be("interview.transcript.md");
        transcriptArtifact.ContentType.Should().Be("text/markdown");

        // Verify file was actually written
        File.Exists(transcriptArtifact.FilePath).Should().BeTrue(
            $"Transcript file should exist at {transcriptArtifact.FilePath}");

        // Verify content structure
        var fileContent = await File.ReadAllTextAsync(transcriptArtifact.FilePath);
        fileContent.Should().Contain("# Interview Transcript");
        fileContent.Should().Contain(session.SessionId);
        fileContent.Should().Contain($"**Run ID:** {runId}");
    }

    [Fact]
    public async Task InterviewEvidence_WritesSummary_ToRunArtifactsDirectory()
    {
        // Arrange
        var runId = "RUN-EVD-002";
        var session = new InterviewSession { RunId = runId };

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        var expectedSpecPath = Path.Combine(_aosDirectory, "spec", "project.json");
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns(expectedSpecPath);

        Directory.CreateDirectory(Path.Combine(_aosDirectory, "spec"));

        var workspaceRoot = _tempDirectory;
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("workspace"))
            .Returns(workspaceRoot);

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        result.Success.Should().BeTrue();

        // Verify summary artifact
        var summaryArtifact = result.Artifacts.FirstOrDefault(a => a.ArtifactId == "interview-summary");
        summaryArtifact.Should().NotBeNull();
        summaryArtifact!.FileName.Should().Be("interview.summary.md");
        summaryArtifact.ContentType.Should().Be("text/markdown");

        // Verify file was written
        File.Exists(summaryArtifact.FilePath).Should().BeTrue(
            $"Summary file should exist at {summaryArtifact.FilePath}");

        // Verify content structure
        var fileContent = await File.ReadAllTextAsync(summaryArtifact.FilePath);
        fileContent.Should().Contain("# Interview Summary");
        fileContent.Should().Contain(session.SessionId);
        fileContent.Should().Contain($"**Run ID:** {runId}");
        fileContent.Should().Contain("## Key Decisions");
    }

    [Fact]
    public async Task InterviewEvidence_ArtifactsAttachedToRun_ReturnCorrectPaths()
    {
        // Arrange
        var runId = "RUN-EVD-003";
        var session = new InterviewSession { RunId = runId };

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        var expectedSpecPath = Path.Combine(_aosDirectory, "spec", "project.json");
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns(expectedSpecPath);

        Directory.CreateDirectory(Path.Combine(_aosDirectory, "spec"));

        var workspaceRoot = _tempDirectory;
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("workspace"))
            .Returns(workspaceRoot);

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        result.Success.Should().BeTrue();
        result.Artifacts.Should().HaveCount(2);

        // Verify all artifacts are under the run artifacts directory
        foreach (var artifact in result.Artifacts)
        {
            artifact.FilePath.Should().Contain($"evidence{Path.DirectorySeparatorChar}runs{Path.DirectorySeparatorChar}{runId}");
            artifact.FilePath.Should().Contain("artifacts");
            File.Exists(artifact.FilePath).Should().BeTrue();
        }
    }

    [Fact]
    public async Task InterviewEvidence_ContainsAllQAPairs_InTranscript()
    {
        // Arrange
        var runId = "RUN-EVD-004";
        var session = new InterviewSession { RunId = runId };

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        var expectedSpecPath = Path.Combine(_aosDirectory, "spec", "project.json");
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns(expectedSpecPath);

        Directory.CreateDirectory(Path.Combine(_aosDirectory, "spec"));

        var workspaceRoot = _tempDirectory;
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("workspace"))
            .Returns(workspaceRoot);

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        result.Success.Should().BeTrue();

        var transcriptArtifact = result.Artifacts.First(a => a.ArtifactId == "interview-transcript");
        var fileContent = await File.ReadAllTextAsync(transcriptArtifact.FilePath);

        // Should contain all interview phases
        fileContent.Should().Contain("Discovery Phase");
        fileContent.Should().Contain("Clarification Phase");
        fileContent.Should().Contain("Confirmation Phase");

        // Should contain Q&A pairs (the actual questions from the interview)
        fileContent.Should().Contain("What is the name of your project?");
        fileContent.Should().Contain("What problem does this project solve?");
        fileContent.Should().Contain("What technology stack will you be using?");
        fileContent.Should().Contain("Do these requirements accurately capture your project needs?");
    }

    [Fact]
    public async Task InterviewEvidence_SummaryContainsKeyDecisions()
    {
        // Arrange
        var runId = "RUN-EVD-005";
        var session = new InterviewSession { RunId = runId };

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        var expectedSpecPath = Path.Combine(_aosDirectory, "spec", "project.json");
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns(expectedSpecPath);

        Directory.CreateDirectory(Path.Combine(_aosDirectory, "spec"));

        var workspaceRoot = _tempDirectory;
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("workspace"))
            .Returns(workspaceRoot);

        // Act
        var result = await _sut.ConductInterviewAsync(session);

        // Assert
        result.Success.Should().BeTrue();

        var summaryArtifact = result.Artifacts.First(a => a.ArtifactId == "interview-summary");
        var fileContent = await File.ReadAllTextAsync(summaryArtifact.FilePath);

        // Should contain key sections
        fileContent.Should().Contain("## Project Goals");
        fileContent.Should().Contain("## Key Features");
        fileContent.Should().Contain("Technology Stack:");

        // Should contain the actual project data
        fileContent.Should().Contain(result.ProjectSpec!.Name);
        fileContent.Should().Contain("Project Goals");
    }

    #endregion

    #region 6.5 Validation test: validate spec passes after interview completion

    [Fact]
    public async Task InterviewCompletion_ValidateSpec_Passes()
    {
        // Arrange
        var runId = "RUN-VAL-001";
        var session = new InterviewSession { RunId = runId };

        _fakeLlmProvider
            .EnqueueTextResponse("Discovery response")
            .EnqueueTextResponse("Clarification response")
            .EnqueueTextResponse("Confirmation response");

        var expectedSpecPath = Path.Combine(_aosDirectory, "spec", "project.json");
        _workspaceMock.Setup(x => x.GetAbsolutePathForArtifactId("project"))
            .Returns(expectedSpecPath);

        Directory.CreateDirectory(Path.Combine(_aosDirectory, "spec"));

        // Create a minimal valid workspace structure for validation
        await CreateMinimalWorkspaceStructureAsync(_aosDirectory);

        // Act - Run the interview
        var interviewResult = await _sut.ConductInterviewAsync(session);

        // Assert - Interview succeeded
        interviewResult.Success.Should().BeTrue();
        File.Exists(expectedSpecPath).Should().BeTrue();

        // Act - Validate spec
        var validationReport = AosWorkspaceValidator.Validate(
            _tempDirectory,
            [AosWorkspaceLayer.Spec]
        );

        // Assert - Validation passes with no issues
        var issueDetails = validationReport.IsValid
            ? ""
            : "\nIssues:\n" + string.Join("\n", validationReport.Issues.Select(i => $"  - [{i.Layer}] {i.ContractPath}: {i.Message}"));
        validationReport.IsValid.Should().BeTrue($"Spec validation should pass after interview completion. Found {validationReport.Issues.Count} issues.{issueDetails}");
    }

    private static async Task CreateMinimalWorkspaceStructureAsync(string aosDirectory)
    {
        // Use the bootstrapper to create a fully initialized workspace
        // This creates all required directories and files including the schema registry
        var aosRootPath = Path.GetDirectoryName(aosDirectory)!;
        AosWorkspaceBootstrapper.EnsureInitialized(aosRootPath);

        // The bootstrapper creates a default project.json, but we need to remove it
        // so the interview can create a fresh one
        var projectJsonPath = Path.Combine(aosDirectory, "spec", "project.json");
        if (File.Exists(projectJsonPath))
        {
            File.Delete(projectJsonPath);
        }
    }

    #endregion
}
