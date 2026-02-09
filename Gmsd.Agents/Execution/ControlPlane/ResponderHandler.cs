using Gmsd.Aos.Contracts.Commands;

namespace Gmsd.Agents.Execution.ControlPlane;

public sealed class ResponderHandler
{
    public Task<CommandRouteResult> HandleAsync(CommandRequest request, string runId, CancellationToken ct = default)
    {
        return Task.FromResult(CommandRouteResult.Success("This is a conversational response."));
    }
}
