using Gmsd.Aos.Engine.Paths;
using System.Text;
using System.Text.Json;
using Gmsd.Aos.Engine;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosPathRouterTests
{
    [Fact]
    public void PathRouting_ProducesApprovedSnapshot_ForRepresentativeArtifactIds()
    {
        var repoRoot = FindRepositoryRootFrom(AppContext.BaseDirectory);

        var fixturesRoot = Path.Combine(
            repoRoot,
            "tests",
            "Gmsd.Aos.Tests",
            "Fixtures",
            "EngineSnapshots",
            "v1"
        );

        var approvedOutputPath = Path.Combine(
            fixturesRoot,
            "approved",
            "path-router-contract-paths",
            "output.json"
        );

        Assert.True(File.Exists(approvedOutputPath), $"Approved fixture not found at '{approvedOutputPath}'.");

        var routes = new[]
        {
            Create("MS-0001"),
            Create("PH-0001"),
            Create("TSK-000001"),
            Create("ISS-0001"),
            Create("UAT-0001"),
            Create("PCK-0001"),
            Create("0123456789abcdef0123456789abcdef")
        };

        var tempRoot = CreateTempDirectory();
        try
        {
            var actualOutputPath = Path.Combine(tempRoot, "output.json");
            DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
                actualOutputPath,
                routes,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
                writeIndented: true
            );

            var actualBytes = File.ReadAllBytes(actualOutputPath);
            var expectedBytes = File.ReadAllBytes(approvedOutputPath);

            AssertNoUtf8Bom(expectedBytes, "approved fixture output.json");
            AssertNoUtf8Bom(actualBytes, "actual output.json");

            AssertEndsWithLf(expectedBytes, "approved fixture output.json");
            AssertEndsWithLf(actualBytes, "actual output.json");

            AssertDoesNotContainCr(actualBytes, "actual output.json");

            // Approved fixtures may be checked out with CRLF on Windows, but canonical output
            // MUST emit LF-only. Compare bytes after normalizing expected line endings to LF.
            var normalizedExpectedBytes = RemoveCrBytes(expectedBytes);
            if (!normalizedExpectedBytes.AsSpan().SequenceEqual(actualBytes))
            {
                var expectedText = Encoding.UTF8.GetString(normalizedExpectedBytes);
                var actualText = Encoding.UTF8.GetString(actualBytes);

                Assert.Fail(
                    "Path routing snapshot mismatch." + Environment.NewLine +
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

    [Theory]
    [InlineData("MS-0001", "Milestone", ".aos/spec/milestones/MS-0001/milestone.json")]
    [InlineData("PH-0001", "Phase", ".aos/spec/phases/PH-0001/phase.json")]
    [InlineData("TSK-000001", "Task", ".aos/spec/tasks/TSK-000001/task.json")]
    [InlineData("ISS-0001", "Issue", ".aos/spec/issues/ISS-0001.json")]
    [InlineData("UAT-0001", "Uat", ".aos/spec/uat/UAT-0001.json")]
    [InlineData("PCK-0001", "ContextPack", ".aos/context/packs/PCK-0001.json")]
    [InlineData("0123456789abcdef0123456789abcdef", "Run", ".aos/evidence/runs/0123456789abcdef0123456789abcdef/")]
    public void GetContractPathForArtifactId_ProducesDeterministicCanonicalContractPath(
        string id,
        string expectedKindName,
        string expectedContractPath)
    {
        var expectedKind = Enum.Parse<AosArtifactKind>(expectedKindName, ignoreCase: false);
        Assert.True(AosPathRouter.TryParseArtifactId(id, out var kind, out var normalized, out var error), error);
        Assert.Equal(expectedKind, kind);
        Assert.Equal(id, normalized);

        var contractPath = AosPathRouter.GetContractPathForArtifactId(id);
        Assert.Equal(expectedContractPath, contractPath);

        // Cross-platform determinism: contract paths must not include OS separators.
        Assert.DoesNotContain('\\', contractPath);
        Assert.StartsWith(".aos/", contractPath, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("MS-1", "MS-####")]
    [InlineData("PH-00001", "PH-####")]
    [InlineData("TSK-001", "TSK-######")]
    [InlineData("UAT-00001", "UAT-####")]
    [InlineData("ISS-01", "ISS-####")]
    [InlineData("PCK-01", "PCK-####")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF", "32 lower-case hex")]
    public void TryParseArtifactId_RejectsInvalidIds_WithActionableDiagnostics(string id, string expectedMessageFragment)
    {
        Assert.False(AosPathRouter.TryParseArtifactId(id, out _, out _, out var error));
        Assert.Contains(expectedMessageFragment, error, StringComparison.Ordinal);
    }

    [Fact]
    public void ToAosRootPath_ConvertsContractPathsToFilesystemPaths_Deterministically()
    {
        var aosRootPath = Path.Combine(Path.GetTempPath(), "gmsd-aos-router-tests", ".aos");
        var contractPath = ".aos/spec/tasks/TSK-000001/task.json";
        var physical = AosPathRouter.ToAosRootPath(aosRootPath, contractPath);

        var expected = Path.Combine(aosRootPath, "spec", "tasks", "TSK-000001", "task.json");
        Assert.Equal(expected, physical);
    }

    [Fact]
    public void WorkspaceLockContractPath_IsCanonical_AndResolvesDeterministically()
    {
        Assert.Equal(".aos/locks/workspace.lock.json", AosPathRouter.WorkspaceLockContractPath);
        Assert.DoesNotContain('\\', AosPathRouter.WorkspaceLockContractPath);
        Assert.StartsWith(".aos/", AosPathRouter.WorkspaceLockContractPath, StringComparison.Ordinal);

        var aosRootPath = Path.Combine(Path.GetTempPath(), "gmsd-aos-router-tests", Guid.NewGuid().ToString("N"), ".aos");
        var lockPath = AosPathRouter.GetWorkspaceLockPath(aosRootPath);
        Assert.Equal(Path.Combine(aosRootPath, "locks", "workspace.lock.json"), lockPath);
    }

    private static PathRouteSnapshot Create(string id)
    {
        Assert.True(AosPathRouter.TryParseArtifactId(id, out var kind, out var normalized, out var error), error);
        Assert.Equal(id, normalized);

        var contractPath = AosPathRouter.GetContractPath(kind, normalized);

        // Cross-platform determinism: contract paths must not include OS separators.
        Assert.DoesNotContain('\\', contractPath);
        Assert.StartsWith(".aos/", contractPath, StringComparison.Ordinal);

        return new PathRouteSnapshot(normalized, kind.ToString(), contractPath);
    }

    private sealed record PathRouteSnapshot(string Id, string Kind, string ContractPath);

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
        var root = Path.Combine(Path.GetTempPath(), "gmsd-aos-engine-snapshots", Guid.NewGuid().ToString("N"));
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
            if (File.Exists(Path.Combine(dir.FullName, "Gmsd.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate repository root from '{startPath}'.");
    }
}

