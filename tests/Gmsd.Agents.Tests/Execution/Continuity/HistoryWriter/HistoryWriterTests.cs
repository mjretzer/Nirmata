using FluentAssertions;
using Gmsd.Agents.Execution.Continuity.HistoryWriter;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Continuity.HistoryWriter;

public class HistoryWriterTests : IDisposable
{
    private readonly string _tempAosRoot;
    private readonly Agents.Execution.Continuity.HistoryWriter.HistoryWriter _sut;

    public HistoryWriterTests()
    {
        _tempAosRoot = Path.Combine(Path.GetTempPath(), $"aos-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempAosRoot);
        Directory.CreateDirectory(Path.Combine(_tempAosRoot, "evidence", "runs"));
        Directory.CreateDirectory(Path.Combine(_tempAosRoot, "cache"));

        _sut = new Agents.Execution.Continuity.HistoryWriter.HistoryWriter(_tempAosRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempAosRoot))
            {
                Directory.Delete(_tempAosRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private string CreateValidRunId() => Guid.NewGuid().ToString("N").ToLowerInvariant();

    private void CreateRunEvidenceSummary(string runId, int exitCode = 0, string status = "completed")
    {
        var runPath = Path.Combine(_tempAosRoot, "evidence", "runs", runId);
        Directory.CreateDirectory(runPath);

        var summary = $@"{{
  ""runId"": ""{runId}"",
  ""status"": ""{status}"",
  ""startedAtUtc"": ""{DateTimeOffset.UtcNow.AddMinutes(-5):O}"",
  ""finishedAtUtc"": ""{DateTimeOffset.UtcNow:O}"",
  ""exitCode"": {exitCode},
  ""artifacts"": {{
    ""runMetadata"": "".aos/evidence/runs/{runId}/artifacts/run.json"",
    ""packet"": "".aos/evidence/runs/{runId}/artifacts/packet.json"",
    ""result"": "".aos/evidence/runs/{runId}/artifacts/result.json""
  }}
}}";

        File.WriteAllText(Path.Combine(runPath, "summary.json"), summary);
    }

    [Fact]
    public void Constructor_WhenAosRootPathIsNull_ThrowsArgumentNullException()
    {
        var act = () => new Agents.Execution.Continuity.HistoryWriter.HistoryWriter(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("aosRootPath");
    }

    [Fact]
    public void Constructor_WhenAosRootPathDoesNotExist_ThrowsArgumentException()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid():N}");
        var act = () => new Agents.Execution.Continuity.HistoryWriter.HistoryWriter(nonExistentPath);
        act.Should().Throw<ArgumentException>().WithMessage("*AOS root path does not exist*");
    }

    [Fact]
    public void SummaryPath_ReturnsCorrectPath()
    {
        _sut.SummaryPath.Should().Be(Path.Combine(_tempAosRoot, "spec", "summary.md"));
    }

    [Fact]
    public async Task AppendAsync_WhenRunIdIsNull_ThrowsArgumentNullException()
    {
        var act = async () => await _sut.AppendAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("runId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("RUN-123")]
    [InlineData("1234567890123456789012345678901g")] // 32 chars but contains non-hex
    public async Task AppendAsync_WhenRunIdIsInvalid_ThrowsArgumentException(string invalidRunId)
    {
        var act = async () => await _sut.AppendAsync(invalidRunId);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Invalid run id*");
    }

    [Fact]
    public async Task AppendAsync_WhenEvidenceDoesNotExist_ThrowsFileNotFoundException()
    {
        var runId = CreateValidRunId();
        var act = async () => await _sut.AppendAsync(runId);
        await act.Should().ThrowAsync<FileNotFoundException>().WithMessage("*Evidence not found*");
    }

    [Fact]
    public async Task AppendAsync_WithValidRunId_CreatesHistoryEntryWithCorrectKey()
    {
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId);

        var entry = await _sut.AppendAsync(runId);

        entry.Should().NotBeNull();
        entry.Key.Should().Be(runId);
        entry.RunId.Should().Be(runId);
        entry.TaskId.Should().BeNull();
    }

    [Fact]
    public async Task AppendAsync_WithTaskId_CreatesHistoryEntryWithCompoundKey()
    {
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId);

        var entry = await _sut.AppendAsync(runId, "TSK-000001");

        entry.Key.Should().Be($"{runId}/TSK-000001");
        entry.RunId.Should().Be(runId);
        entry.TaskId.Should().Be("TSK-000001");
    }

    [Fact]
    public async Task AppendAsync_IncludesTimestampInIso8601Format()
    {
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId);
        var before = DateTimeOffset.UtcNow;

        var entry = await _sut.AppendAsync(runId);
        var after = DateTimeOffset.UtcNow;

        var timestamp = DateTimeOffset.Parse(entry.Timestamp);
        timestamp.Should().BeOnOrAfter(before);
        timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public async Task AppendAsync_IncludesSchemaVersion()
    {
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId);

        var entry = await _sut.AppendAsync(runId);

        entry.SchemaVersion.Should().Be("1.0");
    }

    [Fact]
    public async Task AppendAsync_WithSuccessfulRun_CreatesPassedVerification()
    {
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId, exitCode: 0, status: "completed");

        var entry = await _sut.AppendAsync(runId);

        entry.Verification.Should().NotBeNull();
        entry.Verification.Status.Should().Be("passed");
        entry.Verification.Method.Should().Be("run-verifier");
        entry.Verification.Issues.Should().BeNull();
        entry.Verification.Details.Should().Be("Run completed successfully");
    }

    [Fact]
    public async Task AppendAsync_WithFailedRun_CreatesFailedVerification()
    {
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId, exitCode: 1, status: "failed");

        var entry = await _sut.AppendAsync(runId);

        entry.Verification.Status.Should().Be("failed");
        entry.Verification.Method.Should().Be("run-failure-detector");
        entry.Verification.Issues.Should().Be(1);
        entry.Verification.Details.Should().Be("Run exited with code 1");
    }

    [Fact]
    public async Task AppendAsync_CreatesEvidencePointersFromArtifacts()
    {
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId);

        var entry = await _sut.AppendAsync(runId);

        entry.Evidence.Should().HaveCount(4); // summary + 3 artifacts
        entry.Evidence.Should().Contain(e => e.Type == "summary" && e.Path.Contains(runId));
        entry.Evidence.Should().Contain(e => e.Type == "metadata");
        entry.Evidence.Should().Contain(e => e.Type == "packet");
        entry.Evidence.Should().Contain(e => e.Type == "result");
    }

    [Fact]
    public async Task AppendAsync_WithNarrative_IncludesNarrativeInEntry()
    {
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId);
        var narrative = "Completed task implementation with all tests passing.";

        var entry = await _sut.AppendAsync(runId, narrative: narrative);

        entry.Narrative.Should().Be(narrative);
    }

    [Fact]
    public async Task AppendAsync_AppendsToSummaryMdFile()
    {
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId);

        await _sut.AppendAsync(runId, narrative: "First entry");

        var summaryPath = Path.Combine(_tempAosRoot, "spec", "summary.md");
        File.Exists(summaryPath).Should().BeTrue();

        var content = await File.ReadAllTextAsync(summaryPath);
        content.Should().Contain($"### {runId}");
        content.Should().Contain("First entry");
        content.Should().Contain("**Timestamp:**");
        content.Should().Contain("**Verification:**");
    }

    [Fact]
    public async Task AppendAsync_MultipleCalls_AppendsMultipleEntries()
    {
        var runId1 = CreateValidRunId();
        var runId2 = CreateValidRunId();
        CreateRunEvidenceSummary(runId1);
        CreateRunEvidenceSummary(runId2);

        await _sut.AppendAsync(runId1, narrative: "First run");
        await _sut.AppendAsync(runId2, narrative: "Second run");

        var summaryPath = Path.Combine(_tempAosRoot, "spec", "summary.md");
        var content = await File.ReadAllTextAsync(summaryPath);

        content.Should().Contain($"### {runId1}");
        content.Should().Contain($"### {runId2}");
        content.Should().Contain("First run");
        content.Should().Contain("Second run");
    }

    [Fact]
    public void Exists_WhenEntryDoesNotExist_ReturnsFalse()
    {
        var runId = CreateValidRunId();
        _sut.Exists(runId).Should().BeFalse();
    }

    [Fact]
    public async Task Exists_WhenEntryExists_ReturnsTrue()
    {
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId);
        await _sut.AppendAsync(runId);

        _sut.Exists(runId).Should().BeTrue();
    }

    [Fact]
    public async Task Exists_WithTaskId_WhenCompoundEntryExists_ReturnsTrue()
    {
        var runId = CreateValidRunId();
        CreateRunEvidenceSummary(runId);
        await _sut.AppendAsync(runId, "TSK-000001");

        _sut.Exists(runId, "TSK-000001").Should().BeTrue();
        _sut.Exists(runId).Should().BeFalse(); // Run-level entry doesn't exist
    }

    [Fact]
    public async Task AppendAsync_WithMalformedEvidence_ReturnsMinimalEvidence()
    {
        var runId = CreateValidRunId();
        var runPath = Path.Combine(_tempAosRoot, "evidence", "runs", runId);
        Directory.CreateDirectory(runPath);
        File.WriteAllText(Path.Combine(runPath, "summary.json"), "{ invalid json");

        var entry = await _sut.AppendAsync(runId);

        entry.Should().NotBeNull();
        entry.RunId.Should().Be(runId);
        entry.Verification.Should().NotBeNull();
    }

    [Fact]
    public async Task AppendAsync_CreatesSpecDirectoryIfNotExists()
    {
        var runId = CreateValidRunId();
        // Delete spec directory if it exists
        var specDir = Path.Combine(_tempAosRoot, "spec");
        if (Directory.Exists(specDir))
        {
            Directory.Delete(specDir, true);
        }

        CreateRunEvidenceSummary(runId);
        await _sut.AppendAsync(runId);

        Directory.Exists(specDir).Should().BeTrue();
    }

    [Fact]
    public async Task AppendAsync_CreatesCacheDirectoryIfNotExists()
    {
        var runId = CreateValidRunId();
        // Delete cache directory if it exists
        var cacheDir = Path.Combine(_tempAosRoot, "cache");
        if (Directory.Exists(cacheDir))
        {
            Directory.Delete(cacheDir, true);
        }

        CreateRunEvidenceSummary(runId);
        await _sut.AppendAsync(runId);

        Directory.Exists(cacheDir).Should().BeTrue();
    }
}
