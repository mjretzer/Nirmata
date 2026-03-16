using System.Security.Cryptography;
using System.Text.Json;
using nirmata.Aos.Engine.Paths;
using nirmata.Aos.Engine;

namespace nirmata.Aos.Engine.Evidence.Runs;

internal static class AosRunManifestWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void WriteRunManifest(string aosRootPath, string runId)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        var runRootPath = AosPathRouter.GetRunEvidenceRootPath(aosRootPath, runId);
        var artifactsRootPath = AosPathRouter.GetRunArtifactsRootPath(aosRootPath, runId);
        var outputsRootPath = AosPathRouter.GetRunOutputsRootPath(aosRootPath, runId);
        var manifestPath = AosPathRouter.GetRunManifestPath(aosRootPath, runId);

        Directory.CreateDirectory(runRootPath);
        Directory.CreateDirectory(artifactsRootPath);

        var outputs = new List<RunManifestOutputDocument>();

        if (Directory.Exists(outputsRootPath))
        {
            foreach (var fullPath in Directory.EnumerateFiles(outputsRootPath, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(outputsRootPath, fullPath)
                    .Replace('\\', '/')
                    .Trim();

                if (string.IsNullOrWhiteSpace(rel))
                {
                    continue;
                }

                outputs.Add(
                    new RunManifestOutputDocument(
                        RelativePath: rel,
                        Sha256: ComputeSha256HexLower(fullPath)
                    )
                );
            }
        }

        var ordered = outputs
            .OrderBy(o => o.RelativePath, StringComparer.Ordinal)
            .ToArray();

        // Canonical deterministic JSON (stable bytes + atomic write semantics).
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            manifestPath,
            new RunManifestDocument(
                SchemaVersion: 1,
                RunId: runId,
                Outputs: ordered
            ),
            JsonOptions,
            writeIndented: true
        );
    }

    private static string ComputeSha256HexLower(string fullPath)
    {
        using var stream = File.OpenRead(fullPath);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record RunManifestDocument(
        int SchemaVersion,
        string RunId,
        IReadOnlyList<RunManifestOutputDocument> Outputs);

    private sealed record RunManifestOutputDocument(
        string RelativePath,
        string Sha256);
}

