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
    private readonly IDeterministicJsonSerializer _jsonSerializer;

    public ContextPackManager(IWorkspace workspace, IDeterministicJsonSerializer jsonSerializer)
    {
        _workspace = workspace;
        _jsonSerializer = jsonSerializer;
    }

    /// <inheritdoc />
    public Task<string> CreatePackAsync(string mode, string drivingId, ContextPackBudget budget, CancellationToken ct = default)
    {
        var packId = $"PACK-{System.Guid.NewGuid():N}";
        var packDocument = AosContextPackBuilder.Build(_workspace.AosRootPath, packId, mode, drivingId, budget);

        var packPath = System.IO.Path.Combine(_workspace.AosRootPath, ".aos", "context", "packs", $"{packId}.json");
        var packDir = System.IO.Path.GetDirectoryName(packPath);
        if (packDir != null)
        {
            System.IO.Directory.CreateDirectory(packDir);
        }

        _jsonSerializer.WriteAtomic(packPath, packDocument, DeterministicJsonOptions.Standard, writeIndented: true);

        return Task.FromResult(packId);
    }
}
