using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;
using System.Text.Json;

namespace nirmata.Agents.Execution.Planning.RoadmapModifier;

/// <summary>
/// Provides gating logic for roadmap modification commands.
/// Checks prerequisites and conditions before allowing modifications.
/// </summary>
public sealed class RoadmapModificationGate
{
    private readonly SpecStore _specStore;
    private readonly IEventStore _eventStore;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="RoadmapModificationGate"/> class.
    /// </summary>
    public RoadmapModificationGate(SpecStore specStore, IEventStore eventStore)
    {
        _specStore = specStore ?? throw new ArgumentNullException(nameof(specStore));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    }

    /// <summary>
    /// Checks if a modification operation is allowed.
    /// </summary>
    /// <param name="operation">The operation type.</param>
    /// <param name="phaseId">Optional phase ID for remove operations.</param>
    /// <param name="runId">The current run identifier.</param>
    /// <returns>A gate check result indicating if the operation is allowed.</returns>
    public async Task<GateCheckResult> CheckAsync(
        RoadmapModifyOperation operation,
        string? phaseId,
        string runId)
    {
        var checks = new List<GateCheck>();

        // Check 1: Roadmap exists
        var roadmapExists = CheckRoadmapExists();
        checks.Add(new GateCheck("RoadmapExists", roadmapExists, "Roadmap spec file not found"));

        // Check 2: State exists
        var stateExists = CheckStateExists();
        checks.Add(new GateCheck("StateExists", stateExists, "State file not found"));

        // Check 3: No active run in progress (simple check)
        var noActiveRun = await CheckNoActiveRunAsync(runId);
        checks.Add(new GateCheck("NoActiveRun", noActiveRun, "Another run is currently in progress"));

        // Check 4: For remove operations, check if phase exists
        if (operation == RoadmapModifyOperation.Remove && !string.IsNullOrEmpty(phaseId))
        {
            var phaseExists = CheckPhaseExists(phaseId);
            checks.Add(new GateCheck("PhaseExists", phaseExists, $"Phase {phaseId} not found"));
        }

        // Check 5: For insert operations, validate position is valid
        if (operation == RoadmapModifyOperation.Insert)
        {
            var canInsert = await CheckCanInsertAsync();
            checks.Add(new GateCheck("CanInsert", canInsert, "Cannot insert phase at this time"));
        }

        var failedChecks = checks.Where(c => !c.Passed).ToList();

        if (failedChecks.Count == 0)
        {
            return GateCheckResult.Allowed();
        }

        var reason = string.Join("; ", failedChecks.Select(c => c.FailureMessage));
        return GateCheckResult.Denied(reason);
    }

    /// <summary>
    /// Validates that the prerequisite conditions for any modification are met.
    /// </summary>
    public async Task<bool> ValidatePrerequisitesAsync(CancellationToken ct = default)
    {
        // Check that AOS workspace is initialized
        var aosRoot = GetAosRootPath();
        if (!Directory.Exists(Path.Combine(aosRoot, ".aos")))
        {
            return false;
        }

        // Check that spec directory exists
        if (!Directory.Exists(Path.Combine(aosRoot, ".aos/spec")))
        {
            return false;
        }

        // Check that state directory exists
        if (!Directory.Exists(Path.Combine(aosRoot, ".aos/state")))
        {
            return false;
        }

        // Check that roadmap file exists or can be created
        var roadmapPath = Path.Combine(aosRoot, ".aos/spec/roadmap.json");
        if (!File.Exists(roadmapPath))
        {
            // Allow creating new roadmap
            return true;
        }

        // Validate roadmap is readable
        try
        {
            _specStore.Inner.ReadRoadmap();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool CheckRoadmapExists()
    {
        try
        {
            _specStore.Inner.ReadRoadmap();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool CheckStateExists()
    {
        var statePath = Path.Combine(GetAosRootPath(), ".aos/state/state.json");
        return File.Exists(statePath);
    }

    private async Task<bool> CheckNoActiveRunAsync(string currentRunId)
    {
        // For simplicity, always allow if we have a valid runId
        // In a more complex implementation, this would check a run registry
        return !string.IsNullOrEmpty(currentRunId);
    }

    private bool CheckPhaseExists(string phaseId)
    {
        try
        {
            _specStore.Inner.ReadPhase(phaseId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckCanInsertAsync()
    {
        // Generally allow inserts unless at some maximum limit
        // For now, always allow
        return true;
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

/// <summary>
/// Represents the result of a gate check.
/// </summary>
public sealed class GateCheckResult
{
    /// <summary>
    /// Whether the operation is allowed.
    /// </summary>
    public bool IsAllowed { get; private init; }

    /// <summary>
    /// The reason if the operation was denied.
    /// </summary>
    public string? DenialReason { get; private init; }

    /// <summary>
    /// Creates an allowed result.
    /// </summary>
    public static GateCheckResult Allowed()
    {
        return new GateCheckResult { IsAllowed = true };
    }

    /// <summary>
    /// Creates a denied result with a reason.
    /// </summary>
    public static GateCheckResult Denied(string reason)
    {
        return new GateCheckResult { IsAllowed = false, DenialReason = reason };
    }
}

/// <summary>
/// Represents an individual gate check.
/// </summary>
public sealed class GateCheck
{
    /// <summary>
    /// The name of the check.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether the check passed.
    /// </summary>
    public bool Passed { get; }

    /// <summary>
    /// The failure message if the check did not pass.
    /// </summary>
    public string FailureMessage { get; }

    public GateCheck(string name, bool passed, string failureMessage)
    {
        Name = name;
        Passed = passed;
        FailureMessage = failureMessage;
    }
}
