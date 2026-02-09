using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.FileGraphBuilder;

public class FileGraphBuilderTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly Agents.Execution.Brownfield.FileGraphBuilder.FileGraphBuilder _builder;

    public FileGraphBuilderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"filegraph-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _builder = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphBuilder();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task BuildAsync_WithNoProjects_ReturnsEmptyGraph()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = false
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Nodes.Should().BeEmpty();
        result.Edges.Should().BeEmpty();
        result.Statistics.TotalNodes.Should().Be(0);
        result.Statistics.TotalEdges.Should().Be(0);
    }

    [Fact]
    public async Task BuildAsync_WithSingleProject_CreatesProjectNode()
    {
        // Arrange
        var projectPath = CreateTestProject("TestProject", Array.Empty<string>());
        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = false
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Nodes.Should().HaveCount(1);
        var projectNode = result.Nodes.First();
        projectNode.NodeType.Should().Be(Agents.Execution.Brownfield.FileGraphBuilder.NodeType.Project);
        projectNode.Id.Should().EndWith("TestProject.csproj");
        result.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_WithProjectReference_CreatesProjectReferenceEdge()
    {
        // Arrange
        var libraryPath = CreateTestProject("Library", Array.Empty<string>());
        var appPath = CreateTestProject("App", new[] { libraryPath });

        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = false
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Nodes.Should().HaveCount(2);
        result.Edges.Should().HaveCount(1);

        var edge = result.Edges.First();
        edge.EdgeType.Should().Be(Agents.Execution.Brownfield.FileGraphBuilder.EdgeType.ProjectReference);
        edge.SourceId.Should().Contain("App.csproj");
        edge.TargetId.Should().Contain("Library.csproj");
        edge.Weight.Should().Be(1.0);
    }

    [Fact]
    public async Task BuildAsync_WithMultipleProjectReferences_CreatesAllReferenceEdges()
    {
        // Arrange
        var coreLibPath = CreateTestProject("Core", Array.Empty<string>());
        var utilsLibPath = CreateTestProject("Utils", Array.Empty<string>());
        var appPath = CreateTestProject("App", new[] { coreLibPath, utilsLibPath });

        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = false
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Nodes.Should().HaveCount(3);
        result.Edges.Should().HaveCount(2);

        var projectRefEdges = result.Edges.Where(e => e.EdgeType == Agents.Execution.Brownfield.FileGraphBuilder.EdgeType.ProjectReference).ToList();
        projectRefEdges.Should().HaveCount(2);

        // Verify bidirectional relationship - App references both libraries
        projectRefEdges.All(e => e.SourceId.Contains("App.csproj")).Should().BeTrue();
        projectRefEdges.Any(e => e.TargetId.Contains("Core.csproj")).Should().BeTrue();
        projectRefEdges.Any(e => e.TargetId.Contains("Utils.csproj")).Should().BeTrue();
    }

    [Fact]
    public async Task BuildAsync_WithTransitiveDependencies_CreatesAllEdges()
    {
        // Arrange - A -> B -> C (A depends on B, B depends on C)
        var cPath = CreateTestProject("ProjectC", Array.Empty<string>());
        var bPath = CreateTestProject("ProjectB", new[] { cPath });
        var aPath = CreateTestProject("ProjectA", new[] { bPath });

        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = false
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Nodes.Should().HaveCount(3);
        result.Edges.Should().HaveCount(2);

        // Verify A -> B and B -> C (not A -> C, direct edges only)
        var edges = result.Edges.ToList();
        edges.Should().Contain(e => e.SourceId.Contains("ProjectA.csproj") && e.TargetId.Contains("ProjectB.csproj"));
        edges.Should().Contain(e => e.SourceId.Contains("ProjectB.csproj") && e.TargetId.Contains("ProjectC.csproj"));
    }

    [Fact]
    public async Task BuildAsync_WithCircularReferences_CreatesEdgesCorrectly()
    {
        // Arrange - A <-> B (circular dependency)
        // Note: We create B first, then A with reference to B, then update B to reference A
        var bDir = Path.Combine(_tempDirectory, "ProjectB");
        Directory.CreateDirectory(bDir);
        var aDir = Path.Combine(_tempDirectory, "ProjectA");
        Directory.CreateDirectory(aDir);

        var bProjectPath = Path.Combine(bDir, "ProjectB.csproj");
        var aProjectPath = Path.Combine(aDir, "ProjectA.csproj");

        // Create initial B project without reference to A
        var initialBContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        await File.WriteAllTextAsync(bProjectPath, initialBContent);

        // Create A project with reference to B
        var aContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\ProjectB\ProjectB.csproj"" />
  </ItemGroup>
</Project>";
        await File.WriteAllTextAsync(aProjectPath, aContent);

        // Now update B to reference A (creating circular dependency)
        var circularBContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\ProjectA\ProjectA.csproj"" />
  </ItemGroup>
</Project>";
        await File.WriteAllTextAsync(bProjectPath, circularBContent);

        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = false
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Nodes.Should().HaveCount(2);
        result.Edges.Should().HaveCount(2);

        // Verify circular edges A <-> B
        result.Edges.Should().Contain(e => e.SourceId.Contains("ProjectA.csproj") && e.TargetId.Contains("ProjectB.csproj"));
        result.Edges.Should().Contain(e => e.SourceId.Contains("ProjectB.csproj") && e.TargetId.Contains("ProjectA.csproj"));
    }

    [Fact]
    public async Task BuildAsync_WithSourceFiles_CreatesFileIncludeEdges()
    {
        // Arrange
        var projectDir = Path.Combine(_tempDirectory, "TestProject");
        Directory.CreateDirectory(projectDir);
        var projectPath = Path.Combine(projectDir, "TestProject.csproj");

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        await File.WriteAllTextAsync(projectPath, csprojContent);

        // Add source files
        var class1Path = Path.Combine(projectDir, "Class1.cs");
        var class2Path = Path.Combine(projectDir, "Class2.cs");
        await File.WriteAllTextAsync(class1Path, "namespace TestProject { public class Class1 { } }");
        await File.WriteAllTextAsync(class2Path, "namespace TestProject { public class Class2 { } }");

        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = true
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Nodes.Should().HaveCount(3); // 1 project + 2 source files
        result.Nodes.Count(n => n.NodeType == Agents.Execution.Brownfield.FileGraphBuilder.NodeType.Project).Should().Be(1);
        result.Nodes.Count(n => n.NodeType == Agents.Execution.Brownfield.FileGraphBuilder.NodeType.SourceFile).Should().Be(2);

        // Should have 2 file include edges (project -> source file)
        result.Edges.Should().HaveCount(2);
        result.Edges.All(e => e.EdgeType == Agents.Execution.Brownfield.FileGraphBuilder.EdgeType.FileInclude).Should().BeTrue();
        result.Edges.All(e => e.SourceId.Contains("TestProject.csproj")).Should().BeTrue();
    }

    [Fact]
    public async Task BuildAsync_ProjectNodeHasCorrectMetadata()
    {
        // Arrange
        var projectDir = Path.Combine(_tempDirectory, "MyProject");
        Directory.CreateDirectory(projectDir);
        var projectPath = Path.Combine(projectDir, "MyProject.csproj");

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        await File.WriteAllTextAsync(projectPath, csprojContent);

        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = false
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var projectNode = result.Nodes.First();
        projectNode.NodeType.Should().Be(Agents.Execution.Brownfield.FileGraphBuilder.NodeType.Project);
        projectNode.Extension.Should().Be(".csproj");
        projectNode.FullPath.Should().EndWith("MyProject.csproj");
        projectNode.RelativePath.Should().Be("MyProject/MyProject.csproj");
        projectNode.Id.Should().Be("MyProject/MyProject.csproj");
        projectNode.ProjectId.Should().BeNull();
        projectNode.FileSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BuildAsync_SourceFileNodeHasCorrectMetadata()
    {
        // Arrange
        var projectDir = Path.Combine(_tempDirectory, "TestProject");
        Directory.CreateDirectory(projectDir);
        var projectPath = Path.Combine(projectDir, "TestProject.csproj");

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        await File.WriteAllTextAsync(projectPath, csprojContent);

        var sourceFilePath = Path.Combine(projectDir, "TestClass.cs");
        await File.WriteAllTextAsync(sourceFilePath, "namespace TestProject { public class TestClass { } }");

        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = true
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var sourceFileNode = result.Nodes.First(n => n.NodeType == Agents.Execution.Brownfield.FileGraphBuilder.NodeType.SourceFile);
        sourceFileNode.NodeType.Should().Be(Agents.Execution.Brownfield.FileGraphBuilder.NodeType.SourceFile);
        sourceFileNode.Extension.Should().Be(".cs");
        sourceFileNode.ProjectId.Should().Be("TestProject/TestProject.csproj");
        sourceFileNode.FullPath.Should().EndWith("TestClass.cs");
        sourceFileNode.Id.Should().Be("TestProject/TestClass.cs");
    }

    [Fact]
    public async Task BuildAsync_WithExcludedPatterns_SkipsMatchingFiles()
    {
        // Arrange
        var projectDir = Path.Combine(_tempDirectory, "TestProject");
        var objDir = Path.Combine(projectDir, "obj");
        Directory.CreateDirectory(objDir);
        var projectPath = Path.Combine(projectDir, "TestProject.csproj");

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        await File.WriteAllTextAsync(projectPath, csprojContent);

        // Add source files in both project root and obj folder
        var class1Path = Path.Combine(projectDir, "Class1.cs");
        var class2Path = Path.Combine(objDir, "Class2.cs");
        await File.WriteAllTextAsync(class1Path, "namespace TestProject { public class Class1 { } }");
        await File.WriteAllTextAsync(class2Path, "namespace TestProject { public class Class2 { } }");

        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = true,
                ExcludePatterns = new[] { "bin/", "obj/", ".git/" }
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Nodes.Count(n => n.NodeType == Agents.Execution.Brownfield.FileGraphBuilder.NodeType.SourceFile).Should().Be(1);
        result.Nodes.Should().Contain(n => n.Id.Contains("Class1.cs"));
        result.Nodes.Should().NotContain(n => n.Id.Contains("Class2.cs") && n.Id.Contains("obj/"));
    }

    [Fact]
    public async Task BuildAsync_Statistics_AreCorrectlyCalculated()
    {
        // Arrange
        var lib1Path = CreateTestProject("Lib1", Array.Empty<string>());
        var lib2Path = CreateTestProject("Lib2", Array.Empty<string>());
        var appPath = CreateTestProject("App", new[] { lib1Path, lib2Path });

        // Add source files
        var appDir = Path.GetDirectoryName(appPath)!;
        await File.WriteAllTextAsync(Path.Combine(appDir, "Class1.cs"), "public class Class1 {}");
        await File.WriteAllTextAsync(Path.Combine(appDir, "Class2.cs"), "public class Class2 {}");

        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = true
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Statistics.ProjectCount.Should().Be(3);
        result.Statistics.SourceFileCount.Should().Be(2); // App has 2 source files
        result.Statistics.TotalNodes.Should().Be(5); // 3 projects + 2 source files
        result.Statistics.ProjectReferenceCount.Should().Be(2);
        result.Statistics.BuildDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task BuildAsync_WithSpecificProjectFiles_OnlyAnalyzesSpecifiedProjects()
    {
        // Arrange
        var libPath = CreateTestProject("Library", Array.Empty<string>());
        var appPath = CreateTestProject("App", new[] { libPath });
        var unusedPath = CreateTestProject("Unused", Array.Empty<string>());

        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            ProjectFiles = new[] { libPath, appPath }, // Don't include "Unused"
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = false
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Nodes.Should().HaveCount(2);
        result.Nodes.Should().Contain(n => n.Id.Contains("Library.csproj"));
        result.Nodes.Should().Contain(n => n.Id.Contains("App.csproj"));
        result.Nodes.Should().NotContain(n => n.Id.Contains("Unused.csproj"));
    }

    [Fact]
    public async Task BuildAsync_ReturnsError_WhenRepositoryPathDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "non-existent-repo");
        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = nonExistentPath,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions()
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BuildAsync_EdgesHaveConsistentSourceAndTargetIds()
    {
        // Arrange
        var libPath = CreateTestProject("Library", Array.Empty<string>());
        var appPath = CreateTestProject("Application", new[] { libPath });

        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = true
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // All edge source and target IDs should exist in nodes
        var nodeIds = result.Nodes.Select(n => n.Id).ToHashSet();
        foreach (var edge in result.Edges)
        {
            nodeIds.Should().Contain(edge.SourceId, $"Edge {edge.Id} source {edge.SourceId} should exist in nodes");
            nodeIds.Should().Contain(edge.TargetId, $"Edge {edge.Id} target {edge.TargetId} should exist in nodes");
        }
    }

    [Fact]
    public async Task BuildAsync_ProducesDeterministicOutput()
    {
        // Arrange
        var libPath = CreateTestProject("Library", Array.Empty<string>());
        var appPath = CreateTestProject("Application", new[] { libPath });

        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = true
            }
        };

        // Act - build twice
        var result1 = await _builder.BuildAsync(request);
        var result2 = await _builder.BuildAsync(request);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();

        // Node IDs should be identical
        result1.Nodes.Select(n => n.Id).Should().Equal(result2.Nodes.Select(n => n.Id));
        result1.Edges.Select(e => e.Id).Should().Equal(result2.Edges.Select(e => e.Id));

        // Order should be the same (sorted)
        result1.Nodes.Should().BeInAscendingOrder(n => n.Id);
        result1.Edges.Should().BeInAscendingOrder(e => e.Id);
    }

    [Fact]
    public async Task BuildAsync_WithRelativeProjectPaths_ResolvesReferences()
    {
        // Arrange
        var sharedDir = Path.Combine(_tempDirectory, "Shared");
        var appDir = Path.Combine(_tempDirectory, "Applications", "MainApp");
        Directory.CreateDirectory(sharedDir);
        Directory.CreateDirectory(appDir);

        var libPath = Path.Combine(sharedDir, "Common.csproj");
        var appPath = Path.Combine(appDir, "MainApp.csproj");

        await File.WriteAllTextAsync(libPath, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

        // Reference using relative path "../../Shared/Common.csproj"
        await File.WriteAllTextAsync(appPath, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""../../Shared/Common.csproj"" />
  </ItemGroup>
</Project>");

        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = true,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = false
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Nodes.Should().HaveCount(2);
        result.Edges.Should().HaveCount(1);

        var edge = result.Edges.First();
        edge.SourceId.Should().Contain("MainApp.csproj");
        edge.TargetId.Should().Contain("Common.csproj");
    }

    [Fact]
    public async Task BuildAsync_WithDisabledOptions_RespectsOptionSettings()
    {
        // Arrange
        var libPath = CreateTestProject("Library", Array.Empty<string>());
        var appPath = CreateTestProject("Application", new[] { libPath });
        var appDir = Path.GetDirectoryName(appPath)!;
        await File.WriteAllTextAsync(Path.Combine(appDir, "Class1.cs"), "public class Class1 {}");

        // Test with all options disabled
        var request = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphRequest
        {
            RepositoryPath = _tempDirectory,
            Options = new Agents.Execution.Brownfield.FileGraphBuilder.FileGraphOptions
            {
                IncludeProjectReferences = false,
                IncludeImportDependencies = false,
                IncludeIntraProjectEdges = false,
                CalculateEdgeWeights = false
            }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert - should only have project nodes, no edges
        result.IsSuccess.Should().BeTrue();
        result.Nodes.Count(n => n.NodeType == Agents.Execution.Brownfield.FileGraphBuilder.NodeType.Project).Should().Be(2);
        result.Nodes.Count(n => n.NodeType == Agents.Execution.Brownfield.FileGraphBuilder.NodeType.SourceFile).Should().Be(0); // intra-project disabled
        result.Edges.Should().BeEmpty();
    }

    private string CreateTestProject(string projectName, string[] projectReferences)
    {
        var projectDir = Path.Combine(_tempDirectory, projectName);
        Directory.CreateDirectory(projectDir);
        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");

        var referencesSection = projectReferences.Length > 0
            ? string.Join("\n", projectReferences.Select(r =>
                $"    <ProjectReference Include=\"{Path.GetRelativePath(projectDir, r)}\" />"))
            : "";

        var itemGroup = string.IsNullOrEmpty(referencesSection)
            ? ""
            : $"\n  <ItemGroup>\n{referencesSection}\n  </ItemGroup>";

        var csprojContent = @$"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>{itemGroup}
</Project>";

        File.WriteAllText(projectPath, csprojContent);
        return projectPath;
    }
}
