using System.Globalization;
using System.Text.Json;
using Gmsd.Aos.Engine;
using Gmsd.Aos.Engine.Paths;
using Gmsd.Aos.Public.Context.Packs;

namespace Gmsd.Aos.Context.Packs;

internal static class AosContextPackWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static (string PackId, string ContractPath, string FilePath, ContextPackDocument Pack) BuildAndWriteNewPack(
        string aosRootPath,
        string mode,
        string drivingId,
        ContextPackBudget budget)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (string.IsNullOrWhiteSpace(mode)) throw new ArgumentNullException(nameof(mode));
        if (string.IsNullOrWhiteSpace(drivingId)) throw new ArgumentNullException(nameof(drivingId));

        var packsRoot = Path.Combine(aosRootPath, "context", "packs");
        Directory.CreateDirectory(packsRoot);

        var packId = AllocateNextPackId(packsRoot);
        var contractPath = AosPathRouter.GetContractPath(AosArtifactKind.ContextPack, packId);
        var filePath = AosPathRouter.ToAosRootPath(aosRootPath, contractPath);

        var pack = AosContextPackBuilder.Build(
            aosRootPath,
            packId: packId,
            mode: mode,
            drivingId: drivingId,
            budget: budget
        );

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            filePath,
            pack,
            serializerOptions: JsonOptions,
            writeIndented: true
        );

        return (packId, contractPath, filePath, pack);
    }

    internal static string AllocateNextPackId(string packsRootPath)
    {
        // Deterministic, monotonic ID allocation based on existing canonical pack files.
        // Format: PCK-#### (4 digits).
        var max = 0;
        if (Directory.Exists(packsRootPath))
        {
            foreach (var file in Directory.EnumerateFiles(packsRootPath, "PCK-*.json", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                if (string.IsNullOrWhiteSpace(name) || !name.EndsWith(".json", StringComparison.Ordinal))
                {
                    continue;
                }

                var stem = name[..^".json".Length];
                if (!AosPathRouter.TryParseArtifactId(stem, out var kind, out var normalizedId, out _)
                    || kind != AosArtifactKind.ContextPack
                    || !string.Equals(normalizedId, stem, StringComparison.Ordinal))
                {
                    continue; // ignore non-canonical
                }

                var digits = stem["PCK-".Length..];
                if (int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                {
                    max = Math.Max(max, n);
                }
            }
        }

        var next = max + 1;
        if (next > 9999)
        {
            throw new InvalidOperationException("Context pack id space exhausted (max PCK-9999).");
        }

        return $"PCK-{next:0000}";
    }
}

