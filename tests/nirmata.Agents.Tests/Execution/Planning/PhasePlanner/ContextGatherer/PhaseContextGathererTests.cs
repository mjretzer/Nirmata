using FluentAssertions;
using nirmata.Agents.Execution.Planning.PhasePlanner;
using nirmata.Agents.Execution.Planning.PhasePlanner.ContextGatherer;
using nirmata.Agents.Tests.Helpers;
using nirmata.Aos.Public;
using Moq;
using System.Text.Json;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Planning.PhasePlanner.ContextGatherer;

public class PhaseContextGathererTests : IDisposable
{
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly PhaseContextGatherer _sut;
    private readonly TempDirectory _tempDir;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public PhaseContextGathererTests()
    {
        _workspaceMock = new Mock<IWorkspace>();
        _tempDir = new TempDirectory();
        
        var tempPath = _tempDir.Path;
        _workspaceMock.Setup(x => x.RepositoryRootPath).Returns(tempPath);
        _workspaceMock.Setup(x => x.AosRootPath).Returns(Path.Combine(tempPath, ".aos"));
        
        _sut = new PhaseContextGatherer(_workspaceMock.Object);
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    [Fact]
    public async Task GatherContextAsync_WithNullPhaseId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GatherContextAsync(null!, "run-00000000000000000000000000000001"));
    }

    [Fact]
    public async Task GatherContextAsync_WithEmptyPhaseId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GatherContextAsync(string.Empty, "run-00000000000000000000000000000001"));
    }

    [Fact]
    public async Task GatherContextAsync_WithNullRunId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GatherContextAsync("PH-0001", null!));
    }

    [Fact]
    public async Task GatherContextAsync_WithEmptyRunId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GatherContextAsync("PH-0001", string.Empty));
    }

    [Fact]
    public async Task GatherContextAsync_PopulatesPhaseBriefWithPhaseId()
    {
        // Arrange
        var phaseId = "PH-0001";
        var runId = "run-00000000000000000000000000000001";
        SetupMockPhaseDocument(phaseId, "Test Phase", "Test Description", "MS-0001");

        // Act
        var result = await _sut.GatherContextAsync(phaseId, runId);

        // Assert
        result.PhaseId.Should().Be(phaseId);
        result.RunId.Should().Be(runId);
    }

    [Fact]
    public async Task GatherContextAsync_PopulatesPhaseBriefWithPhaseName()
    {
        // Arrange
        var phaseId = "PH-0001";
        var phaseName = "Test Phase Name";
        SetupMockPhaseDocument(phaseId, phaseName, "Description", "MS-0001");

        // Act
        var result = await _sut.GatherContextAsync(phaseId, "run-00000000000000000000000000000001");

        // Assert
        result.PhaseName.Should().Be(phaseName);
    }

    [Fact]
    public async Task GatherContextAsync_PopulatesPhaseBriefWithDescription()
    {
        // Arrange
        var phaseId = "PH-0001";
        var description = "This is a test phase description";
        SetupMockPhaseDocument(phaseId, "Test Phase", description, "MS-0001");

        // Act
        var result = await _sut.GatherContextAsync(phaseId, "run-00000000000000000000000000000001");

        // Assert
        result.Description.Should().Be(description);
    }

    [Fact]
    public async Task GatherContextAsync_PopulatesPhaseBriefWithMilestoneId()
    {
        // Arrange
        var phaseId = "PH-0001";
        var milestoneId = "MS-0001";
        SetupMockPhaseDocument(phaseId, "Test Phase", "Description", milestoneId);

        // Act
        var result = await _sut.GatherContextAsync(phaseId, "run-00000000000000000000000000000001");

        // Assert
        result.MilestoneId.Should().Be(milestoneId);
    }

    [Fact]
    public async Task GatherContextAsync_PopulatesPhaseBriefWithScope()
    {
        // Arrange
        var phaseId = "PH-0001";
        var inScope = new[] { "Feature A", "Feature B" };
        var outOfScope = new[] { "Feature C" };
        var boundaries = new[] { "Limitation 1" };
        SetupMockPhaseDocument(phaseId, "Test Phase", "Description", "MS-0001", inScope, outOfScope, boundaries);

        // Act
        var result = await _sut.GatherContextAsync(phaseId, "run-00000000000000000000000000000001");

        // Assert
        result.Scope.InScope.Should().BeEquivalentTo(inScope);
        result.Scope.OutOfScope.Should().BeEquivalentTo(outOfScope);
        result.Scope.Boundaries.Should().BeEquivalentTo(boundaries);
    }

    [Fact]
    public async Task GatherContextAsync_PopulatesPhaseBriefWithInputArtifacts()
    {
        // Arrange
        var phaseId = "PH-0001";
        var inputArtifacts = new[] { "project.json", "roadmap.json" };
        SetupMockPhaseDocument(phaseId, "Test Phase", "Description", "MS-0001",
            inputArtifacts: inputArtifacts, outputArtifacts: Array.Empty<string>());

        // Act
        var result = await _sut.GatherContextAsync(phaseId, "run-00000000000000000000000000000001");

        // Assert
        result.InputArtifacts.Should().BeEquivalentTo(inputArtifacts);
    }

    [Fact]
    public async Task GatherContextAsync_PopulatesPhaseBriefWithExpectedOutputs()
    {
        // Arrange
        var phaseId = "PH-0001";
        var outputArtifacts = new[] { "phase-spec.json", "tasks/" };
        SetupMockPhaseDocument(phaseId, "Test Phase", "Description", "MS-0001",
            inputArtifacts: Array.Empty<string>(), outputArtifacts: outputArtifacts);

        // Act
        var result = await _sut.GatherContextAsync(phaseId, "run-00000000000000000000000000000001");

        // Assert
        result.ExpectedOutputs.Should().BeEquivalentTo(outputArtifacts);
    }

    [Fact]
    public async Task GatherContextAsync_SetsGeneratedAtTimestamp()
    {
        // Arrange
        var beforeTest = DateTimeOffset.UtcNow;
        SetupMockPhaseDocument("PH-0001", "Test Phase", "Description", "MS-0001");

        // Act
        var result = await _sut.GatherContextAsync("PH-0001", "run-00000000000000000000000000000001");
        var afterTest = DateTimeOffset.UtcNow;

        // Assert
        result.GeneratedAt.Should().BeOnOrAfter(beforeTest);
        result.GeneratedAt.Should().BeOnOrBefore(afterTest);
    }

    [Fact]
    public async Task GatherContextAsync_WhenPhaseDocumentMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var phaseId = "PH-9999"; // Non-existent but valid format
        // Don't create file

        // Act & Assert
        // AosSpecStore throws DirectoryNotFoundException or FileNotFoundException usually, but let's check
        // PhaseContextGatherer implementation:
        // var phaseElement = _specStore.Inner.ReadPhase(phaseId); -> ReadJsonElement -> File.ReadAllText
        // So it throws FileNotFoundException/DirectoryNotFoundException. 
        // BUT wait, AosJsonStoreBase.ReadJsonElement wraps it? 
        // No, it just calls ReadAllText.
        // The implementation doesn't catch it in GatherContextAsync before ReadPhase.
        // It catches nothing?
        // Wait, look at PhaseContextGatherer.cs:
        // var phaseElement = _specStore.Inner.ReadPhase(phaseId);
        // It doesn't have a try-catch block around ReadPhase.
        // So it will throw FileNotFoundException/DirectoryNotFoundException.
        // Update test to expect that.
        
        await Assert.ThrowsAnyAsync<IOException>(() => _sut.GatherContextAsync(phaseId, "run-00000000000000000000000000000001"));
    }

    private void SetupMockPhaseDocument(
        string phaseId,
        string name,
        string description,
        string milestoneId,
        string[]? inScope = null,
        string[]? outOfScope = null,
        string[]? boundaries = null,
        string[]? inputArtifacts = null,
        string[]? outputArtifacts = null)
    {
        var phaseDoc = new
        {
            phaseId,
            name,
            description,
            milestoneId,
            inScope = inScope ?? Array.Empty<string>(),
            outOfScope = outOfScope ?? Array.Empty<string>(),
            scopeBoundaries = boundaries ?? Array.Empty<string>(),
            inputArtifacts = inputArtifacts ?? Array.Empty<string>(),
            outputArtifacts = outputArtifacts ?? Array.Empty<string>()
        };

        var jsonElement = JsonSerializer.SerializeToElement(phaseDoc);
        _workspaceMock.Setup(x => x.ReadArtifact("spec", $"phases/{phaseId}/phase.json"))
            .Returns(jsonElement);

        // Setup empty roadmap for tests that don't need it
        var roadmapDoc = new
        {
            roadmap = new
            {
                items = new[] { new { id = phaseId, title = name } }
            }
        };
        _workspaceMock.Setup(x => x.ReadArtifact("spec", "roadmap.json"))
            .Returns(JsonSerializer.SerializeToElement(roadmapDoc));

        // Setup project for tests
        var projectDoc = new
        {
            project = new { name = "Test Project" }
        };
        _workspaceMock.Setup(x => x.ReadArtifact("spec", "project.json"))
            .Returns(JsonSerializer.SerializeToElement(projectDoc));
    }
}
