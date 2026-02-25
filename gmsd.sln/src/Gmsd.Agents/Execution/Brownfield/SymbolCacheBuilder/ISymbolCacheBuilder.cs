namespace Gmsd.Agents.Execution.Brownfield.SymbolCacheBuilder;

/// <summary>
/// Defines the contract for the Symbol Cache Builder.
/// Extracts and indexes symbols from source files for fast lookup.
/// </summary>
public interface ISymbolCacheBuilder
{
    /// <summary>
    /// Builds a symbol cache from the repository source files.
    /// </summary>
    /// <param name="request">The build request containing repository path and options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The symbol cache result containing all extracted symbols.</returns>
    Task<SymbolCacheResult> BuildAsync(SymbolCacheRequest request, CancellationToken ct = default);
}
