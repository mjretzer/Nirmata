namespace Gmsd.Agents.Execution.ControlPlane.Chat;

using Models;
using Commands;

/// <summary>
/// Detects when a user's chat input likely indicates they want to execute a command,
/// and generates structured command proposals for confirmation.
/// </summary>
public sealed class CommandSuggestionDetector
{
    private readonly ICommandParser _commandParser;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>
    /// Keywords that suggest the user might want to execute a command.
    /// </summary>
    private static readonly string[] CommandSuggestionKeywords = new[]
    {
        "run", "execute", "start", "begin", "perform", "do",
        "plan", "create plan", "make plan",
        "verify", "check", "validate", "test",
        "fix", "repair", "resolve", "solve",
        "help", "show commands", "what can you do",
        "status", "current status", "how are things"
    };

    public CommandSuggestionDetector(ICommandParser commandParser, ICommandRegistry commandRegistry)
    {
        _commandParser = commandParser ?? throw new ArgumentNullException(nameof(commandParser));
        _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
    }

    /// <summary>
    /// Analyzes chat input to detect if the user likely wants to execute a command.
    /// </summary>
    /// <returns>A command suggestion if detected, otherwise null.</returns>
    public CommandSuggestion? DetectSuggestion(string chatInput)
    {
        if (string.IsNullOrWhiteSpace(chatInput))
        {
            return null;
        }

        var lowerInput = chatInput.ToLowerInvariant();

        // Check for explicit command keywords in order of priority
        // Help commands should be checked first
        var helpKeywords = new[] { "help", "show commands", "what can you do" };
        var fixKeywords = new[] { "fix", "repair", "resolve", "solve" };
        var verifyKeywords = new[] { "verify", "check", "validate", "test" };
        var statusKeywords = new[] { "status", "current status", "how are things" };
        var planKeywords = new[] { "plan", "create plan", "make plan" };
        var runKeywords = new[] { "run", "execute", "start", "begin", "perform", "do" };

        var keywordGroups = new[] { helpKeywords, fixKeywords, verifyKeywords, statusKeywords, planKeywords, runKeywords };

        foreach (var keywordGroup in keywordGroups)
        {
            foreach (var keyword in keywordGroup)
            {
                if (lowerInput.Contains(keyword))
                {
                    return AnalyzeForCommandSuggestion(chatInput, keyword);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Analyzes the input to generate a specific command suggestion.
    /// </summary>
    private CommandSuggestion? AnalyzeForCommandSuggestion(string input, string detectedKeyword)
    {
        var suggestion = detectedKeyword.ToLowerInvariant() switch
        {
            "help" or "show commands" or "what can you do" => SuggestHelpCommand(input),
            "status" or "current status" or "how are things" => SuggestStatusCommand(input),
            "run" or "execute" or "start" or "begin" or "perform" or "do" => SuggestRunCommand(input),
            "plan" or "create plan" or "make plan" => SuggestPlanCommand(input),
            "verify" or "check" or "validate" or "test" => SuggestVerifyCommand(input),
            "fix" or "repair" or "resolve" or "solve" => SuggestFixCommand(input),
            _ => null
        };

        return suggestion;
    }

    private CommandSuggestion? SuggestHelpCommand(string input)
    {
        return new CommandSuggestion
        {
            CommandName = "help",
            Arguments = new Dictionary<string, string>(),
            Confidence = 0.95,
            Reasoning = "User asked for help or available commands"
        };
    }

    private CommandSuggestion? SuggestStatusCommand(string input)
    {
        return new CommandSuggestion
        {
            CommandName = "status",
            Arguments = new Dictionary<string, string>(),
            Confidence = 0.90,
            Reasoning = "User asked about current status"
        };
    }

    private CommandSuggestion? SuggestRunCommand(string input)
    {
        // Try to extract workflow name from the input
        var workflowName = ExtractWorkflowName(input);
        
        return new CommandSuggestion
        {
            CommandName = "run",
            Arguments = workflowName != null ? new Dictionary<string, string> { { "workflow", workflowName } } : new Dictionary<string, string>(),
            Confidence = workflowName != null ? 0.85 : 0.70,
            Reasoning = workflowName != null 
                ? $"User wants to run workflow '{workflowName}'"
                : "User wants to run or execute something"
        };
    }

    private CommandSuggestion? SuggestPlanCommand(string input)
    {
        // Try to extract task description from the input
        var taskDescription = ExtractTaskDescription(input);
        
        return new CommandSuggestion
        {
            CommandName = "plan",
            Arguments = taskDescription != null ? new Dictionary<string, string> { { "task", taskDescription } } : new Dictionary<string, string>(),
            Confidence = taskDescription != null ? 0.80 : 0.65,
            Reasoning = taskDescription != null
                ? $"User wants to plan task: '{taskDescription}'"
                : "User wants to create a plan"
        };
    }

    private CommandSuggestion? SuggestVerifyCommand(string input)
    {
        var target = ExtractTarget(input);
        
        return new CommandSuggestion
        {
            CommandName = "verify",
            Arguments = target != null ? new Dictionary<string, string> { { "target", target } } : new Dictionary<string, string>(),
            Confidence = target != null ? 0.85 : 0.75,
            Reasoning = target != null
                ? $"User wants to verify '{target}'"
                : "User wants to verify something"
        };
    }

    private CommandSuggestion? SuggestFixCommand(string input)
    {
        var issue = ExtractIssue(input);
        
        return new CommandSuggestion
        {
            CommandName = "fix",
            Arguments = issue != null ? new Dictionary<string, string> { { "issue", issue } } : new Dictionary<string, string>(),
            Confidence = issue != null ? 0.80 : 0.70,
            Reasoning = issue != null
                ? $"User wants to fix issue: '{issue}'"
                : "User wants to fix something"
        };
    }

    private static string? ExtractWorkflowName(string input)
    {
        // Simple heuristic: look for quoted text or words after "run"/"execute"
        var words = input.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var runIndex = -1;

        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Equals("run", StringComparison.OrdinalIgnoreCase) ||
                words[i].Equals("execute", StringComparison.OrdinalIgnoreCase))
            {
                runIndex = i;
                break;
            }
        }

        if (runIndex >= 0 && runIndex < words.Length - 1)
        {
            return words[runIndex + 1].Trim('"', '\'');
        }

        return null;
    }

    private static string? ExtractTaskDescription(string input)
    {
        var words = input.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var planIndex = -1;

        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Equals("plan", StringComparison.OrdinalIgnoreCase) ||
                words[i].Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                planIndex = i;
                break;
            }
        }

        if (planIndex >= 0 && planIndex < words.Length - 1)
        {
            return string.Join(" ", words.Skip(planIndex + 1)).Trim('"', '\'');
        }

        return null;
    }

    private static string? ExtractTarget(string input)
    {
        var words = input.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var verifyIndex = -1;

        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Equals("verify", StringComparison.OrdinalIgnoreCase) ||
                words[i].Equals("check", StringComparison.OrdinalIgnoreCase))
            {
                verifyIndex = i;
                break;
            }
        }

        if (verifyIndex >= 0 && verifyIndex < words.Length - 1)
        {
            return string.Join(" ", words.Skip(verifyIndex + 1)).Trim('"', '\'');
        }

        return null;
    }

    private static string? ExtractIssue(string input)
    {
        var words = input.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var fixIndex = -1;

        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Equals("fix", StringComparison.OrdinalIgnoreCase) ||
                words[i].Equals("repair", StringComparison.OrdinalIgnoreCase))
            {
                fixIndex = i;
                break;
            }
        }

        if (fixIndex >= 0 && fixIndex < words.Length - 1)
        {
            return string.Join(" ", words.Skip(fixIndex + 1)).Trim('"', '\'');
        }

        return null;
    }
}

/// <summary>
/// Represents a suggested command based on chat input analysis.
/// </summary>
public sealed class CommandSuggestion
{
    /// <summary>
    /// The suggested command name.
    /// </summary>
    public required string CommandName { get; init; }

    /// <summary>
    /// Arguments to pass to the command.
    /// </summary>
    public required Dictionary<string, string> Arguments { get; init; }

    /// <summary>
    /// Confidence score for this suggestion (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Reasoning for the suggestion.
    /// </summary>
    public string? Reasoning { get; init; }
}
