namespace nirmata.Agents.Execution.Brownfield.CodebaseScanner;

/// <summary>
/// Defines the contract for the Codebase Scanner workflow.
/// Scans repository structure and builds codebase intelligence.
/// </summary>
public interface ICodebaseScanner
{
    /// <summary>
    /// Scans the repository and produces a complete codebase intelligence pack.
    /// </summary>
    /// <param name="request">The scan request containing repository path and options.</param>
    /// <param name="progress">Optional progress reporter for long-running operations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The scan result with repository structure and metadata.</returns>
    Task<CodebaseScanResult> ScanAsync(CodebaseScanRequest request, IProgress<CodebaseScanProgress>? progress = null, CancellationToken ct = default);
}
