using System.Security.Cryptography;
using System.Text.Json;
using nirmata.Data.Dto.Models.Codebase;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

/// <summary>
/// Reads workspace codebase intelligence artifacts from <c>.aos/codebase/</c>.
/// Classifies artifact freshness using <c>hash-manifest.json</c> and surfaces
/// language and stack metadata from <c>map.json</c> and <c>stack.json</c>.
/// All methods are resilient to missing directories or malformed files.
/// </summary>
public sealed class CodebaseService : ICodebaseService
{
    // Stable artifact id → relative path within .aos/codebase/ (forward-slash convention).
    private static readonly IReadOnlyDictionary<string, string> KnownArtifacts =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["map"]          = "map.json",
            ["stack"]        = "stack.json",
            ["architecture"] = "architecture.json",
            ["structure"]    = "structure.json",
            ["conventions"]  = "conventions.json",
            ["testing"]      = "testing.json",
            ["integrations"] = "integrations.json",
            ["concerns"]     = "concerns.json",
            ["symbols"]      = "cache/symbols.json",
            ["file-graph"]   = "cache/file-graph.json",
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── Public API ──────────────────────────────────────────────────────────

    public async Task<CodebaseInventoryDto> GetInventoryAsync(
        string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var codebaseDir = Path.Combine(workspaceRoot, ".aos", "codebase");
        var manifest = await LoadManifestAsync(codebaseDir, cancellationToken);

        var artifacts = new List<CodebaseArtifactDto>(KnownArtifacts.Count);
        foreach (var (id, relPath) in KnownArtifacts)
        {
            var fullPath = ResolveArtifactPath(codebaseDir, relPath);
            var status = await ClassifyStatusAsync(fullPath, relPath, manifest, cancellationToken);
            artifacts.Add(new CodebaseArtifactDto
            {
                Id = id,
                Type = id,
                Status = status,
                Path = $".aos/codebase/{relPath}",
                LastUpdated = File.Exists(fullPath)
                    ? new DateTimeOffset(File.GetLastWriteTimeUtc(fullPath), TimeSpan.Zero)
                    : null,
            });
        }

        var languages = await ExtractLanguagesAsync(codebaseDir, cancellationToken);
        var stack = await ExtractStackAsync(codebaseDir, cancellationToken);

        return new CodebaseInventoryDto
        {
            Artifacts = artifacts,
            Languages = languages,
            Stack = stack,
        };
    }

    public async Task<CodebaseArtifactDetailDto?> GetArtifactAsync(
        string workspaceRoot, string artifactId, CancellationToken cancellationToken = default)
    {
        if (!KnownArtifacts.TryGetValue(artifactId, out var relPath))
            return null; // unsupported artifact id → caller returns 404

        var codebaseDir = Path.Combine(workspaceRoot, ".aos", "codebase");
        var fullPath = ResolveArtifactPath(codebaseDir, relPath);
        var manifest = await LoadManifestAsync(codebaseDir, cancellationToken);
        var status = await ClassifyStatusAsync(fullPath, relPath, manifest, cancellationToken);

        JsonElement? payload = null;
        if (status is CodebaseArtifactStatus.Ready or CodebaseArtifactStatus.Stale)
        {
            payload = await TryParseJsonAsync(fullPath, cancellationToken);
        }

        return new CodebaseArtifactDetailDto
        {
            Id = artifactId.ToLowerInvariant(),
            Type = artifactId.ToLowerInvariant(),
            Status = status,
            Path = $".aos/codebase/{relPath}",
            LastUpdated = File.Exists(fullPath)
                ? new DateTimeOffset(File.GetLastWriteTimeUtc(fullPath), TimeSpan.Zero)
                : null,
            Payload = payload,
        };
    }

    // ── Status classification ────────────────────────────────────────────────

    private static async Task<string> ClassifyStatusAsync(
        string fullPath,
        string relPath,
        Dictionary<string, string>? manifest,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(fullPath))
            return CodebaseArtifactStatus.Missing;

        // Verify the file is parseable JSON.
        var element = await TryParseJsonAsync(fullPath, cancellationToken);
        if (element is null)
            return CodebaseArtifactStatus.Error;

        // No manifest → cannot verify hash, report stale.
        if (manifest is null || !manifest.TryGetValue(relPath, out var expectedHash))
            return CodebaseArtifactStatus.Stale;

        // Hash comparison.
        try
        {
            await using var stream = File.OpenRead(fullPath);
            var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
            var actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            return actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)
                ? CodebaseArtifactStatus.Ready
                : CodebaseArtifactStatus.Stale;
        }
        catch (IOException)
        {
            return CodebaseArtifactStatus.Error;
        }
    }

    // ── Language / stack extraction ──────────────────────────────────────────

    /// <summary>
    /// Extracts language names from <c>stack.json → languages[].name</c>.
    /// Returns an empty list on any failure.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ExtractLanguagesAsync(
        string codebaseDir, CancellationToken cancellationToken)
    {
        var stackPath = Path.Combine(codebaseDir, "stack.json");
        var element = await TryParseJsonAsync(stackPath, cancellationToken);
        if (element is null)
            return [];

        var root = element.Value;
        if (!root.TryGetProperty("languages", out var languages) ||
            languages.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<string>();
        foreach (var item in languages.EnumerateArray())
        {
            if (item.TryGetProperty("name", out var name) &&
                name.ValueKind == JsonValueKind.String)
            {
                var nameStr = name.GetString();
                if (!string.IsNullOrWhiteSpace(nameStr))
                    result.Add(nameStr);
            }
        }
        return result;
    }

    /// <summary>
    /// Extracts framework/runtime names from <c>stack.json → frameworks[].name</c>.
    /// Returns an empty list on any failure.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ExtractStackAsync(
        string codebaseDir, CancellationToken cancellationToken)
    {
        var stackPath = Path.Combine(codebaseDir, "stack.json");
        var element = await TryParseJsonAsync(stackPath, cancellationToken);
        if (element is null)
            return [];

        var root = element.Value;
        var result = new List<string>();

        // Collect from frameworks array.
        if (root.TryGetProperty("frameworks", out var frameworks) &&
            frameworks.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in frameworks.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var name) &&
                    name.ValueKind == JsonValueKind.String)
                {
                    var nameStr = name.GetString();
                    if (!string.IsNullOrWhiteSpace(nameStr))
                        result.Add(nameStr);
                }
            }
        }

        // Also collect from runtimes array if present.
        if (root.TryGetProperty("runtimes", out var runtimes) &&
            runtimes.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in runtimes.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var name) &&
                    name.ValueKind == JsonValueKind.String)
                {
                    var nameStr = name.GetString();
                    if (!string.IsNullOrWhiteSpace(nameStr))
                        result.Add(nameStr);
                }
            }
        }

        return result;
    }

    // ── Manifest loading ─────────────────────────────────────────────────────

    /// <summary>
    /// Loads the hash manifest from <c>.aos/codebase/hash-manifest.json</c>.
    /// Returns <see langword="null"/> when the file is absent or malformed.
    /// </summary>
    private static async Task<Dictionary<string, string>?> LoadManifestAsync(
        string codebaseDir, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(codebaseDir, "hash-manifest.json");
        if (!File.Exists(manifestPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("files", out var filesEl) ||
                filesEl.ValueKind != JsonValueKind.Object)
                return null;

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in filesEl.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                    result[prop.Name] = prop.Value.GetString()!;
            }
            return result;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a forward-slash artifact relative path to a platform-native absolute path.
    /// </summary>
    private static string ResolveArtifactPath(string codebaseDir, string relPath) =>
        Path.Combine(codebaseDir, relPath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// Reads and parses a JSON file.
    /// Returns <see langword="null"/> on any I/O or parse error.
    /// </summary>
    private static async Task<JsonElement?> TryParseJsonAsync(
        string fullPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(fullPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(fullPath, cancellationToken);
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone(); // Clone to allow doc disposal
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }
}
