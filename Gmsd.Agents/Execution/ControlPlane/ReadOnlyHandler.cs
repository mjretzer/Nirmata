using Gmsd.Agents.Execution.Preflight;

namespace Gmsd.Agents.Execution.ControlPlane;

public sealed class ReadOnlyHandler
{
    public Task<OrchestratorResult> HandleAsync(Intent intent)
    {
        string response;
        switch (intent.Kind)
        {
            case IntentKind.Help:
                response = "This is the help response.";
                break;
            case IntentKind.Status:
                response = "This is the status response.";
                break;
            default:
                response = "I'm not sure how to handle that read-only command.";
                break;
        }

        var result = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "ReadOnly",
            Artifacts = new Dictionary<string, object> { ["response"] = response }
        };

        return Task.FromResult(result);
    }
}
