using System.Diagnostics;
using System.Text;

namespace Gmsd.Agents.Execution.Execution.AtomicGitCommitter;

/// <summary>
/// Represents the result of executing a git command.
/// </summary>
public sealed class GitCommandResult
{
    /// <summary>
    /// The exit code from the git command (0 = success).
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Standard output from the git command.
    /// </summary>
    public string Stdout { get; init; } = string.Empty;

    /// <summary>
    /// Standard error from the git command.
    /// </summary>
    public string Stderr { get; init; } = string.Empty;

    /// <summary>
    /// Whether the command succeeded (exit code 0).
    /// </summary>
    public bool IsSuccess => ExitCode == 0;
}

/// <summary>
/// Executes git commands safely with stdout/stderr/exit code capture.
/// </summary>
internal sealed class GitCommandRunner
{
    private readonly string _workingDirectory;
    private readonly string _gitExecutable;

    /// <summary>
    /// Creates a new GitCommandRunner for the specified working directory.
    /// </summary>
    /// <param name="workingDirectory">The directory where git commands will be executed.</param>
    /// <param name="gitExecutable">Path to the git executable (defaults to "git").</param>
    public GitCommandRunner(string workingDirectory, string gitExecutable = "git")
    {
        _workingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
        _gitExecutable = gitExecutable ?? throw new ArgumentNullException(nameof(gitExecutable));
    }

    /// <summary>
    /// Executes a git command with the specified arguments.
    /// </summary>
    /// <param name="arguments">The command arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command result with exit code, stdout, and stderr.</returns>
    public async Task<GitCommandResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _gitExecutable,
            Arguments = arguments,
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdoutBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderrBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new GitCommandResult
        {
            ExitCode = process.ExitCode,
            Stdout = stdoutBuilder.ToString().TrimEnd(),
            Stderr = stderrBuilder.ToString().TrimEnd()
        };
    }

    /// <summary>
    /// Stages the specified files using 'git add'.
    /// </summary>
    /// <param name="files">The files to stage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command result.</returns>
    /// <exception cref="ArgumentException">Thrown when files list is empty.</exception>
    public async Task<GitCommandResult> StageFilesAsync(IEnumerable<string> files, CancellationToken cancellationToken = default)
    {
        var fileList = files?.ToList() ?? throw new ArgumentNullException(nameof(files));

        if (fileList.Count == 0)
        {
            return new GitCommandResult
            {
                ExitCode = 0,
                Stdout = "No files to stage",
                Stderr = string.Empty
            };
        }

        var escapedFiles = fileList.Select(EscapeFilePath);
        var arguments = $"add {string.Join(" ", escapedFiles)}";

        return await ExecuteAsync(arguments, cancellationToken);
    }

    /// <summary>
    /// Creates a commit with the specified message using 'git commit'.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command result.</returns>
    /// <exception cref="ArgumentException">Thrown when message is null or empty.</exception>
    public async Task<GitCommandResult> CommitAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Commit message cannot be null or empty", nameof(message));
        }

        var escapedMessage = EscapeCommitMessage(message);
        var arguments = $"commit -m \"{escapedMessage}\"";

        return await ExecuteAsync(arguments, cancellationToken);
    }

    /// <summary>
    /// Gets the short commit hash of HEAD.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The commit hash, or null if not available.</returns>
    public async Task<string?> GetHeadCommitHashAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync("rev-parse --short HEAD", cancellationToken);
        return result.IsSuccess ? result.Stdout.Trim() : null;
    }

    /// <summary>
    /// Gets diff statistics (--stat) for staged changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The diff stat output, or null if not available.</returns>
    public async Task<string?> GetStagedDiffStatAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync("diff --cached --stat", cancellationToken);
        return result.IsSuccess ? result.Stdout : null;
    }

    /// <summary>
    /// Checks if the working directory is a valid git repository.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the directory is a git repository, false otherwise.</returns>
    public async Task<bool> IsGitRepositoryAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync("rev-parse --git-dir", cancellationToken);
        return result.IsSuccess;
    }

    /// <summary>
    /// Escapes a file path for use in git command arguments.
    /// </summary>
    private static string EscapeFilePath(string path)
    {
        // Escape quotes and wrap in quotes for paths with spaces
        if (path.Contains(' ') || path.Contains('"'))
        {
            return "\"" + path.Replace("\"", "\\\"") + "\"";
        }
        return path;
    }

    /// <summary>
    /// Escapes a commit message for use in git command arguments.
    /// </summary>
    private static string EscapeCommitMessage(string message)
    {
        // Escape double quotes
        return message.Replace("\"", "\\\"");
    }
}
