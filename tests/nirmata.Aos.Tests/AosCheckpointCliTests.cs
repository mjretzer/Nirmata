using System.Diagnostics;
using System.Text;
using System.Text.Json;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosCheckpointCliTests
{
    [Fact]
    public async Task CheckpointCreate_CreatesCheckpointArtifacts_AndAppendsEvent()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (initExit, _, initErr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );
            Assert.True(initExit == 0, $"Expected init exit code 0, got {initExit}. STDERR:{Environment.NewLine}{initErr}");
            WriteRepoMarker(tempWorkspaceRoot);

            // Seed a non-default state so the snapshot is meaningful.
            var statePath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "state.json");
            File.WriteAllText(
                statePath,
                "{\n  \"schemaVersion\": 1,\n  \"cursor\": {\"hello\": \"world\"}\n}\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            var (createExit, createOut, createErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "checkpoint",
                "create"
            );

            Assert.True(createExit == 0, $"Expected checkpoint create exit code 0, got {createExit}. STDERR:{Environment.NewLine}{createErr}");

            var checkpointId = createOut.Trim();
            Assert.Matches("^CHK-[A-Za-z0-9][A-Za-z0-9-]*$", checkpointId);

            var checkpointDir = Path.Combine(tempWorkspaceRoot, ".aos", "state", "checkpoints", checkpointId);
            Assert.True(Directory.Exists(checkpointDir), $"Expected checkpoint directory at '{checkpointDir}'.");

            var metadataPath = Path.Combine(checkpointDir, "checkpoint.json");
            var snapshotPath = Path.Combine(checkpointDir, "state.json");
            Assert.True(File.Exists(metadataPath), $"Expected checkpoint metadata file at '{metadataPath}'.");
            Assert.True(File.Exists(snapshotPath), $"Expected checkpoint snapshot file at '{snapshotPath}'.");

            // Validate checkpoint.json shape.
            using (var meta = JsonDocument.Parse(File.ReadAllText(metadataPath)))
            {
                Assert.True(meta.RootElement.TryGetProperty("schemaVersion", out var sv) && sv.GetInt32() == 1);
                Assert.True(meta.RootElement.TryGetProperty("checkpointId", out var id) && id.GetString() == checkpointId);
                Assert.True(meta.RootElement.TryGetProperty("sourceStateContractPath", out var src) && src.GetString() == ".aos/state/state.json");
                Assert.True(meta.RootElement.TryGetProperty("snapshotFile", out var sf) && sf.GetString() == "state.json");
            }

            // Ensure snapshot matches the state at time of create.
            using (var expectedDoc = JsonDocument.Parse(File.ReadAllText(statePath, Encoding.UTF8)))
            using (var snapshotDoc = JsonDocument.Parse(File.ReadAllText(snapshotPath, Encoding.UTF8)))
            {
                Assert.True(
                    JsonDeepEquals(expectedDoc.RootElement, snapshotDoc.RootElement),
                    "Expected checkpoint snapshot JSON to be semantically equivalent to the source state JSON."
                );
            }

            // Ensure event appended.
            var eventsPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "events.ndjson");
            Assert.True(File.Exists(eventsPath), $"Expected events log at '{eventsPath}'.");

            var events = ReadNdjsonObjects(eventsPath);
            Assert.True(events.Count >= 1, "Expected at least one event after checkpoint create.");
            var last = events[^1];
            Assert.True(last.TryGetProperty("kind", out var kind) && kind.GetString() == "checkpoint.created");
            Assert.True(last.TryGetProperty("checkpointId", out var evtId) && evtId.GetString() == checkpointId);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task CheckpointRestore_RestoresStateAndAppendsEvent()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (initExit, _, initErr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );
            Assert.True(initExit == 0, $"Expected init exit code 0, got {initExit}. STDERR:{Environment.NewLine}{initErr}");
            WriteRepoMarker(tempWorkspaceRoot);

            var statePath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "state.json");

            // Set state A, create checkpoint.
            File.WriteAllText(
                statePath,
                "{\n  \"cursor\": {\"phase\": \"A\"},\n  \"schemaVersion\": 1\n}\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            var (createExit, createOut, createErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "checkpoint",
                "create"
            );
            Assert.True(createExit == 0, $"Expected checkpoint create exit code 0, got {createExit}. STDERR:{Environment.NewLine}{createErr}");
            var checkpointId = createOut.Trim();

            var snapshotPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "checkpoints", checkpointId, "state.json");
            Assert.True(File.Exists(snapshotPath), $"Expected checkpoint snapshot at '{snapshotPath}'.");
            var snapshotText = NormalizeJsonText(File.ReadAllText(snapshotPath, Encoding.UTF8));

            // Change state B.
            File.WriteAllText(
                statePath,
                "{\n  \"schemaVersion\": 1,\n  \"cursor\": {\"phase\": \"B\"}\n}\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            var (restoreExit, restoreOut, restoreErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "checkpoint",
                "restore",
                "--checkpoint-id",
                checkpointId
            );
            Assert.True(restoreExit == 0, $"Expected checkpoint restore exit code 0, got {restoreExit}. STDERR:{Environment.NewLine}{restoreErr}");
            Assert.Contains(checkpointId, restoreOut, StringComparison.OrdinalIgnoreCase);

            // Restored state should match the snapshot.
            var restoredText = NormalizeJsonText(File.ReadAllText(statePath, Encoding.UTF8));
            Assert.Equal(snapshotText, restoredText);

            // Ensure restore event appended.
            var eventsPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "events.ndjson");
            var events = ReadNdjsonObjects(eventsPath);
            Assert.True(events.Count >= 2, "Expected at least two events after create + restore.");
            var last = events[^1];
            Assert.True(last.TryGetProperty("kind", out var kind) && kind.GetString() == "checkpoint.restored");
            Assert.True(last.TryGetProperty("checkpointId", out var evtId) && evtId.GetString() == checkpointId);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task CheckpointCreate_WhenLocked_FailsWithStableExitCode()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (initExit, _, initErr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );
            Assert.True(initExit == 0, $"Expected init exit code 0, got {initExit}. STDERR:{Environment.NewLine}{initErr}");
            WriteRepoMarker(tempWorkspaceRoot);

            var (lockExit, _, lockErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "lock",
                "acquire"
            );
            Assert.True(lockExit == 0, $"Expected lock acquire exit code 0, got {lockExit}. STDERR:{Environment.NewLine}{lockErr}");

            var (createExit, _, createErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "checkpoint",
                "create"
            );

            Assert.True(createExit == 4, $"Expected lock contention exit code 4, got {createExit}. STDERR:{Environment.NewLine}{createErr}");
            Assert.Contains("Workspace is locked.", createErr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static List<JsonElement> ReadNdjsonObjects(string path)
    {
        var list = new List<JsonElement>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            list.Add(doc.RootElement.Clone());
        }

        return list;
    }

    private static bool JsonDeepEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var aProps = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
                var bProps = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
                if (aProps.Count != bProps.Count)
                {
                    return false;
                }

                foreach (var (name, av) in aProps)
                {
                    if (!bProps.TryGetValue(name, out var bv))
                    {
                        return false;
                    }

                    if (!JsonDeepEquals(av, bv))
                    {
                        return false;
                    }
                }

                return true;
            }

            case JsonValueKind.Array:
            {
                var aArr = a.EnumerateArray().ToArray();
                var bArr = b.EnumerateArray().ToArray();
                if (aArr.Length != bArr.Length)
                {
                    return false;
                }

                for (var i = 0; i < aArr.Length; i++)
                {
                    if (!JsonDeepEquals(aArr[i], bArr[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            case JsonValueKind.String:
                return string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number:
                return string.Equals(a.GetRawText(), b.GetRawText(), StringComparison.Ordinal);
            case JsonValueKind.True:
            case JsonValueKind.False:
                return a.GetBoolean() == b.GetBoolean();
            case JsonValueKind.Null:
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeJsonText(string text)
        => text.Replace("\r\n", "\n").TrimEnd('\n', '\r');

    private static void WriteRepoMarker(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "nirmata.slnx");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "nirmata-aos-checkpoint-cli", Guid.NewGuid().ToString("N"));
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

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunDotNetAsync(
        string? workingDirectory,
        string dllPath,
        params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

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
}

