using nirmata.Aos.Context.Packs;
using nirmata.Aos.Public.Context.Packs;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.Context;

/// <summary>
/// Manages the creation and persistence of context packs.
/// </summary>
public sealed class ContextPackManager : IContextPackManager
{
    private readonly IWorkspace _workspace;

    public ContextPackManager(IWorkspace workspace)
    {
        _workspace = workspace;
    }

    /// <inheritdoc />
    public Task<string> CreatePackAsync(string mode, string drivingId, ContextPackBudget budget, CancellationToken ct = default)
    {
        var (packId, _, _, _) = AosContextPackWriter.BuildAndWriteNewPack(
            _workspace.AosRootPath,
            mode,
            drivingId,
            budget);

        return Task.FromResult(packId);
    }
}
