using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gmsd.Web.Tests.Pages.Issues;

public class IssuesPageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _stateDir;

    public IssuesPageTests()
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

    private Gmsd.Web.Pages.Issues.IndexModel CreateIndexModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new Gmsd.Web.Pages.Issues.IndexModel(
            NullLogger<Gmsd.Web.Pages.Issues.IndexModel>.Instance,
            configuration);
    }

    private Gmsd.Web.Pages.Issues.DetailsModel CreateDetailsModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new Gmsd.Web.Pages.Issues.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Issues.DetailsModel>.Instance,
            configuration);
    }

    private void CreateIssuesJson()
    {
        var issuesDir = Path.Combine(_specDir, "issues");
        Directory.CreateDirectory(issuesDir);
        var issuesFile = Path.Combine(issuesDir, "issues.json");
        var json = @"{
            ""schemaVersion"": 1,
            ""issues"": [
                {
                    ""id"": ""ISSUE-001"",
                    ""title"": ""Test Issue 1"",
                    ""description"": ""This is a test issue"",
                    ""status"": ""Open"",
                    ""severity"": ""High"",
                    ""type"": ""Bug"",
                    ""taskId"": ""TASK-001""
                },
                {
                    ""id"": ""ISSUE-002"",
                    ""title"": ""Test Issue 2"",
                    ""description"": ""Another test issue"",
                    ""status"": ""InProgress"",
                    ""severity"": ""Medium"",
                    ""type"": ""Task"",
                    ""taskId"": ""TASK-002""
                }
            ]
        }";
        File.WriteAllText(issuesFile, json);
    }

    [Fact]
    public void IndexModel_OnGet_NoWorkspace_SetsErrorMessage()
    {
        var model = CreateIndexModel();

        model.OnGet();

        model.ErrorMessage.Should().Contain("No workspace selected");
    }

    [Fact]
    public void IndexModel_OnGet_WithWorkspace_LoadsIssues()
    {
        var model = CreateIndexModel();
        CreateWorkspaceConfig();
        CreateIssuesJson();

        model.OnGet();

        model.Issues.Should().HaveCount(2);
    }

    [Fact]
    public void IndexModel_OnGet_FiltersByStatus()
    {
        var model = CreateIndexModel();
        CreateWorkspaceConfig();
        CreateIssuesJson();

        model.FilterStatus = "Open";
        model.OnGet();

        model.Issues.Should().Contain(i => i.Status.ToString() == "Open");
    }

    [Fact]
    public void IndexModel_Properties_AreInitialized()
    {
        var model = CreateIndexModel();

        model.Issues.Should().NotBeNull();
    }

    [Fact]
    public void DetailsModel_OnGet_ValidId_LoadsIssue()
    {
        var model = CreateDetailsModel();
        CreateWorkspaceConfig();
        CreateIssuesJson();

        model.OnGet("ISSUE-001");

        model.Issue.Should().NotBeNull();
        model.Issue!.Id.Should().Be("ISSUE-001");
        model.Issue.Title.Should().Be("Test Issue 1");
    }

    [Fact]
    public void DetailsModel_OnGet_InvalidId_ShowsNotFound()
    {
        var model = CreateDetailsModel();
        CreateWorkspaceConfig();
        CreateIssuesJson();

        model.OnGet("ISSUE-999");

        model.NotFoundMessage.Should().NotBeNull();
    }

    [Fact]
    public void DetailsModel_Properties_AreInitialized()
    {
        var model = CreateDetailsModel();

        model.Issue.Should().BeNull();
    }
}
