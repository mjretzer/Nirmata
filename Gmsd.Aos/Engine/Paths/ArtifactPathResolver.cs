using Gmsd.Aos.Public;

namespace Gmsd.Aos.Engine.Paths;

/// <summary>
/// Resolves artifact IDs to canonical contract paths under .aos/.
/// </summary>
public sealed class ArtifactPathResolver : IArtifactPathResolver
{
    /// <inheritdoc />
    public string ResolveMilestonePath(string id)
    {
        ValidateId(id, "MS", 4);
        return $".aos/spec/milestones/{id}/milestone.json";
    }

    /// <inheritdoc />
    public string ResolvePhasePath(string id)
    {
        ValidateId(id, "PH", 4);
        return $".aos/spec/phases/{id}/phase.json";
    }

    /// <inheritdoc />
    public string ResolveTaskPath(string id)
    {
        ValidateId(id, "TSK", 6);
        return $".aos/spec/tasks/{id}/task.json";
    }

    /// <inheritdoc />
    public string ResolveIssuePath(string id)
    {
        ValidateId(id, "ISS", 4);
        return $".aos/spec/issues/{id}.json";
    }

    /// <inheritdoc />
    public string ResolveUatPath(string id)
    {
        ValidateId(id, "UAT", 4);
        return $".aos/spec/uat/{id}.json";
    }

    /// <inheritdoc />
    public string ResolveContextPackPath(string id)
    {
        ValidateId(id, "PCK", 4);
        return $".aos/context/packs/{id}.json";
    }

    /// <inheritdoc />
    public string ResolveRunPath(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length != 32)
        {
            throw new ArgumentException("Run ID must be 32 characters.", nameof(id));
        }

        foreach (var c in id)
        {
            if (!char.IsAsciiHexDigit(c) || char.IsUpper(c))
            {
                throw new ArgumentException("Run ID must be 32 lower-case hex characters.", nameof(id));
            }
        }

        return $".aos/evidence/runs/{id}/";
    }

    /// <inheritdoc />
    public string GetWorkspaceLockPath() => ".aos/locks/workspace.lock.json";

    /// <inheritdoc />
    public string GetStatePath() => ".aos/state/state.json";

    /// <inheritdoc />
    public string GetEventsPath() => ".aos/state/events.ndjson";

    /// <inheritdoc />
    public string GetRunIndexPath() => ".aos/evidence/runs/index.json";

    private static void ValidateId(string id, string prefix, int digitCount)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentException("ID cannot be null or empty.", nameof(id));
        }

        var expectedPrefix = prefix + "-";
        if (!id.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException($"ID must start with '{expectedPrefix}'.", nameof(id));
        }

        var digits = id[expectedPrefix.Length..];
        if (digits.Length != digitCount)
        {
            throw new ArgumentException($"ID must have {digitCount} digits after the prefix.", nameof(id));
        }

        foreach (var c in digits)
        {
            if (!char.IsDigit(c))
            {
                throw new ArgumentException($"ID must contain only digits after the prefix.", nameof(id));
            }
        }
    }
}
