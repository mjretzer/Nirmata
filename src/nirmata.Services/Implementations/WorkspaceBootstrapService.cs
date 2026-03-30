using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

public sealed class WorkspaceBootstrapService : IWorkspaceBootstrapService
{
    private static readonly string[] AosSubdirectories =
    [
        "spec", "state", "evidence", "codebase", "context", "cache", "schemas"
    ];

    private readonly ILogger<WorkspaceBootstrapService> _logger;

    public WorkspaceBootstrapService(ILogger<WorkspaceBootstrapService> logger)
    {
        _logger = logger;
    }

    public async Task<WorkspaceBootstrapResult> BootstrapAsync(string path, CancellationToken cancellationToken = default, string? remoteUrl = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Failure(BootstrapFailureKind.InvalidPath, "Workspace path is required.");

        if (!Path.IsPathRooted(path))
            return Failure(BootstrapFailureKind.InvalidPath, "Workspace path must be an absolute path.");

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Failure(BootstrapFailureKind.InvalidPath, $"Workspace path is not valid: {ex.Message}");
        }

        if (!Directory.Exists(normalizedPath))
            return Failure(BootstrapFailureKind.DirectoryNotFound, $"Workspace directory does not exist: {normalizedPath}");

        // Step 1: Ensure git repository is present
        bool gitCreated;
        try
        {
            gitCreated = await EnsureGitRepositoryAsync(normalizedPath, cancellationToken);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            _logger.LogError(ex, "Git executable not found for workspace root '{Path}'", normalizedPath);
            return Failure(BootstrapFailureKind.GitNotFound,
                "Git was not found on PATH. Please install Git and ensure it is accessible.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Git initialization failed for workspace root '{Path}'", normalizedPath);
            return Failure(BootstrapFailureKind.GitCommandFailed, $"Git initialization failed: {ex.Message}");
        }

        // Step 2: Seed missing AOS scaffold directories
        bool aosCreated;
        try
        {
            aosCreated = EnsureAosScaffold(normalizedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AOS scaffold creation failed for workspace root '{Path}'", normalizedPath);
            return Failure(BootstrapFailureKind.FileSystemError, $"AOS scaffold creation failed: {ex.Message}");
        }

        var originConfigured = false;
        if (!string.IsNullOrWhiteSpace(remoteUrl))
        {
            try
            {
                originConfigured = await EnsureOriginRemoteAsync(normalizedPath, remoteUrl, cancellationToken);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
            {
                _logger.LogError(ex, "Git executable not found while configuring origin for workspace root '{Path}'", normalizedPath);
                return Failure(BootstrapFailureKind.GitNotFound,
                    "Git was not found on PATH. Please install Git and ensure it is accessible.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Git origin configuration failed for workspace root '{Path}'", normalizedPath);
                return Failure(BootstrapFailureKind.GitCommandFailed, $"Git origin configuration failed: {ex.Message}");
            }
        }

        _logger.LogInformation(
            "Workspace bootstrap complete for '{Path}': git {GitAction}, AOS scaffold {AosAction}, origin {OriginAction}",
            normalizedPath,
            gitCreated ? "initialized" : "already present",
            aosCreated ? "created" : "already present",
            originConfigured ? "configured" : (string.IsNullOrWhiteSpace(remoteUrl) ? "not requested" : "already present"));

        return new WorkspaceBootstrapResult
        {
            Success = true,
            GitRepositoryCreated = gitCreated,
            AosScaffoldCreated = aosCreated,
            OriginConfigured = originConfigured,
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures a git repository exists at <paramref name="path"/>.
    /// Returns <c>true</c> if a new repository was initialized, <c>false</c> if one already existed.
    /// Throws <see cref="InvalidOperationException"/> when <c>git init</c> fails.
    /// </summary>
    private async Task<bool> EnsureGitRepositoryAsync(string path, CancellationToken cancellationToken)
    {
        var gitDir = Path.Combine(path, ".git");
        if (Directory.Exists(gitDir))
        {
            _logger.LogDebug("Git repository already exists at '{Path}'", path);
            return false;
        }

        _logger.LogInformation("Running 'git init' at '{Path}'", path);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "init",
                WorkingDirectory = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();

        // Read stdout/stderr concurrently before waiting to avoid deadlocks on full output buffers.
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stderr = await stderrTask;
        _ = await stdoutTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'git init' exited with code {process.ExitCode}. {stderr}".TrimEnd());
        }

        return true;
    }

    /// <summary>
    /// Ensures the repository has an <c>origin</c> remote configured for the provided URL.
    /// Preserves existing history and updates the remote URL in-place when one already exists.
    /// </summary>
    private async Task<bool> EnsureOriginRemoteAsync(string path, string remoteUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
            return false;

        var getResult = await RunGitAsync(path, cancellationToken, "remote", "get-url", "origin");
        if (getResult.ExitCode == 0)
        {
            if (string.Equals(getResult.Stdout.Trim(), remoteUrl.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            var setResult = await RunGitAsync(path, cancellationToken, "remote", "set-url", "origin", remoteUrl);
            if (setResult.ExitCode != 0)
                throw new InvalidOperationException($"'git remote set-url origin' exited with code {setResult.ExitCode}. {setResult.Stderr}".TrimEnd());

            return true;
        }

        var addResult = await RunGitAsync(path, cancellationToken, "remote", "add", "origin", remoteUrl);
        if (addResult.ExitCode != 0)
            throw new InvalidOperationException($"'git remote add origin' exited with code {addResult.ExitCode}. {addResult.Stderr}".TrimEnd());

        return true;
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunGitAsync(
        string path,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = string.Join(' ', arguments.Select(QuoteGitArgument)),
                WorkingDirectory = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stderr = await stderrTask;
        var stdout = await stdoutTask;

        return (process.ExitCode, stdout, stderr);
    }

    private static string QuoteGitArgument(string value)
        => value.Any(char.IsWhiteSpace) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;

    /// <summary>
    /// Ensures the <c>.aos/</c> scaffold exists under <paramref name="path"/>,
    /// creating any missing directories without overwriting existing ones.
    /// Returns <c>true</c> if any directory was created, <c>false</c> if all already existed.
    /// </summary>
    private bool EnsureAosScaffold(string path)
    {
        var aosRoot = Path.Combine(path, ".aos");
        var anyCreated = false;

        if (!Directory.Exists(aosRoot))
        {
            Directory.CreateDirectory(aosRoot);
            anyCreated = true;
        }

        foreach (var subdirectory in AosSubdirectories)
        {
            var subdirectoryPath = Path.Combine(aosRoot, subdirectory);
            if (!Directory.Exists(subdirectoryPath))
            {
                Directory.CreateDirectory(subdirectoryPath);
                anyCreated = true;
            }
        }

        return anyCreated;
    }

    private static WorkspaceBootstrapResult Failure(BootstrapFailureKind kind, string error) => new()
    {
        Success = false,
        FailureKind = kind,
        Error = error,
    };
}
