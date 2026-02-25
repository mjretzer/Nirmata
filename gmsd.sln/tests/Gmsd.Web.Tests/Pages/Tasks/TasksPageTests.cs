using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gmsd.Web.Tests.Pages.Tasks;

public class TasksPageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _stateDir;

    public TasksPageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _specDir = Path.Combine(_aosDir, "spec");
        _stateDir = Path.Combine(_aosDir, "state");
        Directory.CreateDirectory(_specDir);
        Directory.CreateDirectory(_stateDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    private void CreateWorkspaceConfig()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var configDir = Path.Combine(appData, "Gmsd");
        Directory.CreateDirectory(configDir);
        var configFile = Path.Combine(configDir, "workspace-config.json");
        var json = $"{{\"SelectedWorkspacePath\": \"{_tempDir.Replace("\\", "\\\\")}\"}}";
        File.WriteAllText(configFile, json);
    }

    private Gmsd.Web.Pages.Tasks.IndexModel CreateIndexModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new Gmsd.Web.Pages.Tasks.IndexModel(
            NullLogger<Gmsd.Web.Pages.Tasks.IndexModel>.Instance,
            configuration);
    }

    private Gmsd.Web.Pages.Tasks.DetailsModel CreateDetailsModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new Gmsd.Web.Pages.Tasks.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Tasks.DetailsModel>.Instance,
            configuration);
    }

    private void CreateTasksJson()
    {
        var tasksDir = Path.Combine(_specDir, "tasks");
        Directory.CreateDirectory(tasksDir);
        var tasksFile = Path.Combine(tasksDir, "tasks.json");
        var json = @"{
            ""schemaVersion"": 1,
            ""tasks"": [
                {
                    ""id"": ""TASK-001"",
                    ""title"": ""Test Task 1"",
                    ""phaseId"": ""PH-001"",
                    ""status"": ""Draft""
                },
                {
                    ""id"": ""TASK-002"",
                    ""title"": ""Test Task 2"",
                    ""phaseId"": ""PH-001"",
                    ""status"": ""InProgress""
                }
            ]
        }";
        File.WriteAllText(tasksFile, json);
    }

    [Fact]
    public void IndexModel_OnGet_NoWorkspace_SetsErrorMessage()
    {
        var model = CreateIndexModel();

        model.OnGet();

        model.ErrorMessage.Should().Contain("No workspace selected");
    }

    [Fact]
    public void IndexModel_OnGet_WithWorkspace_LoadsTasks()
    {
        var model = CreateIndexModel();
        CreateWorkspaceConfig();
        CreateTasksJson();

        model.OnGet();

        model.Tasks.Should().HaveCount(2);
    }

    [Fact]
    public void IndexModel_Properties_AreInitialized()
    {
        var model = CreateIndexModel();

        model.Tasks.Should().NotBeNull();
    }

    [Fact]
    public void DetailsModel_OnGet_ValidId_LoadsTask()
    {
        var model = CreateDetailsModel();
        CreateWorkspaceConfig();
        CreateTasksJson();

        model.OnGet("TASK-001");

        model.Task.Should().NotBeNull();
        model.Task!.Id.Should().Be("TASK-001");
        model.Task.Title.Should().Be("Test Task 1");
    }

    [Fact]
    public void DetailsModel_OnGet_InvalidId_ShowsNotFound()
    {
        var model = CreateDetailsModel();
        CreateWorkspaceConfig();
        CreateTasksJson();

        model.OnGet("TASK-999");

        model.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public void DetailsModel_Properties_AreInitialized()
    {
        var model = CreateDetailsModel();

        model.Task.Should().BeNull();
    }

    [Fact]
    public void DetailsModel_LoadsSpecArtifacts_IfPresent()
    {
        var model = CreateDetailsModel();
        CreateWorkspaceConfig();
        CreateTasksJson();
        
        // Create task.json
        var taskDir = Path.Combine(_specDir, "tasks", "TASK-001");
        Directory.CreateDirectory(taskDir);
        var taskJson = @"{ ""id"": ""TASK-001"", ""title"": ""Test Task"", ""description"": ""Test description"" }";
        File.WriteAllText(Path.Combine(taskDir, "task.json"), taskJson);
        
        // Create plan.json
        var planJson = @"{ ""steps"": [{""description"": ""Step 1""}] }";
        File.WriteAllText(Path.Combine(taskDir, "plan.json"), planJson);

        model.OnGet("TASK-001");

        model.TaskJson.Should().NotBeNullOrEmpty();
        model.PlanJson.Should().NotBeNullOrEmpty();
    }
}
