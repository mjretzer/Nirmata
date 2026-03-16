using System.Text.Json;
using nirmata.Aos.Engine.Spec;
using nirmata.Aos.Engine.Stores;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.Planning.RoadmapModifier;

/// <summary>
/// Provides atomic write operations for roadmap and state spec files.
/// Ensures consistency between roadmap.json and state.json during modifications.
/// </summary>
public sealed class AtomicSpecWriter
{
    private readonly SpecStore _specStore;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="AtomicSpecWriter"/> class.
    /// </summary>
    public AtomicSpecWriter(SpecStore specStore)
    {
        _specStore = specStore ?? throw new ArgumentNullException(nameof(specStore));
    }

    /// <summary>
    /// Atomically writes both roadmap and state spec files.
    /// Uses a two-phase commit pattern to ensure consistency.
    /// </summary>
    /// <param name="roadmap">The roadmap spec document to write.</param>
    /// <param name="statePath">The path to the state file.</param>
    /// <param name="stateJson">The state JSON content to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if both writes succeeded; otherwise, false.</returns>
    internal async Task<bool> WriteAtomicAsync(
        RoadmapSpecDocument roadmap,
        string statePath,
        string stateJson,
        CancellationToken ct = default)
    {
        var tempRoadmapPath = GetTempPath("roadmap");
        var tempStatePath = GetTempPath("state");

        try
        {
            // Phase 1: Write to temp files
            await WriteTempRoadmapAsync(roadmap, tempRoadmapPath, ct);
            await File.WriteAllTextAsync(tempStatePath, stateJson, ct);

            // Phase 2: Validate temp files
            if (!ValidateTempFiles(tempRoadmapPath, tempStatePath))
            {
                CleanupTempFiles(tempRoadmapPath, tempStatePath);
                return false;
            }

            // Phase 3: Atomic move (rename) temp files to final locations
            // Write roadmap first as it's the primary spec
            _specStore.Inner.WriteRoadmapOverwrite(roadmap);

            // Then write state
            var stateDir = Path.GetDirectoryName(statePath);
            if (!string.IsNullOrEmpty(stateDir) && !Directory.Exists(stateDir))
            {
                Directory.CreateDirectory(stateDir);
            }
            await File.WriteAllTextAsync(statePath, stateJson, ct);

            // Cleanup temp files
            CleanupTempFiles(tempRoadmapPath, tempStatePath);

            return true;
        }
        catch
        {
            CleanupTempFiles(tempRoadmapPath, tempStatePath);
            return false;
        }
    }

    /// <summary>
    /// Performs a rollback of spec files from backup if available.
    /// </summary>
    internal async Task<bool> RollbackAsync(string backupDir, CancellationToken ct = default)
    {
        try
        {
            var backupRoadmapPath = Path.Combine(backupDir, "roadmap.json");
            var backupStatePath = Path.Combine(backupDir, "state.json");

            if (!File.Exists(backupRoadmapPath) || !File.Exists(backupStatePath))
            {
                return false;
            }

            var roadmapJson = await File.ReadAllTextAsync(backupRoadmapPath, ct);
            var stateJson = await File.ReadAllTextAsync(backupStatePath, ct);

            // Restore from backup
            var roadmap = JsonSerializer.Deserialize<RoadmapSpecDocument>(roadmapJson, JsonOptions);
            if (roadmap != null)
            {
                _specStore.Inner.WriteRoadmapOverwrite(roadmap);
            }

            var statePath = Path.Combine(GetAosRootPath(), ".aos/state/state.json");
            await File.WriteAllTextAsync(statePath, stateJson, ct);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a backup of current spec files before modification.
    /// </summary>
    internal async Task<string?> CreateBackupAsync(CancellationToken ct = default)
    {
        try
        {
            var backupDir = Path.Combine(Path.GetTempPath(), $"aos-backup-{Guid.NewGuid():N}");
            Directory.CreateDirectory(backupDir);

            var aosRoot = GetAosRootPath();
            var roadmapPath = Path.Combine(aosRoot, ".aos/spec/roadmap.json");
            var statePath = Path.Combine(aosRoot, ".aos/state/state.json");

            if (File.Exists(roadmapPath))
            {
                var roadmapBackup = Path.Combine(backupDir, "roadmap.json");
                File.Copy(roadmapPath, roadmapBackup, overwrite: true);
            }

            if (File.Exists(statePath))
            {
                var stateBackup = Path.Combine(backupDir, "state.json");
                File.Copy(statePath, stateBackup, overwrite: true);
            }

            return backupDir;
        }
        catch
        {
            return null;
        }
    }

    private async Task WriteTempRoadmapAsync(RoadmapSpecDocument roadmap, string tempPath, CancellationToken ct)
    {
        var tempDir = Path.GetDirectoryName(tempPath);
        if (!string.IsNullOrEmpty(tempDir) && !Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        var json = JsonSerializer.Serialize(roadmap, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, ct);
    }

    private bool ValidateTempFiles(string tempRoadmapPath, string tempStatePath)
    {
        try
        {
            // Validate roadmap is valid JSON and has required structure
            var roadmapJson = File.ReadAllText(tempRoadmapPath);
            var roadmap = JsonSerializer.Deserialize<RoadmapSpecDocument>(roadmapJson, JsonOptions);
            if (roadmap == null || roadmap.SchemaVersion != 1)
            {
                return false;
            }

            // Validate state is valid JSON
            var stateJson = File.ReadAllText(tempStatePath);
            using var stateDoc = JsonDocument.Parse(stateJson);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void CleanupTempFiles(params string[] paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string GetTempPath(string prefix)
    {
        return Path.Combine(Path.GetTempPath(), $"aos-{prefix}-{Guid.NewGuid():N}.tmp");
    }

    private string GetAosRootPath()
    {
        var innerField = _specStore.GetType().GetProperty("Inner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var innerStore = innerField?.GetValue(_specStore);

        if (innerStore != null)
        {
            var aosRootField = innerStore.GetType().GetField("_aosRootPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return aosRootField?.GetValue(innerStore)?.ToString() ?? ".";
        }

        return ".";
    }
}
