namespace Gmsd.Agents.Execution.Preflight;

/// <summary>
/// Classifies user input intent using explicit command prefixes.
/// 
/// BREAKING CHANGE: This classifier no longer uses regex keyword matching.
/// Commands must use explicit /command syntax (e.g., /run, /plan, /status).
/// Freeform text without a command prefix is treated as chat (SideEffect.None).
/// </summary>
public sealed class InputClassifier
{
    private readonly CommandParser _commandParser;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>
    /// Default confidence threshold for classification.
    /// </summary>
    public double ConfidenceThreshold { get; init; } = 0.9;

    public InputClassifier()
    {
        _commandRegistry = new CommandRegistry();
        _commandParser = new CommandParser(_commandRegistry);
    }

    /// <summary>
    /// For testing - allows injecting custom registry and parser.
    /// </summary>
    public InputClassifier(ICommandRegistry commandRegistry, CommandParser commandParser)
    {
        _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
        _commandParser = commandParser ?? throw new ArgumentNullException(nameof(commandParser));
    }

    /// <summary>
    /// Classifies user input based on explicit command prefixes.
    /// 
    /// Pattern: /^\/(\w+)/ - commands must start with /
    /// Default: Freeform text without / prefix → chat intent (SideEffect.None)
    /// </summary>
    /// <param name="input">The user input to classify.</param>
    /// <returns>Intent classification result with confidence and reasoning.</returns>
    public IntentClassificationResult Classify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            var emptyIntent = new Intent
            {
                Kind = IntentKind.Unknown,
                SideEffect = SideEffect.None,
                Confidence = 1.0,
                Reasoning = "Empty input received; classifying as unknown intent"
            };

            return IntentClassificationResult.Chat(emptyIntent, "empty_input");
        }

        var trimmed = input.Trim();

        // Check for greeting/small talk patterns
        if (IsGreeting(trimmed))
        {
            var smallTalkIntent = new Intent
            {
                Kind = IntentKind.SmallTalk,
                SideEffect = SideEffect.None,
                Confidence = 1.0,
                Reasoning = $"Matched greeting pattern: '{trimmed}' is conversational small talk"
            };

            return IntentClassificationResult.Chat(smallTalkIntent, "greeting_pattern");
        }

        // Try to parse as a command (legacy '/...' or structured JSON intent)
        var parseResult = _commandParser.ParseDetailed(input);
        var parsedCommand = parseResult.ParsedCommand;

        if (parsedCommand != null)
        {
            // It's a command - classify based on the command
            return ClassifyCommand(parsedCommand, input, parseResult);
        }

        if (parseResult.IsStructuredInput)
        {
            var structuredRejectionIntent = new Intent
            {
                Kind = IntentKind.Unknown,
                SideEffect = SideEffect.None,
                Confidence = 0.8,
                Reasoning = parseResult.ValidationMessage ?? "Structured intent could not be parsed into a command."
            };

            return IntentClassificationResult.Chat(structuredRejectionIntent, "structured_rejected");
        }

        // No command prefix - default to chat
        // This is the key behavioral change: freeform text is now chat, not workflow
        var chatIntent = new Intent
        {
            Kind = IntentKind.Unknown,
            SideEffect = SideEffect.None,
            Confidence = 0.9,
            Reasoning = "No explicit command prefix detected; treating as freeform chat message"
        };

        return IntentClassificationResult.Chat(chatIntent, "default_chat");
    }

    private IntentClassificationResult ClassifyCommand(ParsedCommand command, string rawInput, CommandParseResult parseResult)
    {
        var method = parseResult.ParseMode.Equals("structured", StringComparison.OrdinalIgnoreCase)
            ? "structured_match"
            : "prefix_match";

        if (!command.IsKnownCommand)
        {
            // Unknown command - still treat as chat but with explanation
            var unknownCommandIntent = new Intent
            {
                Kind = IntentKind.Unknown,
                SideEffect = SideEffect.None,
                Confidence = 0.8,
                Reasoning = $"Unknown command '/{command.CommandName}'. Available commands: {string.Join(", ", _commandRegistry.GetAllCommands().Select(c => $"/{c.Name}"))}"
            };

            if (command.Suggestions != null && command.Suggestions.Length > 0)
            {
                unknownCommandIntent = new Intent
                {
                    Kind = unknownCommandIntent.Kind,
                    SideEffect = unknownCommandIntent.SideEffect,
                    Confidence = unknownCommandIntent.Confidence,
                    Reasoning = $"{unknownCommandIntent.Reasoning}. Did you mean: {string.Join(", ", command.Suggestions.Select(s => $"/{s}"))}?"
                };
            }

            return IntentClassificationResult.Command(
                unknownCommandIntent,
                command,
                parseResult.ParseMode.Equals("structured", StringComparison.OrdinalIgnoreCase)
                    ? "structured_unknown_command"
                    : "unknown_command");
        }

        // Known command - classify based on side effect
        var (kind, reasoning) = command.SideEffect switch
        {
            SideEffect.ReadOnly => (IntentKind.Status, $"Command '/{command.CommandName}' is read-only"),
            SideEffect.Write => (IntentKind.WorkflowCommand, $"Command '/{command.CommandName}' is a write operation"),
            _ => (IntentKind.Unknown, $"Command '/{command.CommandName}' has unknown side effects")
        };

        // Determine the specific intent kind based on command name
        kind = command.CommandName.ToLowerInvariant() switch
        {
            "help" or "?" or "commands" => IntentKind.Help,
            "status" => IntentKind.Status,
            "view" or "open" or "show" => IntentKind.Navigation,
            _ => kind
        };

        var intent = new Intent
        {
            Kind = kind,
            SideEffect = command.SideEffect,
            Confidence = command.Confidence,
            Command = command.CommandName,
            Reasoning = reasoning
        };

        return IntentClassificationResult.Command(intent, command, method);
    }

    private static bool IsGreeting(string input)
    {
        var greetings = new[] { "hi", "hello", "yo", "thanks", "lol", "hey", "bye", "good morning", "good afternoon", "good evening" };
        return greetings.Any(g => input.Equals(g, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Legacy method for backward compatibility. Returns just the Intent.
    /// </summary>
    public Intent ClassifyLegacy(string input)
    {
        return Classify(input).Intent;
    }
}
