using System.Diagnostics;
using System.Text.Json.Serialization;
using nirmata.Aos.Contracts.Tools;
using nirmata.Agents.Execution.ControlPlane.Tools.Contracts;

namespace nirmata.Agents.Execution.ControlPlane.Tools.Standard;

/// <summary>
/// Tool for executing dotnet test commands with TRX result parsing and structured output.
/// </summary>
public sealed class TestTool : ITool
{
    private const int DefaultTimeoutMinutes = 10;

    public ToolDescriptor Descriptor => new()
    {
        Id = "standard.run_test",
        Name = "run_test",
        Description = "Executes 'dotnet test' with TRX logger and parses results into structured data with a 10-minute timeout.",
        Parameters = new[]
        {
            new ToolParameter { Name = "projectPath", Description = "The path to the test project or solution", Type = "string", Required = true },
            new ToolParameter { Name = "configuration", Description = "Build configuration (Debug or Release)", Type = "string", Required = false }
        }
    };

    public async Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
    {
        if (!request.Parameters.TryGetValue("projectPath", out var projectPathObj) || projectPathObj is not string projectPath)
        {
            return ToolResult.Failure("MissingPath", "The 'projectPath' parameter is required and must be a string.");
        }

        try
        {
            var configuration = request.Parameters.TryGetValue("configuration", out var configObj) && configObj is string config
                ? config
                : "Debug";

            if (!File.Exists(projectPath) && !Directory.Exists(projectPath))
            {
                return ToolResult.Failure("FileNotFound", $"Project path not found: {projectPath}");
            }

            var startTime = DateTime.UtcNow;
            var tempDir = Path.Combine(Path.GetTempPath(), $"test-results-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{projectPath}\" -c {configuration} --logger:trx --results-directory \"{tempDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return ToolResult.Failure("ProcessError", "Failed to start dotnet test process");
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
                    return ToolResult.Failure("Timeout", $"Tests exceeded {DefaultTimeoutMinutes} minute timeout");
                }

                var output = await outputTask;
                var errors = await errorTask;
                var duration = DateTime.UtcNow - startTime;

                // Find and parse TRX file
                var trxFiles = Directory.GetFiles(tempDir, "*.trx");
                var testResults = new
                {
                    success = process.ExitCode == 0,
                    exitCode = process.ExitCode,
                    totalTests = 0,
                    passed = 0,
                    failed = 0,
                    failures = new List<object>(),
                    logs = output,
                    duration = duration.TotalSeconds,
                    configuration = configuration
                };

                if (trxFiles.Length > 0)
                {
                    var parser = new TrxResultParser();
                    var parsedResults = parser.ParseTrxFile(trxFiles[0]);
                    return ToolResult.Success(new
                    {
                        success = process.ExitCode == 0,
                        exitCode = process.ExitCode,
                        totalTests = parsedResults.TotalTests,
                        passed = parsedResults.Passed,
                        failed = parsedResults.Failed,
                        failures = parsedResults.Failures,
                        logs = output,
                        duration = duration.TotalSeconds,
                        configuration = configuration
                    });
                }

                return ToolResult.Success(testResults);
            }
            finally
            {
                linkedCts.Dispose();
                timeoutCts.Dispose();
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
        catch (Exception ex)
        {
            return ToolResult.Failure("Error", $"Test execution failed: {ex.Message}");
        }
    }
}
