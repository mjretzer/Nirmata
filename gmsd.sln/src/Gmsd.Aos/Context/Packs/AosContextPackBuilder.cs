using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gmsd.Aos.Engine;
using Gmsd.Aos.Engine.Paths;
using Gmsd.Aos.Public.Context.Packs;

namespace Gmsd.Aos.Context.Packs;

internal static class AosContextPackBuilder
{
    public const string ModeTask = "task";
    public const string ModePhase = "phase";

    public static ContextPackDocument Build(
        string aosRootPath,
        string packId,
        string mode,
        string drivingId,
        ContextPackBudget budget)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (string.IsNullOrWhiteSpace(packId)) throw new ArgumentNullException(nameof(packId));
        if (string.IsNullOrWhiteSpace(mode)) throw new ArgumentNullException(nameof(mode));
        if (string.IsNullOrWhiteSpace(drivingId)) throw new ArgumentNullException(nameof(drivingId));

        var normalizedMode = mode.Trim().ToLowerInvariant();
        if (normalizedMode is not ModeTask and not ModePhase)
        {
            throw new ArgumentException($"Unsupported pack mode '{mode}'. Expected '{ModeTask}' or '{ModePhase}'.", nameof(mode));
        }

        var candidates = GetCandidateContractPaths(normalizedMode, drivingId);
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No candidate artifacts found for context pack.");
        }

        // Mandatory driving artifact MUST be included; we always attempt it first, then proceed in stable order.
        var drivingContractPath = candidates[0];
        var remaining = candidates
            .Skip(1)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        var orderedCandidates = new List<string>(capacity: candidates.Count);
        orderedCandidates.Add(drivingContractPath);
        orderedCandidates.AddRange(remaining);

        var entries = new List<ContextPackEntryDocument>(capacity: orderedCandidates.Count);
        var usedBytes = 0;
        var usedItems = 0;

        for (var i = 0; i < orderedCandidates.Count; i++)
        {
            if (usedItems >= budget.MaxItems)
            {
                break;
            }

            var contractPath = orderedCandidates[i];
            var isDriving = i == 0;

            ContextPackEntryDocument entry;
            if (isDriving)
            {
                entry = ReadEntry(aosRootPath, contractPath, required: true);
            }
            else
            {
                if (!TryReadEntry(aosRootPath, contractPath, out entry))
                {
                    // Optional artifacts are included only when present.
                    continue;
                }
            }

            if (budget.MaxBytes > 0 && usedBytes + entry.Bytes > budget.MaxBytes)
            {
                if (isDriving)
                {
                    throw new InvalidOperationException(
                        $"Context pack byte budget ({budget.MaxBytes}) is too small to include driving artifact '{contractPath}' ({entry.Bytes} bytes).");
                }

                break; // stable stop-at-boundary behavior
            }

            entries.Add(entry);
            usedBytes += entry.Bytes;
            usedItems += 1;
        }

        if (entries.Count == 0)
        {
            throw new InvalidOperationException("Context pack build produced no entries.");
        }

        // Enforce driving artifact inclusion.
        if (!string.Equals(entries[0].ContractPath, drivingContractPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Context pack build did not include the driving artifact.");
        }

        return new ContextPackDocument(
            SchemaVersion: 1,
            PackId: packId.Trim(),
            Mode: normalizedMode,
            DrivingId: drivingId.Trim(),
            Budget: budget,
            Summary: new ContextPackSummary(TotalBytes: usedBytes, TotalItems: usedItems),
            Entries: entries
        );
    }

    private static List<string> GetCandidateContractPaths(string mode, string drivingId)
    {
        if (mode == ModeTask)
        {
            if (!AosPathRouter.TryParseArtifactId(drivingId, out var kind, out var normalizedId, out var error) ||
                kind != AosArtifactKind.Task)
            {
                throw new ArgumentException(error, nameof(drivingId));
            }

            // Driving task plan is required by spec.
            var root = $".aos/spec/tasks/{normalizedId}/";
            return
            [
                root + "plan.json",
                root + "task.json",
                root + "links.json"
            ];
        }

        if (mode == ModePhase)
        {
            if (!AosPathRouter.TryParseArtifactId(drivingId, out var kind, out var normalizedId, out var error) ||
                kind != AosArtifactKind.Phase)
            {
                throw new ArgumentException(error, nameof(drivingId));
            }

            // Driving phase spec is required by spec.
            return
            [
                $".aos/spec/phases/{normalizedId}/phase.json"
            ];
        }

        throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported pack mode.");
    }

    private static bool TryReadEntry(string aosRootPath, string contractPath, out ContextPackEntryDocument entry)
    {
        entry = default!;
        var fullPath = AosPathRouter.ToAosRootPath(aosRootPath, contractPath);
        if (!File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            return false;
        }

        entry = ReadEntry(aosRootPath, contractPath, required: false);
        return true;
    }

    private static ContextPackEntryDocument ReadEntry(string aosRootPath, string contractPath, bool required)
    {
        if (contractPath is null) throw new ArgumentNullException(nameof(contractPath));
        if (contractPath.Contains('\\'))
        {
            throw new ArgumentException("Contract paths MUST use '/' separators.", nameof(contractPath));
        }
        if (!contractPath.StartsWith(".aos/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Contract path must start with '.aos/'.", nameof(contractPath));
        }

        var fullPath = AosPathRouter.ToAosRootPath(aosRootPath, contractPath);
        if (!File.Exists(fullPath))
        {
            if (!required)
            {
                throw new FileNotFoundException($"Missing expected file: {contractPath}", fullPath);
            }
            throw new FileNotFoundException($"Missing required artifact: {contractPath}", fullPath);
        }
        if (Directory.Exists(fullPath))
        {
            if (!required)
            {
                throw new InvalidOperationException($"Expected file, found directory at '{contractPath}'.");
            }
            throw new InvalidOperationException($"Expected file, found directory at '{contractPath}'.");
        }

        var contentType = contractPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? "application/json"
            : "text/plain";

        byte[] canonicalBytes;
        string canonicalContent;

        if (contentType == "application/json")
        {
            using var stream = File.OpenRead(fullPath);
            using var doc = JsonDocument.Parse(stream);
            canonicalBytes = DeterministicJsonFileWriter.CanonicalizeToUtf8Bytes(doc.RootElement, writeIndented: false);
            canonicalContent = Encoding.UTF8.GetString(canonicalBytes);
        }
        else
        {
            var rawBytes = File.ReadAllBytes(fullPath);
            canonicalBytes = CanonicalizeTextToUtf8Bytes(rawBytes);
            canonicalContent = Encoding.UTF8.GetString(canonicalBytes);
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(canonicalBytes)).ToLowerInvariant();

        return new ContextPackEntryDocument(
            ContractPath: contractPath,
            ContentType: contentType,
            Content: canonicalContent,
            Sha256: sha256,
            Bytes: canonicalBytes.Length
        );
    }

    private static byte[] CanonicalizeTextToUtf8Bytes(byte[] rawBytes)
    {
        // Keep this predictable across platforms by normalizing line endings to LF and ensuring a trailing LF.
        // (Similar to DeterministicJsonFileWriter behavior.)
        var text = Encoding.UTF8.GetString(rawBytes);
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        if (!text.EndsWith("\n", StringComparison.Ordinal))
        {
            text += "\n";
        }
        return Encoding.UTF8.GetBytes(text);
    }
}

