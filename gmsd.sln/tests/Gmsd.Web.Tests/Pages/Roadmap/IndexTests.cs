using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gmsd.Web.Tests.Pages.Roadmap;

public class RoadmapPageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _stateDir;

    public RoadmapPageTests()
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

    private Gmsd.Web.Pages.Roadmap.IndexModel CreateModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new Gmsd.Web.Pages.Roadmap.IndexModel(
            NullLogger<Gmsd.Web.Pages.Roadmap.IndexModel>.Instance, 
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
                    {""id"": ""PH-001"", ""title"": ""Phase 1"", ""kind"": ""phase""},
                    {""id"": ""PH-002"", ""title"": ""Phase 2"", ""kind"": ""phase""}
                ]
            }
        }";
        File.WriteAllText(roadmapFile, json);
    }

    private void CreateStateJson()
    {
        var stateFile = Path.Combine(_stateDir, "state.json");
        var json = @"{
            ""cursor"": {
                ""phaseId"": ""PH-001"",
                ""milestoneId"": ""MS-001""
            },
            ""phases"": {
                ""PH-001"": {""status"": ""inprogress""},
                ""PH-002"": {""status"": ""planned""}
            }
        }";
        File.WriteAllText(stateFile, json);
    }

    [Fact]
    public void OnGet_NoWorkspace_SetsErrorMessage()
    {
        var model = CreateModel();

        model.OnGet();

        model.ErrorMessage.Should().Contain("No workspace selected");
    }

    [Fact]
    public void OnGet_WithWorkspace_LoadsRoadmap()
    {
        var model = CreateModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.OnGet();

        model.Roadmap.Milestones.Should().HaveCount(1);
        model.Roadmap.Milestones.First().Phases.Should().HaveCount(2);
    }

    [Fact]
    public void OnGet_WithState_AppliesCursorAndStatus()
    {
        var model = CreateModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();
        CreateStateJson();

        model.OnGet();

        model.Roadmap.CurrentPhaseId.Should().Be("PH-001");
        model.Roadmap.CurrentMilestoneId.Should().Be("MS-001");
    }

    [Fact]
    public void OnGet_NoRoadmap_SetsErrorMessage()
    {
        var model = CreateModel();
        CreateWorkspaceConfig();

        model.OnGet();

        model.ErrorMessage.Should().Contain("Roadmap not found");
    }

    [Fact]
    public void OnPostAddPhase_ValidName_AddsPhase()
    {
        var model = CreateModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.NewPhaseName = "New Test Phase";
        var result = model.OnPostAddPhase();

        result.Should().BeOfType<PageResult>();
        model.SuccessMessage.Should().Contain("added successfully");
        
        // Verify file was updated
        var roadmapFile = Path.Combine(_specDir, "roadmap.json");
        var content = File.ReadAllText(roadmapFile);
        content.Should().Contain("New Test Phase");
    }

    [Fact]
    public void OnPostAddPhase_EmptyName_ShowsError()
    {
        var model = CreateModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.NewPhaseName = "";
        var result = model.OnPostAddPhase();

        result.Should().BeOfType<PageResult>();
        model.ErrorMessage.Should().Contain("required");
    }

    [Fact]
    public void OnPostInsertPhase_ValidInput_InsertsPhase()
    {
        var model = CreateModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.NewPhaseName = "Inserted Phase";
        model.InsertAfterPhaseId = "PH-001";
        var result = model.OnPostInsertPhase();

        result.Should().BeOfType<PageResult>();
        model.SuccessMessage.Should().Contain("inserted successfully");
    }

    [Fact]
    public void OnPostInsertPhase_MissingAfterId_ShowsError()
    {
        var model = CreateModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();

        model.NewPhaseName = "Test Phase";
        model.InsertAfterPhaseId = null;
        var result = model.OnPostInsertPhase();

        result.Should().BeOfType<PageResult>();
        model.ErrorMessage.Should().Contain("insert after");
    }

    [Fact]
    public void OnPostRemovePhase_ValidId_RemovesPhase()
    {
        var model = CreateModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();
        CreateStateJson();

        model.RemovePhaseId = "PH-002"; // Not the current phase, not completed
        var result = model.OnPostRemovePhase();

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public void OnPostRemovePhase_CurrentPhase_ShowsError()
    {
        var model = CreateModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();
        CreateStateJson();

        model.RemovePhaseId = "PH-001"; // Current phase
        var result = model.OnPostRemovePhase();

        result.Should().BeOfType<PageResult>();
        model.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public void RoadmapProperties_AreInitialized()
    {
        var model = CreateModel();

        model.Roadmap.Should().NotBeNull();
        model.Roadmap.Milestones.Should().NotBeNull();
        model.Roadmap.Warnings.Should().NotBeNull();
    }

    [Fact]
    public void OnGet_GeneratesAlignmentWarnings_WhenCursorMissing()
    {
        var model = CreateModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();
        // Don't create state file - cursor is missing

        model.OnGet();

        model.Roadmap.Warnings.Should().Contain(w => w.Type == "Cursor");
    }

    [Fact]
    public void OnGet_GeneratesAlignmentWarnings_WhenPhaseNotInRoadmap()
    {
        var model = CreateModel();
        CreateWorkspaceConfig();
        CreateRoadmapJson();
        
        // Create state with non-existent phase
        var stateFile = Path.Combine(_stateDir, "state.json");
        File.WriteAllText(stateFile, @"{""cursor"": {""phaseId"": ""PH-999""}}");

        model.OnGet();

        model.Roadmap.Warnings.Should().Contain(w => w.Type == "Alignment");
    }
}
