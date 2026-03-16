using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using nirmata.Aos.Engine;
using nirmata.Aos.Engine.Evidence;
using nirmata.Aos.Engine.Evidence.Calls;
using nirmata.Aos.Engine.Paths;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosCallEnvelopeSnapshotTests
{
    private const string StableRunId = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void CallEnvelope_Succeeded_ProducesApprovedSnapshot()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var logger = new AosCallEnvelopeFileLogger(aosRoot, StableRunId);

            _ = AosCallEnvelopeRuntime.InvokeRecordOnly(
                runId: StableRunId,
                provider: "aos",
                tool: "execute-plan.write-outputs",
                callId: "execute-plan.write-outputs",
                request: new { outputCount = 2 },
                invoke: () => 42,
                logger: logger
            );

            var artifactPath = AosPathRouter.GetRunCallEnvelopeLogPath(aosRoot, StableRunId, "execute-plan.write-outputs");
            Assert.True(File.Exists(artifactPath), $"Expected call envelope log at '{artifactPath}'.");

            AssertArtifactSnapshot(caseName: "call-envelope-succeeded", artifactJsonPath: artifactPath);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void CallEnvelope_Failed_ProducesApprovedSnapshot()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(tempRoot, ".aos");
            Directory.CreateDirectory(aosRoot);

            var logger = new AosCallEnvelopeFileLogger(aosRoot, StableRunId);

            Assert.Throws<InvalidOperationException>(() =>
                AosCallEnvelopeRuntime.InvokeRecordOnly<int>(
                    runId: StableRunId,
                    provider: "aos",
                    tool: "execute-plan.write-outputs",
                    callId: "execute-plan.write-outputs",
                    request: new { outputCount = 1 },
                    invoke: () => throw new InvalidOperationException("boom"),
                    logger: logger
                )
            );

            var artifactPath = AosPathRouter.GetRunCallEnvelopeLogPath(aosRoot, StableRunId, "execute-plan.write-outputs");
            Assert.True(File.Exists(artifactPath), $"Expected call envelope log at '{artifactPath}'.");

            AssertArtifactSnapshot(caseName: "call-envelope-failed", artifactJsonPath: artifactPath);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void AssertArtifactSnapshot(string caseName, string artifactJsonPath)
    {
        var repoRoot = FindRepositoryRootFrom(AppContext.BaseDirectory);
        var fixturesRoot = Path.Combine(repoRoot, "tests", "nirmata.Aos.Tests", "Fixtures", "EngineSnapshots", "v1");

        var approvedOutputPath = Path.Combine(fixturesRoot, "approved", caseName, "output.json");
        Assert.True(File.Exists(approvedOutputPath), $"Approved fixture not found at '{approvedOutputPath}'.");

        var actualJsonText = File.ReadAllText(artifactJsonPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var normalized = NormalizeRunId(actualJsonText, out var runIdActual);

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

            // Approved fixtures may be checked out with CRLF on Windows; canonical output MUST emit LF-only.
            var normalizedExpectedBytes = RemoveCrBytes(expectedBytes);
            if (!normalizedExpectedBytes.AsSpan().SequenceEqual(actualBytes))
            {
                var expectedText = Encoding.UTF8.GetString(normalizedExpectedBytes);
                var actualText = Encoding.UTF8.GetString(actualBytes);
                Assert.Fail(
                    $"Call envelope snapshot mismatch for case '{caseName}' (runId was '{runIdActual}')." + Environment.NewLine +
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

    private static string NormalizeRunId(string jsonText, out string runIdActual)
    {
        var node = JsonNode.Parse(jsonText) as JsonObject;
        Assert.NotNull(node);

        runIdActual = (node!["runId"]?.GetValue<string>()) ?? string.Empty;
        Assert.True(AosRunId.IsValid(runIdActual), $"Expected artifact runId to be valid, got '{runIdActual}'.");

        node["runId"] = "RUN_ID";

        return node.ToJsonString(
                   new JsonSerializerOptions
                   {
                       WriteIndented = true,
                       Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                   }
               ) + "\n";
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "nirmata-aos-call-envelope-snapshots", Guid.NewGuid().ToString("N"));
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

