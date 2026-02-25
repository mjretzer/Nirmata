using System.Text.Json;
using Gmsd.Aos.Engine.Evidence;
using Gmsd.Aos.Engine.Workspace;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosRunPauseResumeStateTransitionTests
{
    [Fact]
    public void PauseRun_WhenRunInStartedStatus_TransitionsToParused()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var runId = AosRunId.New();
            var startedAtUtc = DateTimeOffset.UtcNow;

            AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
                aosRootPath: aosRoot,
                runId: runId,
                startedAtUtc: startedAtUtc,
                command: "run start",
                args: Array.Empty<string>()
            );

            var runJsonPath = Path.Combine(aosRoot, "evidence", "runs", runId, "artifacts", "run.json");
            var runJson = ReadJson(runJsonPath);
            Assert.Equal("started", runJson.RootElement.GetProperty("status").GetString());

            var pausedAtUtc = DateTimeOffset.UtcNow;
            AosRunEvidenceScaffolder.PauseRun(aosRoot, runId, pausedAtUtc);

            var pausedRunJson = ReadJson(runJsonPath);
            Assert.Equal("paused", pausedRunJson.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public void PauseRun_WhenRunNotInStartedStatus_ThrowsInvalidOperationException()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var runId = AosRunId.New();
            var startedAtUtc = DateTimeOffset.UtcNow;

            AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
                aosRootPath: aosRoot,
                runId: runId,
                startedAtUtc: startedAtUtc,
                command: "run start",
                args: Array.Empty<string>()
            );

            var pausedAtUtc = DateTimeOffset.UtcNow;
            AosRunEvidenceScaffolder.PauseRun(aosRoot, runId, pausedAtUtc);

            var act = () => AosRunEvidenceScaffolder.PauseRun(aosRoot, runId, pausedAtUtc);

            var ex = Assert.Throws<InvalidOperationException>(act);
            Assert.Contains("Cannot pause run in 'paused' status", ex.Message);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public void ResumeRun_WhenRunInPausedStatus_TransitionsToStarted()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var runId = AosRunId.New();
            var startedAtUtc = DateTimeOffset.UtcNow;

            AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
                aosRootPath: aosRoot,
                runId: runId,
                startedAtUtc: startedAtUtc,
                command: "run start",
                args: Array.Empty<string>()
            );

            var pausedAtUtc = DateTimeOffset.UtcNow;
            AosRunEvidenceScaffolder.PauseRun(aosRoot, runId, pausedAtUtc);

            var runJsonPath = Path.Combine(aosRoot, "evidence", "runs", runId, "artifacts", "run.json");
            var pausedRunJson = ReadJson(runJsonPath);
            Assert.Equal("paused", pausedRunJson.RootElement.GetProperty("status").GetString());

            var resumedAtUtc = DateTimeOffset.UtcNow;
            AosRunEvidenceScaffolder.ResumeRun(aosRoot, runId, resumedAtUtc);

            var resumedRunJson = ReadJson(runJsonPath);
            Assert.Equal("started", resumedRunJson.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public void ResumeRun_WhenRunNotInPausedStatus_ThrowsInvalidOperationException()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var runId = AosRunId.New();
            var startedAtUtc = DateTimeOffset.UtcNow;

            AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
                aosRootPath: aosRoot,
                runId: runId,
                startedAtUtc: startedAtUtc,
                command: "run start",
                args: Array.Empty<string>()
            );

            var resumedAtUtc = DateTimeOffset.UtcNow;
            var act = () => AosRunEvidenceScaffolder.ResumeRun(aosRoot, runId, resumedAtUtc);

            var ex = Assert.Throws<InvalidOperationException>(act);
            Assert.Contains("Cannot resume run in 'started' status", ex.Message);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public void PauseRun_UpdatesRunIndexStatus()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var runId = AosRunId.New();
            var startedAtUtc = DateTimeOffset.UtcNow;

            AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
                aosRootPath: aosRoot,
                runId: runId,
                startedAtUtc: startedAtUtc,
                command: "run start",
                args: Array.Empty<string>()
            );

            var indexJsonPath = Path.Combine(aosRoot, "evidence", "runs", "index.json");
            var indexJson = ReadJson(indexJsonPath);
            var items = indexJson.RootElement.GetProperty("items").EnumerateArray().ToArray();
            var runItem = items.First(i => i.GetProperty("runId").GetString() == runId);
            Assert.Equal("started", runItem.GetProperty("status").GetString());

            var pausedAtUtc = DateTimeOffset.UtcNow;
            AosRunEvidenceScaffolder.PauseRun(aosRoot, runId, pausedAtUtc);

            var pausedIndexJson = ReadJson(indexJsonPath);
            var pausedItems = pausedIndexJson.RootElement.GetProperty("items").EnumerateArray().ToArray();
            var pausedRunItem = pausedItems.First(i => i.GetProperty("runId").GetString() == runId);
            Assert.Equal("paused", pausedRunItem.GetProperty("status").GetString());
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public void ResumeRun_UpdatesRunIndexStatus()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var runId = AosRunId.New();
            var startedAtUtc = DateTimeOffset.UtcNow;

            AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
                aosRootPath: aosRoot,
                runId: runId,
                startedAtUtc: startedAtUtc,
                command: "run start",
                args: Array.Empty<string>()
            );

            var pausedAtUtc = DateTimeOffset.UtcNow;
            AosRunEvidenceScaffolder.PauseRun(aosRoot, runId, pausedAtUtc);

            var indexJsonPath = Path.Combine(aosRoot, "evidence", "runs", "index.json");
            var pausedIndexJson = ReadJson(indexJsonPath);
            var pausedItems = pausedIndexJson.RootElement.GetProperty("items").EnumerateArray().ToArray();
            var pausedRunItem = pausedItems.First(i => i.GetProperty("runId").GetString() == runId);
            Assert.Equal("paused", pausedRunItem.GetProperty("status").GetString());

            var resumedAtUtc = DateTimeOffset.UtcNow;
            AosRunEvidenceScaffolder.ResumeRun(aosRoot, runId, resumedAtUtc);

            var resumedIndexJson = ReadJson(indexJsonPath);
            var resumedItems = resumedIndexJson.RootElement.GetProperty("items").EnumerateArray().ToArray();
            var resumedRunItem = resumedItems.First(i => i.GetProperty("runId").GetString() == runId);
            Assert.Equal("started", resumedRunItem.GetProperty("status").GetString());
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public void PauseRun_WithInvalidRunId_ThrowsArgumentException()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var invalidRunId = "invalid-run-id";
            var pausedAtUtc = DateTimeOffset.UtcNow;

            var act = () => AosRunEvidenceScaffolder.PauseRun(aosRoot, invalidRunId, pausedAtUtc);

            var ex = Assert.Throws<ArgumentException>(act);
            Assert.Contains("Invalid run id", ex.Message);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public void ResumeRun_WithInvalidRunId_ThrowsArgumentException()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var invalidRunId = "invalid-run-id";
            var resumedAtUtc = DateTimeOffset.UtcNow;

            var act = () => AosRunEvidenceScaffolder.ResumeRun(aosRoot, invalidRunId, resumedAtUtc);

            var ex = Assert.Throws<ArgumentException>(act);
            Assert.Contains("Invalid run id", ex.Message);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public void PauseRun_WithNonexistentRun_ThrowsFileNotFoundException()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var runId = AosRunId.New();
            var pausedAtUtc = DateTimeOffset.UtcNow;

            var act = () => AosRunEvidenceScaffolder.PauseRun(aosRoot, runId, pausedAtUtc);

            var ex = Assert.Throws<FileNotFoundException>(act);
            Assert.Contains("Run metadata not found", ex.Message);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public void ResumeRun_WithNonexistentRun_ThrowsFileNotFoundException()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var runId = AosRunId.New();
            var resumedAtUtc = DateTimeOffset.UtcNow;

            var act = () => AosRunEvidenceScaffolder.ResumeRun(aosRoot, runId, resumedAtUtc);

            var ex = Assert.Throws<FileNotFoundException>(act);
            Assert.Contains("Run metadata not found", ex.Message);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public void PauseResumeRun_MultipleTransitions_MaintainsCorrectStatus()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var runId = AosRunId.New();
            var startedAtUtc = DateTimeOffset.UtcNow;

            AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
                aosRootPath: aosRoot,
                runId: runId,
                startedAtUtc: startedAtUtc,
                command: "run start",
                args: Array.Empty<string>()
            );

            var runJsonPath = Path.Combine(aosRoot, "evidence", "runs", runId, "artifacts", "run.json");

            var runJson = ReadJson(runJsonPath);
            Assert.Equal("started", runJson.RootElement.GetProperty("status").GetString());

            AosRunEvidenceScaffolder.PauseRun(aosRoot, runId, DateTimeOffset.UtcNow);
            var pausedJson = ReadJson(runJsonPath);
            Assert.Equal("paused", pausedJson.RootElement.GetProperty("status").GetString());

            AosRunEvidenceScaffolder.ResumeRun(aosRoot, runId, DateTimeOffset.UtcNow);
            var resumedJson = ReadJson(runJsonPath);
            Assert.Equal("started", resumedJson.RootElement.GetProperty("status").GetString());

            AosRunEvidenceScaffolder.PauseRun(aosRoot, runId, DateTimeOffset.UtcNow);
            var pausedAgainJson = ReadJson(runJsonPath);
            Assert.Equal("paused", pausedAgainJson.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static string CreateTempDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"gmsd-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);
        return tempPath;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static JsonDocument ReadJson(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonDocument.Parse(json);
    }
}
