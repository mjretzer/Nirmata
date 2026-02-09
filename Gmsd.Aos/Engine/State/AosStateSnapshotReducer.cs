using System.Text.Json;

namespace Gmsd.Aos.Engine.State;

/// <summary>
/// Deterministic reducer that derives <c>.aos/state/state.json</c> from the ordered
/// event stream in <c>.aos/state/events.ndjson</c>.
/// </summary>
internal static class AosStateSnapshotReducer
{
    public const string CursorSetEventType = "cursor.set";
    public const string CheckpointRestoredKind = "checkpoint.restored";
    public const string CheckpointCreatedKind = "checkpoint.created";

    public static StateSnapshotDocument Reduce(
        StateSnapshotDocument baseline,
        IReadOnlyList<JsonElement> orderedEvents,
        Func<string, StateSnapshotDocument> loadSnapshotByContractPath)
    {
        if (loadSnapshotByContractPath is null) throw new ArgumentNullException(nameof(loadSnapshotByContractPath));
        if (baseline is null) throw new ArgumentNullException(nameof(baseline));

        var current = CanonicalizeSnapshot(baseline);

        foreach (var evt in orderedEvents)
        {
            if (evt.ValueKind != JsonValueKind.Object)
            {
                // NDJSON parsing enforces object-per-line; keep the reducer defensive and deterministic.
                continue;
            }

            if (!TryGetEventKindOrType(evt, out var kindOrType))
            {
                continue;
            }

            // Preferred convention is eventType, but some legacy emitters use kind.
            if (string.Equals(kindOrType, CursorSetEventType, StringComparison.Ordinal))
            {
                if (TryGetCursorPatch(evt, out var patch))
                {
                    current = current with { Cursor = ApplyCursorPatch(current.Cursor, patch) };
                }

                continue;
            }

            // Legacy checkpoint events (kind-based). Created is audit-only; restored mutates the snapshot.
            if (string.Equals(kindOrType, CheckpointCreatedKind, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(kindOrType, CheckpointRestoredKind, StringComparison.Ordinal))
            {
                if (!TryGetStringProperty(evt, "snapshotContractPath", out var snapshotContractPath) ||
                    string.IsNullOrWhiteSpace(snapshotContractPath))
                {
                    throw new InvalidOperationException(
                        "checkpoint.restored event is missing required 'snapshotContractPath'.");
                }

                current = CanonicalizeSnapshot(loadSnapshotByContractPath(snapshotContractPath));
            }
        }

        return CanonicalizeSnapshot(current);
    }

    private static StateSnapshotDocument CanonicalizeSnapshot(StateSnapshotDocument doc)
    {
        if (doc.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported state snapshot schemaVersion '{doc.SchemaVersion}'.");
        }

        return doc with { Cursor = doc.Cursor ?? new StateCursorDocument() };
    }

    private static bool TryGetEventKindOrType(JsonElement evt, out string? kindOrType)
    {
        kindOrType = null;

        if (TryGetStringProperty(evt, "eventType", out var eventType) && !string.IsNullOrWhiteSpace(eventType))
        {
            kindOrType = eventType;
            return true;
        }

        if (TryGetStringProperty(evt, "kind", out var kind) && !string.IsNullOrWhiteSpace(kind))
        {
            kindOrType = kind;
            return true;
        }

        return false;
    }

    private static bool TryGetCursorPatch(JsonElement evt, out JsonElement patch)
    {
        patch = default;

        if (evt.TryGetProperty("cursor", out var cursor) && cursor.ValueKind == JsonValueKind.Object)
        {
            patch = cursor;
            return true;
        }

        if (evt.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            if (data.TryGetProperty("cursor", out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                patch = nested;
                return true;
            }

            // If the event places cursor fields directly in "data", treat "data" as the patch.
            patch = data;
            return true;
        }

        return false;
    }

    private static StateCursorDocument ApplyCursorPatch(StateCursorDocument current, JsonElement patch)
    {
        var cursor = current ?? new StateCursorDocument();

        cursor = ApplyNullableString(cursor, patch, "milestoneId", static (c, v) => c with { MilestoneId = v });
        cursor = ApplyNullableString(cursor, patch, "phaseId", static (c, v) => c with { PhaseId = v });
        cursor = ApplyNullableString(cursor, patch, "taskId", static (c, v) => c with { TaskId = v });
        cursor = ApplyNullableString(cursor, patch, "stepId", static (c, v) => c with { StepId = v });

        cursor = ApplyNullableString(cursor, patch, "milestoneStatus", static (c, v) => c with { MilestoneStatus = v });
        cursor = ApplyNullableString(cursor, patch, "phaseStatus", static (c, v) => c with { PhaseStatus = v });
        cursor = ApplyNullableString(cursor, patch, "taskStatus", static (c, v) => c with { TaskStatus = v });
        cursor = ApplyNullableString(cursor, patch, "stepStatus", static (c, v) => c with { StepStatus = v });

        // Legacy cursor reference fields.
        cursor = ApplyNullableString(cursor, patch, "kind", static (c, v) => c with { Kind = v });
        cursor = ApplyNullableString(cursor, patch, "id", static (c, v) => c with { Id = v });

        return cursor;
    }

    private static StateCursorDocument ApplyNullableString(
        StateCursorDocument current,
        JsonElement patch,
        string propertyName,
        Func<StateCursorDocument, string?, StateCursorDocument> apply)
    {
        if (!patch.TryGetProperty(propertyName, out var value))
        {
            return current;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return apply(current, null);
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return apply(current, value.GetString());
        }

        throw new InvalidOperationException(
            $"cursor.set patch property '{propertyName}' must be a string or null (was {value.ValueKind}).");
    }

    private static bool TryGetStringProperty(JsonElement obj, string propertyName, out string? value)
    {
        value = null;
        if (!obj.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = prop.GetString();
        return true;
    }
}

