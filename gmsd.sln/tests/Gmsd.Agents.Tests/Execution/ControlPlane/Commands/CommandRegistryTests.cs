using Gmsd.Agents.Execution.ControlPlane.Commands;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ControlPlane.Commands;

public class CommandRegistryTests
{
    private readonly CommandRegistry _registry = new();

    [Fact]
    public void Constructor_InitializesDefaultCommands()
    {
        var commands = _registry.GetAllCommands();

        Assert.NotEmpty(commands);
        Assert.Contains(commands, c => c.Name.Equals("help", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(commands, c => c.Name.Equals("status", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(commands, c => c.Name.Equals("run", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(commands, c => c.Name.Equals("plan", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(commands, c => c.Name.Equals("verify", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(commands, c => c.Name.Equals("fix", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RegisterCommand_AddsCommandToRegistry()
    {
        var metadata = new CommandMetadata
        {
            Name = "custom",
            HelpText = "A custom command",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>()
        };

        _registry.RegisterCommand(metadata);
        var retrieved = _registry.GetCommand("custom");

        Assert.NotNull(retrieved);
        Assert.Equal("custom", retrieved.Name);
    }

    [Fact]
    public void GetCommand_IsCaseInsensitive()
    {
        var cmd1 = _registry.GetCommand("HELP");
        var cmd2 = _registry.GetCommand("Help");
        var cmd3 = _registry.GetCommand("help");

        Assert.NotNull(cmd1);
        Assert.NotNull(cmd2);
        Assert.NotNull(cmd3);
        Assert.Equal(cmd1.Name, cmd2.Name);
        Assert.Equal(cmd2.Name, cmd3.Name);
    }

    [Fact]
    public void GetCommand_WithUnregisteredCommand_ReturnsNull()
    {
        var result = _registry.GetCommand("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void GetAllCommands_ReturnsReadOnlyList()
    {
        var commands = _registry.GetAllCommands();

        Assert.NotNull(commands);
        Assert.IsAssignableFrom<IReadOnlyList<CommandMetadata>>(commands);
    }

    [Fact]
    public void ValidateArguments_WithValidArguments_ReturnsNoErrors()
    {
        var arguments = new Dictionary<string, object?> { { "command", "test" } };
        var errors = _registry.ValidateArguments("help", arguments);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateArguments_WithMissingRequiredArg_ReturnsError()
    {
        var arguments = new Dictionary<string, object?>();
        var errors = _registry.ValidateArguments("run", arguments);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("workflow") && e.Contains("missing"));
    }

    [Fact]
    public void ValidateArguments_WithUnregisteredCommand_ReturnsError()
    {
        var arguments = new Dictionary<string, object?>();
        var errors = _registry.ValidateArguments("nonexistent", arguments);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("not registered"));
    }

    [Fact]
    public void ValidateArguments_WithIntegerType_ValidatesCorrectly()
    {
        var metadata = new CommandMetadata
        {
            Name = "test-int",
            HelpText = "Test integer validation",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>
            {
                {
                    "count",
                    new ArgumentSchema
                    {
                        Name = "count",
                        Type = ArgumentType.Integer,
                        Required = true
                    }
                }
            }
        };
        _registry.RegisterCommand(metadata);

        var validArgs = new Dictionary<string, object?> { { "count", "42" } };
        var validErrors = _registry.ValidateArguments("test-int", validArgs);
        Assert.Empty(validErrors);

        var invalidArgs = new Dictionary<string, object?> { { "count", "not-a-number" } };
        var invalidErrors = _registry.ValidateArguments("test-int", invalidArgs);
        Assert.NotEmpty(invalidErrors);
        Assert.Contains(invalidErrors, e => e.Contains("integer"));
    }

    [Fact]
    public void ValidateArguments_WithBooleanType_ValidatesCorrectly()
    {
        var metadata = new CommandMetadata
        {
            Name = "test-bool",
            HelpText = "Test boolean validation",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>
            {
                {
                    "enabled",
                    new ArgumentSchema
                    {
                        Name = "enabled",
                        Type = ArgumentType.Boolean,
                        Required = true
                    }
                }
            }
        };
        _registry.RegisterCommand(metadata);

        var validArgs = new Dictionary<string, object?> { { "enabled", "true" } };
        var validErrors = _registry.ValidateArguments("test-bool", validArgs);
        Assert.Empty(validErrors);

        var invalidArgs = new Dictionary<string, object?> { { "enabled", "maybe" } };
        var invalidErrors = _registry.ValidateArguments("test-bool", invalidArgs);
        Assert.NotEmpty(invalidErrors);
        Assert.Contains(invalidErrors, e => e.Contains("boolean"));
    }

    [Fact]
    public void ValidateArguments_WithPathType_ValidatesCorrectly()
    {
        var metadata = new CommandMetadata
        {
            Name = "test-path",
            HelpText = "Test path validation",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>
            {
                {
                    "filepath",
                    new ArgumentSchema
                    {
                        Name = "filepath",
                        Type = ArgumentType.Path,
                        Required = true
                    }
                }
            }
        };
        _registry.RegisterCommand(metadata);

        var validArgs = new Dictionary<string, object?> { { "filepath", "/home/user/file.txt" } };
        var validErrors = _registry.ValidateArguments("test-path", validArgs);
        Assert.Empty(validErrors);

        var invalidArgs = new Dictionary<string, object?> { { "filepath", "" } };
        var invalidErrors = _registry.ValidateArguments("test-path", invalidArgs);
        Assert.NotEmpty(invalidErrors);
        Assert.Contains(invalidErrors, e => e.Contains("empty path"));
    }

    [Fact]
    public void ValidateArguments_WithEnumValues_ValidatesCorrectly()
    {
        var metadata = new CommandMetadata
        {
            Name = "test-enum",
            HelpText = "Test enum validation",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>
            {
                {
                    "level",
                    new ArgumentSchema
                    {
                        Name = "level",
                        Type = ArgumentType.String,
                        Required = true,
                        AllowedValues = new List<string> { "low", "medium", "high" }
                    }
                }
            }
        };
        _registry.RegisterCommand(metadata);

        var validArgs = new Dictionary<string, object?> { { "level", "high" } };
        var validErrors = _registry.ValidateArguments("test-enum", validArgs);
        Assert.Empty(validErrors);

        var invalidArgs = new Dictionary<string, object?> { { "level", "critical" } };
        var invalidErrors = _registry.ValidateArguments("test-enum", invalidArgs);
        Assert.NotEmpty(invalidErrors);
        Assert.Contains(invalidErrors, e => e.Contains("one of"));
    }

    [Fact]
    public void ValidateArguments_WithOptionalArg_AllowsOmission()
    {
        var metadata = new CommandMetadata
        {
            Name = "test-optional",
            HelpText = "Test optional argument",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>
            {
                {
                    "filter",
                    new ArgumentSchema
                    {
                        Name = "filter",
                        Type = ArgumentType.String,
                        Required = false
                    }
                }
            }
        };
        _registry.RegisterCommand(metadata);

        var args = new Dictionary<string, object?>();
        var errors = _registry.ValidateArguments("test-optional", args);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateArguments_IsCaseInsensitiveForArgNames()
    {
        var metadata = new CommandMetadata
        {
            Name = "test-case",
            HelpText = "Test case insensitivity",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>
            {
                {
                    "target",
                    new ArgumentSchema
                    {
                        Name = "target",
                        Type = ArgumentType.String,
                        Required = true
                    }
                }
            }
        };
        _registry.RegisterCommand(metadata);

        var args = new Dictionary<string, object?> { { "TARGET", "value" } };
        var errors = _registry.ValidateArguments("test-case", args);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateArguments_WithMultipleErrors_ReturnsAllErrors()
    {
        var metadata = new CommandMetadata
        {
            Name = "test-multi",
            HelpText = "Test multiple validations",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>
            {
                {
                    "arg1",
                    new ArgumentSchema
                    {
                        Name = "arg1",
                        Type = ArgumentType.Integer,
                        Required = true
                    }
                },
                {
                    "arg2",
                    new ArgumentSchema
                    {
                        Name = "arg2",
                        Type = ArgumentType.Integer,
                        Required = true
                    }
                }
            }
        };
        _registry.RegisterCommand(metadata);

        var args = new Dictionary<string, object?> { { "arg1", "not-int" } };
        var errors = _registry.ValidateArguments("test-multi", args);

        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void RegisterCommand_OverwritesPreviousRegistration()
    {
        var metadata1 = new CommandMetadata
        {
            Name = "custom",
            HelpText = "First version",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>()
        };
        var metadata2 = new CommandMetadata
        {
            Name = "custom",
            HelpText = "Second version",
            ArgumentSchemas = new Dictionary<string, ArgumentSchema>()
        };

        _registry.RegisterCommand(metadata1);
        _registry.RegisterCommand(metadata2);

        var retrieved = _registry.GetCommand("custom");
        Assert.Equal("Second version", retrieved?.HelpText);
    }
}
