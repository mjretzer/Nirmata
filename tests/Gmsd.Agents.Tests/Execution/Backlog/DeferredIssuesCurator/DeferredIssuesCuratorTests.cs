using System.Text.Json;
using FluentAssertions;
using Gmsd.Agents.Execution.Backlog.DeferredIssuesCurator;
using Gmsd.Agents.Tests.Fakes;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Backlog.DeferredIssuesCurator;

public class DeferredIssuesCuratorTests : IDisposable
{
    private readonly FakeWorkspace _workspace;
    private readonly Agents.Execution.Backlog.DeferredIssuesCurator.DeferredIssuesCurator _sut;

    public DeferredIssuesCuratorTests()
    {
        _workspace = new FakeWorkspace();
        _sut = new Agents.Execution.Backlog.DeferredIssuesCurator.DeferredIssuesCurator(
            new FakeDeterministicJsonSerializer());

        // Create directories
        Directory.CreateDirectory(Path.Combine(_workspace.AosRootPath, "spec", "issues"));
        Directory.CreateDirectory(Path.Combine(_workspace.AosRootPath, "state"));
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public async Task CurateAsync_WithCriticalIssue_RoutesToMainLoop()
    {
        // Arrange
        CreateTestIssue("ISS-0001", "critical", "Test critical issue");

        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            MinimumSeverityForMainLoop = "high",
            ApplyDecisions = true,
            WriteEvents = true
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.MainLoopCount.Should().Be(1);
        result.Recommendations[0].Decision.Should().Be(RoutingDecision.MainLoop);
        result.Recommendations[0].Severity.Should().Be("critical");
    }

    [Fact]
    public async Task CurateAsync_WithHighSeverityIssue_RoutesToMainLoop()
    {
        // Arrange
        CreateTestIssue("ISS-0001", "high", "Test high severity issue");

        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            MinimumSeverityForMainLoop = "high",
            ApplyDecisions = true,
            WriteEvents = true
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.MainLoopCount.Should().Be(1);
        result.Recommendations[0].Decision.Should().Be(RoutingDecision.MainLoop);
        result.Recommendations[0].Severity.Should().Be("high");
    }

    [Fact]
    public async Task CurateAsync_WithMediumSeverity_StaysDeferred()
    {
        // Arrange
        CreateTestIssue("ISS-0001", "medium", "Test medium severity issue");

        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            MinimumSeverityForMainLoop = "high",
            ApplyDecisions = true,
            WriteEvents = true
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.DeferredCount.Should().Be(1);
        result.Recommendations[0].Decision.Should().Be(RoutingDecision.Deferred);
        result.Recommendations[0].Severity.Should().Be("medium");
    }

    [Fact]
    public async Task CurateAsync_WithLowSeverity_StaysDeferred()
    {
        // Arrange
        CreateTestIssue("ISS-0001", "low", "Test low severity issue");

        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            MinimumSeverityForMainLoop = "high",
            ApplyDecisions = true,
            WriteEvents = true
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.DeferredCount.Should().Be(1);
        result.Recommendations[0].Decision.Should().Be(RoutingDecision.Deferred);
    }

    [Fact]
    public async Task CurateAsync_ResolvedIssue_IsDiscarded()
    {
        // Arrange
        CreateTestIssue("ISS-0001", "high", "Test resolved issue", status: "resolved");

        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            MinimumSeverityForMainLoop = "high",
            ApplyDecisions = true,
            WriteEvents = true
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.DiscardedCount.Should().Be(1);
        result.Recommendations[0].Decision.Should().Be(RoutingDecision.Discarded);
    }

    [Fact]
    public async Task CurateAsync_MultipleIssues_CorrectCounts()
    {
        // Arrange
        CreateTestIssue("ISS-0001", "critical", "Critical issue");
        CreateTestIssue("ISS-0002", "high", "High issue");
        CreateTestIssue("ISS-0003", "medium", "Medium issue");
        CreateTestIssue("ISS-0004", "low", "Low issue");

        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            MinimumSeverityForMainLoop = "high",
            ApplyDecisions = true,
            WriteEvents = true
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.MainLoopCount.Should().Be(2); // critical + high
        result.DeferredCount.Should().Be(2); // medium + low
        result.Recommendations.Should().HaveCount(4);
    }

    [Fact]
    public async Task CurateAsync_UpdatesIssueFileWithTriageDecision()
    {
        // Arrange
        CreateTestIssue("ISS-0001", "critical", "Test critical issue");

        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            MinimumSeverityForMainLoop = "high",
            ApplyDecisions = true,
            WriteEvents = false
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Recommendations[0].IssueFileUpdated.Should().BeTrue();

        var issuePath = Path.Combine(_workspace.AosRootPath, "spec", "issues", "ISS-0001.json");
        var json = await File.ReadAllTextAsync(issuePath);
        var issue = JsonSerializer.Deserialize<JsonElement>(json);

        issue.GetProperty("status").GetString().Should().Be("triaged");
        issue.GetProperty("routingDecision").GetString().Should().Be("main-loop");
        issue.GetProperty("severity").GetString().Should().Be("critical");
        issue.GetProperty("rationale").GetString().Should().Contain("critical");
    }

    [Fact]
    public async Task CurateAsync_WritesTriageEventToEventsNdjson()
    {
        // Arrange
        CreateTestIssue("ISS-0001", "high", "Test high severity issue");

        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            MinimumSeverityForMainLoop = "high",
            ApplyDecisions = false,
            WriteEvents = true
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Recommendations[0].EventWritten.Should().BeTrue();

        var eventsPath = Path.Combine(_workspace.AosRootPath, "state", "events.ndjson");
        File.Exists(eventsPath).Should().BeTrue();

        var lines = await File.ReadAllLinesAsync(eventsPath);
        lines.Should().HaveCountGreaterThanOrEqualTo(1);

        var evt = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        evt.GetProperty("eventType").GetString().Should().Be("triage");
        evt.GetProperty("issueId").GetString().Should().Be("ISS-0001");
        evt.GetProperty("severity").GetString().Should().Be("high");
        evt.GetProperty("routingDecision").GetString().Should().Be("main-loop");
        evt.GetProperty("schemaVersion").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task CurateAsync_WithEmptyIssuesDirectory_ReturnsEmptyResult()
    {
        // Arrange - no issues created
        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            MinimumSeverityForMainLoop = "high"
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Recommendations.Should().BeEmpty();
    }

    [Fact]
    public async Task CurateAsync_FilterBySpecificIssueIds_OnlyTriagesMatching()
    {
        // Arrange
        CreateTestIssue("ISS-0001", "critical", "Critical issue 1");
        CreateTestIssue("ISS-0002", "low", "Low issue 2");
        CreateTestIssue("ISS-0003", "high", "High issue 3");

        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            IssueIds = new[] { "ISS-0001", "ISS-0003" },
            MinimumSeverityForMainLoop = "high"
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Recommendations.Should().HaveCount(2);
        result.Recommendations.Select(r => r.IssueId).Should().ContainInOrder("ISS-0001", "ISS-0003");
    }

    [Fact]
    public async Task CurateAsync_WithLowerThreshold_MediumRoutesToMainLoop()
    {
        // Arrange
        CreateTestIssue("ISS-0001", "medium", "Medium severity issue");

        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            MinimumSeverityForMainLoop = "medium",
            ApplyDecisions = true
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.MainLoopCount.Should().Be(1);
        result.Recommendations[0].Decision.Should().Be(RoutingDecision.MainLoop);
    }

    [Fact]
    public async Task CurateAsync_WithoutApplyDecisions_DoesNotUpdateFiles()
    {
        // Arrange
        CreateTestIssue("ISS-0001", "high", "Test issue");

        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            MinimumSeverityForMainLoop = "high",
            ApplyDecisions = false,
            WriteEvents = false
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Recommendations[0].IssueFileUpdated.Should().BeFalse();

        var issuePath = Path.Combine(_workspace.AosRootPath, "spec", "issues", "ISS-0001.json");
        var json = await File.ReadAllTextAsync(issuePath);
        var issue = JsonSerializer.Deserialize<JsonElement>(json);

        // Original issue should not have triage fields
        issue.TryGetProperty("status", out _).Should().BeFalse();
        issue.TryGetProperty("routingDecision", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CurateAsync_WithoutWriteEvents_DoesNotCreateEventsFile()
    {
        // Arrange
        CreateTestIssue("ISS-0001", "high", "Test issue");

        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            MinimumSeverityForMainLoop = "high",
            ApplyDecisions = false,
            WriteEvents = false
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Recommendations[0].EventWritten.Should().BeFalse();

        var eventsPath = Path.Combine(_workspace.AosRootPath, "state", "events.ndjson");
        File.Exists(eventsPath).Should().BeFalse();
    }

    [Fact]
    public async Task CurateAsync_PreviouslyDiscardedIssue_StaysDiscarded()
    {
        // Arrange - issue already marked as discarded
        CreateTestIssue("ISS-0001", "critical", "Test issue", routingDecision: "discarded");

        var request = new DeferredIssuesCurationRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            MinimumSeverityForMainLoop = "high"
        };

        // Act
        var result = await _sut.CurateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.DiscardedCount.Should().Be(1);
        result.Recommendations[0].Decision.Should().Be(RoutingDecision.Discarded);
    }

    private void CreateTestIssue(
        string issueId,
        string severity,
        string description,
        string? status = null,
        string? routingDecision = null)
    {
        var issuesDir = Path.Combine(_workspace.AosRootPath, "spec", "issues");
        Directory.CreateDirectory(issuesDir);

        var issue = new Dictionary<string, object>
        {
            ["schemaVersion"] = 1,
            ["id"] = issueId,
            ["title"] = $"Issue {issueId}",
            ["description"] = description,
            ["severity"] = severity
        };

        if (status != null)
            issue["status"] = status;

        if (routingDecision != null)
            issue["routingDecision"] = routingDecision;

        var json = JsonSerializer.Serialize(issue, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        File.WriteAllText(Path.Combine(issuesDir, $"{issueId}.json"), json);
    }
}
