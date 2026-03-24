using System.Text.Json;
using nirmata.Data.Dto.Requests.Issues;
using nirmata.Services.Implementations;
using Xunit;

namespace nirmata.Services.Tests;

/// <summary>
/// Unit tests for <see cref="IssueService"/>.
/// Each test creates an isolated temp workspace and cleans up after itself.
/// </summary>
public sealed class IssueServiceTests : IDisposable
{
    private readonly IssueService _sut = new();
    private readonly string _root;

    public IssueServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"nirmata-issue-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private string IssuesDir() => Path.Combine(_root, ".aos", "spec", "issues");

    private void WriteIssueFile(string issueId, object payload)
    {
        var dir = IssuesDir();
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{issueId}.json"), JsonSerializer.Serialize(payload));
    }

    // ── Empty workspace ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_EmptyWorkspace_ReturnsEmptyList()
    {
        // Arrange — no .aos directory exists

        // Act
        var result = await _sut.GetAllAsync(_root);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_EmptyIssuesDirectory_ReturnsEmptyList()
    {
        // Arrange — directory exists but no files
        Directory.CreateDirectory(IssuesDir());

        // Act
        var result = await _sut.GetAllAsync(_root);

        // Assert
        Assert.Empty(result);
    }

    // ── Persistence: Create ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WritesIssueToDisk_WithAutoAssignedId()
    {
        // Arrange
        var request = new IssueCreateRequest { Title = "Button is broken" };

        // Act
        var dto = await _sut.CreateAsync(_root, request);

        // Assert — id was assigned and file exists
        Assert.NotNull(dto);
        Assert.StartsWith("ISS-", dto.Id);
        Assert.True(File.Exists(Path.Combine(IssuesDir(), $"{dto.Id}.json")));
    }

    [Fact]
    public async Task CreateAsync_FirstIssue_GetsIdIss0001()
    {
        // Arrange — empty workspace
        var request = new IssueCreateRequest { Title = "First issue" };

        // Act
        var dto = await _sut.CreateAsync(_root, request);

        // Assert
        Assert.Equal("ISS-0001", dto.Id);
    }

    [Fact]
    public async Task CreateAsync_SequentialIssues_GetIncrementingIds()
    {
        // Arrange
        var r1 = new IssueCreateRequest { Title = "Issue 1" };
        var r2 = new IssueCreateRequest { Title = "Issue 2" };
        var r3 = new IssueCreateRequest { Title = "Issue 3" };

        // Act
        var dto1 = await _sut.CreateAsync(_root, r1);
        var dto2 = await _sut.CreateAsync(_root, r2);
        var dto3 = await _sut.CreateAsync(_root, r3);

        // Assert
        Assert.Equal("ISS-0001", dto1.Id);
        Assert.Equal("ISS-0002", dto2.Id);
        Assert.Equal("ISS-0003", dto3.Id);
    }

    [Fact]
    public async Task CreateAsync_PersistsAllRequestFields()
    {
        // Arrange
        var request = new IssueCreateRequest
        {
            Title = "Login fails",
            Severity = "high",
            Scope = "auth",
            Repro = "1. Open login page 2. Submit form",
            Expected = "Redirect to dashboard",
            Actual = "500 error",
            ImpactedFiles = ["src/auth/login.ts"],
            PhaseId = "PH-0001",
            TaskId = "TSK-000001",
            MilestoneId = "MS-0001",
        };

        // Act
        var dto = await _sut.CreateAsync(_root, request);

        // Assert — every field from the request is present in the returned DTO
        Assert.Equal("Login fails", dto.Title);
        Assert.Equal("high", dto.Severity);
        Assert.Equal("auth", dto.Scope);
        Assert.Equal("1. Open login page 2. Submit form", dto.Repro);
        Assert.Equal("Redirect to dashboard", dto.Expected);
        Assert.Equal("500 error", dto.Actual);
        Assert.Contains("src/auth/login.ts", dto.ImpactedFiles);
        Assert.Equal("PH-0001", dto.PhaseId);
        Assert.Equal("TSK-000001", dto.TaskId);
        Assert.Equal("MS-0001", dto.MilestoneId);
    }

    [Fact]
    public async Task CreateAsync_NewIssue_HasStatusOpen()
    {
        // Arrange
        var request = new IssueCreateRequest { Title = "New bug" };

        // Act
        var dto = await _sut.CreateAsync(_root, request);

        // Assert — status always starts as "open"
        Assert.Equal("open", dto.Status);
    }

    // ── Persistence: GetById ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingIssue_ReturnsDto()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "Bug A", status = "open" });

        // Act
        var dto = await _sut.GetByIdAsync(_root, "ISS-0001");

        // Assert
        Assert.NotNull(dto);
        Assert.Equal("ISS-0001", dto.Id);
        Assert.Equal("Bug A", dto.Title);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentIssue_ReturnsNull()
    {
        // Arrange — workspace exists but issue does not
        Directory.CreateDirectory(IssuesDir());

        // Act
        var dto = await _sut.GetByIdAsync(_root, "ISS-9999");

        // Assert
        Assert.Null(dto);
    }

    [Fact]
    public async Task GetByIdAsync_IdMissingInFile_FallsBackToFilename()
    {
        // Arrange — file exists but JSON has no "id" field
        WriteIssueFile("ISS-0007", new { title = "Fallback", status = "open" });

        // Act
        var dto = await _sut.GetByIdAsync(_root, "ISS-0007");

        // Assert — id is inferred from filename
        Assert.NotNull(dto);
        Assert.Equal("ISS-0007", dto.Id);
    }

    // ── Persistence: GetAll (unfiltered) ──────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_MultipleIssues_ReturnsAll()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "A", status = "open" });
        WriteIssueFile("ISS-0002", new { id = "ISS-0002", title = "B", status = "resolved" });
        WriteIssueFile("ISS-0003", new { id = "ISS-0003", title = "C", status = "open" });

        // Act
        var result = await _sut.GetAllAsync(_root);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_MalformedJsonFile_IsSkippedGracefully()
    {
        // Arrange — one broken file, one valid file
        Directory.CreateDirectory(IssuesDir());
        File.WriteAllText(Path.Combine(IssuesDir(), "ISS-0001.json"), "{ not valid }}}");
        WriteIssueFile("ISS-0002", new { id = "ISS-0002", title = "Valid", status = "open" });

        // Act — must not throw
        var result = await _sut.GetAllAsync(_root);

        // Assert — only the valid issue is returned
        Assert.Single(result);
        Assert.Equal("ISS-0002", result[0].Id);
    }

    // ── Persistence: Update ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ExistingIssue_UpdatesFieldsAndReturnsDto()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "Old title", status = "open" });
        var request = new IssueUpdateRequest { Title = "New title", Severity = "critical" };

        // Act
        var dto = await _sut.UpdateAsync(_root, "ISS-0001", request);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal("New title", dto.Title);
        Assert.Equal("critical", dto.Severity);
        Assert.Equal("ISS-0001", dto.Id);
    }

    [Fact]
    public async Task UpdateAsync_PreservesExistingStatus()
    {
        // Arrange — issue already has status "investigating"
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "Bug", status = "investigating" });
        var request = new IssueUpdateRequest { Title = "Updated bug" };

        // Act
        var dto = await _sut.UpdateAsync(_root, "ISS-0001", request);

        // Assert — status is not wiped
        Assert.Equal("investigating", dto!.Status);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentIssue_ReturnsNull()
    {
        // Arrange
        Directory.CreateDirectory(IssuesDir());
        var request = new IssueUpdateRequest { Title = "Doesn't matter" };

        // Act
        var dto = await _sut.UpdateAsync(_root, "ISS-9999", request);

        // Assert
        Assert.Null(dto);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangesToDisk()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "Original", status = "open" });
        var request = new IssueUpdateRequest { Title = "Persisted change", Severity = "low" };

        // Act
        await _sut.UpdateAsync(_root, "ISS-0001", request);

        // Assert — re-read the file from disk to confirm persistence
        var reloaded = await _sut.GetByIdAsync(_root, "ISS-0001");
        Assert.NotNull(reloaded);
        Assert.Equal("Persisted change", reloaded.Title);
        Assert.Equal("low", reloaded.Severity);
    }

    // ── Persistence: Delete ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingIssue_ReturnsTrueAndRemovesFile()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "To delete", status = "open" });

        // Act
        var deleted = await _sut.DeleteAsync(_root, "ISS-0001");

        // Assert
        Assert.True(deleted);
        Assert.False(File.Exists(Path.Combine(IssuesDir(), "ISS-0001.json")));
    }

    [Fact]
    public async Task DeleteAsync_NonExistentIssue_ReturnsFalse()
    {
        // Arrange
        Directory.CreateDirectory(IssuesDir());

        // Act
        var deleted = await _sut.DeleteAsync(_root, "ISS-9999");

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteAsync_IssueNoLongerReturnedAfterDeletion()
    {
        // Arrange — two issues, delete one
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "Keep", status = "open" });
        WriteIssueFile("ISS-0002", new { id = "ISS-0002", title = "Remove", status = "open" });

        // Act
        await _sut.DeleteAsync(_root, "ISS-0002");
        var remaining = await _sut.GetAllAsync(_root);

        // Assert
        Assert.Single(remaining);
        Assert.Equal("ISS-0001", remaining[0].Id);
    }

    // ── Status updates ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_ExistingIssue_UpdatesStatusField()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "Bug", status = "open" });

        // Act
        var dto = await _sut.UpdateStatusAsync(_root, "ISS-0001", "resolved");

        // Assert
        Assert.NotNull(dto);
        Assert.Equal("resolved", dto.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_PreservesOtherFields()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new
        {
            id = "ISS-0001", title = "Important bug", status = "open",
            severity = "high", phaseId = "PH-0001",
        });

        // Act
        var dto = await _sut.UpdateStatusAsync(_root, "ISS-0001", "investigating");

        // Assert — other fields are not wiped
        Assert.Equal("Important bug", dto!.Title);
        Assert.Equal("high", dto.Severity);
        Assert.Equal("PH-0001", dto.PhaseId);
    }

    [Fact]
    public async Task UpdateStatusAsync_PersistsChangesToDisk()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "Bug", status = "open" });

        // Act
        await _sut.UpdateStatusAsync(_root, "ISS-0001", "deferred");
        var reloaded = await _sut.GetByIdAsync(_root, "ISS-0001");

        // Assert — status is persisted
        Assert.Equal("deferred", reloaded!.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_NonExistentIssue_ReturnsNull()
    {
        // Arrange
        Directory.CreateDirectory(IssuesDir());

        // Act
        var dto = await _sut.UpdateStatusAsync(_root, "ISS-9999", "resolved");

        // Assert
        Assert.Null(dto);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("investigating")]
    [InlineData("resolved")]
    [InlineData("deferred")]
    [InlineData("wontfix")]
    public async Task UpdateStatusAsync_AllValidStatuses_AreAccepted(string newStatus)
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "Bug", status = "open" });

        // Act
        var dto = await _sut.UpdateStatusAsync(_root, "ISS-0001", newStatus);

        // Assert
        Assert.Equal(newStatus, dto!.Status);
    }

    // ── Filters ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_FilterByStatus_ReturnsOnlyMatchingIssues()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "A", status = "open" });
        WriteIssueFile("ISS-0002", new { id = "ISS-0002", title = "B", status = "resolved" });
        WriteIssueFile("ISS-0003", new { id = "ISS-0003", title = "C", status = "open" });

        // Act
        var result = await _sut.GetAllAsync(_root, status: "open");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, i => Assert.Equal("open", i.Status));
    }

    [Fact]
    public async Task GetAllAsync_FilterBySeverity_ReturnsOnlyMatchingIssues()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "A", status = "open", severity = "high" });
        WriteIssueFile("ISS-0002", new { id = "ISS-0002", title = "B", status = "open", severity = "low" });
        WriteIssueFile("ISS-0003", new { id = "ISS-0003", title = "C", status = "open", severity = "high" });

        // Act
        var result = await _sut.GetAllAsync(_root, severity: "high");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, i => Assert.Equal("high", i.Severity));
    }

    [Fact]
    public async Task GetAllAsync_FilterByPhaseId_ReturnsOnlyMatchingIssues()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "A", status = "open", phaseId = "PH-0001" });
        WriteIssueFile("ISS-0002", new { id = "ISS-0002", title = "B", status = "open", phaseId = "PH-0002" });

        // Act
        var result = await _sut.GetAllAsync(_root, phaseId: "PH-0001");

        // Assert
        Assert.Single(result);
        Assert.Equal("PH-0001", result[0].PhaseId);
    }

    [Fact]
    public async Task GetAllAsync_FilterByTaskId_ReturnsOnlyMatchingIssues()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "A", status = "open", taskId = "TSK-000001" });
        WriteIssueFile("ISS-0002", new { id = "ISS-0002", title = "B", status = "open", taskId = "TSK-000002" });

        // Act
        var result = await _sut.GetAllAsync(_root, taskId: "TSK-000001");

        // Assert
        Assert.Single(result);
        Assert.Equal("TSK-000001", result[0].TaskId);
    }

    [Fact]
    public async Task GetAllAsync_FilterByMilestoneId_ReturnsOnlyMatchingIssues()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "A", status = "open", milestoneId = "MS-0001" });
        WriteIssueFile("ISS-0002", new { id = "ISS-0002", title = "B", status = "open", milestoneId = "MS-0002" });

        // Act
        var result = await _sut.GetAllAsync(_root, milestoneId: "MS-0001");

        // Assert
        Assert.Single(result);
        Assert.Equal("MS-0001", result[0].MilestoneId);
    }

    [Fact]
    public async Task GetAllAsync_FilterByCombinedStatusAndSeverity_ReturnsOnlyMatchingIssues()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "A", status = "open", severity = "high" });
        WriteIssueFile("ISS-0002", new { id = "ISS-0002", title = "B", status = "resolved", severity = "high" });
        WriteIssueFile("ISS-0003", new { id = "ISS-0003", title = "C", status = "open", severity = "low" });

        // Act — only open+high
        var result = await _sut.GetAllAsync(_root, status: "open", severity: "high");

        // Assert
        Assert.Single(result);
        Assert.Equal("ISS-0001", result[0].Id);
    }

    [Fact]
    public async Task GetAllAsync_FilterWithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "A", status = "open" });

        // Act
        var result = await _sut.GetAllAsync(_root, status: "resolved");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_FilterIsCaseInsensitive_MatchesRegardlessOfCase()
    {
        // Arrange — status stored as "Open" (capitalised)
        WriteIssueFile("ISS-0001", new { id = "ISS-0001", title = "A", status = "Open" });

        // Act — filter with lowercase
        var result = await _sut.GetAllAsync(_root, status: "open");

        // Assert — still matched
        Assert.Single(result);
    }

    // ── Workspace isolation ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_TwoWorkspaces_IssuesAreIsolated()
    {
        // Arrange — two separate workspace roots
        var root2 = Path.Combine(Path.GetTempPath(), $"nirmata-issue-tests-ws2-{Guid.NewGuid():N}");
        try
        {
            WriteIssueFile("ISS-0001", new { id = "ISS-WS1", title = "WS1", status = "open" });

            var ws2IssuesDir = Path.Combine(root2, ".aos", "spec", "issues");
            Directory.CreateDirectory(ws2IssuesDir);
            File.WriteAllText(
                Path.Combine(ws2IssuesDir, "ISS-0001.json"),
                JsonSerializer.Serialize(new { id = "ISS-WS2", title = "WS2", status = "resolved" }));

            // Act
            var result1 = await _sut.GetAllAsync(_root);
            var result2 = await _sut.GetAllAsync(root2);

            // Assert — each workspace sees only its own issues
            Assert.Single(result1);
            Assert.Equal("ISS-WS1", result1[0].Id);

            Assert.Single(result2);
            Assert.Equal("ISS-WS2", result2[0].Id);
        }
        finally
        {
            if (Directory.Exists(root2))
                Directory.Delete(root2, recursive: true);
        }
    }
}
