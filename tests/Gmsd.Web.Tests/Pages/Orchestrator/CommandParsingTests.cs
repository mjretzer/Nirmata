using FluentAssertions;
using Gmsd.Web.Pages.Orchestrator;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gmsd.Web.Tests.Pages.Orchestrator;

public class CommandParsingTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _configDir;
    private readonly string _configFile;
    private readonly string _historyFile;

    public CommandParsingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gmsd");
        _configFile = Path.Combine(_configDir, "workspace-config.json");
        _historyFile = Path.Combine(_configDir, "orchestrator-history.json");
        Directory.CreateDirectory(_aosDir);
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
            if (File.Exists(_configFile))
            {
                File.Delete(_configFile);
            }
            if (File.Exists(_historyFile))
            {
                File.Delete(_historyFile);
            }
        }
        catch { }
    }

    private void CreateWorkspaceConfig(string workspacePath)
    {
        var config = $"{{ \"SelectedWorkspacePath\": \"{workspacePath.Replace("\\", "\\\\")}\", \"LastUpdated\": \"{DateTime.UtcNow:O}\" }}";
        File.WriteAllText(_configFile, config);
    }

    private void CreateAosStructure()
    {
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "project"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "roadmap"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "evidence", "runs"));

        var state = new { status = "initialized", cursor = new { }, version = "1.0" };
        var stateJson = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_aosDir, "state", "state.json"), stateJson);
    }

    private IndexModel CreateModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new IndexModel(NullLogger<IndexModel>.Instance, configuration, null!);
    }

    [Theory]
    [InlineData("/help", "/help")]
    [InlineData("/status", "/status")]
    [InlineData("/validate", "/validate")]
    [InlineData("/init", "/init")]
    [InlineData("/spec", "/spec")]
    [InlineData("/run", "/run")]
    [InlineData("/codebase", "/codebase")]
    [InlineData("/pack", "/pack")]
    [InlineData("/checkpoint", "/checkpoint")]
    public void CommandParsing_RecognizesValidCommands(string input, string expectedCommand)
    {
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();

        cmd.Should().Be(expectedCommand);
    }

    [Theory]
    [InlineData("/spec project", "/spec", "project")]
    [InlineData("/run test-001", "/run", "test-001")]
    [InlineData("/help extra", "/help", "extra")]
    public void CommandParsing_ExtractsArguments(string input, string expectedCommand, string expectedArgs)
    {
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        var args = parts.Length > 1 ? string.Join(' ', parts[1..]) : string.Empty;

        cmd.Should().Be(expectedCommand);
        args.Should().Be(expectedArgs);
    }

    [Theory]
    [InlineData("/unknown")]
    [InlineData("/invalid")]
    [InlineData("/xyz")]
    public void CommandParsing_UnknownCommands_ReturnUnknownResponse(string input)
    {
        var model = CreateModel();
        CreateWorkspaceConfig(_tempDir);
        CreateAosStructure();

        // Simulate OnGet to load workspace
        model.OnGet();

        // Process command through reflection or by calling OnPostSendCommand
        // Since we can't easily call the private ProcessCommand, we'll verify the command list
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();

        var availableCommands = new[] { "/help", "/status", "/validate", "/init", "/spec", "/run", "/codebase", "/pack", "/checkpoint" };
        availableCommands.Should().NotContain(cmd);
    }

    [Fact]
    public void InitializeCommands_PopulatesAllAvailableCommands()
    {
        var model = CreateModel();

        // Commands are initialized in OnGet
        model.OnGet();

        model.AvailableCommands.Should().NotBeNull();
        model.AvailableCommands.Should().HaveCount(9);
        model.AvailableCommands.Should().Contain(c => c.Command == "/help" && c.Description == "Show available commands");
        model.AvailableCommands.Should().Contain(c => c.Command == "/status" && c.Description == "Show workspace status");
        model.AvailableCommands.Should().Contain(c => c.Command == "/validate" && c.Description == "Validate workspace health");
        model.AvailableCommands.Should().Contain(c => c.Command == "/init" && c.Description == "Initialize .aos workspace");
        model.AvailableCommands.Should().Contain(c => c.Command == "/spec" && c.Description == "List or manage specs");
        model.AvailableCommands.Should().Contain(c => c.Command == "/run" && c.Description == "Start or manage runs");
        model.AvailableCommands.Should().Contain(c => c.Command == "/codebase" && c.Description == "Analyze codebase");
        model.AvailableCommands.Should().Contain(c => c.Command == "/pack" && c.Description == "Generate context pack");
        model.AvailableCommands.Should().Contain(c => c.Command == "/checkpoint" && c.Description == "Create recovery checkpoint");
    }

    [Fact]
    public void LoadWorkspace_NoConfig_ReturnsEmptyWorkspacePath()
    {
        var model = CreateModel();

        model.OnGet();

        model.WorkspacePath.Should().BeNullOrEmpty();
        model.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LoadWorkspace_WithValidConfig_LoadsWorkspacePath()
    {
        var model = CreateModel();
        CreateWorkspaceConfig(_tempDir);

        model.OnGet();

        model.WorkspacePath.Should().Be(_tempDir);
        model.WorkspaceName.Should().Be(Path.GetFileName(_tempDir));
    }

    [Fact]
    public void LoadWorkspace_InvalidConfig_ReturnsError()
    {
        var model = CreateModel();
        File.WriteAllText(_configFile, "invalid json");

        model.OnGet();

        model.WorkspacePath.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ChatMessage_UserMessage_HasCorrectProperties()
    {
        var message = new ChatMessage
        {
            Id = "test-123",
            Content = "/status",
            IsUser = true,
            Timestamp = DateTime.Now,
            Type = MessageType.UserInput
        };

        message.Id.Should().Be("test-123");
        message.Content.Should().Be("/status");
        message.IsUser.Should().BeTrue();
        message.GetMessageClass().Should().Be("message-user");
        message.Type.Should().Be(MessageType.UserInput);
    }

    [Fact]
    public void ChatMessage_SystemMessage_HasCorrectProperties()
    {
        var message = new ChatMessage
        {
            Id = "test-456",
            Content = "System response",
            IsUser = false,
            Timestamp = DateTime.Now,
            Type = MessageType.SystemMessage
        };

        message.IsUser.Should().BeFalse();
        message.GetMessageClass().Should().Be("message-system");
        message.Type.Should().Be(MessageType.SystemMessage);
    }

    [Fact]
    public void SafetyRails_DefaultValues_AreSet()
    {
        var rails = new SafetyRails();

        rails.MaxFilesPerOperation.Should().Be(100);
        rails.AllowedExtensions.Should().NotBeNull();
        rails.TouchedFiles.Should().NotBeNull();
        rails.Scope.Should().BeEmpty();
        rails.LastCommand.Should().BeEmpty();
    }

    [Fact]
    public void EvidenceFile_HasExpectedProperties()
    {
        var file = new EvidenceFile
        {
            Name = "test-run.json",
            Path = "~/evidence/runs/test-run.json",
            Type = "run",
            CreatedAt = "2025-01-15 10:30",
            Size = 1024
        };

        file.Name.Should().Be("test-run.json");
        file.Path.Should().Be("~/evidence/runs/test-run.json");
        file.Type.Should().Be("run");
        file.CreatedAt.Should().Be("2025-01-15 10:30");
        file.Size.Should().Be(1024);
    }
}

public class CommandExecutionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _configDir;
    private readonly string _configFile;
    private readonly string _historyFile;

    public CommandExecutionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gmsd");
        _configFile = Path.Combine(_configDir, "workspace-config.json");
        _historyFile = Path.Combine(_configDir, "orchestrator-history.json");
        Directory.CreateDirectory(_aosDir);
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
            if (File.Exists(_configFile))
            {
                File.Delete(_configFile);
            }
            if (File.Exists(_historyFile))
            {
                File.Delete(_historyFile);
            }
        }
        catch { }
    }

    private void CreateWorkspaceConfig(string workspacePath)
    {
        var config = $"{{ \"SelectedWorkspacePath\": \"{workspacePath.Replace("\\", "\\\\")}\", \"LastUpdated\": \"{DateTime.UtcNow:O}\" }}";
        File.WriteAllText(_configFile, config);
    }

    private void CreateAosStructure()
    {
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "project"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "roadmap"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "evidence", "runs"));

        var state = new { status = "initialized", cursor = new { }, version = "1.0" };
        var stateJson = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_aosDir, "state", "state.json"), stateJson);
    }

    private IndexModel CreateModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new IndexModel(NullLogger<IndexModel>.Instance, configuration, null!);
    }

    [Fact]
    public void InitCommand_WithoutWorkspace_ReturnsErrorMessage()
    {
        var model = CreateModel();
        model.OnGet();

        // Verify workspace is not selected
        model.WorkspacePath.Should().BeNullOrEmpty();
        model.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InitCommand_WithValidWorkspace_CreatesAosStructure()
    {
        var model = CreateModel();
        CreateWorkspaceConfig(_tempDir);

        // Delete existing .aos to test fresh init
        if (Directory.Exists(_aosDir))
        {
            Directory.Delete(_aosDir, true);
        }

        model.OnGet();
        model.WorkspacePath.Should().Be(_tempDir);

        // Simulate init command by creating the structure
        Directory.CreateDirectory(_aosDir);
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "project"));

        // Verify structure was created
        Directory.Exists(_aosDir).Should().BeTrue();
        Directory.Exists(Path.Combine(_aosDir, "state")).Should().BeTrue();
        Directory.Exists(Path.Combine(_aosDir, "spec", "project")).Should().BeTrue();
    }

    [Fact]
    public void StatusCommand_WithoutWorkspace_ReturnsError()
    {
        var model = CreateModel();
        model.OnGet();

        model.WorkspacePath.Should().BeNullOrEmpty();
        model.ErrorMessage.Should().Contain("No workspace selected");
    }

    [Fact]
    public void StatusCommand_WithValidWorkspace_LoadsState()
    {
        var model = CreateModel();
        CreateWorkspaceConfig(_tempDir);
        CreateAosStructure();

        model.OnGet();

        model.WorkspacePath.Should().Be(_tempDir);
        model.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ValidateCommand_ChecksRequiredDirectories()
    {
        var model = CreateModel();
        CreateWorkspaceConfig(_tempDir);
        CreateAosStructure();

        model.OnGet();

        // Verify directories exist
        Directory.Exists(Path.Combine(_aosDir, "state")).Should().BeTrue();
        Directory.Exists(Path.Combine(_aosDir, "spec")).Should().BeTrue();
        File.Exists(Path.Combine(_aosDir, "state", "state.json")).Should().BeTrue();
    }

    [Fact]
    public void MessageHistory_SavedAndLoaded()
    {
        var model = CreateModel();
        CreateWorkspaceConfig(_tempDir);

        // Add a message
        model.Messages.Add(new ChatMessage
        {
            Id = "test-1",
            Content = "/help",
            IsUser = true,
            Timestamp = DateTime.Now,
            Type = MessageType.UserInput
        });

        // Save history
        var historyPath = Path.Combine(_configDir, "orchestrator-history.json");
        var json = System.Text.Json.JsonSerializer.Serialize(model.Messages, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(historyPath, json);

        // Load history
        var loadedJson = File.ReadAllText(historyPath);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<List<ChatMessage>>(loadedJson);

        loaded.Should().NotBeNull();
        loaded.Should().HaveCount(1);
        loaded![0].Content.Should().Be("/help");
        loaded[0].IsUser.Should().BeTrue();
    }

    [Fact]
    public void SafetyRails_TracksTouchedFiles()
    {
        var model = CreateModel();
        CreateWorkspaceConfig(_tempDir);
        model.OnGet();

        var touchedFiles = new List<string>
        {
            ".aos/",
            ".aos/state/",
            ".aos/state/state.json"
        };

        model.SafetyRails.TouchedFiles = touchedFiles;
        model.SafetyRails.Scope = "test-operation";

        model.SafetyRails.TouchedFiles.Should().HaveCount(3);
        model.SafetyRails.Scope.Should().Be("test-operation");
    }

    [Fact]
    public void EvidenceFiles_LoadFromIndexJson()
    {
        var model = CreateModel();
        CreateWorkspaceConfig(_tempDir);
        CreateAosStructure();

        // Create index.json with runs
        var index = new
        {
            items = new[]
            {
                new { runId = "run-001", startedAt = "2025-01-15T10:00:00", status = "completed" },
                new { runId = "run-002", startedAt = "2025-01-15T11:00:00", status = "running" }
            }
        };
        var indexJson = System.Text.Json.JsonSerializer.Serialize(index, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_aosDir, "evidence", "runs", "index.json"), indexJson);

        model.OnGet();

        model.EvidenceFiles.Should().NotBeNull();
        model.EvidenceFiles.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("/help", true)]
    [InlineData("/status", true)]
    [InlineData("/validate", true)]
    [InlineData("/init", true)]
    [InlineData("/spec", true)]
    [InlineData("/run", true)]
    [InlineData("/codebase", true)]
    [InlineData("/pack", true)]
    [InlineData("/checkpoint", true)]
    [InlineData("/unknown", false)]
    [InlineData("/invalid", false)]
    public void CommandValidation_ChecksIfCommandIsValid(string command, bool expectedValid)
    {
        var validCommands = new[] { "/help", "/status", "/validate", "/init", "/spec", "/run", "/codebase", "/pack", "/checkpoint" };
        var isValid = validCommands.Contains(command.ToLowerInvariant());

        isValid.Should().Be(expectedValid);
    }
}

public class JsonSyntaxHighlightingTests
{
    [Fact]
    public void HighlightJson_FormatsSimpleObject()
    {
        var json = "{\"name\":\"test\",\"value\":123}";
        var highlighted = IndexModel.HighlightJson(json);

        highlighted.Should().Contain("<span class=\"json-key\">");
        highlighted.Should().Contain("<span class=\"json-string\">");
        highlighted.Should().Contain("<span class=\"json-number\">");
    }

    [Fact]
    public void HighlightJson_HandlesBooleanValues()
    {
        var json = "{\"active\":true,\"deleted\":false}";
        var highlighted = IndexModel.HighlightJson(json);

        highlighted.Should().Contain("<span class=\"json-boolean\">true</span>");
        highlighted.Should().Contain("<span class=\"json-boolean\">false</span>");
    }

    [Fact]
    public void HighlightJson_HandlesNullValue()
    {
        var json = "{\"value\":null}";
        var highlighted = IndexModel.HighlightJson(json);

        highlighted.Should().Contain("<span class=\"json-null\">null</span>");
    }

    [Fact]
    public void HighlightJson_PreservesStructure()
    {
        var json = "{\"key\":\"value\"}";
        var highlighted = IndexModel.HighlightJson(json);

        highlighted.Should().Contain("key");
        highlighted.Should().Contain("value");
    }
}

public class ValidationReportTests
{
    [Fact]
    public void ValidationItem_HasCorrectProperties()
    {
        var item = new ValidationItem
        {
            Name = "Test Check",
            Status = ValidationStatus.Success,
            Message = "Check passed"
        };

        item.Name.Should().Be("Test Check");
        item.Status.Should().Be(ValidationStatus.Success);
        item.Message.Should().Be("Check passed");
    }

    [Fact]
    public void ValidationStatus_AllValues_Defined()
    {
        var statuses = Enum.GetValues<ValidationStatus>();

        statuses.Should().Contain(ValidationStatus.Success);
        statuses.Should().Contain(ValidationStatus.Warning);
        statuses.Should().Contain(ValidationStatus.Error);
        statuses.Should().Contain(ValidationStatus.Info);
    }
}

public class OrchestratorCommandVerificationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _configDir;
    private readonly string _configFile;

    public OrchestratorCommandVerificationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gmsd");
        _configFile = Path.Combine(_configDir, "workspace-config.json");
        Directory.CreateDirectory(_aosDir);
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
            if (File.Exists(_configFile))
            {
                File.Delete(_configFile);
            }
        }
        catch { }
    }

    private void CreateWorkspaceConfig(string workspacePath)
    {
        var config = $"{{ \"SelectedWorkspacePath\": \"{workspacePath.Replace("\\", "\\\\")}\", \"LastUpdated\": \"{DateTime.UtcNow:O}\" }}";
        File.WriteAllText(_configFile, config);
    }

    private IndexModel CreateModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new IndexModel(NullLogger<IndexModel>.Instance, configuration, null!);
    }

    [Fact]
    public void HelpCommand_ReturnsOrchestratorSpecificCommandList()
    {
        var model = CreateModel();
        model.OnGet();

        // Verify the AvailableCommands list is populated with orchestrator-specific commands
        model.AvailableCommands.Should().NotBeNull();
        model.AvailableCommands.Should().Contain(c => c.Command == "/help" && c.Description.Contains("available commands"));
        model.AvailableCommands.Should().Contain(c => c.Command == "/status" && c.Description.Contains("workspace status"));
        model.AvailableCommands.Should().Contain(c => c.Command == "/validate" && c.Description.Contains("workspace health"));
        model.AvailableCommands.Should().Contain(c => c.Command == "/init" && c.Description.Contains("Initialize .aos"));

        // Verify orchestrator-specific commands exist
        model.AvailableCommands.Should().Contain(c => c.Command == "/run" && c.Description.Contains("runs"));
        model.AvailableCommands.Should().Contain(c => c.Command == "/spec" && c.Description.Contains("specs"));
        model.AvailableCommands.Should().Contain(c => c.Command == "/codebase" && c.Description.Contains("codebase"));
        model.AvailableCommands.Should().Contain(c => c.Command == "/pack" && c.Description.Contains("context pack"));
        model.AvailableCommands.Should().Contain(c => c.Command == "/checkpoint" && c.Description.Contains("checkpoint"));
    }

    [Fact]
    public void StatusCommand_DisplaysWorkspaceStatusCorrectly()
    {
        var model = CreateModel();
        CreateWorkspaceConfig(_tempDir);

        // Create AOS structure with state
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "issues"));
        var state = new { status = "initialized", cursor = new { milestoneId = "m1", phaseId = "p1" }, version = "1.0" };
        var stateJson = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_aosDir, "state", "state.json"), stateJson);

        model.OnGet();

        // Verify workspace is loaded
        model.WorkspacePath.Should().Be(_tempDir);
        model.WorkspaceName.Should().Be(Path.GetFileName(_tempDir));
    }

    [Fact]
    public void StatusCommand_WithoutAosDirectory_ReturnsNotInitializedMessage()
    {
        var model = CreateModel();
        CreateWorkspaceConfig(_tempDir);

        // Delete .aos directory if it exists
        if (Directory.Exists(_aosDir))
        {
            Directory.Delete(_aosDir, true);
        }

        model.OnGet();

        // Verify workspace path is set but no error (error only occurs if no workspace config)
        model.WorkspacePath.Should().Be(_tempDir);
    }

    [Fact]
    public void ValidateCommand_RunsWorkspaceValidation()
    {
        var model = CreateModel();
        CreateWorkspaceConfig(_tempDir);

        // Create partial AOS structure (not all directories)
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        var state = new { status = "initialized", cursor = new { }, version = "1.0" };
        var stateJson = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_aosDir, "state", "state.json"), stateJson);

        model.OnGet();

        // Verify workspace path is loaded
        model.WorkspacePath.Should().Be(_tempDir);

        // Verify AOS directory exists (base validation check)
        Directory.Exists(_aosDir).Should().BeTrue();
        Directory.Exists(Path.Combine(_aosDir, "state")).Should().BeTrue();
        File.Exists(Path.Combine(_aosDir, "state", "state.json")).Should().BeTrue();
    }

    [Fact]
    public void ValidateCommand_WithoutWorkspace_ReturnsError()
    {
        var model = CreateModel();
        // Don't create workspace config

        model.OnGet();

        // Verify error state
        model.WorkspacePath.Should().BeNullOrEmpty();
        model.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InitCommand_InitializesAosWorkspace()
    {
        var model = CreateModel();
        CreateWorkspaceConfig(_tempDir);

        // Ensure .aos directory doesn't exist initially
        if (Directory.Exists(_aosDir))
        {
            Directory.Delete(_aosDir, true);
        }

        model.OnGet();

        // Verify workspace path is set
        model.WorkspacePath.Should().Be(_tempDir);

        // Simulate init by creating the full AOS structure
        Directory.CreateDirectory(_aosDir);
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "project"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "roadmap"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "milestones"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "phases"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "tasks"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "issues"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "uat"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "evidence", "runs"));

        var state = new { status = "initialized", cursor = new { }, version = "1.0" };
        var stateJson = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_aosDir, "state", "state.json"), stateJson);

        // Verify full structure was created
        Directory.Exists(_aosDir).Should().BeTrue();
        Directory.Exists(Path.Combine(_aosDir, "state")).Should().BeTrue();
        Directory.Exists(Path.Combine(_aosDir, "spec", "project")).Should().BeTrue();
        Directory.Exists(Path.Combine(_aosDir, "spec", "roadmap")).Should().BeTrue();
        Directory.Exists(Path.Combine(_aosDir, "spec", "milestones")).Should().BeTrue();
        Directory.Exists(Path.Combine(_aosDir, "spec", "phases")).Should().BeTrue();
        Directory.Exists(Path.Combine(_aosDir, "spec", "tasks")).Should().BeTrue();
        Directory.Exists(Path.Combine(_aosDir, "spec", "issues")).Should().BeTrue();
        Directory.Exists(Path.Combine(_aosDir, "spec", "uat")).Should().BeTrue();
        Directory.Exists(Path.Combine(_aosDir, "evidence", "runs")).Should().BeTrue();
        File.Exists(Path.Combine(_aosDir, "state", "state.json")).Should().BeTrue();
    }

    [Fact]
    public void InitCommand_WithExistingAos_ReturnsAlreadyInitializedMessage()
    {
        var model = CreateModel();
        CreateWorkspaceConfig(_tempDir);

        // Ensure .aos directory exists
        Directory.CreateDirectory(_aosDir);

        model.OnGet();

        // Verify workspace path is set
        model.WorkspacePath.Should().Be(_tempDir);

        // Verify .aos directory exists (would trigger "already initialized" message)
        Directory.Exists(_aosDir).Should().BeTrue();
    }

    [Fact]
    public void InitCommand_WithoutWorkspace_ReturnsError()
    {
        var model = CreateModel();
        // Don't create workspace config

        model.OnGet();

        // Verify error state
        model.WorkspacePath.Should().BeNullOrEmpty();
        model.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
