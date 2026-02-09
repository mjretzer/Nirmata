using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gmsd.Web.Tests.Pages.Milestones;

public class MilestonesPageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _stateDir;

    public MilestonesPageTests()
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

    private Gmsd.Web.Pages.Milestones.IndexModel CreateIndexModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new Gmsd.Web.Pages.Milestones.IndexModel(
            NullLogger<Gmsd.Web.Pages.Milestones.IndexModel>.Instance,
            configuration);
    }

    private Gmsd.Web.Pages.Milestones.DetailsModel CreateDetailsModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new Gmsd.Web.Pages.Milestones.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Milestones.DetailsModel>.Instance,
            configuration);
    }

    private void CreateRoadmapJson()
    {
        var roadmapFile = Path.Combine(_specDir, "roadmap.json");
        var json = @"{
            ""schemaVersion"": 1,
            ""roadmap"": {
                ""title"": ""Test Roadmap"",
                ""items"": [
                    {""id"": ""MS-001"", ""title"": ""Milestone 1"", ""kind"": ""milestone""},
                    {""id"": ""PH-001"", ""title"": ""Phase 1"", ""kind"": ""phase"", ""milestoneId"": ""MS-001""},
                    {""id"": ""PH-002"", ""title"": ""Phase 2"", ""kind"": ""phase"", ""milestoneId"": ""MS-001""}
                ]
            }
        }";
        File.WriteAllText(roadmapFile, json);
    }

    [Fact]
    public void IndexModel_OnGet_NoWorkspace_SetsErrorMessage()
    {
        var model = CreateIndexModel();

        model.OnGet();

        model.ErrorMessage.Should().Contain("No workspace selected");
    }

    [Fact]
    public void IndexModel_OnGet_WithWorkspace_LoadsMilestones()
    {
        var model = CreateIndexModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.OnGet();

        model.Milestones.Should().HaveCount(1);
        model.Milestones.First().Id.Should().Be("MS-001");
        model.Milestones.First().Name.Should().Be("Milestone 1");
    }

    [Fact]
    public void IndexModel_OnPostCreate_ValidName_AddsMilestone()
    {
        var model = CreateIndexModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.NewMilestoneName = "New Test Milestone";
        var result = model.OnPostCreate();

        result.Should().BeOfType<PageResult>();
        model.SuccessMessage.Should().Contain("created successfully");
    }

    [Fact]
    public void IndexModel_OnPostCreate_EmptyName_ShowsError()
    {
        var model = CreateIndexModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.NewMilestoneName = "";
        var result = model.OnPostCreate();

        result.Should().BeOfType<PageResult>();
        model.ErrorMessage.Should().Contain("required");
    }

    [Fact]
    public void DetailsModel_OnGet_ValidId_LoadsMilestone()
    {
        var model = CreateDetailsModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.OnGet("MS-001");

        model.Milestone.Should().NotBeNull();
        model.Milestone!.Id.Should().Be("MS-001");
        model.Milestone.Name.Should().Be("Milestone 1");
        model.Milestone.Phases.Should().HaveCount(2);
    }

    [Fact]
    public void DetailsModel_OnGet_InvalidId_ShowsNotFound()
    {
        var model = CreateDetailsModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.OnGet("MS-999");

        model.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public void DetailsModel_Properties_AreInitialized()
    {
        var model = CreateDetailsModel();

        model.Milestone.Should().BeNull();
    }
}
