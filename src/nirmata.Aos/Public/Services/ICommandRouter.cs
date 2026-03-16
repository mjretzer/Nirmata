using nirmata.Aos.Contracts.Commands;

namespace nirmata.Aos.Public.Services;

/// <summary>
/// Public command routing abstraction (compile-time contract).
/// </summary>
public interface ICommandRouter
{
    /// <summary>
    /// Routes a command request to the appropriate handler.
    /// </summary>
    /// <param name="request">The command request containing group, command, and arguments.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result of command routing including success/failure and output.</returns>
    Task<CommandRouteResult> RouteAsync(CommandRequest request, CancellationToken ct = default);
}
