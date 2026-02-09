namespace Gmsd.Agents.Execution.Brownfield.FileGraphBuilder;

/// <summary>
/// Defines the contract for the File Graph Builder.
/// Derives file dependency graph for relationship mapping.
/// </summary>
public interface IFileGraphBuilder
{
    /// <summary>
    /// Builds a file dependency graph from the repository source files.
    /// </summary>
    /// <param name="request">The build request containing repository path and options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The file graph result containing nodes and edges representing file relationships.</returns>
    Task<FileGraphResult> BuildAsync(FileGraphRequest request, CancellationToken ct = default);
}
