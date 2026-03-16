using nirmata.Agents.Execution.Preflight.CommandSuggestion;
using System.Text.Json;

namespace nirmata.Agents.Execution.Preflight;

/// <summary>
/// Result of intent classification with detailed metadata for streaming events.
/// </summary>
public sealed class IntentClassificationResult
{
    /// <summary>
    /// The classified intent.
    /// </summary>
    public required Intent Intent { get; init; }

    /// <summary>
    /// The parsed command if a command prefix was detected.
    /// </summary>
    public ParsedCommand? ParsedCommand { get; init; }

    /// <summary>
    /// Whether this classification requires user confirmation.
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// The confidence threshold used for this classification.
    /// </summary>
    public double ConfirmationThreshold { get; init; } = 0.9;

    /// <summary>
    /// Timestamp when the classification occurred.
    /// </summary>
    public DateTimeOffset ClassifiedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The classification method used (e.g., "prefix", "regex", "default").
    /// </summary>
    public string ClassificationMethod { get; init; } = "unknown";

    /// <summary>
    /// The suggested command if the classifier determined the input could be a command.
    /// </summary>
    public CommandProposal? SuggestedCommand { get; init; }

    /// <summary>
    /// Source of the suggestion for telemetry tracking (e.g., "llm", "keyword").
    /// </summary>
    public string? SuggestionSource { get; init; }

    /// <summary>
    /// Ambiguity signals detected during classification.
    /// </summary>
    public AmbiguitySignals? Ambiguity { get; init; }

    /// <summary>
    /// Creates a classification result for a chat/non-workflow intent.
    /// </summary>
    public static IntentClassificationResult Chat(Intent intent, string method = "default")
    {
        return new IntentClassificationResult
        {
            Intent = intent,
            ClassificationMethod = method,
            RequiresConfirmation = false
        };
    }

    /// <summary>
    /// Creates a classification result for a command-based intent.
    /// </summary>
    public static IntentClassificationResult Command(Intent intent, ParsedCommand command, string method = "prefix")
    {
        return new IntentClassificationResult
        {
            Intent = intent,
            ParsedCommand = command,
            ClassificationMethod = method,
            RequiresConfirmation = command.SideEffect == SideEffect.Write && command.Confidence < 1.0
        };
    }

    /// <summary>
    /// Creates a classification result with a suggested command.
    /// </summary>
    public static IntentClassificationResult Suggestion(Intent intent, CommandProposal suggestion, string method = "suggestion")
    {
        return new IntentClassificationResult
        {
            Intent = intent,
            SuggestedCommand = suggestion,
            SuggestionSource = "llm",
            ClassificationMethod = method,
            RequiresConfirmation = true
        };
    }

    /// <summary>
    /// Returns true if this classification result has a suggested command.
    /// </summary>
    public bool HasSuggestion()
    {
        return SuggestedCommand != null && SuggestedCommand.IsValid();
    }

    /// <summary>
    /// Returns true if this classification is ambiguous based on detected signals.
    /// </summary>
    public bool IsAmbiguous()
    {
        return Ambiguity?.IsAmbiguous ?? false;
    }
}

/// <summary>
/// Represents a parsed command from user input.
/// </summary>
public sealed class ParsedCommand
{
    /// <summary>
    /// The raw input string that was parsed.
    /// </summary>
    public required string RawInput { get; init; }

    /// <summary>
    /// The command name (e.g., "run", "plan", "status").
    /// </summary>
    public required string CommandName { get; init; }

    /// <summary>
    /// Arguments provided with the command.
    /// </summary>
    public string[] Arguments { get; init; } = [];

    /// <summary>
    /// The side effect level of this command.
    /// </summary>
    public required SideEffect SideEffect { get; init; }

    /// <summary>
    /// Confidence score for the command detection (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Whether this command was explicitly prefixed with '/'.
    /// </summary>
    public bool IsExplicitCommand { get; init; } = true;

    /// <summary>
    /// Whether this is a known registered command.
    /// </summary>
    public bool IsKnownCommand { get; init; } = true;

    /// <summary>
    /// Suggestions if the command was not recognized.
    /// </summary>
    public string[]? Suggestions { get; init; }

    /// <summary>
    /// The parser mode used for this command (e.g., "legacy", "structured").
    /// </summary>
    public string ParseMode { get; init; } = "legacy";

    /// <summary>
    /// Optional parser validation details for diagnostics.
    /// </summary>
    public string? ParseDetails { get; init; }
}

/// <summary>
/// Detailed command parsing result including parser mode and validation diagnostics.
/// </summary>
public sealed class CommandParseResult
{
    /// <summary>
    /// Parsed command when parsing succeeded, otherwise null.
    /// </summary>
    public ParsedCommand? ParsedCommand { get; init; }

    /// <summary>
    /// Parser mode used for the attempt (e.g., "legacy", "structured").
    /// </summary>
    public string ParseMode { get; init; } = "legacy";

    /// <summary>
    /// Whether the input was treated as structured JSON intent.
    /// </summary>
    public bool IsStructuredInput { get; init; }

    /// <summary>
    /// Detailed validation or rejection message for telemetry and debugging.
    /// </summary>
    public string? ValidationMessage { get; init; }
}

/// <summary>
/// Parses command input to detect explicit command prefixes and extract command details.
/// </summary>
public sealed class CommandParser
{
    private readonly ICommandRegistry _commandRegistry;

    public CommandParser(ICommandRegistry commandRegistry)
    {
        _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
    }

    /// <summary>
    /// Parses input to detect if it starts with a command prefix (e.g., "/run", "/plan").
    /// </summary>
    /// <param name="input">The user input to parse.</param>
    /// <returns>Parsed command details if a prefix is detected, otherwise null.</returns>
    public ParsedCommand? Parse(string input)
    {
        return ParseDetailed(input).ParsedCommand;
    }

    /// <summary>
    /// Parses input and returns detailed parser diagnostics.
    /// </summary>
    public CommandParseResult ParseDetailed(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new CommandParseResult
            {
                ParsedCommand = null,
                ParseMode = "legacy",
                IsStructuredInput = false,
                ValidationMessage = null
            };
        }

        var trimmed = input.Trim();

        if (trimmed.StartsWith('{'))
        {
            return ParseStructuredCommand(input, trimmed);
        }

        // Check for explicit command prefix: /^\/(\w+)/
        if (!trimmed.StartsWith('/'))
        {
            return new CommandParseResult
            {
                ParsedCommand = null,
                ParseMode = "legacy",
                IsStructuredInput = false,
                ValidationMessage = null
            };
        }

        // Remove the leading '/' and split by whitespace
        var withoutPrefix = trimmed[1..];
        var parts = withoutPrefix.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return new CommandParseResult
            {
                ParsedCommand = null,
                ParseMode = "legacy",
                IsStructuredInput = false,
                ValidationMessage = "Command prefix '/' was provided without a command name."
            };
        }

        var commandName = parts[0];
        var arguments = parts.Length > 1 ? parts[1..] : [];

        // Look up the command in the registry
        var registration = _commandRegistry.GetCommand(commandName);

        if (registration != null)
        {
            return new CommandParseResult
            {
                ParsedCommand = new ParsedCommand
                {
                    RawInput = input,
                    CommandName = commandName,
                    Arguments = arguments,
                    SideEffect = registration.SideEffect,
                    Confidence = 1.0,
                    IsExplicitCommand = true,
                    IsKnownCommand = true,
                    ParseMode = "legacy"
                },
                ParseMode = "legacy",
                IsStructuredInput = false,
                ValidationMessage = null
            };
        }

        // Unknown command - return with suggestions
        var suggestions = _commandRegistry.GetSuggestions(commandName, 3).ToArray();

        return new CommandParseResult
        {
            ParsedCommand = new ParsedCommand
            {
                RawInput = input,
                CommandName = commandName,
                Arguments = arguments,
                SideEffect = SideEffect.None,
                Confidence = 0.5,
                IsExplicitCommand = true,
                IsKnownCommand = false,
                Suggestions = suggestions.Length > 0 ? suggestions : null,
                ParseMode = "legacy",
                ParseDetails = $"Unknown legacy command '/{commandName}'."
            },
            ParseMode = "legacy",
            IsStructuredInput = false,
            ValidationMessage = $"Unknown command '/{commandName}'."
        };
    }

    private CommandParseResult ParseStructuredCommand(string rawInput, string trimmedInput)
    {
        try
        {
            using var document = JsonDocument.Parse(trimmedInput);
            var root = document.RootElement;

            if (root.TryGetProperty("commands", out var commandsProperty))
            {
                var commandCount = commandsProperty.ValueKind == JsonValueKind.Array
                    ? commandsProperty.GetArrayLength()
                    : 1;

                return StructuredRejection(
                    $"Structured intent must contain a single 'command' value; received {commandCount} commands.");
            }

            if (!root.TryGetProperty("command", out var commandProperty) ||
                commandProperty.ValueKind != JsonValueKind.String)
            {
                return StructuredRejection(
                    "Structured intent is missing required string field 'command'.");
            }

            var fullCommand = commandProperty.GetString();
            if (string.IsNullOrWhiteSpace(fullCommand) || !fullCommand.StartsWith('/'))
            {
                return StructuredRejection(
                    "Structured intent field 'command' must start with '/' (e.g., '/run').");
            }

            if (fullCommand.Contains(' '))
            {
                return StructuredRejection(
                    "Structured intent field 'command' must contain exactly one command token with no spaces.");
            }

            if (!root.TryGetProperty("intent", out var intentProperty) ||
                intentProperty.ValueKind != JsonValueKind.Object)
            {
                return StructuredRejection(
                    "Structured intent is missing required object field 'intent'.");
            }

            var commandName = fullCommand[1..];
            var arguments = ExtractStructuredArguments(intentProperty, out var extractionError);
            if (extractionError is not null)
            {
                return StructuredRejection(extractionError);
            }

            var registration = _commandRegistry.GetCommand(commandName);
            if (registration is null)
            {
                var suggestions = _commandRegistry.GetSuggestions(commandName, 3).ToArray();
                return new CommandParseResult
                {
                    ParsedCommand = new ParsedCommand
                    {
                        RawInput = rawInput,
                        CommandName = commandName,
                        Arguments = arguments,
                        SideEffect = SideEffect.None,
                        Confidence = 0.5,
                        IsExplicitCommand = true,
                        IsKnownCommand = false,
                        Suggestions = suggestions.Length > 0 ? suggestions : null,
                        ParseMode = "structured",
                        ParseDetails = $"Unknown structured command '/{commandName}'."
                    },
                    ParseMode = "structured",
                    IsStructuredInput = true,
                    ValidationMessage = $"Unknown structured command '/{commandName}'."
                };
            }

            return new CommandParseResult
            {
                ParsedCommand = new ParsedCommand
                {
                    RawInput = rawInput,
                    CommandName = commandName,
                    Arguments = arguments,
                    SideEffect = registration.SideEffect,
                    Confidence = 1.0,
                    IsExplicitCommand = true,
                    IsKnownCommand = true,
                    ParseMode = "structured"
                },
                ParseMode = "structured",
                IsStructuredInput = true,
                ValidationMessage = null
            };
        }
        catch (JsonException ex)
        {
            return StructuredRejection(
                $"Structured intent JSON is malformed: {ex.Message}");
        }
    }

    private static string[] ExtractStructuredArguments(JsonElement intentProperty, out string? error)
    {
        error = null;

        if (!intentProperty.TryGetProperty("parameters", out var parametersProperty))
        {
            return [];
        }

        if (parametersProperty.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        if (parametersProperty.ValueKind != JsonValueKind.Object)
        {
            error = "Structured intent field 'intent.parameters' must be an object map of string values.";
            return [];
        }

        var arguments = new List<string>();
        foreach (var property in parametersProperty.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                error = $"Structured parameter '{property.Name}' must be a string value.";
                return [];
            }

            arguments.Add($"--{property.Name}");
            arguments.Add(property.Value.GetString() ?? string.Empty);
        }

        return arguments.ToArray();
    }

    private static CommandParseResult StructuredRejection(string message)
    {
        return new CommandParseResult
        {
            ParsedCommand = null,
            ParseMode = "structured",
            IsStructuredInput = true,
            ValidationMessage = message
        };
    }

    /// <summary>
    /// Determines if the input is a freeform chat message (no command prefix).
    /// </summary>
    public bool IsChatInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();

        // If it doesn't start with '/', it's a chat message
        if (!trimmed.StartsWith('/'))
        {
            return true;
        }

        // Check if it's a valid command
        var withoutPrefix = trimmed[1..];
        var parts = withoutPrefix.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return true; // Just "/" is treated as chat
        }

        // If the first part doesn't look like a valid command name, treat as chat
        var commandName = parts[0];
        return !_commandRegistry.IsKnownCommand(commandName) && !IsCommandLike(commandName);
    }

    private static bool IsCommandLike(string input)
    {
        // Check if the input looks like a command (alphanumeric, no spaces, reasonable length)
        return input.Length > 0 &&
               input.Length <= 20 &&
               input.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
    }
}
