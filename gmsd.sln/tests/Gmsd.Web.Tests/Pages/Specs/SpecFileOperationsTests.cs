using FluentAssertions;
using Gmsd.Web.Pages.Specs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gmsd.Web.Tests.Pages.Specs;

public class SpecFileOperationsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _configDir;
    private readonly string _configFile;

    public SpecFileOperationsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _specDir = Path.Combine(_aosDir, "spec");
        _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gmsd");
        _configFile = Path.Combine(_configDir, "workspace-config.json");
        Directory.CreateDirectory(_specDir);
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

    private ViewModel CreateViewModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new ViewModel(NullLogger<ViewModel>.Instance, configuration, null!);
    }

    [Fact]
    public void ViewModel_Properties_AreInitialized()
    {
        var model = CreateViewModel();

        model.ValidationErrors.Should().NotBeNull();
        model.FormFields.Should().NotBeNull();
        model.GitHistory.Should().NotBeNull();
        model.DiffLines.Should().NotBeNull();
    }

    [Fact]
    public void FormField_DefaultValues_AreSet()
    {
        var field = new FormField();

        field.Key.Should().BeEmpty();
        field.DisplayName.Should().BeEmpty();
        field.Value.Should().BeEmpty();
        field.Type.Should().Be("text");
        field.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void ValidationError_HasExpectedProperties()
    {
        var error = new ValidationError
        {
            Path = "$.name",
            Message = "Name is required"
        };

        error.Path.Should().Be("$.name");
        error.Message.Should().Be("Name is required");
    }

    [Fact]
    public void ValidationRequest_HasRequiredAttributes()
    {
        var request = new ValidationRequest
        {
            Path = "test.json",
            Content = "{}"
        };

        request.Path.Should().Be("test.json");
        request.Content.Should().Be("{}");
    }

    [Fact]
    public void GitCommitInfo_ShortHash_ReturnsFirst7Chars()
    {
        var commit = new GitCommitInfo
        {
            Hash = "abc123def456",
            Message = "Test commit",
            Author = "Test Author",
            Date = DateTime.UtcNow
        };

        commit.ShortHash.Should().Be("abc123d");
    }

    [Fact]
    public void GitCommitInfo_ShortHash_ShortHashHandled()
    {
        var commit = new GitCommitInfo
        {
            Hash = "abc",
            Message = "Short hash",
            Author = "Test Author",
            Date = DateTime.UtcNow
        };

        commit.ShortHash.Should().Be("abc");
    }

    [Fact]
    public void DiffLine_HasExpectedProperties()
    {
        var line = new DiffLine
        {
            Type = DiffLineType.Added,
            OldLineNumber = null,
            NewLineNumber = 5,
            Content = "New line content"
        };

        line.Type.Should().Be(DiffLineType.Added);
        line.OldLineNumber.Should().BeNull();
        line.NewLineNumber.Should().Be(5);
        line.Content.Should().Be("New line content");
    }

    [Fact]
    public void DiffLineType_EnumValues_AreDefined()
    {
        var values = Enum.GetValues<DiffLineType>();

        values.Should().Contain(DiffLineType.Unchanged);
        values.Should().Contain(DiffLineType.Added);
        values.Should().Contain(DiffLineType.Removed);
    }
}

public class SpecFileLoadTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _configDir;
    private readonly string _configFile;

    public SpecFileLoadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _specDir = Path.Combine(_aosDir, "spec");
        _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gmsd");
        _configFile = Path.Combine(_configDir, "workspace-config.json");
        Directory.CreateDirectory(_specDir);
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

    private ViewModel CreateViewModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new ViewModel(NullLogger<ViewModel>.Instance, configuration, null!);
    }

    [Fact]
    public void LoadWorkspace_NoConfig_ReturnsEmptyWorkspacePath()
    {
        var model = CreateViewModel();

        model.OnGet();

        model.WorkspacePath.Should().BeNullOrEmpty();
    }

    [Fact]
    public void LoadWorkspace_WithValidConfig_LoadsWorkspacePath()
    {
        var model = CreateViewModel();
        CreateWorkspaceConfig(_tempDir);

        model.OnGet();

        model.WorkspacePath.Should().Be(_tempDir);
    }

    [Fact]
    public void LoadSpecFile_ValidJsonFile_LoadsContent()
    {
        var model = CreateViewModel();
        CreateWorkspaceConfig(_tempDir);

        var testFile = Path.Combine(_specDir, "project.json");
        var jsonContent = "{\"name\": \"Test Project\", \"version\": \"1.0\"}";
        File.WriteAllText(testFile, jsonContent);

        model.Path = "project.json";
        model.OnGet();

        model.RawContent.Should().Be(jsonContent);
        model.IsJson.Should().BeTrue();
        model.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public void LoadSpecFile_InvalidJson_SetsErrorMessage()
    {
        var model = CreateViewModel();
        CreateWorkspaceConfig(_tempDir);

        var testFile = Path.Combine(_specDir, "invalid.json");
        File.WriteAllText(testFile, "{invalid json}");

        model.Path = "invalid.json";
        model.OnGet();

        model.IsJson.Should().BeTrue();
        model.ErrorMessage.Should().Contain("Invalid JSON");
    }

    [Fact]
    public void LoadSpecFile_NonJsonFile_SetsIsJsonFalse()
    {
        var model = CreateViewModel();
        CreateWorkspaceConfig(_tempDir);

        var testFile = Path.Combine(_specDir, "readme.md");
        File.WriteAllText(testFile, "# Test Spec");

        model.Path = "readme.md";
        model.OnGet();

        model.IsJson.Should().BeFalse();
        model.RawContent.Should().Be("# Test Spec");
    }

    [Fact]
    public void LoadSpecFile_MissingFile_SetsErrorMessage()
    {
        var model = CreateViewModel();
        CreateWorkspaceConfig(_tempDir);

        model.Path = "nonexistent.json";
        model.OnGet();

        model.ErrorMessage.Should().Contain("File not found");
    }

    [Fact]
    public void DiskPath_ReturnsFileUri()
    {
        var model = CreateViewModel();
        CreateWorkspaceConfig(_tempDir);

        var testFile = Path.Combine(_specDir, "test.json");
        File.WriteAllText(testFile, "{}");

        model.Path = "test.json";
        model.OnGet();

        model.DiskPath.Should().StartWith("file://");
        model.DiskPath.Should().Contain("test.json");
    }
}

public class SpecFileSaveTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _configDir;
    private readonly string _configFile;

    public SpecFileSaveTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _specDir = Path.Combine(_aosDir, "spec");
        _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gmsd");
        _configFile = Path.Combine(_configDir, "workspace-config.json");
        Directory.CreateDirectory(_specDir);
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

    private ViewModel CreateViewModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new ViewModel(NullLogger<ViewModel>.Instance, configuration, null!);
    }

    [Fact]
    public void SaveFile_ValidJson_SavesContent()
    {
        var model = CreateViewModel();
        CreateWorkspaceConfig(_tempDir);

        var testFile = Path.Combine(_specDir, "test.json");
        File.WriteAllText(testFile, "{\"old\": \"value\"}");

        var newContent = "{\"new\": \"value\"}";
        var result = model.OnPost("test.json", "raw", new Dictionary<string, string>(), newContent);

        result.Should().BeOfType<RedirectToPageResult>();
        File.ReadAllText(testFile).Should().Be(newContent);
    }

    [Fact]
    public void SaveFile_InvalidJson_DoesNotSave()
    {
        var model = CreateViewModel();
        CreateWorkspaceConfig(_tempDir);

        var testFile = Path.Combine(_specDir, "test.json");
        var originalContent = "{\"valid\": \"json\"}";
        File.WriteAllText(testFile, originalContent);

        var invalidContent = "{invalid json}";
        var result = model.OnPost("test.json", "raw", new Dictionary<string, string>(), invalidContent);

        result.Should().BeOfType<RedirectToPageResult>();
        File.ReadAllText(testFile).Should().Be(originalContent); // Should not have changed
    }

    [Fact]
    public void SaveFile_CreatesBackup()
    {
        var model = CreateViewModel();
        CreateWorkspaceConfig(_tempDir);

        var testFile = Path.Combine(_specDir, "test.json");
        File.WriteAllText(testFile, "{\"original\": \"data\"}");

        var newContent = "{\"updated\": \"data\"}";
        model.OnPost("test.json", "raw", new Dictionary<string, string>(), newContent);

        var backupFile = testFile + ".backup";
        File.Exists(backupFile).Should().BeTrue();
        File.ReadAllText(backupFile).Should().Be("{\"original\": \"data\"}");
    }

    [Fact]
    public void SaveFile_SecurityCheck_RejectsInvalidPath()
    {
        var model = CreateViewModel();
        CreateWorkspaceConfig(_tempDir);

        var result = model.OnPost("../../../etc/passwd", "raw", new Dictionary<string, string>(), "{}");

        result.Should().BeOfType<RedirectToPageResult>();
    }
}

public class SpecValidationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _configDir;
    private readonly string _configFile;

    public SpecValidationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _specDir = Path.Combine(_aosDir, "spec");
        _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gmsd");
        _configFile = Path.Combine(_configDir, "workspace-config.json");
        Directory.CreateDirectory(_specDir);
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

    private ViewModel CreateViewModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new ViewModel(NullLogger<ViewModel>.Instance, configuration, null!);
    }

    [Fact]
    public void ValidateSchema_InvalidJson_ReturnsErrors()
    {
        var model = CreateViewModel();
        CreateWorkspaceConfig(_tempDir);

        var result = model.OnPostValidate(new ValidationRequest
        {
            Path = "test.json",
            Content = "{invalid}"
        });

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value as dynamic;
        ((bool)value!.valid).Should().BeFalse();
    }

    [Fact]
    public void ValidateSchema_ValidJson_ReturnsSuccess()
    {
        var model = CreateViewModel();
        CreateWorkspaceConfig(_tempDir);

        var result = model.OnPostValidate(new ValidationRequest
        {
            Path = "test.json",
            Content = "{\"name\": \"test\"}"
        });

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value as dynamic;
        ((bool)value!.valid).Should().BeTrue();
    }

    [Fact]
    public void ValidateSchema_NoWorkspace_ReturnsError()
    {
        var model = CreateViewModel();

        var result = model.OnPostValidate(new ValidationRequest
        {
            Path = "test.json",
            Content = "{}"
        });

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value as dynamic;
        ((bool)value!.valid).Should().BeFalse();
    }
}

public class SpecIndexModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _specDir;
    private readonly string _configDir;
    private readonly string _configFile;

    public SpecIndexModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _specDir = Path.Combine(_aosDir, "spec");
        _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gmsd");
        _configFile = Path.Combine(_configDir, "workspace-config.json");
        Directory.CreateDirectory(_specDir);
        Directory.CreateDirectory(Path.Combine(_specDir, "project"));
        Directory.CreateDirectory(Path.Combine(_specDir, "roadmap"));
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

    private IndexModel CreateIndexModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new IndexModel(NullLogger<IndexModel>.Instance, configuration);
    }

    [Fact]
    public void IndexModel_Properties_AreInitialized()
    {
        var model = CreateIndexModel();

        model.SearchResults.Should().NotBeNull();
        model.SpecCategories.Should().NotBeNull();
    }

    [Fact]
    public void SearchResultItem_HasExpectedProperties()
    {
        var item = new SpecSearchResult
        {
            RelativePath = "project/test.json",
            FileType = "json",
            MatchingContent = "test match",
            LastModified = DateTime.UtcNow
        };

        item.RelativePath.Should().Be("project/test.json");
        item.FileType.Should().Be("json");
        item.MatchingContent.Should().Be("test match");
    }

    [Fact]
    public void SpecCategory_HasExpectedProperties()
    {
        var category = new SpecCategory
        {
            Name = "project",
            FileCount = 5
        };

        category.Name.Should().Be("project");
        category.FileCount.Should().Be(5);
    }
}

public class SpecTreeNodeTests
{
    [Fact]
    public void SpecTreeNode_DefaultValues_AreSet()
    {
        var node = new SpecTreeNode();

        node.Name.Should().BeEmpty();
        node.RelativePath.Should().BeEmpty();
        node.IsDirectory.Should().BeFalse();
        node.Children.Should().NotBeNull();
        node.Children.Should().BeEmpty();
    }

    [Fact]
    public void SpecTreeNode_WithChildren_HasNestedStructure()
    {
        var parent = new SpecTreeNode
        {
            Name = "spec",
            RelativePath = "",
            IsDirectory = true
        };

        var child = new SpecTreeNode
        {
            Name = "project.json",
            RelativePath = "project.json",
            IsDirectory = false
        };

        parent.Children.Add(child);

        parent.Children.Should().HaveCount(1);
        parent.Children[0].Name.Should().Be("project.json");
    }
}
