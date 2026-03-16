using nirmata.Agents.Execution.Brownfield.CodebaseScanner;

namespace nirmata.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of ICodebaseScanner for testing.
/// Supports progress reporting and configurable scan results.
/// </summary>
public sealed class FakeCodebaseScanner : ICodebaseScanner
{
    private CodebaseScanResult? _nextResult;
    public bool ScanWasCalled { get; private set; }

    public void SetScanResult(CodebaseScanResult result)
    {
        _nextResult = result;
        ScanWasCalled = false;
    }

    public Task<CodebaseScanResult> ScanAsync(CodebaseScanRequest request, IProgress<CodebaseScanProgress>? progress = null, CancellationToken ct = default)
    {
        ScanWasCalled = true;

        // Report initial progress if callback provided
        progress?.Report(new CodebaseScanProgress
        {
            Phase = CodebaseScanPhase.DetectingRepository,
            StepNumber = 1,
            TotalSteps = 6,
            PercentComplete = 0,
            Message = "Starting scan...",
            ItemsProcessed = 0,
            TotalItems = 1
        });

        // Report completion progress
        progress?.Report(new CodebaseScanProgress
        {
            Phase = CodebaseScanPhase.Completed,
            StepNumber = 6,
            TotalSteps = 6,
            PercentComplete = 100,
            Message = "Scan completed",
            ItemsProcessed = 1,
            TotalItems = 1
        });

        return Task.FromResult(_nextResult ?? new CodebaseScanResult
        {
            IsSuccess = true,
            Solutions = new List<SolutionInfo>(),
            Projects = new List<ProjectInfo>(),
            RepositoryRoot = request.RepositoryPath ?? string.Empty,
            ScanTimestamp = DateTimeOffset.UtcNow
        });
    }
}
