namespace nirmata.Aos.Public;

/// <summary>
/// Resolves artifact IDs to canonical contract paths under .aos/.
/// </summary>
public interface IArtifactPathResolver
{
    /// <summary>
    /// Resolves a milestone ID to its canonical contract path.
    /// </summary>
    /// <param name="id">The milestone ID (e.g., "MS-0001").</param>
    /// <returns>The contract path (e.g., ".aos/spec/milestones/MS-0001/milestone.json").</returns>
    string ResolveMilestonePath(string id);

    /// <summary>
    /// Resolves a phase ID to its canonical contract path.
    /// </summary>
    /// <param name="id">The phase ID (e.g., "PH-0001").</param>
    /// <returns>The contract path (e.g., ".aos/spec/phases/PH-0001/phase.json").</returns>
    string ResolvePhasePath(string id);

    /// <summary>
    /// Resolves a task ID to its canonical contract path.
    /// </summary>
    /// <param name="id">The task ID (e.g., "TSK-000001").</param>
    /// <returns>The contract path (e.g., ".aos/spec/tasks/TSK-000001/task.json").</returns>
    string ResolveTaskPath(string id);

    /// <summary>
    /// Resolves an issue ID to its canonical contract path.
    /// </summary>
    /// <param name="id">The issue ID (e.g., "ISS-0001").</param>
    /// <returns>The contract path (e.g., ".aos/spec/issues/ISS-0001.json").</returns>
    string ResolveIssuePath(string id);

    /// <summary>
    /// Resolves a UAT ID to its canonical contract path.
    /// </summary>
    /// <param name="id">The UAT ID (e.g., "UAT-0001").</param>
    /// <returns>The contract path (e.g., ".aos/spec/uat/UAT-0001.json").</returns>
    string ResolveUatPath(string id);

    /// <summary>
    /// Resolves a context pack ID to its canonical contract path.
    /// </summary>
    /// <param name="id">The context pack ID (e.g., "PCK-0001").</param>
    /// <returns>The contract path (e.g., ".aos/context/packs/PCK-0001.json").</returns>
    string ResolveContextPackPath(string id);

    /// <summary>
    /// Resolves a run ID to its evidence root contract path.
    /// </summary>
    /// <param name="id">The run ID (32-character hex format).</param>
    /// <returns>The contract path (e.g., ".aos/evidence/runs/<run-id>/").</returns>
    string ResolveRunPath(string id);

    /// <summary>
    /// Gets the workspace lock file path.
    /// </summary>
    /// <returns>The contract path ".aos/locks/workspace.lock.json".</returns>
    string GetWorkspaceLockPath();

    /// <summary>
    /// Gets the state file path.
    /// </summary>
    /// <returns>The contract path ".aos/state/state.json".</returns>
    string GetStatePath();

    /// <summary>
    /// Gets the events file path.
    /// </summary>
    /// <returns>The contract path ".aos/state/events.ndjson".</returns>
    string GetEventsPath();

    /// <summary>
    /// Gets the run index file path.
    /// </summary>
    /// <returns>The contract path ".aos/evidence/runs/index.json".</returns>
    string GetRunIndexPath();
}
