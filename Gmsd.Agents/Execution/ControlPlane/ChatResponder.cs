namespace Gmsd.Agents.Execution.ControlPlane;

public sealed class ChatResponder
{
    public OrchestratorResult Respond(string input)
    {
        return new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Chat",
            Artifacts = new Dictionary<string, object>
            {
                ["response"] = "Hey — what do you want to work on?"
            }
        };
    }
}
