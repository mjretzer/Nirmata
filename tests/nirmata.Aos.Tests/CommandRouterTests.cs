using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Engine.Commands;
using nirmata.Aos.Public.Models;
using nirmata.Aos.Public.Services;
using nirmata.Aos.Engine.Commands.Help;
using nirmata.Aos.Public;
using nirmata.Aos.Public.Catalogs;
using Xunit;

namespace nirmata.Aos.Tests;

public class CommandRouterTests
{
    [Fact]
    public async Task RouteAsync_UnknownCommand_ReturnsStructuredError()
    {
        // Arrange
        var catalog = new CommandCatalog();
        var workspace = Workspace.FromRepositoryRoot(Path.GetTempPath());
        var router = new CommandRouter(catalog, workspace);
        var request = CommandRequest.Create("unknown", "command");

        // Act
        var result = await router.RouteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.NotNull(result.ErrorOutput);
        Assert.Contains("Unknown command", result.ErrorOutput);
        Assert.NotEmpty(result.Errors);
        Assert.Equal("UnknownCommand", result.Errors[0].Code);
    }

    [Fact]
    public async Task RouteAsync_MissingGroup_ReturnsValidationError()
    {
        // Arrange
        var catalog = new CommandCatalog();
        var workspace = Workspace.FromRepositoryRoot(Path.GetTempPath());
        var router = new CommandRouter(catalog, workspace);
        var request = new CommandRequest { Group = "", Command = "test" };

        // Act
        var result = await router.RouteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("group is required", result.ErrorOutput);
    }

    [Fact]
    public async Task RouteAsync_MissingCommand_ReturnsValidationError()
    {
        // Arrange
        var catalog = new CommandCatalog();
        var workspace = Workspace.FromRepositoryRoot(Path.GetTempPath());
        var router = new CommandRouter(catalog, workspace);
        var request = new CommandRequest { Group = "test", Command = "" };

        // Act
        var result = await router.RouteAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("name is required", result.ErrorOutput);
    }

    [Fact]
    public async Task RouteAsync_RegisteredHandler_ExecutesSuccessfully()
    {
        // Arrange
        var catalog = new CommandCatalog();
        var workspace = Workspace.FromRepositoryRoot(Path.GetTempPath());
        var router = new CommandRouter(catalog, workspace);

        var metadata = new CommandMetadata
        {
            Group = "test",
            Command = "hello",
            Id = "test.hello",
            Description = "Test command"
        };

        catalog.Register(metadata, ctx => Task.FromResult(CommandResult.Success("Hello, World!")));

        var request = CommandRequest.Create("test", "hello");

        // Act
        var result = await router.RouteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Hello, World!", result.Output);
    }

    [Fact]
    public void CommandCatalog_TryResolve_ExistingCommand_ReturnsTrue()
    {
        // Arrange
        var catalog = new CommandCatalog();
        var metadata = new CommandMetadata
        {
            Group = "test",
            Command = "cmd",
            Id = "test.cmd",
            Description = "Test"
        };
        catalog.Register(metadata, _ => Task.FromResult(CommandResult.Success()));

        // Act
        var found = catalog.TryResolve("test", "cmd", out var registration);

        // Assert
        Assert.True(found);
        Assert.NotNull(registration);
        Assert.Equal("test:cmd", registration.Metadata.Key);
    }

    [Fact]
    public void CommandCatalog_TryResolve_NonExistingCommand_ReturnsFalse()
    {
        // Arrange
        var catalog = new CommandCatalog();

        // Act
        var found = catalog.TryResolve("missing", "cmd", out var registration);

        // Assert
        Assert.False(found);
        Assert.Null(registration);
    }

    [Fact]
    public void CommandCatalog_Register_DuplicateCommand_Throws()
    {
        // Arrange
        var catalog = new CommandCatalog();
        var metadata = new CommandMetadata
        {
            Group = "test",
            Command = "dup",
            Id = "test.dup",
            Description = "Test"
        };
        catalog.Register(metadata, _ => Task.FromResult(CommandResult.Success()));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            catalog.Register(metadata, _ => Task.FromResult(CommandResult.Success())));
    }

    [Fact]
    public void CommandCatalog_GetAllCommands_ReturnsAllRegistered()
    {
        // Arrange
        var catalog = new CommandCatalog();
        catalog.Register(
            new CommandMetadata { Group = "a", Command = "1", Id = "a.1", Description = "A1" },
            _ => Task.FromResult(CommandResult.Success()));
        catalog.Register(
            new CommandMetadata { Group = "b", Command = "2", Id = "b.2", Description = "B2" },
            _ => Task.FromResult(CommandResult.Success()));

        // Act
        var commands = catalog.GetAllCommands().ToList();

        // Assert
        Assert.Equal(2, commands.Count);
    }

    [Fact]
    public void CommandCatalog_GetCommandsByGroup_ReturnsFilteredCommands()
    {
        // Arrange
        var catalog = new CommandCatalog();
        catalog.Register(
            new CommandMetadata { Group = "alpha", Command = "one", Id = "alpha.one", Description = "One" },
            _ => Task.FromResult(CommandResult.Success()));
        catalog.Register(
            new CommandMetadata { Group = "beta", Command = "two", Id = "beta.two", Description = "Two" },
            _ => Task.FromResult(CommandResult.Success()));

        // Act
        var alphaCommands = catalog.GetCommandsByGroup("alpha").ToList();

        // Assert
        Assert.Single(alphaCommands);
        Assert.Equal("one", alphaCommands[0].Command);
    }

    [Fact]
    public async Task HelpCommandHandler_GeneratesHelpOutput()
    {
        // Arrange
        var catalog = new CommandCatalog();
        catalog.Register(
            new CommandMetadata { Group = "test", Command = "cmd", Id = "test.cmd", Description = "A test command" },
            _ => Task.FromResult(CommandResult.Success()));

        var handler = new HelpCommandHandler(catalog);
        var context = CommandContext.Create(Workspace.FromRepositoryRoot(Path.GetTempPath()));

        // Act
        var result = await handler.ExecuteAsync(context);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Contains("AOS Command Reference", result.Output);
        Assert.Contains("test", result.Output);
        Assert.Contains("cmd", result.Output);
        Assert.Contains("A test command", result.Output);
    }

    [Fact]
    public void CommandRouter_ImplementsICommandRouter()
    {
        // Arrange
        var catalog = new CommandCatalog();
        var workspace = Workspace.FromRepositoryRoot(Path.GetTempPath());

        // Act
        var router = new CommandRouter(catalog, workspace);

        // Assert
        Assert.IsAssignableFrom<ICommandRouter>(router);
    }

    [Fact]
    public void CommandIds_HasExpectedValues()
    {
        // Assert all expected command IDs exist
        Assert.Equal("init", CommandIds.Init);
        Assert.Equal("status", CommandIds.Status);
        Assert.Equal("config.get", CommandIds.ConfigGet);
        Assert.Equal("config.set", CommandIds.ConfigSet);
        Assert.Equal("config.list", CommandIds.ConfigList);
        Assert.Equal("validate", CommandIds.Validate);
        Assert.Equal("spec.list", CommandIds.SpecList);
        Assert.Equal("spec.show", CommandIds.SpecShow);
        Assert.Equal("spec.apply", CommandIds.SpecApply);
        Assert.Equal("state.show", CommandIds.StateShow);
        Assert.Equal("state.diff", CommandIds.StateDiff);
        Assert.Equal("run.execute", CommandIds.RunExecute);
        Assert.Equal("run.list", CommandIds.RunList);
        Assert.Equal("help", CommandIds.Help);
    }
}
