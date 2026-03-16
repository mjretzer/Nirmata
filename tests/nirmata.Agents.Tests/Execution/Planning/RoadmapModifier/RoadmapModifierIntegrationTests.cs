using FluentAssertions;
using nirmata.Agents.Execution.Planning.RoadmapModifier;
using nirmata.Agents.Persistence.Runs;
using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Engine.Validation;
using nirmata.Aos.Engine.Workspace;
using nirmata.Aos.Public;
using Moq;
using System.Text.Json;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Planning.RoadmapModifier;

/// <summary>
/// Integration tests for the RoadmapModifier workflow with full file system operations.
/// These tests verify that all components work together and produce valid AOS workspace artifacts.
/// </summary>
public class RoadmapModifierIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _aosDirectory;
    private readonly SpecStore _specStore;
    private readonly RoadmapRenumberer _renumberer;
    private readonly nirmata.Agents.Execution.Planning.RoadmapModifier.RoadmapModifier _roadmapModifier;
    private readonly Mock<IEventStore> _eventStoreMock;
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly RoadmapModifierHandler _handler;

    public RoadmapModifierIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"nirmata-roadmap-modifier-test-{Guid.NewGuid():N}");
        _aosDirectory = Path.Combine(_tempDirectory, ".aos");
        Directory.CreateDirectory(_tempDirectory);

        // Initialize workspace with bootstrapper
        AosWorkspaceBootstrapper.EnsureInitialized(_tempDirectory);

        _specStore = SpecStore.FromAosRoot(_aosDirectory);
        _renumberer = new RoadmapRenumberer();
        _eventStoreMock = new Mock<IEventStore>();
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();

        _roadmapModifier = new nirmata.Agents.Execution.Planning.RoadmapModifier.RoadmapModifier(_specStore, _renumberer, _eventStoreMock.Object);
        _handler = new RoadmapModifierHandler(_roadmapModifier, _runLifecycleManagerMock.Object, _eventStoreMock.Object);

        // Seed initial roadmap with phases
        SeedInitialRoadmap();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task FullWorkflow_InsertPhase_AtEnd_CreatesPhaseSpecAndUpdatesRoadmap()
    {
        // Arrange
        var runId = "RUN-INSERT-001";
        var request = new RoadmapModifyRequest
        {
            Operation = RoadmapModifyOperation.Insert,
            NewPhaseName = "New Test Phase",
            NewPhaseDescription = "Test phase description",
            Position = InsertPosition.AtEnd
        };

        // Act
        var result = await _roadmapModifier.InsertPhaseAsync(request, runId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.AffectedPhaseId.Should().NotBeNullOrEmpty();
        result.NewPhase.Should().NotBeNull();

        // Verify phase spec file was created
        var phasePath = Path.Combine(_aosDirectory, "spec", "phases", result.AffectedPhaseId!, "phase.json");
        File.Exists(phasePath).Should().BeTrue($"Phase spec should exist at {phasePath}");

        // Verify roadmap was updated
        var roadmapPath = Path.Combine(_aosDirectory, "spec", "roadmap.json");
        var roadmapContent = await File.ReadAllTextAsync(roadmapPath);
        var roadmapDoc = JsonDocument.Parse(roadmapContent);
        var items = roadmapDoc.RootElement.GetProperty("roadmap").GetProperty("items");
        items.GetArrayLength().Should().Be(3); // 2 initial + 1 inserted

        // Verify phase index was updated
        var phaseIndexPath = Path.Combine(_aosDirectory, "spec", "phases", "index.json");
        var indexContent = await File.ReadAllTextAsync(phaseIndexPath);
        var indexDoc = JsonDocument.Parse(indexContent);
        var indexItems = indexDoc.RootElement.GetProperty("items");
        indexItems.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task FullWorkflow_InsertPhase_EmitsRoadmapModifiedEvent()
    {
        // Arrange
        var runId = "RUN-INSERT-002";
        var request = new RoadmapModifyRequest
        {
            Operation = RoadmapModifyOperation.Insert,
            NewPhaseName = "Event Test Phase",
            Position = InsertPosition.AtEnd
        };

        // Act
        var result = await _roadmapModifier.InsertPhaseAsync(request, runId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _eventStoreMock.Verify(x => x.AppendEvent(It.Is<JsonElement>(e =>
            e.GetProperty("eventType").GetString() == "roadmap.modified" &&
            e.GetProperty("runId").GetString() == runId &&
            e.GetProperty("data").GetProperty("operation").GetString() == "insert"
        )), Times.Once);
    }

    [Fact]
    public async Task FullWorkflow_RemovePhase_DeletesPhaseSpecAndUpdatesRoadmap()
    {
        // Arrange
        var runId = "RUN-REMOVE-001";

        // First insert a phase we can safely remove (not the active one)
        var insertRequest = new RoadmapModifyRequest
        {
            Operation = RoadmapModifyOperation.Insert,
            NewPhaseName = "Removable Phase",
            Position = InsertPosition.AtEnd
        };
        var insertResult = await _roadmapModifier.InsertPhaseAsync(insertRequest, runId);
        var removablePhaseId = insertResult.AffectedPhaseId!;

        // Verify phase exists before removal
        var phasePath = Path.Combine(_aosDirectory, "spec", "phases", removablePhaseId, "phase.json");
        File.Exists(phasePath).Should().BeTrue();

        // Act - remove the phase with force=true since cursor might be on a different phase
        var removeResult = await _roadmapModifier.RemovePhaseAsync(removablePhaseId, force: true, runId);

        // Assert
        removeResult.IsSuccess.Should().BeTrue();

        // Verify phase spec file was deleted
        File.Exists(phasePath).Should().BeFalse("Phase spec should be deleted after removal");

        // Verify roadmap was updated
        var roadmapPath = Path.Combine(_aosDirectory, "spec", "roadmap.json");
        var roadmapContent = await File.ReadAllTextAsync(roadmapPath);
        var roadmapDoc = JsonDocument.Parse(roadmapContent);
        var items = roadmapDoc.RootElement.GetProperty("roadmap").GetProperty("items");

        var phaseIds = items.EnumerateArray()
            .Select(i => i.GetProperty("id").GetString())
            .ToList();
        phaseIds.Should().NotContain(removablePhaseId);
    }

    [Fact]
    public async Task FullWorkflow_RemovePhase_EmitsRoadmapModifiedEvent()
    {
        // Arrange
        var runId = "RUN-REMOVE-002";

        // Insert and then remove a phase
        var insertRequest = new RoadmapModifyRequest
        {
            Operation = RoadmapModifyOperation.Insert,
            NewPhaseName = "Event Test Phase To Remove",
            Position = InsertPosition.AtEnd
        };
        var insertResult = await _roadmapModifier.InsertPhaseAsync(insertRequest, runId);
        var phaseId = insertResult.AffectedPhaseId!;

        _eventStoreMock.Invocations.Clear();

        // Act
        await _roadmapModifier.RemovePhaseAsync(phaseId, force: true, runId);

        // Assert
        _eventStoreMock.Verify(x => x.AppendEvent(It.Is<JsonElement>(e =>
            e.GetProperty("eventType").GetString() == "roadmap.modified" &&
            e.GetProperty("data").GetProperty("operation").GetString() == "remove"
        )), Times.Once);
    }

    [Fact]
    public async Task FullWorkflow_RenumberPhases_UpdatesAllPhaseIdsConsistently()
    {
        // Arrange
        var runId = "RUN-RENUMBER-001";

        // Insert a phase to create non-contiguous numbering
        var insertRequest = new RoadmapModifyRequest
        {
            Operation = RoadmapModifyOperation.Insert,
            NewPhaseName = "Renumber Test Phase",
            Position = InsertPosition.AtEnd
        };
        await _roadmapModifier.InsertPhaseAsync(insertRequest, runId);

        // Act
        var result = await _roadmapModifier.RenumberPhasesAsync(runId);

        // Assert
        result.IsSuccess.Should().BeTrue($"RenumberPhasesAsync should succeed. Error: {result.ErrorMessage}");
        result.PhaseIdMapping.Should().NotBeNull();

        // Verify all phase IDs follow the PH-#### format and are contiguous
        var roadmapPath = Path.Combine(_aosDirectory, "spec", "roadmap.json");
        var roadmapContent = await File.ReadAllTextAsync(roadmapPath);
        var roadmapDoc = JsonDocument.Parse(roadmapContent);
        var items = roadmapDoc.RootElement.GetProperty("roadmap").GetProperty("items");

        var phaseIds = items.EnumerateArray()
            .Select(i => i.GetProperty("id").GetString() ?? string.Empty)
            .OrderBy(id => id)
            .ToList();

        phaseIds.Should().HaveCount(3);
        phaseIds[0].Should().Be("PH-0001");
        phaseIds[1].Should().Be("PH-0002");
        phaseIds[2].Should().Be("PH-0003");
    }

    [Fact]
    public async Task FullWorkflow_InsertThenRemove_MaintainsConsistentState()
    {
        // Arrange
        var runId = "RUN-INSERT-REMOVE-001";

        // Act - Insert phase
        var insertRequest = new RoadmapModifyRequest
        {
            Operation = RoadmapModifyOperation.Insert,
            NewPhaseName = "Temporary Phase",
            Position = InsertPosition.AtEnd
        };
        var insertResult = await _roadmapModifier.InsertPhaseAsync(insertRequest, runId);
        insertResult.IsSuccess.Should().BeTrue();

        // Act - Remove the inserted phase
        var phaseIdToRemove = insertResult.AffectedPhaseId!;
        var removeResult = await _roadmapModifier.RemovePhaseAsync(phaseIdToRemove, force: true, runId);
        removeResult.IsSuccess.Should().BeTrue();

        // Assert - Final state should have original 2 phases with consistent numbering
        var roadmapPath = Path.Combine(_aosDirectory, "spec", "roadmap.json");
        var roadmapContent = await File.ReadAllTextAsync(roadmapPath);
        var roadmapDoc = JsonDocument.Parse(roadmapContent);
        var items = roadmapDoc.RootElement.GetProperty("roadmap").GetProperty("items");

        items.GetArrayLength().Should().Be(2);

        var phaseIds = items.EnumerateArray()
            .Select(i => i.GetProperty("id").GetString())
            .ToList();
        phaseIds.Should().Contain("PH-0001");
        phaseIds.Should().Contain("PH-0002");
    }

    [Fact]
    public async Task Handler_HandleAsync_InsertCommand_ReturnsSuccess()
    {
        // Arrange
        var runId = "RUN-HANDLER-001";
        var commandRequest = new CommandRequest
        {
            Group = "roadmap",
            Command = "insert",
            Options = new Dictionary<string, string?>
            {
                { "operation", "insert" },
                { "name", "Handler Test Phase" },
                { "milestone-id", "MS-0001" }
            },
            Arguments = Array.Empty<string>()
        };

        // Act
        var result = await _handler.HandleAsync(commandRequest, runId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("inserted successfully");

        _runLifecycleManagerMock.Verify(x => x.RecordCommandAsync(
            runId,
            "roadmap",
            "insert",
            "completed",
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task Handler_HandleAsync_RemoveCommand_ReturnsSuccess()
    {
        // Arrange
        var runId = "RUN-HANDLER-002";

        // Insert a phase first
        var insertRequest = new RoadmapModifyRequest
        {
            Operation = RoadmapModifyOperation.Insert,
            NewPhaseName = "Phase To Remove via Handler",
            Position = InsertPosition.AtEnd
        };
        var insertResult = await _roadmapModifier.InsertPhaseAsync(insertRequest, runId);
        var phaseIdToRemove = insertResult.AffectedPhaseId ?? throw new InvalidOperationException("Phase ID should not be null");

        var commandRequest = new CommandRequest
        {
            Group = "roadmap",
            Command = "remove",
            Options = new Dictionary<string, string?>
            {
                { "phase-id", phaseIdToRemove },
                { "force", "true" }
            },
            Arguments = Array.Empty<string>()
        };

        // Act
        var result = await _handler.HandleAsync(commandRequest, runId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("removed successfully");
    }

    [Fact]
    public async Task Handler_HandleAsync_InvalidOperation_ReturnsFailure()
    {
        // Arrange
        var runId = "RUN-HANDLER-003";
        var commandRequest = new CommandRequest
        {
            Group = "roadmap",
            Command = "invalid",
            Options = new Dictionary<string, string?>(),
            Arguments = Array.Empty<string>()
        };

        // Act
        var result = await _handler.HandleAsync(commandRequest, runId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("Operation type is required");
    }

    [Fact]
    public async Task FullWorkflow_AfterInsert_AosWorkspaceValidatorSpecLayer_Passes()
    {
        // Arrange
        var runId = "RUN-VALIDATE-001";
        var request = new RoadmapModifyRequest
        {
            Operation = RoadmapModifyOperation.Insert,
            NewPhaseName = "Validation Test Phase",
            Position = InsertPosition.AtEnd
        };

        // Act
        var result = await _roadmapModifier.InsertPhaseAsync(request, runId);
        result.IsSuccess.Should().BeTrue();

        // Validate with AosWorkspaceValidator
        var validationReport = AosWorkspaceValidator.Validate(
            _tempDirectory,
            [AosWorkspaceLayer.Spec]
        );

        // Assert
        validationReport.IsValid.Should().BeTrue(
            $"Spec validation should pass. Issues: {string.Join(", ", validationReport.Issues.Select(i => $"{i.ContractPath}: {i.Message}"))}");
    }

    [Fact]
    public async Task FullWorkflow_AfterRemove_AosWorkspaceValidatorSpecLayer_Passes()
    {
        // Arrange
        var runId = "RUN-VALIDATE-002";

        // Insert and remove a phase
        var insertRequest = new RoadmapModifyRequest
        {
            Operation = RoadmapModifyOperation.Insert,
            NewPhaseName = "Validation Remove Test Phase",
            Position = InsertPosition.AtEnd
        };
        var insertResult = await _roadmapModifier.InsertPhaseAsync(insertRequest, runId);

        await _roadmapModifier.RemovePhaseAsync(insertResult.AffectedPhaseId!, force: true, runId);

        // Act - Validate with AosWorkspaceValidator
        var validationReport = AosWorkspaceValidator.Validate(
            _tempDirectory,
            [AosWorkspaceLayer.Spec]
        );

        // Assert
        validationReport.IsValid.Should().BeTrue(
            $"Spec validation should pass after removal. Issues: {string.Join(", ", validationReport.Issues.Select(i => $"{i.ContractPath}: {i.Message}"))}");
    }

    [Fact]
    public async Task FullWorkflow_AfterRenumber_AosWorkspaceValidatorSpecLayer_Passes()
    {
        // Arrange
        var runId = "RUN-VALIDATE-003";

        // Insert a phase to create non-contiguous IDs
        var insertRequest = new RoadmapModifyRequest
        {
            Operation = RoadmapModifyOperation.Insert,
            NewPhaseName = "Validation Renumber Test Phase",
            Position = InsertPosition.AtEnd
        };
        await _roadmapModifier.InsertPhaseAsync(insertRequest, runId);

        // Renumber
        await _roadmapModifier.RenumberPhasesAsync(runId);

        // Act - Validate with AosWorkspaceValidator
        var validationReport = AosWorkspaceValidator.Validate(
            _tempDirectory,
            [AosWorkspaceLayer.Spec]
        );

        // Assert
        validationReport.IsValid.Should().BeTrue(
            $"Spec validation should pass after renumbering. Issues: {string.Join(", ", validationReport.Issues.Select(i => $"{i.ContractPath}: {i.Message}"))}");
    }

    private void SeedInitialRoadmap()
    {
        // Create initial roadmap with 2 phases
        var roadmapDoc = new
        {
            schemaVersion = 1,
            roadmap = new
            {
                title = "Test Roadmap",
                items = new[]
                {
                    new { id = "PH-0001", title = "Initial Phase 1", kind = "phase" },
                    new { id = "PH-0002", title = "Initial Phase 2", kind = "phase" }
                }
            }
        };

        var roadmapPath = Path.Combine(_aosDirectory, "spec", "roadmap.json");
        File.WriteAllText(roadmapPath, JsonSerializer.Serialize(roadmapDoc, new JsonSerializerOptions { WriteIndented = true }));

        // Create phase spec files
        CreatePhaseSpec("PH-0001", "Initial Phase 1", 1);
        CreatePhaseSpec("PH-0002", "Initial Phase 2", 2);

        // Update phase index
        var indexDoc = new
        {
            schemaVersion = 1,
            items = new[] { "PH-0001", "PH-0002" }
        };
        var indexPath = Path.Combine(_aosDirectory, "spec", "phases", "index.json");
        File.WriteAllText(indexPath, JsonSerializer.Serialize(indexDoc, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void CreatePhaseSpec(string phaseId, string name, int sequenceOrder)
    {
        var phaseDir = Path.Combine(_aosDirectory, "spec", "phases", phaseId);
        Directory.CreateDirectory(phaseDir);

        var phaseDoc = new
        {
            schema = "nirmata:aos:schema:phase:v1",
            phaseId,
            name,
            description = "Test phase",
            milestoneId = "MS-0001",
            sequenceOrder,
            status = "pending",
            deliverables = Array.Empty<object>(),
            inputArtifacts = Array.Empty<string>(),
            outputArtifacts = Array.Empty<string>()
        };

        var phasePath = Path.Combine(phaseDir, "phase.json");
        File.WriteAllText(phasePath, JsonSerializer.Serialize(phaseDoc, new JsonSerializerOptions { WriteIndented = true }));
    }
}
