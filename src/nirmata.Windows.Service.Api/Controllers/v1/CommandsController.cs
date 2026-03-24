using Microsoft.AspNetCore.Mvc;
using nirmata.Windows.Service.Api;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class CommandRequest
{
    /// <summary>
    /// Argv array for the command to execute (e.g. ["aos", "status"] or ["plan-phase", "PH-0001"]).
    /// </summary>
    public required string[] Argv { get; init; }

    /// <summary>
    /// Optional explicit working directory for command execution.
    /// </summary>
    public string? WorkingDirectory { get; init; }
}

public sealed class CommandResponse
{
    public bool Ok { get; init; }
    public string Output { get; init; } = string.Empty;
}

[ApiController]
[Route("api/v1/commands")]
public class CommandsController(DaemonRuntimeState state, DaemonCommandExecutor executor) : ControllerBase
{
    /// <summary>
    /// Executes an AOS CLI command through the daemon backend and returns the captured output.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExecuteCommand([FromBody] CommandRequest request)
    {
        if (request.Argv is not { Length: > 0 })
            return BadRequest(new CommandResponse { Ok = false, Output = "argv must be a non-empty array." });

        var isAosInit = request.Argv.Length >= 2
                        && string.Equals(request.Argv[0], "aos", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(request.Argv[1], "init", StringComparison.OrdinalIgnoreCase);

        if (isAosInit)
        {
            var initRoot = !string.IsNullOrWhiteSpace(request.WorkingDirectory)
                ? request.WorkingDirectory
                : state.HostProfile?.WorkspacePath;

            if (!string.IsNullOrWhiteSpace(initRoot) && !request.Argv.Contains("--root", StringComparer.OrdinalIgnoreCase))
            {
                var initArgs = request.Argv.Concat(new[] { "--root", initRoot }).ToArray();
                var initResult = await executor.RunAsync(initArgs, initRoot);
                return Ok(new CommandResponse { Ok = initResult.Ok, Output = initResult.Output });
            }
        }

        var workingDir = !string.IsNullOrWhiteSpace(request.WorkingDirectory)
            ? request.WorkingDirectory
            : state.HostProfile?.WorkspacePath;
        var result = await executor.RunAsync(request.Argv, workingDir);
        return Ok(new CommandResponse { Ok = result.Ok, Output = result.Output });
    }
}
