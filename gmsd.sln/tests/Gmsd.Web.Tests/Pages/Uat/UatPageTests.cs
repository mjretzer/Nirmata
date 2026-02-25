using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gmsd.Web.Tests.Pages.Uat;

public class UatPageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _stateDir;

    public UatPageTests()
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

    private Gmsd.Web.Pages.Uat.IndexModel CreateIndexModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new Gmsd.Web.Pages.Uat.IndexModel(
            NullLogger<Gmsd.Web.Pages.Uat.IndexModel>.Instance,
            configuration);
    }

    private Gmsd.Web.Pages.Uat.VerifyModel CreateVerifyModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new Gmsd.Web.Pages.Uat.VerifyModel(
            NullLogger<Gmsd.Web.Pages.Uat.VerifyModel>.Instance,
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
                    ""status"": ""Completed"",
                    ""acceptanceCriteria"": [""Criterion 1"", ""Criterion 2""]
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
    public void IndexModel_OnGet_WithWorkspace_LoadsUatSessions()
    {
        var model = CreateIndexModel();
        CreateWorkspaceConfig();
        CreateTasksJson();

        model.OnGet();

        model.UatSessions.Should().NotBeNull();
    }

    [Fact]
    public void IndexModel_Properties_AreInitialized()
    {
        var model = CreateIndexModel();

        model.UatSessions.Should().NotBeNull();
    }

    [Fact]
    public void VerifyModel_Properties_AreInitialized()
    {
        var model = CreateVerifyModel();

        model.Checks.Should().NotBeNull();
    }
}
