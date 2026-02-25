using System.Text;
using Gmsd.Aos.Engine;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosDeterministicJsonSnapshotTests
{
    [Fact]
    public void DeterministicJsonWriter_ProducesApprovedSnapshot_Indented_WithTrailingLf_AndNoBom()
    {
        var repoRoot = FindRepositoryRootFrom(AppContext.BaseDirectory);

        var caseRoot = Path.Combine(
            repoRoot,
            "tests",
            "Gmsd.Aos.Tests",
            "Fixtures",
            "EngineSnapshots",
            "v1"
        );

        var inputPath = Path.Combine(caseRoot, "inputs", "deterministic-json-canonical-indented", "input.json");
        var approvedOutputPath = Path.Combine(caseRoot, "approved", "deterministic-json-canonical-indented", "output.json");

        Assert.True(File.Exists(inputPath), $"Input fixture not found at '{inputPath}'.");
        Assert.True(File.Exists(approvedOutputPath), $"Approved fixture not found at '{approvedOutputPath}'.");

        var inputJson = File.ReadAllText(inputPath, Encoding.UTF8);

        var tempRoot = CreateTempDirectory();
        try
        {
            var actualOutputPath = Path.Combine(tempRoot, "output.json");
            DeterministicJsonFileWriter.WriteCanonicalJsonTextOverwrite(actualOutputPath, inputJson, writeIndented: true);

            var actualBytes = File.ReadAllBytes(actualOutputPath);
            var expectedBytes = File.ReadAllBytes(approvedOutputPath);

            AssertNoUtf8Bom(expectedBytes, "approved fixture output.json");
            AssertNoUtf8Bom(actualBytes, "actual output.json");

            AssertEndsWithLf(expectedBytes, "approved fixture output.json");
            AssertEndsWithLf(actualBytes, "actual output.json");

            AssertDoesNotContainCr(actualBytes, "actual output.json");

            // Approved fixtures may be checked out with CRLF on Windows, but the canonical writer
            // MUST emit LF-only. Compare bytes after normalizing expected line endings to LF.
            var normalizedExpectedBytes = RemoveCrBytes(expectedBytes);
            if (!normalizedExpectedBytes.AsSpan().SequenceEqual(actualBytes))
            {
                var diffIndex = FindFirstDiffIndex(normalizedExpectedBytes, actualBytes);
                var expectedText = Encoding.UTF8.GetString(normalizedExpectedBytes);
                var actualText = Encoding.UTF8.GetString(actualBytes);

                var expectedByte = diffIndex >= 0 && diffIndex < normalizedExpectedBytes.Length
                    ? normalizedExpectedBytes[diffIndex]
                    : (byte)0;

                var actualByte = diffIndex >= 0 && diffIndex < actualBytes.Length
                    ? actualBytes[diffIndex]
                    : (byte)0;

                Assert.Fail(
                    "Deterministic JSON snapshot mismatch." + Environment.NewLine +
                    $"Expected length: {normalizedExpectedBytes.Length}, actual length: {actualBytes.Length}, first diff index: {diffIndex}." + Environment.NewLine +
                    $"Expected byte at diff: 0x{expectedByte:X2}, actual byte at diff: 0x{actualByte:X2}." + Environment.NewLine +
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

