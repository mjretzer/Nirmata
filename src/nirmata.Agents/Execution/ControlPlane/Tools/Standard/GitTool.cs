using System.Diagnostics;
using System.Text;
using nirmata.Aos.Contracts.Tools;
using nirmata.Agents.Execution.ControlPlane.Tools.Contracts;

namespace nirmata.Agents.Execution.ControlPlane.Tools.Standard;

/// <summary>
/// Tool for performing Git operations (status, commit) within the working directory.
/// </summary>
public sealed class GitTool : ITool
{
    public ToolDescriptor Descriptor => new()
    {
        Id = "standard.git",
        Name = "git",
        Description = "Performs git operations like status and commit within the working directory.",
        Parameters = new[]
        {
            new ToolParameter { Name = "operation", Description = "The git operation to perform.", Type = "string", Required = true, EnumValues = new[] { "status", "commit" } },
            new ToolParameter { Name = "message", Description = "The commit message (required for 'commit' operation).", Type = "string", Required = false }
        }
    };

    public async Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
    {
        if (!request.Parameters.TryGetValue("operation", out var opObj) || opObj is not string operation)
        {
            return ToolResult.Failure("MissingOperation", "The 'operation' parameter is required.");
        }

        try
        {
            var workingDirectory = GetWorkingDirectory(request);
            if (string.IsNullOrEmpty(workingDirectory))
            {
                return ToolResult.Failure("MissingWorkingDirectory", "Working directory not found in context.");
            }

            return operation.ToLowerInvariant() switch
            {
                "status" => await RunGitCommandAsync(workingDirectory, "status --porcelain", cancellationToken),
                "commit" => await HandleCommitAsync(request, workingDirectory, cancellationToken),
                _ => ToolResult.Failure("InvalidOperation", $"Unsupported git operation: {operation}")
            };
        }
        catch (Exception ex)
        {
            return ToolResult.Failure("ExecutionError", $"Failed to execute git command: {ex.Message}");
        }
    }

    private async Task<ToolResult> HandleCommitAsync(ToolRequest request, string workingDirectory, CancellationToken ct)
    {
        if (!request.Parameters.TryGetValue("message", out var msgObj) || msgObj is not string message || string.IsNullOrWhiteSpace(message))
        {
            return ToolResult.Failure("MissingMessage", "A commit message is required for the 'commit' operation.");
        }

        // First, add all changes (standard behavior for subagents usually)
        var addResult = await RunGitCommandRawAsync(workingDirectory, "add .", ct);
        if (addResult.ExitCode != 0)
        {
            return ToolResult.Failure("AddFailed", "Failed to stage changes for commit.");
        }

        // Then commit
        // Escape double quotes in message
        var escapedMessage = message.Replace("\"", "\\\"");
        return await RunGitCommandAsync(workingDirectory, $"commit -m \"{escapedMessage}\"", ct);
    }

    private async Task<ToolResult> RunGitCommandAsync(string workingDirectory, string args, CancellationToken ct)
    {
        var result = await RunGitCommandRawAsync(workingDirectory, args, ct);
        if (result.ExitCode == 0)
        {
            return ToolResult.Success(result);
        }
        return ToolResult.Failure("GitCommandFailed", $"Git command failed with exit code {result.ExitCode}");
    }

    private async Task<dynamic> RunGitCommandRawAsync(string workingDirectory, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        return new
        {
            exitCode = process.ExitCode,
            stdout = outputBuilder.ToString().Trim(),
            stderr = errorBuilder.ToString().Trim(),
            command = $"git {args}"
        };
    }

    private static string? GetWorkingDirectory(ToolRequest request)
    {
        return request.Metadata.TryGetValue("workingDirectory", out var wd) ? wd : null;
    }
}
