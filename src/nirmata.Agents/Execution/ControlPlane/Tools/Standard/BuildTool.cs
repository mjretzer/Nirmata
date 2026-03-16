using System.Diagnostics;
using System.Text.Json.Serialization;
using nirmata.Aos.Contracts.Tools;
using nirmata.Agents.Execution.ControlPlane.Tools.Contracts;

namespace nirmata.Agents.Execution.ControlPlane.Tools.Standard;

/// <summary>
/// Tool for executing dotnet build commands with timeout and structured result capture.
/// </summary>
public sealed class BuildTool : ITool
{
    private const int DefaultTimeoutMinutes = 5;

    public ToolDescriptor Descriptor => new()
    {
        Id = "standard.run_build",
        Name = "run_build",
        Description = "Executes 'dotnet build' on the solution file with a 5-minute timeout and returns structured results.",
        Parameters = new[]
        {
            new ToolParameter { Name = "solutionPath", Description = "The path to the solution file (.sln)", Type = "string", Required = true },
            new ToolParameter { Name = "configuration", Description = "Build configuration (Debug or Release)", Type = "string", Required = false }
        }
    };

    public async Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
    {
        if (!request.Parameters.TryGetValue("solutionPath", out var solutionPathObj) || solutionPathObj is not string solutionPath)
        {
            return ToolResult.Failure("MissingPath", "The 'solutionPath' parameter is required and must be a string.");
        }

        try
        {
            var configuration = request.Parameters.TryGetValue("configuration", out var configObj) && configObj is string config
                ? config
                : "Debug";

            if (!File.Exists(solutionPath))
            {
                return ToolResult.Failure("FileNotFound", $"Solution file not found: {solutionPath}");
            }

            var startTime = DateTime.UtcNow;
            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{solutionPath}\" -c {configuration}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return ToolResult.Failure("ProcessError", "Failed to start dotnet build process");
            }

            var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(DefaultTimeoutMinutes));
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                var completedTask = await Task.WhenAny(
                    Task.Run(() => process.WaitForExit(), linkedCts.Token),
                    Task.Delay(Timeout.Infinite, linkedCts.Token));

                if (completedTask == Task.Delay(Timeout.Infinite, linkedCts.Token))
                {
                    process.Kill();
                    return ToolResult.Failure("Timeout", $"Build exceeded {DefaultTimeoutMinutes} minute timeout");
                }

                var output = await outputTask;
                var errors = await errorTask;
                var duration = DateTime.UtcNow - startTime;

                var result = new
                {
                    success = process.ExitCode == 0,
                    exitCode = process.ExitCode,
                    logs = output,
                    errors = errors,
                    duration = duration.TotalSeconds,
                    configuration = configuration
                };

                return ToolResult.Success(result);
            }
            finally
            {
                linkedCts.Dispose();
                timeoutCts.Dispose();
            }
        }
        catch (Exception ex)
        {
            return ToolResult.Failure("Error", $"Build execution failed: {ex.Message}");
        }
    }
}
