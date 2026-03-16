using System.Text;

namespace nirmata.Agents.Execution.ControlPlane.Chat;

/// <summary>
/// Builds system and user prompts for chat responses.
/// </summary>
public sealed class ChatPromptBuilder
{
    /// <summary>
    /// Builds a complete prompt from context and user input.
    /// </summary>
    public Prompt Build(string userInput, ChatContext context)
    {
        var systemPrompt = BuildSystemPrompt(context);
        var userPrompt = BuildUserPrompt(userInput);

        return new Prompt(systemPrompt, userPrompt);
    }

    private string BuildSystemPrompt(ChatContext context)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are nirmata Assistant, an AI orchestration agent helping users with software project planning and execution.");
        sb.AppendLine();
        sb.AppendLine("CURRENT WORKSPACE STATE:");

        // Project info
        if (context.Project != null)
        {
            sb.AppendLine($"- Project: {context.Project.Name}");
            if (!string.IsNullOrWhiteSpace(context.Project.Description))
            {
                sb.AppendLine($"  Description: {context.Project.Description}");
            }
        }
        else
        {
            sb.AppendLine("- Project: Not defined");
        }

        // Roadmap info
        if (context.Roadmap != null)
        {
            sb.AppendLine($"- Roadmap: {context.Roadmap.PhaseCount} phases");
            if (context.Roadmap.Phases.Count > 0)
            {
                sb.AppendLine($"  Phases: {string.Join(", ", context.Roadmap.Phases)}");
            }
            if (!string.IsNullOrWhiteSpace(context.Roadmap.CurrentPhase))
            {
                sb.AppendLine($"  Current phase: {context.Roadmap.CurrentPhase}");
            }
        }
        else
        {
            sb.AppendLine("- Roadmap: Not defined");
        }

        // State info
        sb.AppendLine($"- Current position: {context.State.Cursor ?? "Not set"}");
        if (!string.IsNullOrWhiteSpace(context.State.LastRunStatus))
        {
            sb.AppendLine($"- Last run status: {context.State.LastRunStatus}");
        }

        sb.AppendLine();
        sb.AppendLine("AVAILABLE COMMANDS:");

        foreach (var command in context.AvailableCommands.Take(10))
        {
            sb.AppendLine($"- {command.Syntax} - {command.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("GUIDELINES:");
        sb.AppendLine("- Respond conversationally and helpfully");
        sb.AppendLine("- Reference workspace state when relevant to the user's question");
        sb.AppendLine("- If the user wants to execute a command, suggest the appropriate syntax but ask for confirmation");
        sb.AppendLine("- Keep responses concise unless detailed explanation is requested");
        sb.AppendLine("- If you don't know something about the workspace, say so honestly");

        return sb.ToString();
    }

    private static string BuildUserPrompt(string userInput)
    {
        return $"User: {userInput}\n\nAssistant:";
    }
}

/// <summary>
/// Represents a built prompt with system and user components.
/// </summary>
public sealed record Prompt(string System, string User);
