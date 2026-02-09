using Gmsd.Agents.Execution.Brownfield.SymbolCacheBuilder;

namespace Gmsd.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of ISymbolCacheBuilder for testing.
/// </summary>
public sealed class FakeSymbolCacheBuilder : ISymbolCacheBuilder
{
    private SymbolCacheResult? _nextResult;
    public bool BuildCacheWasCalled { get; private set; }

    public void SetCacheResult(SymbolCacheResult result)
    {
        _nextResult = result;
        BuildCacheWasCalled = false;
    }

    public Task<SymbolCacheResult> BuildAsync(SymbolCacheRequest request, CancellationToken ct = default)
    {
        BuildCacheWasCalled = true;
        return Task.FromResult(_nextResult ?? new SymbolCacheResult
        {
            IsSuccess = true,
            Symbols = Array.Empty<SymbolInfo>(),
            RepositoryRoot = request.RepositoryPath,
            BuildTimestamp = DateTimeOffset.UtcNow
        });
    }
}
