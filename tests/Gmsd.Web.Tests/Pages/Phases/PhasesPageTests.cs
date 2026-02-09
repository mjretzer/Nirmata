using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gmsd.Web.Tests.Pages.Phases;

public class PhasesPageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _stateDir;

    public PhasesPageTests()
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

    private Gmsd.Web.Pages.Phases.IndexModel CreateIndexModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new Gmsd.Web.Pages.Phases.IndexModel(
            NullLogger<Gmsd.Web.Pages.Phases.IndexModel>.Instance,
            configuration);
    }

    private Gmsd.Web.Pages.Phases.DetailsModel CreateDetailsModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new Gmsd.Web.Pages.Phases.DetailsModel(
            NullLogger<Gmsd.Web.Pages.Phases.DetailsModel>.Instance,
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
    public void IndexModel_OnGet_WithWorkspace_LoadsPhases()
    {
        var model = CreateIndexModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.OnGet();

        model.Phases.Should().HaveCount(2);
        model.Phases.First().Id.Should().Be("PH-001");
        model.Phases.First().Name.Should().Be("Phase 1");
    }

    [Fact]
    public void IndexModel_Properties_AreInitialized()
    {
        var model = CreateIndexModel();

        model.Phases.Should().NotBeNull();
    }

    [Fact]
    public void DetailsModel_OnGet_ValidId_LoadsPhase()
    {
        var model = CreateDetailsModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.OnGet("PH-001");

        model.Phase.Should().NotBeNull();
        model.Phase!.Id.Should().Be("PH-001");
        model.Phase.Name.Should().Be("Phase 1");
    }

    [Fact]
    public void DetailsModel_OnGet_InvalidId_ShowsNotFound()
    {
        var model = CreateDetailsModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.OnGet("PH-999");

        model.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public void DetailsModel_OnPostAddAssumption_ValidInput_AddsAssumption()
    {
        var model = CreateDetailsModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        // First load the phase
        model.OnGet("PH-001");
        
        // Then add an assumption
        model.NewAssumption = "Test assumption";
        var result = model.OnPostAddAssumption("PH-001");

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public void DetailsModel_OnPostAddAssumption_EmptyInput_ShowsError()
    {
        var model = CreateDetailsModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.NewAssumption = "";
        var result = model.OnPostAddAssumption("PH-001");

        result.Should().BeOfType<PageResult>();
        model.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public void DetailsModel_Properties_AreInitialized()
    {
        var model = CreateDetailsModel();

        model.Phase.Should().BeNull();
    }
}
