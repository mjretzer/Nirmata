using System.Text.Json;
using Gmsd.Agents.Models.Results;
using Gmsd.Aos.Engine.Spec;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Engine.Stores;
using Gmsd.Aos.Public;

namespace Gmsd.Agents.Execution.Planning.RoadmapModifier;

/// <summary>
/// Handles cursor coherence preservation during roadmap modifications.
/// Ensures the execution cursor remains valid after phase insertions, removals, and renumbering.
/// </summary>
public sealed class CursorCoherencePreserver
{
    private readonly SpecStore _specStore;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="CursorCoherencePreserver"/> class.
    /// </summary>
    public CursorCoherencePreserver(SpecStore specStore)
    {
        _specStore = specStore ?? throw new ArgumentNullException(nameof(specStore));
    }

    /// <summary>
    /// Gets the current state snapshot from state.json.
    /// </summary>
    private string GetStatePath()
    {
        // Get the AOS root path from the spec store
        var aosRootPath = GetAosRootPath();
        return Path.Combine(aosRootPath, ".aos/state/state.json");
    }

    private string GetAosRootPath()
    {
        // Access the private field through reflection
        var innerField = _specStore.GetType().GetProperty("Inner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var innerStore = innerField?.GetValue(_specStore);
        
        if (innerStore != null)
        {
            var aosRootField = innerStore.GetType().GetField("_aosRootPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return aosRootField?.GetValue(innerStore)?.ToString() ?? ".";
        }
        
        return ".";
    }

    /// <summary>
    /// Reads the current state snapshot.
    /// </summary>
    public async Task<StateSnapshot?> ReadStateAsync(CancellationToken ct = default)
    {
        var statePath = GetStatePath();
        
        if (!File.Exists(statePath))
        {
            return null;
        }

        try
        {
            var stateJson = await File.ReadAllTextAsync(statePath, ct);
            return JsonSerializer.Deserialize<StateSnapshot>(stateJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the state snapshot to state.json.
    /// </summary>
    public async Task WriteStateAsync(StateSnapshot state, CancellationToken ct = default)
    {
        var statePath = GetStatePath();
        var stateDir = Path.GetDirectoryName(statePath);
        
        if (!string.IsNullOrEmpty(stateDir) && !Directory.Exists(stateDir))
        {
            Directory.CreateDirectory(stateDir);
        }

        var stateJson = JsonSerializer.Serialize(state, JsonOptions);
        await File.WriteAllTextAsync(statePath, stateJson, ct);
    }

    /// <summary>
    /// Updates the cursor after a phase is removed.
    /// If the removed phase was the cursor phase, moves cursor to the next available phase.
    /// </summary>
    public async Task UpdateCursorAfterRemovalAsync(
        string removedPhaseId,
        List<PhaseSpec> remainingPhases,
        CancellationToken ct = default)
    {
        var state = await ReadStateAsync(ct);
        if (state == null)
        {
            return;
        }

        var cursor = state.Cursor;
        if (cursor?.PhaseId != removedPhaseId)
        {
            // Cursor is not pointing to removed phase, no update needed
            return;
        }

        // Find the next phase after the removed one
        var removedPhaseIndex = remainingPhases.FindIndex(p => 
            _renumberer?.ExtractSequenceNumber(p.PhaseId) > _renumberer?.ExtractSequenceNumber(removedPhaseId));

        string? newPhaseId = null;
        if (removedPhaseIndex >= 0 && removedPhaseIndex < remainingPhases.Count)
        {
            newPhaseId = remainingPhases[removedPhaseIndex].PhaseId;
        }
        else if (remainingPhases.Any())
        {
            // Use the last phase if no next phase
            newPhaseId = remainingPhases.Last().PhaseId;
        }

        var updatedCursor = new StateCursor
        {
            PhaseId = newPhaseId,
            PhaseStatus = newPhaseId != null ? "pending" : null,
            TaskId = null,
            TaskStatus = null,
            StepId = null,
            StepStatus = null,
            MilestoneId = cursor?.MilestoneId,
            MilestoneStatus = cursor?.MilestoneStatus
        };

        var updatedState = new StateSnapshot
        {
            SchemaVersion = state.SchemaVersion,
            Cursor = updatedCursor
        };

        await WriteStateAsync(updatedState, ct);
    }

    private IRoadmapRenumberer? _renumberer;

    /// <summary>
    /// Sets the renumberer for sequence number extraction.
    /// </summary>
    public void SetRenumberer(IRoadmapRenumberer renumberer)
    {
        _renumberer = renumberer;
    }

    /// <summary>
    /// Updates the cursor after phases are renumbered.
    /// If the cursor phase was renumbered, updates the cursor to point to the new ID.
    /// </summary>
    public async Task UpdateCursorAfterRenumberingAsync(
        Dictionary<string, string> idMapping,
        CancellationToken ct = default)
    {
        var state = await ReadStateAsync(ct);
        if (state == null)
        {
            return;
        }

        var cursor = state.Cursor;
        if (cursor?.PhaseId == null)
        {
            return;
        }

        // Check if the cursor phase was renumbered
        if (!idMapping.TryGetValue(cursor.PhaseId, out var newPhaseId))
        {
            // Current phase ID not in mapping, try to find if it was a target
            var reverseMapping = idMapping.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            if (reverseMapping.TryGetValue(cursor.PhaseId, out var oldPhaseId))
            {
                // The cursor phase was a target of renumbering, update it
                newPhaseId = cursor.PhaseId;
            }
            else
            {
                // Phase not found in mapping, no change needed
                return;
            }
        }

        if (newPhaseId == cursor.PhaseId)
        {
            // No change needed
            return;
        }

        var updatedCursor = new StateCursor
        {
            PhaseId = newPhaseId,
            PhaseStatus = cursor.PhaseStatus,
            TaskId = cursor.TaskId,
            TaskStatus = cursor.TaskStatus,
            StepId = cursor.StepId,
            StepStatus = cursor.StepStatus,
            MilestoneId = cursor.MilestoneId,
            MilestoneStatus = cursor.MilestoneStatus
        };

        var updatedState = new StateSnapshot
        {
            SchemaVersion = state.SchemaVersion,
            Cursor = updatedCursor
        };

        await WriteStateAsync(updatedState, ct);
    }

    /// <summary>
    /// Validates that the cursor points to an existing phase.
    /// </summary>
    public async Task<bool> ValidateCursorAsync(List<PhaseSpec> phases, CancellationToken ct = default)
    {
        var state = await ReadStateAsync(ct);
        if (state == null)
        {
            return false;
        }

        var cursor = state.Cursor;
        if (cursor?.PhaseId == null)
        {
            return true; // No cursor set, considered valid
        }

        return phases.Any(p => p.PhaseId == cursor.PhaseId);
    }

    /// <summary>
    /// Repairs the cursor to point to the first available phase if it's invalid.
    /// </summary>
    public async Task RepairCursorAsync(List<PhaseSpec> phases, CancellationToken ct = default)
    {
        var isValid = await ValidateCursorAsync(phases, ct);
        if (isValid || !phases.Any())
        {
            return;
        }

        var state = await ReadStateAsync(ct);
        if (state == null)
        {
            return;
        }

        var firstPhase = phases.OrderBy(p => p.SequenceOrder).First();
        
        var updatedCursor = new StateCursor
        {
            PhaseId = firstPhase.PhaseId,
            PhaseStatus = "pending",
            TaskId = null,
            TaskStatus = null,
            StepId = null,
            StepStatus = null
        };

        var updatedState = new StateSnapshot
        {
            SchemaVersion = state.SchemaVersion,
            Cursor = updatedCursor
        };

        await WriteStateAsync(updatedState, ct);
    }
}
