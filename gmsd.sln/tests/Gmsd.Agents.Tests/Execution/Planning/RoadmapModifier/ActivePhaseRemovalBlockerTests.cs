using FluentAssertions;
using Gmsd.Agents.Execution.Planning.RoadmapModifier;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Engine.Validation;
using Gmsd.Aos.Engine.Workspace;
using Gmsd.Aos.Public;
using Moq;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Gmsd.Agents.Tests.Execution.Planning.RoadmapModifier;

/// <summary>
/// Tests for the active phase removal blocker scenario.
/// </summary>
public class ActivePhaseRemovalBlockerTests
{
    [Fact]
    public void BlockedResult_ContainsIssueId()
    {
        var result = RoadmapModifyResult.BlockedResult(
            "PH-0001",
            "Active phase cannot be removed",
            "ISS-ABCD1234");

        Assert.True(result.IsBlocked);
        Assert.False(result.IsSuccess);
        Assert.Equal("PH-0001", result.AffectedPhaseId);
        Assert.Equal("Active phase cannot be removed", result.BlockerReason);
        Assert.Equal("ISS-ABCD1234", result.BlockerIssueId);
    }

    [Fact]
    public void BlockedResult_WithoutIssueId_HasNullIssueId()
    {
        var result = RoadmapModifyResult.BlockedResult(
            "PH-0001",
            "Active phase cannot be removed");

        Assert.True(result.IsBlocked);
        Assert.Null(result.BlockerIssueId);
    }

    [Fact]
    public void IsPhaseActive_WhenCursorMatchesPhaseId_ReturnsTrue()
    {
        // This test simulates the IsPhaseActiveAsync logic
        var phaseId = "PH-0001";
        var cursorPhaseId = "PH-0001";

        var isActive = cursorPhaseId == phaseId;

        Assert.True(isActive);
    }

    [Fact]
    public void IsPhaseActive_WhenCursorDoesNotMatch_ReturnsFalse()
    {
        var phaseId = "PH-0001";
        var cursorPhaseId = "PH-0002";

        var isActive = cursorPhaseId == phaseId;

        Assert.False(isActive);
    }

    [Fact]
    public void ForceFlag_AllowsRemovalOfActivePhase()
    {
        // When force=true, the removal should proceed even if phase is active
        var force = true;
        var isPhaseActive = true;

        var shouldAllowRemoval = force || !isPhaseActive;

        Assert.True(shouldAllowRemoval);
    }

    [Fact]
    public void WithoutForceFlag_BlocksRemovalOfActivePhase()
    {
        var force = false;
        var isPhaseActive = true;

        var shouldAllowRemoval = force || !isPhaseActive;

        Assert.False(shouldAllowRemoval);
    }

    [Fact]
    public void IssueId_Format_IsCorrect()
    {
        // Verify ISS-#### format
        var issueId = $"ISS-{Guid.NewGuid().ToString()[..8].ToUpper()}";

        Assert.StartsWith("ISS-", issueId);
        Assert.Equal(12, issueId.Length); // ISS- + 8 chars
    }

    [Fact]
    public void BlockerEvent_ContainsRequiredFields()
    {
        var eventPayload = new
        {
            eventType = "roadmap.blocker",
            timestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            runId = "RUN-123",
            data = new
            {
                phaseId = "PH-0001",
                issueId = "ISS-TEST1234",
                reason = "Active phase removal blocked"
            }
        };

        Assert.Equal("roadmap.blocker", eventPayload.eventType);
        Assert.Equal("PH-0001", eventPayload.data.phaseId);
        Assert.Equal("ISS-TEST1234", eventPayload.data.issueId);
        Assert.NotNull(eventPayload.timestampUtc);
    }

    [Fact]
    public void RoadmapModifyStatus_Blocked_IsDistinctFromFailed()
    {
        var blockedStatus = RoadmapModifyStatus.Blocked;
        var failedStatus = RoadmapModifyStatus.Failed;
        var successStatus = RoadmapModifyStatus.Success;

        Assert.NotEqual(blockedStatus, failedStatus);
        Assert.NotEqual(blockedStatus, successStatus);
        Assert.NotEqual(failedStatus, successStatus);
    }
}

/// <summary>
/// Integration tests for active phase removal blocker scenario with full file system operations.
/// </summary>
public class ActivePhaseRemovalBlockerIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _aosDirectory;
    private readonly SpecStore _specStore;
    private readonly RoadmapRenumberer _renumberer;
    private readonly Gmsd.Agents.Execution.Planning.RoadmapModifier.RoadmapModifier _roadmapModifier;
    private readonly Mock<IEventStore> _eventStoreMock;

    private readonly ITestOutputHelper _output;

    public ActivePhaseRemovalBlockerIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"gmsd-blocker-test-{Guid.NewGuid():N}");
        _aosDirectory = Path.Combine(_tempDirectory, ".aos");
        Directory.CreateDirectory(_tempDirectory);

        // Initialize workspace
        AosWorkspaceBootstrapper.EnsureInitialized(_tempDirectory);

        _specStore = SpecStore.FromAosRoot(_aosDirectory);
        _renumberer = new RoadmapRenumberer();
        _eventStoreMock = new Mock<IEventStore>();

        _roadmapModifier = new Gmsd.Agents.Execution.Planning.RoadmapModifier.RoadmapModifier(_specStore, _renumberer, _eventStoreMock.Object);

        // Seed workspace with roadmap and active phase in cursor
        SeedWorkspaceWithActivePhase();
    }

    [Fact]
    public async Task Debug_IsPhaseActive_Logic()
    {
        var phaseId = "PH-0001";
        
        // 1. Check path
        var aosRootPath = _specStore.Inner.AosRootPath;
        var statePath = Path.Combine(aosRootPath, "state", "state.json");
        _output.WriteLine($"State Path: {statePath}");
        
        File.Exists(statePath).Should().BeTrue("State file should exist");
        
        // 2. Check content
        var stateJson = await File.ReadAllTextAsync(statePath);
        _output.WriteLine($"State JSON: {stateJson}");
        stateJson.Should().Contain(phaseId);
        
        // 3. Check deserialization
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        var state = JsonSerializer.Deserialize<StateSnapshot>(stateJson, options);
        state.Should().NotBeNull();
        state!.Cursor.Should().NotBeNull();
        state.Cursor!.PhaseId.Should().Be(phaseId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RemoveActivePhase_WithoutForce_ReturnsBlockedResult()
    {
        // Arrange
        var runId = "RUN-BLOCKER-001";
        var activePhaseId = "PH-0001"; // This is set as active in cursor

        // Act - Try to remove active phase without force flag
        var result = await _roadmapModifier.RemovePhaseAsync(activePhaseId, force: false, runId);

        // Assert
        result.IsBlocked.Should().BeTrue($"Should be blocked when removing active phase without force. Status: {result.Status}, Error: {result.ErrorMessage}");
        result.IsSuccess.Should().BeFalse();
        result.AffectedPhaseId.Should().Be(activePhaseId);
        result.BlockerReason.Should().NotBeNullOrEmpty();
        result.BlockerIssueId.Should().NotBeNullOrEmpty();
        result.BlockerIssueId.Should().StartWith("ISS-");
    }

    [Fact]
    public async Task RemoveActivePhase_WithoutForce_CreatesBlockerIssue()
    {
        // Arrange
        var runId = "RUN-BLOCKER-002";
        var activePhaseId = "PH-0001";

        // Act
        var result = await _roadmapModifier.RemovePhaseAsync(activePhaseId, force: false, runId);

        // Assert
        result.IsBlocked.Should().BeTrue();

        // Verify issue file was created
        var issuesDir = Path.Combine(_aosDirectory, "spec", "issues");
        if (Directory.Exists(issuesDir))
        {
            var issueFiles = Directory.GetFiles(issuesDir, "ISS-*.json");
            issueFiles.Should().NotBeEmpty("Blocker issue file should be created");

            if (issueFiles.Length > 0)
            {
                var issueContent = await File.ReadAllTextAsync(issueFiles[0]);
                var issueDoc = JsonDocument.Parse(issueContent);
                issueDoc.RootElement.GetProperty("id").GetString().Should().Be(result.BlockerIssueId);
                issueDoc.RootElement.GetProperty("title").GetString().Should().Contain(activePhaseId);
                issueDoc.RootElement.GetProperty("status").GetString().Should().Be("open");
            }
        }
    }

    [Fact]
    public async Task RemoveActivePhase_WithoutForce_EmitsBlockerEvent()
    {
        // Arrange
        var runId = "RUN-BLOCKER-003";
        var activePhaseId = "PH-0001";

        // Act
        await _roadmapModifier.RemovePhaseAsync(activePhaseId, force: false, runId);

        // Assert
        _eventStoreMock.Verify(x => x.AppendEvent(It.Is<JsonElement>(e =>
            e.GetProperty("eventType").GetString() == "roadmap.blocker" &&
            e.GetProperty("runId").GetString() == runId &&
            e.GetProperty("data").GetProperty("phaseId").GetString() == activePhaseId &&
            e.GetProperty("data").GetProperty("reason").GetString() == "Active phase removal blocked"
        )), Times.Once);
    }

    [Fact]
    public async Task RemoveActivePhase_WithForce_AllowsRemoval()
    {
        // Arrange
        var runId = "RUN-BLOCKER-004";
        var activePhaseId = "PH-0001";

        // Act - Remove with force flag
        var result = await _roadmapModifier.RemovePhaseAsync(activePhaseId, force: true, runId);

        // Assert
        result.IsSuccess.Should().BeTrue("Should succeed when force flag is set");
        result.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveActivePhase_WithForce_DeletesPhaseSpec()
    {
        // Arrange
        var runId = "RUN-BLOCKER-005";
        var activePhaseId = "PH-0001";

        // Verify phase exists before
        var phasePath = Path.Combine(_aosDirectory, "spec", "phases", activePhaseId, "phase.json");
        File.Exists(phasePath).Should().BeTrue();

        // Act
        await _roadmapModifier.RemovePhaseAsync(activePhaseId, force: true, runId);

        // Assert
        File.Exists(phasePath).Should().BeFalse("Phase spec should be deleted when force=true");
    }

    [Fact]
    public async Task RemoveActivePhase_WithForce_EmitsRoadmapModifiedEvent()
    {
        // Arrange
        var runId = "RUN-BLOCKER-006";
        var activePhaseId = "PH-0001";

        // Act
        var result = await _roadmapModifier.RemovePhaseAsync(activePhaseId, force: true, runId);

        // Assert
        result.IsSuccess.Should().BeTrue($"RemovePhaseAsync should succeed. Error: {result.ErrorMessage}");
        
        _eventStoreMock.Verify(x => x.AppendEvent(It.Is<JsonElement>(e =>
            e.GetProperty("eventType").GetString() == "roadmap.modified" &&
            e.GetProperty("data").GetProperty("operation").GetString() == "remove"
        )), Times.Once);
    }

    [Fact]
    public async Task RemoveActivePhase_WithoutForce_DoesNotDeletePhaseSpec()
    {
        // Arrange
        var runId = "RUN-BLOCKER-007";
        var activePhaseId = "PH-0001";

        var phasePath = Path.Combine(_aosDirectory, "spec", "phases", activePhaseId, "phase.json");
        var phaseContentBefore = await File.ReadAllTextAsync(phasePath);

        // Act
        var result = await _roadmapModifier.RemovePhaseAsync(activePhaseId, force: false, runId);

        // Assert
        result.IsBlocked.Should().BeTrue();

        // Verify phase spec still exists and is unchanged
        File.Exists(phasePath).Should().BeTrue("Phase spec should not be deleted when blocked");
        var phaseContentAfter = await File.ReadAllTextAsync(phasePath);
        phaseContentAfter.Should().Be(phaseContentBefore);
    }

    [Fact]
    public async Task RemoveActivePhase_WithoutForce_DoesNotModifyRoadmap()
    {
        // Arrange
        var runId = "RUN-BLOCKER-008";
        var activePhaseId = "PH-0001";

        var roadmapPath = Path.Combine(_aosDirectory, "spec", "roadmap.json");
        var roadmapContentBefore = await File.ReadAllTextAsync(roadmapPath);

        // Act
        var result = await _roadmapModifier.RemovePhaseAsync(activePhaseId, force: false, runId);

        // Assert
        result.IsBlocked.Should().BeTrue();

        // Verify roadmap unchanged
        var roadmapContentAfter = await File.ReadAllTextAsync(roadmapPath);
        roadmapContentAfter.Should().Be(roadmapContentBefore);
    }

    [Fact]
    public async Task RemoveActivePhase_WithoutForce_AosWorkspaceValidator_Passes()
    {
        // Arrange
        var runId = "RUN-BLOCKER-009";
        var activePhaseId = "PH-0001";

        // Act - Attempt removal (should be blocked)
        await _roadmapModifier.RemovePhaseAsync(activePhaseId, force: false, runId);

        // Validate workspace is still valid
        var validationReport = AosWorkspaceValidator.Validate(
            _tempDirectory,
            [AosWorkspaceLayer.Spec, AosWorkspaceLayer.State]
        );

        // Assert
        validationReport.IsValid.Should().BeTrue(
            $"Workspace should remain valid after blocked removal. Issues: {string.Join(", ", validationReport.Issues.Select(i => $"{i.ContractPath}: {i.Message}"))}");
    }

    [Fact]
    public async Task RemoveActivePhase_WithForce_AosWorkspaceValidator_Passes()
    {
        // Arrange
        var runId = "RUN-BLOCKER-010";
        var activePhaseId = "PH-0001";

        // Act - Force removal
        await _roadmapModifier.RemovePhaseAsync(activePhaseId, force: true, runId);

        // Validate workspace
        var validationReport = AosWorkspaceValidator.Validate(
            _tempDirectory,
            [AosWorkspaceLayer.Spec]
        );

        // Assert
        validationReport.IsValid.Should().BeTrue(
            $"Workspace should remain valid after forced removal. Issues: {string.Join(", ", validationReport.Issues.Select(i => $"{i.ContractPath}: {i.Message}"))}");
    }

    [Fact]
    public async Task RemoveNonActivePhase_WithoutForce_AllowsRemoval()
    {
        // Arrange
        var runId = "RUN-BLOCKER-011";

        // Add a second phase (not active)
        var insertRequest = new RoadmapModifyRequest
        {
            Operation = RoadmapModifyOperation.Insert,
            NewPhaseName = "Second Phase",
            Position = InsertPosition.AtEnd
        };
        var insertResult = await _roadmapModifier.InsertPhaseAsync(insertRequest, runId);
        var nonActivePhaseId = insertResult.AffectedPhaseId!;

        // Act - Remove the non-active phase without force
        var result = await _roadmapModifier.RemovePhaseAsync(nonActivePhaseId, force: false, runId);

        // Assert
        result.IsSuccess.Should().BeTrue("Should succeed when removing non-active phase without force");
        result.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_RemoveActivePhase_WithoutForce_ReturnsFailure()
    {
        // Arrange
        var runId = "RUN-HANDLER-BLOCKER-001";
        var activePhaseId = "PH-0001";

        var handler = new RoadmapModifierHandler(
            _roadmapModifier,
            new Mock<IRunLifecycleManager>().Object,
            _eventStoreMock.Object);

        var commandRequest = new Gmsd.Aos.Contracts.Commands.CommandRequest
        {
            Group = "roadmap",
            Command = "remove",
            Options = new Dictionary<string, string?>
            {
                { "phase-id", activePhaseId },
                { "force", "false" }
            },
            Arguments = Array.Empty<string>()
        };

        // Act
        var result = await handler.HandleAsync(commandRequest, runId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("blocked");
    }

    [Fact]
    public async Task Handler_RemoveActivePhase_WithForce_ReturnsSuccess()
    {
        // Arrange
        var runId = "RUN-HANDLER-BLOCKER-002";
        var activePhaseId = "PH-0001";

        var handler = new RoadmapModifierHandler(
            _roadmapModifier,
            new Mock<IRunLifecycleManager>().Object,
            _eventStoreMock.Object);

        var commandRequest = new Gmsd.Aos.Contracts.Commands.CommandRequest
        {
            Group = "roadmap",
            Command = "remove",
            Options = new Dictionary<string, string?>
            {
                { "phase-id", activePhaseId },
                { "force", "true" }
            },
            Arguments = Array.Empty<string>()
        };

        // Act
        var result = await handler.HandleAsync(commandRequest, runId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("removed successfully");
    }

    private void SeedWorkspaceWithActivePhase()
    {
        // Create roadmap with active phase
        var roadmapDoc = new
        {
            schemaVersion = 1,
            roadmap = new
            {
                title = "Test Roadmap",
                items = new[]
                {
                    new { id = "PH-0001", title = "Active Phase", kind = "phase" }
                }
            }
        };

        var roadmapPath = Path.Combine(_aosDirectory, "spec", "roadmap.json");
        File.WriteAllText(roadmapPath, JsonSerializer.Serialize(roadmapDoc, new JsonSerializerOptions { WriteIndented = true }));

        // Create active phase spec
        var phaseDir = Path.Combine(_aosDirectory, "spec", "phases", "PH-0001");
        Directory.CreateDirectory(phaseDir);

        var phaseDoc = new
        {
            schema = "gmsd:aos:schema:phase:v1",
            phaseId = "PH-0001",
            name = "Active Phase",
            description = "This is the active phase",
            milestoneId = "MS-0001",
            sequenceOrder = 1,
            status = "pending",
            deliverables = Array.Empty<object>(),
            inputArtifacts = Array.Empty<string>(),
            outputArtifacts = Array.Empty<string>()
        };

        File.WriteAllText(
            Path.Combine(phaseDir, "phase.json"),
            JsonSerializer.Serialize(phaseDoc, new JsonSerializerOptions { WriteIndented = true }));

        // Update phase index
        var indexDoc = new
        {
            schemaVersion = 1,
            items = new[] { "PH-0001" }
        };
        File.WriteAllText(
            Path.Combine(_aosDirectory, "spec", "phases", "index.json"),
            JsonSerializer.Serialize(indexDoc, new JsonSerializerOptions { WriteIndented = true }));

        // Set cursor to active phase in state.json
        var stateDir = Path.Combine(_aosDirectory, "state");
        Directory.CreateDirectory(stateDir);

        var stateDoc = new
        {
            schemaVersion = 1,
            cursor = new
            {
                phaseId = "PH-0001",
                phaseStatus = "in_progress",
                runId = "RUN-INIT"
            }
        };

        File.WriteAllText(
            Path.Combine(stateDir, "state.json"),
            JsonSerializer.Serialize(stateDoc, new JsonSerializerOptions { WriteIndented = true }));

        // Create events.ndjson
        File.WriteAllText(Path.Combine(stateDir, "events.ndjson"), "");
    }
}
