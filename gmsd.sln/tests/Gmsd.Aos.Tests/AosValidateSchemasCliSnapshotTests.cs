using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Gmsd.Aos.Engine;
using Gmsd.Aos.Engine.Workspace;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosValidateSchemasCliSnapshotTests
{
    [Fact]
    public async Task AosValidateSchemasCli_Success_ProducesApprovedSnapshot()
    {
        await AssertSnapshotCaseAsync(
            caseName: "validate-schemas-cli-success",
            setupWorkspace: static root =>
            {
                WriteLocalSchemaPack(
                    root,
                    schemaFileName: "test.schema.json",
                    schemaJson:
                        "{\n" +
                        "  \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n" +
                        "  \"$id\": \"test\",\n" +
                        "  \"type\": \"object\"\n" +
                        "}\n"
                );
            }
        );
    }

    [Fact]
    public async Task AosValidateSchemasCli_Failure_ProducesApprovedSnapshot()
    {
        await AssertSnapshotCaseAsync(
            caseName: "validate-schemas-cli-failure",
            setupWorkspace: static root =>
            {
                // Missing required fields ($schema, $id, type) -> deterministic error output.
                WriteLocalSchemaPack(
                    root,
                    schemaFileName: "test.schema.json",
                    schemaJson:
                        "{\n" +
                        "  \"title\": \"test\"\n" +
                        "}\n"
                );
            }
        );
    }

    private static async Task AssertSnapshotCaseAsync(string caseName, Action<string> setupWorkspace)
    {
        var repoRoot = FindRepositoryRootFrom(AppContext.BaseDirectory);
        var fixturesRoot = Path.Combine(repoRoot, "tests", "Gmsd.Aos.Tests", "Fixtures", "EngineSnapshots", "v1");

        var approvedOutputPath = Path.Combine(fixturesRoot, "approved", caseName, "output.json");
        Assert.True(File.Exists(approvedOutputPath), $"Approved fixture not found at '{approvedOutputPath}'.");

        var tempRoot = CreateTempDirectory();
        try
        {
            setupWorkspace(tempRoot);

            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                aosDllPath,
                "validate",
                "schemas",
                "--root",
                tempRoot
            );

            var snapshot = new CliSnapshot(
                ExitCode: exitCode,
                Stdout: NormalizeCliText(stdout, tempRoot),
                Stderr: NormalizeCliText(stderr, tempRoot)
            );

            var actualOutputPath = Path.Combine(tempRoot, "output.json");
            DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
                actualOutputPath,
                snapshot,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                },
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
                    "Validate-schemas CLI snapshot mismatch." + Environment.NewLine +
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

    private static void WriteLocalSchemaPack(string repositoryRoot, string schemaFileName, string schemaJson)
    {
        var schemasRoot = Path.Combine(repositoryRoot, ".aos", "schemas");
        Directory.CreateDirectory(schemasRoot);

        File.WriteAllText(
            Path.Combine(schemasRoot, "registry.json"),
            "{\n  \"schemaVersion\": 1,\n  \"schemas\": [\n    \"" + schemaFileName + "\"\n  ]\n}\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        );

        File.WriteAllText(
            Path.Combine(schemasRoot, schemaFileName),
            schemaJson,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        );
    }

    private static string NormalizeCliText(string text, string repositoryRootPath)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        // Normalize newlines first.
        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // Replace the nondeterministic temp root path with a stable placeholder.
        // (Program prints paths using OS separators; normalize separators after placeholder replacement.)
        normalized = normalized.Replace(repositoryRootPath, "<ROOT>", StringComparison.Ordinal);

        // Cross-platform determinism: normalize path separators in displayed paths.
        normalized = normalized.Replace('\\', '/');

        return normalized;
    }

    private sealed record CliSnapshot(int ExitCode, string Stdout, string Stderr);

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

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunDotNetAsync(string dllPath, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        psi.Environment["DOTNET_NOLOGO"] = "1";
        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        psi.ArgumentList.Add(dllPath);
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        var stdoutTask = process!.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
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
            if (File.Exists(Path.Combine(dir.FullName, "Gmsd.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate repository root from '{startPath}'.");
    }
}

