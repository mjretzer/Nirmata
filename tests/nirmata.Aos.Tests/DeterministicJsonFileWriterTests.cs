using System.Text;
using System.Text.Json;
using nirmata.Aos.Engine;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class DeterministicJsonFileWriterTests
{
    [Fact]
    public void SerializeToCanonicalUtf8Bytes_SortsKeysRecursively_ForNestedDictionaryInputs_AndIsStable()
    {
        // Same semantic data, but intentionally built with different insertion orders at multiple levels.
        var valueA = new Dictionary<string, object?>
        {
            ["z"] = 3,
            ["a"] = new Dictionary<string, object?>
            {
                ["b"] = 2,
                ["a"] = 1,
                ["nested"] = new Dictionary<string, object?>
                {
                    ["y"] = "Y",
                    ["x"] = "X"
                }
            },
            ["m"] = new object?[]
            {
                new Dictionary<string, object?>
                {
                    ["d"] = true,
                    ["c"] = false
                }
            }
        };

        var valueB = new Dictionary<string, object?>
        {
            ["m"] = new object?[]
            {
                new Dictionary<string, object?>
                {
                    ["c"] = false,
                    ["d"] = true
                }
            },
            ["a"] = new Dictionary<string, object?>
            {
                ["nested"] = new Dictionary<string, object?>
                {
                    ["x"] = "X",
                    ["y"] = "Y"
                },
                ["a"] = 1,
                ["b"] = 2
            },
            ["z"] = 3
        };

        var options = new JsonSerializerOptions();

        var bytesA = DeterministicJsonFileWriter.SerializeToCanonicalUtf8Bytes(valueA, options, writeIndented: true);
        var bytesB = DeterministicJsonFileWriter.SerializeToCanonicalUtf8Bytes(valueB, options, writeIndented: true);

        Assert.Equal(bytesA, bytesB);

        // Also validate recursive canonical ordering in the produced JSON (not just equality between two inputs).
        var json = Encoding.UTF8.GetString(bytesA);
        Assert.EndsWith("\n", json);

        using var doc = JsonDocument.Parse(json);
        AssertCanonicalObjectOrdering(doc.RootElement);
    }

    [Fact]
    public async Task WriteCanonicalJsonOverwrite_DoesNotRewrite_WhenCanonicalBytesAreIdentical()
    {
        var root = Path.Combine(Path.GetTempPath(), "nirmata-deterministic-json-no-churn", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "artifact.json");
            var value = new Dictionary<string, object?>
            {
                ["b"] = 2,
                ["a"] = 1
            };

            var options = new JsonSerializerOptions();

            DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(path, value, options, writeIndented: true);
            var t1 = File.GetLastWriteTimeUtc(path);
            var bytes1 = File.ReadAllBytes(path);

            // Ensure the filesystem has enough time resolution that an actual rewrite would be detectable.
            await Task.Delay(1100);

            DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(path, value, options, writeIndented: true);
            var t2 = File.GetLastWriteTimeUtc(path);
            var bytes2 = File.ReadAllBytes(path);

            Assert.Equal(bytes1, bytes2);
            Assert.Equal(t1, t2);
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public void WriteAtomicOverwriteForTest_WhenFailureAfterTempWrite_DoesNotModifyTarget()
    {
        var root = Path.Combine(Path.GetTempPath(), "nirmata-deterministic-json-atomic-failure", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "artifact.json");

            var oldValue = new Dictionary<string, object?>
            {
                ["a"] = 1
            };

            var newValue = new Dictionary<string, object?>
            {
                ["a"] = 1,
                ["b"] = 2
            };

            var options = new JsonSerializerOptions();
            var oldBytes = DeterministicJsonFileWriter.SerializeToCanonicalUtf8Bytes(oldValue, options, writeIndented: true);
            var newBytes = DeterministicJsonFileWriter.SerializeToCanonicalUtf8Bytes(newValue, options, writeIndented: true);

            File.WriteAllBytes(path, oldBytes);
            Assert.Equal(oldBytes, File.ReadAllBytes(path));

            Assert.Throws<InvalidOperationException>(() =>
                DeterministicJsonFileWriter.WriteAtomicOverwriteForTest(
                    path,
                    newBytes,
                    avoidChurn: false,
                    afterTempWriteBeforeCommit: static _ => throw new InvalidOperationException("boom")));

            var afterBytes = File.ReadAllBytes(path);
            Assert.Equal(oldBytes, afterBytes);

            // Sanity: file should remain parseable JSON.
            using var _ = JsonDocument.Parse(Encoding.UTF8.GetString(afterBytes));

            // Temp files should be cleaned up (best-effort).
            var tempPattern = $"{Path.GetFileName(path)}.tmp-*";
            Assert.Empty(Directory.EnumerateFiles(root, tempPattern));
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
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
                // primitives: nothing to validate
                break;
        }
    }
}

