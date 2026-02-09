using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gmsd.Aos.Engine;
using Gmsd.Aos.Engine.Evidence;
using Gmsd.Aos.Engine.Errors;
using Gmsd.Aos.Engine.ExecutePlan;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosRunPacketResultSnapshotTests
{
    private const string StableRunId = "0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset StableStartedAtUtc = new(2026, 01, 01, 00, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset StableFinishedAtUtc = new(2026, 01, 01, 00, 00, 30, TimeSpan.Zero);

    [Fact]
    public void RunPacket_RunStart_ProducesApprovedSnapshot_NormalizingRunId()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
                aosRootPath: aosRoot,
                runId: StableRunId,
                startedAtUtc: StableStartedAtUtc,
                command: "run start",
                args: Array.Empty<string>()
            );

            var packetPath = Path.Combine(aosRoot, "evidence", "runs", StableRunId, "artifacts", "packet.json");
            Assert.True(File.Exists(packetPath), $"Expected packet.json at '{packetPath}'.");

            AssertArtifactSnapshot(
                caseName: "run-packet-run-start",
                artifactJsonPath: packetPath
            );
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void RunPacket_ExecutePlan_ProducesApprovedSnapshot_NormalizingRunId()
    {
        var repoRoot = FindRepositoryRootFrom(AppContext.BaseDirectory);
        var fixturesRoot = Path.Combine(repoRoot, "tests", "Gmsd.Aos.Tests", "Fixtures", "EngineSnapshots", "v1");
        var planFixturePath = Path.Combine(fixturesRoot, "inputs", "run-packet-execute-plan", "plan.json");
        var policyFixturePath = Path.Combine(fixturesRoot, "inputs", "run-packet-execute-plan", ".aos", "config", "policy.json");
        var configFixturePath = Path.Combine(fixturesRoot, "inputs", "run-packet-execute-plan", ".aos", "config", "config.json");

        Assert.True(File.Exists(planFixturePath), $"Input plan fixture not found at '{planFixturePath}'.");
        Assert.True(File.Exists(policyFixturePath), $"Input policy fixture not found at '{policyFixturePath}'.");
        Assert.True(File.Exists(configFixturePath), $"Input config fixture not found at '{configFixturePath}'.");

        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(Path.Combine(aosRoot, "config"));

            // Write config/policy inputs under the workspace .aos/config/ so the packet can hash them deterministically.
            WriteUtf8NoBom(Path.Combine(aosRoot, "config", "policy.json"), File.ReadAllText(policyFixturePath, Encoding.UTF8));
            WriteUtf8NoBom(Path.Combine(aosRoot, "config", "config.json"), File.ReadAllText(configFixturePath, Encoding.UTF8));

            // Place plan under repo root (parent of .aos) so the packet can record a deterministic relative path.
            var planPath = Path.Combine(tempRoot, "plans", "plan.json");
            Directory.CreateDirectory(Path.GetDirectoryName(planPath)!);
            WriteUtf8NoBom(planPath, File.ReadAllText(planFixturePath, Encoding.UTF8));

            AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
                aosRootPath: aosRoot,
                runId: StableRunId,
                startedAtUtc: StableStartedAtUtc,
                command: "execute-plan",
                args: Array.Empty<string>()
            );

            var plan = ExecutePlanPlanLoader.LoadFromFile(planPath);

            AosRunEvidenceScaffolder.PopulateExecutePlanPacketFields(
                aosRootPath: aosRoot,
                runId: StableRunId,
                args: new[] { "--plan", "plans/plan.json", "--dry-run" },
                planPath: planPath,
                plan: plan
            );

            var packetPath = Path.Combine(aosRoot, "evidence", "runs", StableRunId, "artifacts", "packet.json");
            Assert.True(File.Exists(packetPath), $"Expected packet.json at '{packetPath}'.");

            AssertArtifactSnapshot(
                caseName: "run-packet-execute-plan",
                artifactJsonPath: packetPath
            );
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void RunResult_Succeeded_ProducesApprovedSnapshot_NormalizingRunId()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
                aosRootPath: aosRoot,
                runId: StableRunId,
                startedAtUtc: StableStartedAtUtc,
                command: "run start",
                args: Array.Empty<string>()
            );

            // Create one deterministic output so the manifest and producedArtifacts are stable.
            var outputsRoot = Path.Combine(aosRoot, "evidence", "runs", StableRunId, "outputs");
            Directory.CreateDirectory(outputsRoot);
            WriteUtf8NoBom(Path.Combine(outputsRoot, "hello.txt"), "hello\n");

            AosRunEvidenceScaffolder.FinishRun(
                aosRootPath: aosRoot,
                runId: StableRunId,
                finishedAtUtc: StableFinishedAtUtc
            );

            var resultPath = Path.Combine(aosRoot, "evidence", "runs", StableRunId, "artifacts", "result.json");
            Assert.True(File.Exists(resultPath), $"Expected result.json at '{resultPath}'.");

            AssertArtifactSnapshot(
                caseName: "run-result-succeeded",
                artifactJsonPath: resultPath
            );
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void RunResult_Failed_ProducesApprovedSnapshot_NormalizingRunId()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
                aosRootPath: aosRoot,
                runId: StableRunId,
                startedAtUtc: StableStartedAtUtc,
                command: "run start",
                args: Array.Empty<string>()
            );

            var outputsRoot = Path.Combine(aosRoot, "evidence", "runs", StableRunId, "outputs");
            Directory.CreateDirectory(outputsRoot);
            WriteUtf8NoBom(Path.Combine(outputsRoot, "hello.txt"), "hello\n");

            AosRunEvidenceScaffolder.FailRun(
                aosRootPath: aosRoot,
                runId: StableRunId,
                finishedAtUtc: StableFinishedAtUtc,
                exitCode: 42,
                error: new AosErrorEnvelope(
                    Code: "policy-violation",
                    Message: "Plan attempted to write outside allowed output scope.",
                    Details: new { attemptedPath = "../secrets.txt", rule = "outputsPathScope" }
                )
            );

            var resultPath = Path.Combine(aosRoot, "evidence", "runs", StableRunId, "artifacts", "result.json");
            Assert.True(File.Exists(resultPath), $"Expected result.json at '{resultPath}'.");

            AssertArtifactSnapshot(
                caseName: "run-result-failed",
                artifactJsonPath: resultPath
            );
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void AssertArtifactSnapshot(string caseName, string artifactJsonPath)
    {
        var repoRoot = FindRepositoryRootFrom(AppContext.BaseDirectory);
        var fixturesRoot = Path.Combine(repoRoot, "tests", "Gmsd.Aos.Tests", "Fixtures", "EngineSnapshots", "v1");

        var approvedOutputPath = Path.Combine(fixturesRoot, "approved", caseName, "output.json");
        Assert.True(File.Exists(approvedOutputPath), $"Approved fixture not found at '{approvedOutputPath}'.");

        var actualJsonText = File.ReadAllText(artifactJsonPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var normalized = NormalizeRunIdAndPaths(actualJsonText, out var runIdActual);

        // Write the normalized JSON through the deterministic writer for stable bytes.
        var tempRoot = CreateTempDirectory();
        try
        {
            var actualOutputPath = Path.Combine(tempRoot, "output.json");
            DeterministicJsonFileWriter.WriteCanonicalJsonTextOverwrite(actualOutputPath, normalized, writeIndented: true);

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
                var diffIndex = FindFirstDiffIndex(normalizedExpectedBytes, actualBytes);
                var expectedText = Encoding.UTF8.GetString(normalizedExpectedBytes);
                var actualText = Encoding.UTF8.GetString(actualBytes);

                Assert.Fail(
                    $"Run packet/result snapshot mismatch for case '{caseName}' (runId was '{runIdActual}')." + Environment.NewLine +
                    $"Expected length: {normalizedExpectedBytes.Length}, actual length: {actualBytes.Length}, first diff index: {diffIndex}." +
                    Environment.NewLine +
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

    private static string NormalizeRunIdAndPaths(string jsonText, out string runIdActual)
    {
        var node = JsonNode.Parse(jsonText) as JsonObject;
        Assert.NotNull(node);

        runIdActual = (node!["runId"]?.GetValue<string>()) ?? string.Empty;
        Assert.True(AosRunId.IsValid(runIdActual), $"Expected artifact runId to be valid, got '{runIdActual}'.");

        node["runId"] = "RUN_ID";

        // Normalize producedArtifacts[].contractPath where it embeds the run id.
        if (node.TryGetPropertyValue("producedArtifacts", out var producedNode) && producedNode is JsonArray produced)
        {
            foreach (var item in produced)
            {
                if (item is not JsonObject obj)
                {
                    continue;
                }

                var contractPath = obj["contractPath"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(contractPath) && contractPath.Contains(runIdActual, StringComparison.Ordinal))
                {
                    obj["contractPath"] = contractPath.Replace(runIdActual, "RUN_ID", StringComparison.Ordinal);
                }
            }
        }

        return node.ToJsonString(
                   new JsonSerializerOptions
                   {
                       WriteIndented = true,
                       Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                   }
               ) + "\n";
    }

    private static void WriteUtf8NoBom(string path, string content)
        => File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gmsd-aos-run-packet-result-snapshots", Guid.NewGuid().ToString("N"));
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

    private static int FindFirstDiffIndex(byte[] a, byte[] b)
    {
        var min = Math.Min(a.Length, b.Length);
        for (var i = 0; i < min; i++)
        {
            if (a[i] != b[i])
            {
                return i;
            }
        }

        return a.Length == b.Length ? -1 : min;
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

