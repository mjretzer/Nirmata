using System.Text;
using System.Text.Json;
using Gmsd.Aos.Engine.Errors;
using Gmsd.Aos.Engine.Evidence;
using Gmsd.Aos.Engine.Evidence.Agents;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosAgentEvidenceWriterTests
{
    [Fact]
    public void WriteRequest_WritesDeterministicArtifact_AtExpectedPath_WithRequiredFields()
    {
        var repoRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(repoRoot, ".aos");
            var runId = AosRunId.New();
            var agentId = "planner";
            var requestId = "req-0001";

            var inputObj = new Dictionary<string, object?>
            {
                ["z"] = 3,
                ["a"] = new Dictionary<string, object?>
                {
                    ["b"] = 2,
                    ["a"] = 1
                }
            };

            var inputElement = JsonSerializer.SerializeToElement(inputObj, new JsonSerializerOptions());

            AosAgentEvidenceWriter.WriteRequest(aosRoot, runId, agentId, requestId, inputElement);

            var contractPath = AosAgentEvidenceWriter.GetAgentRequestContractPath(runId, agentId, requestId);
            var fullPath = Path.Combine(aosRoot, "evidence", "runs", runId, "agents", agentId, requestId, "request.json");

            Assert.True(File.Exists(fullPath), $"Expected request evidence at '{contractPath}'.");

            var bytes = File.ReadAllBytes(fullPath);
            AssertNoUtf8Bom(bytes, "request.json");
            AssertEndsWithLf(bytes, "request.json");
            AssertDoesNotContainCr(bytes, "request.json");

            var json = Encoding.UTF8.GetString(bytes);
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            Assert.Equal(JsonValueKind.Object, root.ValueKind);

            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(runId, root.GetProperty("runId").GetString());
            Assert.Equal(agentId, root.GetProperty("agentId").GetString());
            Assert.Equal(requestId, root.GetProperty("requestId").GetString());

            Assert.Equal(JsonValueKind.Object, root.GetProperty("input").ValueKind);

            // Canonical ordering should be present throughout (deterministic writer sorts keys).
            AssertCanonicalObjectOrdering(root);
        }
        finally
        {
            TryDeleteDirectory(repoRoot);
        }
    }

    [Fact]
    public void WriteResult_WritesSuccessAndFailureShapes_Deterministically()
    {
        var repoRoot = CreateTempDirectory();
        try
        {
            var aosRoot = Path.Combine(repoRoot, ".aos");
            var runId = AosRunId.New();
            var agentId = "validator";
            var requestId = "req-0002";

            // success
            var outputObj = new Dictionary<string, object?>
            {
                ["b"] = 2,
                ["a"] = 1
            };
            var outputElement = JsonSerializer.SerializeToElement(outputObj, new JsonSerializerOptions());

            AosAgentEvidenceWriter.WriteResultSuccess(aosRoot, runId, agentId, requestId, outputElement);

            var successPath = Path.Combine(aosRoot, "evidence", "runs", runId, "agents", agentId, requestId, "result.json");
            Assert.True(File.Exists(successPath), "Expected success result evidence to exist.");

            var successJson = File.ReadAllText(successPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            using (var doc = JsonDocument.Parse(successJson))
            {
                var root = doc.RootElement;
                Assert.Equal("success", root.GetProperty("outcome").GetString());
                Assert.False(root.TryGetProperty("error", out _));
                Assert.Equal(JsonValueKind.Object, root.GetProperty("output").ValueKind);
                AssertCanonicalObjectOrdering(root);
            }

            // failure (overwrite same request id deterministically)
            AosAgentEvidenceWriter.WriteResultFailure(
                aosRoot,
                runId,
                agentId,
                requestId,
                new AosErrorEnvelope(Code: "E_TEST", Message: "boom", Details: new { x = 1, y = 2 })
            );

            var failureBytes = File.ReadAllBytes(successPath);
            AssertEndsWithLf(failureBytes, "result.json");
            AssertDoesNotContainCr(failureBytes, "result.json");

            var failureJson = Encoding.UTF8.GetString(failureBytes);
            using (var doc = JsonDocument.Parse(failureJson))
            {
                var root = doc.RootElement;
                Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
                Assert.Equal(runId, root.GetProperty("runId").GetString());
                Assert.Equal(agentId, root.GetProperty("agentId").GetString());
                Assert.Equal(requestId, root.GetProperty("requestId").GetString());
                Assert.Equal("failure", root.GetProperty("outcome").GetString());

                var error = root.GetProperty("error");
                Assert.Equal("E_TEST", error.GetProperty("code").GetString());
                Assert.Equal("boom", error.GetProperty("message").GetString());
                Assert.True(error.TryGetProperty("details", out _));

                Assert.Equal(JsonValueKind.Object, root.GetProperty("output").ValueKind);
                AssertCanonicalObjectOrdering(root);
            }
        }
        finally
        {
            TryDeleteDirectory(repoRoot);
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

    private static void AssertCanonicalObjectOrdering(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var namesInOrder = new List<string>();
                foreach (var p in element.EnumerateObject())
                {
                    namesInOrder.Add(p.Name);
                }

                var sorted = namesInOrder.OrderBy(static n => n, StringComparer.Ordinal).ToArray();
                Assert.Equal(sorted, namesInOrder);

                foreach (var p in element.EnumerateObject())
                {
                    AssertCanonicalObjectOrdering(p.Value);
                }

                break;
            }

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    AssertCanonicalObjectOrdering(item);
                }

                break;

            default:
                break;
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gmsd-aos-agent-evidence", Guid.NewGuid().ToString("N"));
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

