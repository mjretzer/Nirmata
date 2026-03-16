namespace nirmata.Agents.Execution.ControlPlane.Commands;

/// <summary>
/// Registry for managing command metadata, help text, and argument schemas.
/// </summary>
public class CommandRegistry : ICommandRegistry
{
    private readonly Dictionary<string, CommandMetadata> _commands = new(StringComparer.OrdinalIgnoreCase);

    public CommandRegistry()
    {
        InitializeDefaultCommands();
    }

    public void RegisterCommand(CommandMetadata metadata)
    {
        _commands[metadata.Name.ToLowerInvariant()] = metadata;
    }

    public CommandMetadata? GetCommand(string commandName)
    {
        _commands.TryGetValue(commandName.ToLowerInvariant(), out var metadata);
        return metadata;
    }

    public IReadOnlyList<CommandMetadata> GetAllCommands()
    {
        return _commands.Values.ToList().AsReadOnly();
    }

    public List<string> ValidateArguments(string commandName, Dictionary<string, object?> arguments)
    {
        var errors = new List<string>();
        var metadata = GetCommand(commandName);

        if (metadata == null)
        {
            errors.Add($"Command '{commandName}' is not registered.");
            return errors;
        }

        var providedKeys = new HashSet<string>(arguments.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var schema in metadata.ArgumentSchemas.Values)
        {
            var hasArg = providedKeys.Any(k => k.Equals(schema.Name, StringComparison.OrdinalIgnoreCase));

            if (schema.Required && !hasArg)
            {
                errors.Add($"Required argument '{schema.Name}' is missing.");
                continue;
            }

            if (hasArg)
            {
                var value = arguments.First(kvp => kvp.Key.Equals(schema.Name, StringComparison.OrdinalIgnoreCase)).Value;
                var validationError = ValidateArgumentValue(schema, value);
                if (validationError != null)
                {
                    errors.Add(validationError);
                }
            }
        }

        return errors;
    }

    private static string? ValidateArgumentValue(ArgumentSchema schema, object? value)
    {
        if (value == null)
        {
            return schema.Required ? $"Argument '{schema.Name}' cannot be null." : null;
        }

        var stringValue = value.ToString() ?? string.Empty;

        return schema.Type switch
        {
            ArgumentType.Integer => ValidateInteger(schema.Name, stringValue),
            ArgumentType.Boolean => ValidateBoolean(schema.Name, stringValue),
            ArgumentType.Path => ValidatePath(schema.Name, stringValue),
            ArgumentType.String => ValidateStringEnum(schema.Name, stringValue, schema.AllowedValues),
            _ => null
        };
    }

    private static string? ValidateInteger(string name, string value)
    {
        return int.TryParse(value, out _) ? null : $"Argument '{name}' must be an integer.";
    }

    private static string? ValidateBoolean(string name, string value)
    {
        return bool.TryParse(value, out _) ? null : $"Argument '{name}' must be a boolean (true/false).";
    }

    private static string? ValidatePath(string name, string value)
    {
        return string.IsNullOrWhiteSpace(value) ? $"Argument '{name}' cannot be an empty path." : null;
    }

    private static string? ValidateStringEnum(string name, string value, List<string>? allowedValues)
    {
        if (allowedValues == null || allowedValues.Count == 0)
        {
            return null;
        }

        return allowedValues.Any(v => v.Equals(value, StringComparison.OrdinalIgnoreCase))
            ? null
            : $"Argument '{name}' must be one of: {string.Join(", ", allowedValues)}.";
    }

    private void InitializeDefaultCommands()
    {
        RegisterCommand(new CommandMetadata
        {
            Name = "help",
            HelpText = "Display help information about available commands.",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>
            {
                {
                    "command",
                    new ArgumentSchema
                    {
                        Name = "command",
                        Type = ArgumentType.String,
                        Required = false,
                        Description = "Specific command to get help for."
                    }
                }
            }
        });

        RegisterCommand(new CommandMetadata
        {
            Name = "status",
            HelpText = "Display the current status of the system.",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>()
        });

        RegisterCommand(new CommandMetadata
        {
            Name = "run",
            HelpText = "Execute a workflow or command.",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>
            {
                {
                    "workflow",
                    new ArgumentSchema
                    {
                        Name = "workflow",
                        Type = ArgumentType.String,
                        Required = true,
                        Description = "Name of the workflow to run."
                    }
                }
            }
        });

        RegisterCommand(new CommandMetadata
        {
            Name = "plan",
            HelpText = "Create or view a plan for a task.",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>
            {
                {
                    "task",
                    new ArgumentSchema
                    {
                        Name = "task",
                        Type = ArgumentType.String,
                        Required = true,
                        Description = "Description of the task to plan."
                    }
                }
            }
        });

        RegisterCommand(new CommandMetadata
        {
            Name = "verify",
            HelpText = "Verify the current state or a specific component.",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>
            {
                {
                    "target",
                    new ArgumentSchema
                    {
                        Name = "target",
                        Type = ArgumentType.String,
                        Required = false,
                        Description = "Specific component to verify."
                    }
                }
            }
        });

        RegisterCommand(new CommandMetadata
        {
            Name = "fix",
            HelpText = "Attempt to fix an identified issue.",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>
            {
                {
                    "issue",
                    new ArgumentSchema
                    {
                        Name = "issue",
                        Type = ArgumentType.String,
                        Required = true,
                        Description = "Description or ID of the issue to fix."
                    }
                }
            }
        });
    }
}
