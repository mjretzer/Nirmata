using FluentAssertions;
using Gmsd.Web.Pages.Specs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Gmsd.Web.Tests.Pages.Specs;

public class ProjectSpecValidationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _schemaDir;
    private readonly string _sqliteDir;

    public ProjectSpecValidationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _specDir = Path.Combine(_aosDir, "spec");
        _schemaDir = Path.Combine(_aosDir, "schemas");
        _sqliteDir = Path.Combine(_tempDir, "sqllitedb");
        Directory.CreateDirectory(_specDir);
        Directory.CreateDirectory(_schemaDir);
        Directory.CreateDirectory(_sqliteDir);
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

    private void CreateWorkspaceFile()
    {
        var workspaceFile = Path.Combine(_sqliteDir, "workspace.txt");
        File.WriteAllText(workspaceFile, _tempDir);
    }

    private Mock<IWebHostEnvironment> CreateMockEnvironment()
    {
        var mock = new Mock<IWebHostEnvironment>();
        mock.Setup(m => m.ContentRootPath).Returns(_tempDir);
        return mock;
    }

    private ProjectModel CreateProjectModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = CreateMockEnvironment().Object;
        return new ProjectModel(NullLogger<ProjectModel>.Instance, configuration, environment);
    }

    [Fact]
    public void ProjectModel_Properties_AreInitialized()
    {
        var model = CreateProjectModel();

        model.ProjectName.Should().NotBeNull();
        model.ProjectDescription.Should().NotBeNull();
        model.Constraints.Should().NotBeNull();
        model.SuccessCriteria.Should().NotBeNull();
        model.ValidationErrors.Should().NotBeNull();
        model.IsValid.Should().BeTrue();
        model.EditMode.Should().Be("form");
    }

    [Fact]
    public void OnGet_NoWorkspace_SetsErrorMessage()
    {
        var model = CreateProjectModel();

        model.OnGet();

        model.ErrorMessage.Should().Contain("No workspace selected");
    }

    [Fact]
    public void OnGet_WithWorkspace_CreatesDefaultProjectJson()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        model.OnGet();

        var projectFile = Path.Combine(_tempDir, ".aos", "spec", "project.json");
        File.Exists(projectFile).Should().BeTrue();
        var content = File.ReadAllText(projectFile);
        content.Should().Contain("schemaVersion");
    }

    [Fact]
    public void OnGet_LoadsExistingProjectJson()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        var projectFile = Path.Combine(_specDir, "project.json");
        var json = @"{
            ""schemaVersion"": 1,
            ""project"": {
                ""name"": ""Test Project"",
                ""description"": ""A test project"",
                ""constraints"": [""Budget: $10k"", ""Timeline: 3 months""],
                ""successCriteria"": [""All tests pass"", ""Performance > 1000 rps""]
            }
        }";
        File.WriteAllText(projectFile, json);

        model.OnGet();

        model.ProjectName.Should().Be("Test Project");
        model.ProjectDescription.Should().Be("A test project");
        model.Constraints.Should().Contain("Budget: $10k");
        model.SuccessCriteria.Should().Contain("All tests pass");
    }

    [Fact]
    public void OnGet_InvalidJson_SetsErrorMessage()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        var projectFile = Path.Combine(_specDir, "project.json");
        File.WriteAllText(projectFile, "{invalid json}");

        model.OnGet();

        model.ErrorMessage.Should().Contain("Invalid JSON");
        model.IsValid.Should().BeFalse();
    }

    [Fact]
    public void OnGet_SetsDiskPath()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        model.OnGet();

        model.DiskPath.Should().StartWith("file://");
        model.DiskPath.Should().Contain("project.json");
    }
}

public class ProjectSpecValidationLogicTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _schemaDir;
    private readonly string _sqliteDir;

    public ProjectSpecValidationLogicTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _specDir = Path.Combine(_aosDir, "spec");
        _schemaDir = Path.Combine(_aosDir, "schemas");
        _sqliteDir = Path.Combine(_tempDir, "sqllitedb");
        Directory.CreateDirectory(_specDir);
        Directory.CreateDirectory(_schemaDir);
        Directory.CreateDirectory(_sqliteDir);
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

    private void CreateWorkspaceFile()
    {
        var workspaceFile = Path.Combine(_sqliteDir, "workspace.txt");
        File.WriteAllText(workspaceFile, _tempDir);
    }

    private Mock<IWebHostEnvironment> CreateMockEnvironment()
    {
        var mock = new Mock<IWebHostEnvironment>();
        mock.Setup(m => m.ContentRootPath).Returns(_tempDir);
        return mock;
    }

    private ProjectModel CreateProjectModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = CreateMockEnvironment().Object;
        return new ProjectModel(NullLogger<ProjectModel>.Instance, configuration, environment);
    }

    [Fact]
    public void OnPostValidate_ValidFormData_ReturnsValid()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        var result = model.OnPostValidate(
            "Test Project",
            "A test description",
            "Constraint 1\nConstraint 2",
            "Criteria 1\nCriteria 2"
        );

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value as dynamic;
        ((bool)value!.valid).Should().BeTrue();
    }

    [Fact]
    public void OnPostValidate_MissingName_ReturnsInvalid()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        var result = model.OnPostValidate(
            "",
            "A test description",
            "",
            ""
        );

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value as dynamic;
        ((bool)value!.valid).Should().BeFalse();
    }

    [Fact]
    public void OnPostValidate_MissingDescription_ReturnsInvalid()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        var result = model.OnPostValidate(
            "Test Project",
            "",
            "",
            ""
        );

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value as dynamic;
        ((bool)value!.valid).Should().BeFalse();
    }

    [Fact]
    public void OnPostValidateRaw_ValidJson_ReturnsValid()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        var result = model.OnPostValidateRaw(new ProjectModel.ValidateRequest 
        { 
            Content = @"{""schemaVersion"": 1, ""project"": {""name"": ""Test"", ""description"": ""Desc""}}" 
        });

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value as dynamic;
        ((bool)value!.valid).Should().BeTrue();
    }

    [Fact]
    public void OnPostValidateRaw_InvalidJson_ReturnsInvalid()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        var result = model.OnPostValidateRaw(new ProjectModel.ValidateRequest 
        { 
            Content = "{invalid json}" 
        });

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value as dynamic;
        ((bool)value!.valid).Should().BeFalse();
    }

    [Fact]
    public void OnPostValidateRaw_MissingSchemaVersion_ReturnsInvalid()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        var result = model.OnPostValidateRaw(new ProjectModel.ValidateRequest 
        { 
            Content = @"{""project"": {""name"": ""Test"", ""description"": ""Desc""}}" 
        });

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value as dynamic;
        ((bool)value!.valid).Should().BeFalse();
    }

    [Fact]
    public void OnPostValidateRaw_MissingProject_ReturnsInvalid()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        var result = model.OnPostValidateRaw(new ProjectModel.ValidateRequest 
        { 
            Content = @"{""schemaVersion"": 1}" 
        });

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value as dynamic;
        ((bool)value!.valid).Should().BeFalse();
    }
}

public class ProjectSpecPersistenceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _schemaDir;
    private readonly string _sqliteDir;

    public ProjectSpecPersistenceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _specDir = Path.Combine(_aosDir, "spec");
        _schemaDir = Path.Combine(_aosDir, "schemas");
        _sqliteDir = Path.Combine(_tempDir, "sqllitedb");
        Directory.CreateDirectory(_specDir);
        Directory.CreateDirectory(_schemaDir);
        Directory.CreateDirectory(_sqliteDir);
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

    private void CreateWorkspaceFile()
    {
        var workspaceFile = Path.Combine(_sqliteDir, "workspace.txt");
        File.WriteAllText(workspaceFile, _tempDir);
    }

    private Mock<IWebHostEnvironment> CreateMockEnvironment()
    {
        var mock = new Mock<IWebHostEnvironment>();
        mock.Setup(m => m.ContentRootPath).Returns(_tempDir);
        return mock;
    }

    private ProjectModel CreateProjectModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = CreateMockEnvironment().Object;
        return new ProjectModel(NullLogger<ProjectModel>.Instance, configuration, environment);
    }

    [Fact]
    public void OnPost_FormMode_SavesProjectJson()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        model.EditMode = "form";
        model.ProjectName = "My Project";
        model.ProjectDescription = "My Description";
        model.Constraints = "Budget: $5k\nTimeline: 2 weeks";
        model.SuccessCriteria = "Tests pass\nCode coverage > 80%";

        var result = model.OnPost();

        result.Should().BeOfType<RedirectToPageResult>();
        var projectFile = Path.Combine(_tempDir, ".aos", "spec", "project.json");
        File.Exists(projectFile).Should().BeTrue();
        
        var content = File.ReadAllText(projectFile);
        content.Should().Contain("My Project");
        content.Should().Contain("My Description");
        content.Should().Contain("Budget: $5k");
        content.Should().Contain("Tests pass");
    }

    [Fact]
    public void OnPost_RawMode_SavesRawContent()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        model.EditMode = "raw";
        model.RawContent = @"{
            ""schemaVersion"": 1,
            ""project"": {
                ""name"": ""Raw Project"",
                ""description"": ""Raw Description""
            }
        }";

        var result = model.OnPost();

        result.Should().BeOfType<RedirectToPageResult>();
        var projectFile = Path.Combine(_tempDir, ".aos", "spec", "project.json");
        var content = File.ReadAllText(projectFile);
        content.Should().Contain("Raw Project");
    }

    [Fact]
    public void OnPost_InvalidJson_DoesNotSave()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        // Create initial valid file
        var projectFile = Path.Combine(_specDir, "project.json");
        var originalContent = @"{""schemaVersion"": 1, ""project"": {""name"": ""Original"", ""description"": ""Original Desc""}}";
        File.WriteAllText(projectFile, originalContent);

        model.EditMode = "raw";
        model.RawContent = "{invalid json}";

        var result = model.OnPost();

        result.Should().BeOfType<RedirectToPageResult>();
        var content = File.ReadAllText(projectFile);
        content.Should().Be(originalContent); // Should not have changed
    }

    [Fact]
    public void OnPost_NoWorkspace_RedirectsWithError()
    {
        var model = CreateProjectModel();

        var result = model.OnPost();

        result.Should().BeOfType<RedirectToPageResult>();
        var errorMessage = model.TempData["ErrorMessage"]?.ToString();
        errorMessage.Should().NotBeNull();
        errorMessage.Should().Contain("No workspace selected");
    }

    [Fact]
    public void OnPost_UpdatesExistingFile()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        // Create initial file
        var projectFile = Path.Combine(_specDir, "project.json");
        File.WriteAllText(projectFile, @"{""schemaVersion"": 1, ""project"": {""name"": ""Old Name"", ""description"": ""Old Desc""}}");

        model.EditMode = "form";
        model.ProjectName = "New Name";
        model.ProjectDescription = "New Description";

        var result = model.OnPost();

        result.Should().BeOfType<RedirectToPageResult>();
        var content = File.ReadAllText(projectFile);
        content.Should().Contain("New Name");
        content.Should().NotContain("Old Name");
    }
}

public class ProjectSpecImportExportTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _sqliteDir;

    public ProjectSpecImportExportTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _specDir = Path.Combine(_aosDir, "spec");
        _sqliteDir = Path.Combine(_tempDir, "sqllitedb");
        Directory.CreateDirectory(_specDir);
        Directory.CreateDirectory(_sqliteDir);
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

    private void CreateWorkspaceFile()
    {
        var workspaceFile = Path.Combine(_sqliteDir, "workspace.txt");
        File.WriteAllText(workspaceFile, _tempDir);
    }

    private Mock<IWebHostEnvironment> CreateMockEnvironment()
    {
        var mock = new Mock<IWebHostEnvironment>();
        mock.Setup(m => m.ContentRootPath).Returns(_tempDir);
        return mock;
    }

    private ProjectModel CreateProjectModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = CreateMockEnvironment().Object;
        return new ProjectModel(NullLogger<ProjectModel>.Instance, configuration, environment);
    }

    [Fact]
    public void OnPostExport_FormData_ReturnsJsonContent()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        var result = model.OnPostExport(new ProjectModel.ExportRequest
        {
            ProjectName = "Export Test",
            ProjectDescription = "Export Description",
            Constraints = "C1\nC2",
            SuccessCriteria = "S1\nS2"
        });

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value as dynamic;
        ((bool)value!.success).Should().BeTrue();
        ((string)value!.content).Should().Contain("Export Test");
        ((string)value!.content).Should().Contain("schemaVersion");
        ((string)value!.fileName).Should().Contain("export_test");
    }

    [Fact]
    public void OnPostExport_RawContent_ReturnsJsonContent()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        var result = model.OnPostExport(new ProjectModel.ExportRequest
        {
            RawContent = @"{""test"": ""value""}"
        });

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value as dynamic;
        ((bool)value!.success).Should().BeTrue();
        ((string)value!.content).Should().Be(@"{""test"": ""value""}");
        ((string)value!.fileName).Should().Be("project.json");
    }

    [Fact]
    public void OnPostExport_NoContent_ReturnsError()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        var result = model.OnPostExport(new ProjectModel.ExportRequest {});

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value as dynamic;
        ((bool)value!.success).Should().BeFalse();
    }
}

public class ProjectSpecInterviewModeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _sqliteDir;

    public ProjectSpecInterviewModeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _specDir = Path.Combine(_aosDir, "spec");
        _sqliteDir = Path.Combine(_tempDir, "sqllitedb");
        Directory.CreateDirectory(_specDir);
        Directory.CreateDirectory(_sqliteDir);
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

    private void CreateWorkspaceFile()
    {
        var workspaceFile = Path.Combine(_sqliteDir, "workspace.txt");
        File.WriteAllText(workspaceFile, _tempDir);
    }

    private Mock<IWebHostEnvironment> CreateMockEnvironment()
    {
        var mock = new Mock<IWebHostEnvironment>();
        mock.Setup(m => m.ContentRootPath).Returns(_tempDir);
        return mock;
    }

    private ProjectModel CreateProjectModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = CreateMockEnvironment().Object;
        return new ProjectModel(NullLogger<ProjectModel>.Instance, configuration, environment);
    }

    [Fact]
    public void OnPost_InterviewData_SavesCorrectly()
    {
        var model = CreateProjectModel();
        CreateWorkspaceFile();

        // Simulate interview answers applied to form
        model.EditMode = "form";
        model.ProjectName = "Interview Project";
        model.ProjectDescription = "Created via interview mode";
        model.Constraints = "Must use .NET 8\nAzure deployment only";
        model.SuccessCriteria = "100% test coverage\nLoad test: 500 req/s";

        var result = model.OnPost();

        result.Should().BeOfType<RedirectToPageResult>();
        
        var projectFile = Path.Combine(_tempDir, ".aos", "spec", "project.json");
        var content = File.ReadAllText(projectFile);
        content.Should().Contain("Interview Project");
        content.Should().Contain("Must use .NET 8");
        content.Should().Contain("100% test coverage");
    }
}
