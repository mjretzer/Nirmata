using System.Text;
using System.Text.Json;
using Gmsd.Aos.Engine.Evidence.TaskEvidence;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosTaskEvidenceLatestWriterTests
{
    private const string StableRunId = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void WriteLatest_WritesSchemaShapedDeterministicJson_WithStableDefaults()
    {
        var root = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(root, ".aos");
            Directory.CreateDirectory(aosRoot);

            AosTaskEvidenceLatestWriter.WriteLatest(
                aosRootPath: aosRoot,
                taskId: "TSK-000001",
                runId: StableRunId,
                gitCommit: null,
                diffstat: null
            );

            var latestPath = Path.Combine(aosRoot, "evidence", "task-evidence", "TSK-000001", "latest.json");
            Assert.True(File.Exists(latestPath), $"Expected latest.json at '{latestPath}'.");

            AssertDeterministicJsonFile(latestPath);

            using var doc = JsonDocument.Parse(File.ReadAllText(latestPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)));
            var rootObj = doc.RootElement;
            Assert.Equal(1, rootObj.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("TSK-000001", rootObj.GetProperty("taskId").GetString());
            Assert.Equal(StableRunId, rootObj.GetProperty("runId").GetString());
            Assert.True(rootObj.TryGetProperty("gitCommit", out var gitCommit));
            Assert.True(gitCommit.ValueKind is JsonValueKind.Null);

            var diffstat = rootObj.GetProperty("diffstat");
            Assert.Equal(0, diffstat.GetProperty("filesChanged").GetInt32());
            Assert.Equal(0, diffstat.GetProperty("insertions").GetInt32());
            Assert.Equal(0, diffstat.GetProperty("deletions").GetInt32());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void WriteLatest_IsAtomic_WhenFailureAfterTempWrite_DoesNotModifyTarget()
    {
        var root = CreateTempDirectory();
        Directory.CreateDirectory(root);
        try
        {
            var aosRoot = Path.Combine(root, ".aos");
            Directory.CreateDirectory(aosRoot);

            var latestPath = Path.Combine(aosRoot, "evidence", "task-evidence", "TSK-000001", "latest.json");

            AosTaskEvidenceLatestWriter.WriteLatest(
                aosRootPath: aosRoot,
                taskId: "TSK-000001",
                runId: StableRunId,
                gitCommit: "deadbeef",
                diffstat: new AosTaskEvidenceLatestWriter.TaskEvidenceDiffstat(1, 2, 3)
            );

            var oldBytes = File.ReadAllBytes(latestPath);
            Assert.NotEmpty(oldBytes);

            Assert.Throws<InvalidOperationException>(() =>
                AosTaskEvidenceLatestWriter.WriteLatestForTest(
                    aosRootPath: aosRoot,
                    taskId: "TSK-000001",
                    runId: StableRunId,
                    gitCommit: "cafebabe",
                    diffstat: new AosTaskEvidenceLatestWriter.TaskEvidenceDiffstat(9, 9, 9),
                    afterTempWriteBeforeCommit: static _ => throw new InvalidOperationException("boom")
                ));

            var afterBytes = File.ReadAllBytes(latestPath);
            Assert.Equal(oldBytes, afterBytes);

            // Sanity: still parseable JSON.
            using var _ = JsonDocument.Parse(Encoding.UTF8.GetString(afterBytes));

            // Temp files should be cleaned up (best-effort).
            var dir = Path.GetDirectoryName(latestPath)!;
            var tempPattern = $"{Path.GetFileName(latestPath)}.tmp-*";
            Assert.Empty(Directory.EnumerateFiles(dir, tempPattern));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void AssertDeterministicJsonFile(string path)
    {
        // No UTF-8 BOM.
        var bytes = File.ReadAllBytes(path);
        Assert.False(
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            $"Expected no UTF-8 BOM in '{path}'."
        );

        // LF line endings + trailing LF.
        var text = File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        Assert.True(text.EndsWith("\n", StringComparison.Ordinal), $"Expected '{path}' to end with LF.");
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gmsd-aos-task-evidence-latest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
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
}

