using System.Text.Json;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Engine;
using nirmata.Aos.Engine.State;

namespace nirmata.Aos.Engine.Stores;

internal sealed class AosStateStore : AosJsonStoreBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions NdjsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public const string StateContractPath = ".aos/state/state.json";
    public const string EventsContractPath = ".aos/state/events.ndjson";

    public AosStateStore(string aosRootPath)
        : base(aosRootPath, ".aos/state/")
    {
    }

    public StateSnapshotDocument ReadStateSnapshot() =>
        ReadJson<StateSnapshotDocument>(StateContractPath, JsonOptions);

    public void WriteStateSnapshotIfMissing(StateSnapshotDocument doc)
        => WriteJsonIfMissing(StateContractPath, CanonicalizeSnapshot(doc), JsonOptions, writeIndented: true);

    public void WriteStateSnapshotOverwrite(StateSnapshotDocument doc)
        => WriteJsonOverwrite(StateContractPath, CanonicalizeSnapshot(doc), JsonOptions, writeIndented: true);

    /// <summary>
    /// Deterministically derives the next snapshot by replaying the ordered event log.
    /// This method does not mutate files; callers can persist via <see cref="WriteStateSnapshotOverwrite"/>.
    /// </summary>
    public StateSnapshotDocument DeriveStateSnapshotFromEvents(StateSnapshotDocument? baseline = null)
    {
        var seed = baseline ?? new StateSnapshotDocument(SchemaVersion: 1, Cursor: new StateCursorDocument());
        var orderedEvents = ReadEvents();

        return AosStateSnapshotReducer.Reduce(
            baseline: seed,
            orderedEvents: orderedEvents,
            loadSnapshotByContractPath: contractPath => ReadJson<StateSnapshotDocument>(contractPath, JsonOptions)
        );
    }

    /// <summary>
    /// Deterministically derives and overwrites <c>.aos/state/state.json</c> from the ordered event log.
    /// </summary>
    public StateSnapshotDocument DeriveAndWriteStateSnapshotFromEventsOverwrite(StateSnapshotDocument? baseline = null)
    {
        var next = DeriveStateSnapshotFromEvents(baseline);
        WriteStateSnapshotOverwrite(next);
        return next;
    }

    public void EnsureEventsLogExists()
    {
        var fullPath = ResolveFilePath(EventsContractPath);
        if (File.Exists(fullPath))
        {
            return;
        }

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Empty file is permitted; line-ending requirements apply to non-empty lines.
        using var _ = File.Open(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
    }

    public void AppendEvent(object evt)
    {
        if (evt is null) throw new ArgumentNullException(nameof(evt));

        EnsureEventsLogExists();

        using var doc = JsonSerializer.SerializeToDocument(evt, NdjsonOptions);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("State events MUST serialize to a JSON object for NDJSON.", nameof(evt));
        }

        // Canonicalize to stable bytes (sorted properties) and ensure a trailing LF per event line.
        var lineBytes = DeterministicJsonFileWriter.CanonicalizeToUtf8Bytes(doc.RootElement, writeIndented: false);

        var fullPath = ResolveFilePath(EventsContractPath);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

        // Enforce LF-only line termination between entries without rewriting history.
        if (stream.Length > 0)
        {
            stream.Seek(-1, SeekOrigin.End);
            var last = stream.ReadByte();
            if (last != (byte)'\n')
            {
                stream.Seek(0, SeekOrigin.End);
                stream.WriteByte((byte)'\n');
            }
        }

        stream.Seek(0, SeekOrigin.End);
        stream.Write(lineBytes, 0, lineBytes.Length);
        stream.Flush(flushToDisk: true);
    }

    public IReadOnlyList<JsonElement> ReadEvents()
    {
        var fullPath = ResolveFilePath(EventsContractPath);
        if (!File.Exists(fullPath))
        {
            return Array.Empty<JsonElement>();
        }

        // Read as UTF-8 text; we validate each non-empty line as a JSON object.
        var text = File.ReadAllText(fullPath);
        if (text.Length == 0)
        {
            return Array.Empty<JsonElement>();
        }

        var events = new List<JsonElement>();
        var lines = text.Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Encountered non-object NDJSON line in '{EventsContractPath}'.");
            }

            events.Add(doc.RootElement.Clone());
        }

        return events;
    }

    /// <summary>
    /// Tails an ordered slice of <c>.aos/state/events.ndjson</c> without re-sorting.
    /// Supports filtering by <c>eventType</c> and legacy <c>kind</c>, plus paging via
    /// <c>sinceLine</c> (exclusive) and <c>maxItems</c>.
    /// </summary>
    public StateEventTailResponse TailEvents(StateEventTailRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (request.SinceLine < 0) throw new ArgumentOutOfRangeException(nameof(request.SinceLine), "sinceLine must be >= 0.");

        if (request.MaxItems is <= 0)
        {
            return new StateEventTailResponse();
        }

        var fullPath = ResolveFilePath(EventsContractPath);
        if (!File.Exists(fullPath))
        {
            return new StateEventTailResponse();
        }

        var items = new List<StateEventEntry>();
        var lineNo = 0;

        foreach (var rawLine in File.ReadLines(fullPath))
        {
            lineNo++;

            // sinceLine is an exclusive 1-based cursor over physical lines.
            if (lineNo <= request.SinceLine)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(rawLine);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Encountered invalid JSON in '{EventsContractPath}' at non-empty line {lineNo}.",
                    ex
                );
            }

            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException(
                        $"Encountered non-object NDJSON line in '{EventsContractPath}' at line {lineNo}."
                    );
                }

                if (!MatchesTailFilters(doc.RootElement, request))
                {
                    continue;
                }

                items.Add(new StateEventEntry
                {
                    LineNumber = lineNo,
                    Payload = doc.RootElement.Clone()
                });
            }

            if (request.MaxItems.HasValue && items.Count >= request.MaxItems.Value)
            {
                break;
            }
        }

        return new StateEventTailResponse { Items = items };
    }

    private static bool MatchesTailFilters(JsonElement payload, StateEventTailRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            if (!TryGetStringProperty(payload, "eventType", out var value) ||
                !string.Equals(value, request.EventType, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            if (!TryGetStringProperty(payload, "kind", out var value) ||
                !string.Equals(value, request.Kind, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetStringProperty(JsonElement obj, string propertyName, out string? value)
    {
        value = null;
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!obj.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = prop.GetString();
        return value is not null;
    }

    private static StateSnapshotDocument CanonicalizeSnapshot(StateSnapshotDocument doc)
    {
        if (doc is null) throw new ArgumentNullException(nameof(doc));
        if (doc.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported state snapshot schemaVersion '{doc.SchemaVersion}'.");
        }

        return doc with { Cursor = doc.Cursor ?? new StateCursorDocument() };
    }
}

