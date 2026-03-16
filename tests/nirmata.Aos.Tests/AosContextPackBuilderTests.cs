using System.Text;
using System.Text.Json;
using nirmata.Aos.Context.Packs;
using nirmata.Aos.Public.Context.Packs;
using nirmata.Aos.Engine;
using nirmata.Aos.Engine.Paths;
using nirmata.Aos.Engine.Validation;
using nirmata.Aos.Engine.Workspace;
using nirmata.Aos.Public.Catalogs;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosContextPackBuilderTests
{
    [Fact]
    public void ContextPackBuild_ProducesApprovedSnapshot_ForTaskMode()
    {
        var repoRoot = FindRepositoryRootFrom(AppContext.BaseDirectory);
        var fixturesRoot = Path.Combine(repoRoot, "tests", "nirmata.Aos.Tests", "Fixtures", "EngineSnapshots", "v1");
        var approvedOutputPath = Path.Combine(fixturesRoot, "approved", "context-pack-build-task", "output.json");
        Assert.True(File.Exists(approvedOutputPath), $"Approved fixture not found at '{approvedOutputPath}'.");

        var tempRoot = CreateTempDirectory();
        try
        {
            AosWorkspaceBootstrapper.EnsureInitialized(tempRoot);
            var aosRootPath = Path.Combine(tempRoot, ".aos");

            SeedTaskArtifacts(aosRootPath, "TSK-000001");

            var pack = AosContextPackBuilder.Build(
                aosRootPath,
                packId: "PCK-0001",
                mode: AosContextPackBuilder.ModeTask,
                drivingId: "TSK-000001",
                budget: new ContextPackBudget(MaxBytes: 1000, MaxItems: 10)
            );

            var actualBytes = DeterministicJsonFileWriter.SerializeToCanonicalUtf8Bytes(
                pack,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
                writeIndented: true
            );
            var expectedBytes = File.ReadAllBytes(approvedOutputPath);

            AssertNoUtf8Bom(expectedBytes, "approved fixture output.json");
            AssertNoUtf8Bom(actualBytes, "actual output");

            AssertEndsWithLf(expectedBytes, "approved fixture output.json");
            AssertEndsWithLf(actualBytes, "actual output");

            AssertDoesNotContainCr(actualBytes, "actual output");

            // Approved fixtures may be checked out with CRLF on Windows, but canonical output
            // MUST emit LF-only. Compare bytes after normalizing expected line endings to LF.
            var normalizedExpectedBytes = RemoveCrBytes(expectedBytes);
            if (!normalizedExpectedBytes.AsSpan().SequenceEqual(actualBytes))
            {
                var expectedText = Encoding.UTF8.GetString(normalizedExpectedBytes);
                var actualText = Encoding.UTF8.GetString(actualBytes);

                Assert.Fail(
                    "Context pack snapshot mismatch." + Environment.NewLine +
                    "--- EXPECTED (normalized to LF) ---" + Environment.NewLine +
                    expectedText + Environment.NewLine +
                    "--- ACTUAL ---" + Environment.NewLine +
                    actualText);
            }
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void ContextPackBuild_IsDeterministic_ForSameInputs()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            AosWorkspaceBootstrapper.EnsureInitialized(tempRoot);
            var aosRootPath = Path.Combine(tempRoot, ".aos");

            SeedTaskArtifacts(aosRootPath, "TSK-000001");

            var budget = new ContextPackBudget(MaxBytes: 1000, MaxItems: 10);

            var a = AosContextPackBuilder.Build(aosRootPath, "PCK-0001", AosContextPackBuilder.ModeTask, "TSK-000001", budget);
            var b = AosContextPackBuilder.Build(aosRootPath, "PCK-0001", AosContextPackBuilder.ModeTask, "TSK-000001", budget);

            var bytesA = DeterministicJsonFileWriter.SerializeToCanonicalUtf8Bytes(
                a,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
                writeIndented: true
            );
            var bytesB = DeterministicJsonFileWriter.SerializeToCanonicalUtf8Bytes(
                b,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
                writeIndented: true
            );

            Assert.True(bytesA.AsSpan().SequenceEqual(bytesB));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void ContextPackBuild_EnforcesByteBudget_WithStableStopBehavior()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            AosWorkspaceBootstrapper.EnsureInitialized(tempRoot);
            var aosRootPath = Path.Combine(tempRoot, ".aos");

            SeedTaskArtifacts(aosRootPath, "TSK-000001");

            // plan.json canonical bytes are 31; links.json would exceed if included.
            var pack = AosContextPackBuilder.Build(
                aosRootPath,
                packId: "PCK-0001",
                mode: AosContextPackBuilder.ModeTask,
                drivingId: "TSK-000001",
                budget: new ContextPackBudget(MaxBytes: 31, MaxItems: 10)
            );

            Assert.Single(pack.Entries);
            Assert.Equal(".aos/spec/tasks/TSK-000001/plan.json", pack.Entries[0].ContractPath);
            Assert.Equal(31, pack.Summary.TotalBytes);
            Assert.Equal(1, pack.Summary.TotalItems);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void ValidateWorkspace_ReportsSchemaIssues_ForInvalidContextPack()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            AosWorkspaceBootstrapper.EnsureInitialized(tempRoot);

            var aosRootPath = Path.Combine(tempRoot, ".aos");
            var packsRoot = Path.Combine(aosRootPath, "context", "packs");
            Directory.CreateDirectory(packsRoot);

            var badPackPath = Path.Combine(packsRoot, "PCK-0001.json");
            File.WriteAllText(badPackPath, "{\n  \"schemaVersion\": 1\n}\n", Encoding.UTF8);

            var report = AosWorkspaceValidator.Validate(tempRoot, new[] { AosWorkspaceLayer.Context });
            Assert.False(report.IsValid);

            var issuesForPack = report.Issues
                .Where(i => i.ContractPath == ".aos/context/packs/PCK-0001.json")
                .ToArray();
            Assert.NotEmpty(issuesForPack);

            Assert.All(issuesForPack, i =>
            {
                Assert.Equal(SchemaIds.ContextPackV1, i.SchemaId);
                Assert.False(string.IsNullOrWhiteSpace(i.InstanceLocation));
                Assert.False(string.IsNullOrWhiteSpace(i.Message));
            });
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void SeedTaskArtifacts(string aosRootPath, string taskId)
    {
        var taskRoot = Path.Combine(aosRootPath, "spec", "tasks", taskId);
        Directory.CreateDirectory(taskRoot);

        var taskJsonPath = AosPathRouter.ToAosRootPath(aosRootPath, $".aos/spec/tasks/{taskId}/task.json");
        var planJsonPath = AosPathRouter.ToAosRootPath(aosRootPath, $".aos/spec/tasks/{taskId}/plan.json");
        var linksJsonPath = AosPathRouter.ToAosRootPath(aosRootPath, $".aos/spec/tasks/{taskId}/links.json");

        DeterministicJsonFileWriter.WriteCanonicalJsonTextOverwrite(taskJsonPath, $"{{\"schemaVersion\":1,\"id\":\"{taskId}\"}}");
        DeterministicJsonFileWriter.WriteCanonicalJsonTextOverwrite(planJsonPath, "{\"schemaVersion\":1,\"steps\":[]}");
        DeterministicJsonFileWriter.WriteCanonicalJsonTextOverwrite(linksJsonPath, "{\"schemaVersion\":1,\"links\":[]}");
    }

    private static void AssertNoUtf8Bom(byte[] bytes, string label)
    {
        Assert.False(
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            $"Expected no UTF-8 BOM in {label}."
        );
    }

    private static void AssertEndsWithLf(byte[] bytes, string label)
    {
        Assert.True(bytes.Length > 0 && bytes[^1] == (byte)'\n', $"Expected {label} to end with LF.");
    }

    private static void AssertDoesNotContainCr(byte[] bytes, string label)
    {
        foreach (var b in bytes)
        {
            Assert.False(b == (byte)'\r', $"Expected {label} to not contain CR bytes.");
        }
    }

    private static byte[] RemoveCrBytes(byte[] bytes)
    {
        var hasCr = false;
        foreach (var b in bytes)
        {
            if (b == (byte)'\r')
            {
                hasCr = true;
                break;
            }
        }

        if (!hasCr)
        {
            return bytes;
        }

        var normalized = new byte[bytes.Length];
        var j = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            if (b == (byte)'\r')
            {
                continue;
            }

            normalized[j++] = b;
        }

        Array.Resize(ref normalized, j);
        return normalized;
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "nirmata-aos-context-pack-tests", Guid.NewGuid().ToString("N"));
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

    private static string FindRepositoryRootFrom(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "nirmata.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate repository root from '{startPath}'.");
    }
}

