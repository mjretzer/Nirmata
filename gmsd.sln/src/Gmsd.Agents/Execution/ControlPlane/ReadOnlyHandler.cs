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
                response = GetHelpText();
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

    private static string GetHelpText()
    {
        return """
Available Commands:
===================

Workflow Commands (Write Operations):
  /run              Execute the current task or workflow
  /plan             Create or update a plan for the current phase
  /verify           Verify the current task execution
  /fix              Create a fix plan for identified issues
  /pause            Pause the current workflow execution
  /resume           Resume a paused workflow execution

Query Commands (Read-Only):
  /status           Check the current workflow status
  /help             Show this help message

Chat Mode:
  Any text without a / prefix is treated as freeform chat.
  Examples: "Hello", "What can you do?", "Explain the code"

Keyboard Shortcuts:
===================
  / or Cmd+K        Focus the chat input
  Esc               Collapse panels, clear selection
  Cmd+[             Navigate back in browser history
  Cmd+]             Navigate forward in browser history
  Cmd+1             Toggle sidebar panel
  Cmd+2             Toggle detail panel
  Cmd+3             Toggle both panels
  Cmd+?             Show/hide keyboard shortcuts help
  Arrow Keys        Navigate lists (when focused)
  Enter             Select list item
  Shift+Enter       New line in chat input

Notes:
  - Commands are case-insensitive
  - Commands must start with / (forward slash)
  - Freeform text no longer triggers workflow actions
  - Write operations may require confirmation if confidence is low
""";
    }
}
