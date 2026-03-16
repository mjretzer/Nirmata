using System.Diagnostics;
using System.Text;
using nirmata.Aos.Contracts.Tools;
using nirmata.Agents.Execution.ControlPlane.Tools.Contracts;

namespace nirmata.Agents.Execution.ControlPlane.Tools.Standard;

/// <summary>
/// Tool for running shell processes (e.g., tests, build commands) within the working directory.
/// </summary>
public sealed class ProcessRunnerTool : ITool
{
    public ToolDescriptor Descriptor => new()
    {
        Id = "standard.process_runner",
        Name = "process_runner",
        Description = "Executes a command in the shell within the working directory.",
        Parameters = new[]
        {
            new ToolParameter { Name = "command", Description = "The command to execute (e.g., 'dotnet test').", Type = "string", Required = true },
            new ToolParameter { Name = "arguments", Description = "Arguments for the command.", Type = "string", Required = false }
        }
    };

    public async Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
    {
        if (!request.Parameters.TryGetValue("command", out var commandObj) || commandObj is not string command)
        {
            return ToolResult.Failure("MissingCommand", "The 'command' parameter is required.");
        }

        request.Parameters.TryGetValue("arguments", out var argsObj);
        var arguments = argsObj as string ?? string.Empty;

        try
        {
            var workingDirectory = GetWorkingDirectory(request);
            if (string.IsNullOrEmpty(workingDirectory))
            {
                return ToolResult.Failure("MissingWorkingDirectory", "Working directory not found in context.");
            }

            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
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

            await process.WaitForExitAsync(cancellationToken);

            var result = new
            {
                exitCode = process.ExitCode,
                stdout = outputBuilder.ToString(),
                stderr = errorBuilder.ToString(),
                command = $"{command} {arguments}".Trim()
            };

            if (process.ExitCode == 0)
            {
                return ToolResult.Success(result);
            }
            else
            {
                return ToolResult.Failure("CommandFailed", $"Command exited with code {process.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            return ToolResult.Failure("ExecutionError", $"Failed to execute command: {ex.Message}");
        }
    }

    private static string? GetWorkingDirectory(ToolRequest request)
    {
        return request.Metadata.TryGetValue("workingDirectory", out var wd) ? wd : null;
    }
}
