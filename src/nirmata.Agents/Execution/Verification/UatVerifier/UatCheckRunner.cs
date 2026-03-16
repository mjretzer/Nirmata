using System.Diagnostics;
using System.Text.RegularExpressions;

namespace nirmata.Agents.Execution.Verification.UatVerifier;

/// <summary>
/// Implementation of the UAT check runner that evaluates acceptance criteria.
/// </summary>
public sealed class UatCheckRunner : IUatCheckRunner
{
    /// <inheritdoc />
    public async Task<UatCheckResult> RunCheckAsync(AcceptanceCriterion criterion, string workspacePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criterion);
        ArgumentNullException.ThrowIfNull(workspacePath);

        return criterion.CheckType.ToLowerInvariant() switch
        {
            UatCheckTypes.FileExists => await RunFileExistsCheckAsync(criterion, workspacePath, ct),
            UatCheckTypes.ContentContains => await RunContentContainsCheckAsync(criterion, workspacePath, ct),
            UatCheckTypes.BuildSucceeds => await RunBuildSucceedsCheckAsync(criterion, workspacePath, ct),
            UatCheckTypes.TestPasses => await RunTestPassesCheckAsync(criterion, workspacePath, ct),
            _ => CreateUnsupportedResult(criterion)
        };
    }

    private static Task<UatCheckResult> RunFileExistsCheckAsync(AcceptanceCriterion criterion, string workspacePath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(criterion.TargetPath))
        {
            return Task.FromResult(new UatCheckResult
            {
                CriterionId = criterion.Id,
                Passed = false,
                Message = "File-exists check failed: TargetPath is required",
                CheckType = criterion.CheckType,
                TargetPath = criterion.TargetPath,
                IsRequired = criterion.IsRequired
            });
        }

        var fullPath = Path.Combine(workspacePath, criterion.TargetPath);
        var exists = File.Exists(fullPath) || Directory.Exists(fullPath);

        return Task.FromResult(new UatCheckResult
        {
            CriterionId = criterion.Id,
            Passed = exists,
            Message = exists
                ? $"File exists: {criterion.TargetPath}"
                : $"File not found: {criterion.TargetPath}",
            CheckType = criterion.CheckType,
            TargetPath = criterion.TargetPath,
            Expected = "file exists",
            Actual = exists ? "file exists" : "file not found",
            IsRequired = criterion.IsRequired
        });
    }

    private static async Task<UatCheckResult> RunContentContainsCheckAsync(AcceptanceCriterion criterion, string workspacePath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(criterion.TargetPath) || string.IsNullOrEmpty(criterion.ExpectedContent))
        {
            return new UatCheckResult
            {
                CriterionId = criterion.Id,
                Passed = false,
                Message = "Content-contains check failed: TargetPath and ExpectedContent are required",
                CheckType = criterion.CheckType,
                TargetPath = criterion.TargetPath,
                IsRequired = criterion.IsRequired
            };
        }

        var fullPath = Path.Combine(workspacePath, criterion.TargetPath);

        if (!File.Exists(fullPath))
        {
            return new UatCheckResult
            {
                CriterionId = criterion.Id,
                Passed = false,
                Message = $"Content-contains check failed: File not found: {criterion.TargetPath}",
                CheckType = criterion.CheckType,
                TargetPath = criterion.TargetPath,
                Expected = criterion.ExpectedContent,
                Actual = "file not found",
                IsRequired = criterion.IsRequired
            };
        }

        try
        {
            var content = await File.ReadAllTextAsync(fullPath, ct);
            var contains = content.Contains(criterion.ExpectedContent, StringComparison.OrdinalIgnoreCase);

            return new UatCheckResult
            {
                CriterionId = criterion.Id,
                Passed = contains,
                Message = contains
                    ? $"Content found in {criterion.TargetPath}"
                    : $"Content not found in {criterion.TargetPath}",
                CheckType = criterion.CheckType,
                TargetPath = criterion.TargetPath,
                Expected = criterion.ExpectedContent,
                Actual = contains ? "content found" : "content not found",
                IsRequired = criterion.IsRequired
            };
        }
        catch (Exception ex)
        {
            return new UatCheckResult
            {
                CriterionId = criterion.Id,
                Passed = false,
                Message = $"Content-contains check error: {ex.Message}",
                CheckType = criterion.CheckType,
                TargetPath = criterion.TargetPath,
                Expected = criterion.ExpectedContent,
                Actual = $"error: {ex.Message}",
                IsRequired = criterion.IsRequired
            };
        }
    }

    private static async Task<UatCheckResult> RunBuildSucceedsCheckAsync(AcceptanceCriterion criterion, string workspacePath, CancellationToken ct)
    {
        try
        {
            // Look for solution or project files
            var solutionFile = Directory.GetFiles(workspacePath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            var projectFiles = Directory.GetFiles(workspacePath, "*.csproj", SearchOption.AllDirectories);

            if (solutionFile == null && projectFiles.Length == 0)
            {
                return new UatCheckResult
                {
                    CriterionId = criterion.Id,
                    Passed = false,
                    Message = "Build check failed: No solution or project files found",
                    CheckType = criterion.CheckType,
                    Expected = "successful build",
                    Actual = "no buildable project found",
                    IsRequired = criterion.IsRequired
                };
            }

            // Use dotnet build
            var target = solutionFile ?? projectFiles.First();
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{target}\" --verbosity quiet",
                WorkingDirectory = workspacePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            var succeeded = process.ExitCode == 0;

            return new UatCheckResult
            {
                CriterionId = criterion.Id,
                Passed = succeeded,
                Message = succeeded
                    ? $"Build succeeded: {Path.GetFileName(target)}"
                    : $"Build failed: {Path.GetFileName(target)}",
                CheckType = criterion.CheckType,
                TargetPath = target,
                Expected = "build succeeds (exit code 0)",
                Actual = succeeded
                    ? $"build succeeded (exit code {process.ExitCode})"
                    : $"build failed (exit code {process.ExitCode})",
                IsRequired = criterion.IsRequired
            };
        }
        catch (Exception ex)
        {
            return new UatCheckResult
            {
                CriterionId = criterion.Id,
                Passed = false,
                Message = $"Build check error: {ex.Message}",
                CheckType = criterion.CheckType,
                Expected = "successful build",
                Actual = $"error: {ex.Message}",
                IsRequired = criterion.IsRequired
            };
        }
    }

    private static async Task<UatCheckResult> RunTestPassesCheckAsync(AcceptanceCriterion criterion, string workspacePath, CancellationToken ct)
    {
        try
        {
            // Look for test project files
            var testProjects = Directory.GetFiles(workspacePath, "*Tests*.csproj", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(workspacePath, "*Test*.csproj", SearchOption.AllDirectories))
                .Distinct()
                .ToList();

            if (testProjects.Count == 0)
            {
                return new UatCheckResult
                {
                    CriterionId = criterion.Id,
                    Passed = false,
                    Message = "Test check failed: No test projects found",
                    CheckType = criterion.CheckType,
                    Expected = "tests pass",
                    Actual = "no test projects found",
                    IsRequired = criterion.IsRequired
                };
            }

            // Use dotnet test with optional filter
            var targetProject = testProjects.First();
            var filter = !string.IsNullOrEmpty(criterion.TargetPath)
                ? $"--filter \"FullyQualifiedName~{criterion.TargetPath}\""
                : "";

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{targetProject}\" {filter} --verbosity quiet --no-build",
                WorkingDirectory = workspacePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            var passed = process.ExitCode == 0;

            return new UatCheckResult
            {
                CriterionId = criterion.Id,
                Passed = passed,
                Message = passed
                    ? "Tests passed"
                    : "Tests failed",
                CheckType = criterion.CheckType,
                TargetPath = criterion.TargetPath,
                Expected = "tests pass (exit code 0)",
                Actual = passed
                    ? $"tests passed (exit code {process.ExitCode})"
                    : $"tests failed (exit code {process.ExitCode})",
                IsRequired = criterion.IsRequired
            };
        }
        catch (Exception ex)
        {
            return new UatCheckResult
            {
                CriterionId = criterion.Id,
                Passed = false,
                Message = $"Test check error: {ex.Message}",
                CheckType = criterion.CheckType,
                Expected = "tests pass",
                Actual = $"error: {ex.Message}",
                IsRequired = criterion.IsRequired
            };
        }
    }

    private static UatCheckResult CreateUnsupportedResult(AcceptanceCriterion criterion)
    {
        return new UatCheckResult
        {
            CriterionId = criterion.Id,
            Passed = false,
            Message = $"Unsupported check type: {criterion.CheckType}",
            CheckType = criterion.CheckType,
            TargetPath = criterion.TargetPath,
            Expected = "valid check",
            Actual = "unsupported check type",
            IsRequired = criterion.IsRequired
        };
    }
}
