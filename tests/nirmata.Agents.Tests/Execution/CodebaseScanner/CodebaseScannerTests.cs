using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace nirmata.Agents.Tests.Execution.CodebaseScanner;

public class CodebaseScannerTests
{
    private readonly Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanner _scanner = new();

    [Fact]
    public async Task ScanAsync_WithValidRepository_ReturnsSuccessResult()
    {
        // Arrange - use the current solution as test data
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };

        // Act
        var result = await _scanner.ScanAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
        result.RepositoryRoot.Should().NotBeNullOrEmpty();
        result.RepositoryName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ScanAsync_ReturnsCorrectRepositoryMetadata()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };

        // Act
        var result = await _scanner.ScanAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RepositoryName.Should().NotBeNullOrEmpty();
        result.ScanTimestamp.Should().BeWithin(TimeSpan.FromMinutes(1));
        result.RepositoryRoot.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ScanAsync_DiscoversSolutions()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };

        // Act
        var result = await _scanner.ScanAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Solutions.Should().NotBeEmpty();
        result.Solutions.Should().Contain(s => s.Name == "nirmata" || s.Name.Contains("nirmata"));
    }

    [Fact]
    public async Task ScanAsync_DiscoversProjects()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };

        // Act
        var result = await _scanner.ScanAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Projects.Should().NotBeEmpty();
        result.Projects.Should().Contain(p => p.Name.Contains("nirmata.Agents") || p.Name.Contains("nirmata.Aos"));
    }

    [Fact]
    public async Task ScanAsync_BuildsDirectoryStructure()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };

        // Act
        var result = await _scanner.ScanAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RootDirectory.Should().NotBeNull();
        result.RootDirectory.Files.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ScanAsync_CalculatesStatistics()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };

        // Act
        var result = await _scanner.ScanAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Statistics.TotalFiles.Should().BeGreaterThan(0);
        result.Statistics.TotalDirectories.Should().BeGreaterThan(0);
        result.Statistics.ProjectCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ScanAsync_DetectsTechnologyStack()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };

        // Act
        var result = await _scanner.ScanAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.TechnologyStack.Languages.Should().NotBeEmpty();
        result.TechnologyStack.Languages.Should().Contain(l => l.Name == "csharp");
    }

    [Fact]
    public async Task ScanAsync_WithInvalidPath_ReturnsFailure()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = "C:\\NonExistentPath\\12345"
        };

        // Act
        var result = await _scanner.ScanAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not exist");
    }

    [Fact]
    public async Task ScanAsync_WithCancellationToken_StopsWhenCancelled()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _scanner.ScanAsync(request, null, cts.Token));
    }

    [Fact]
    public async Task ScanAsync_IdentifiesTestProjects()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };

        // Act
        var result = await _scanner.ScanAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Projects.Should().Contain(p => p.IsTestProject);
        result.Projects.Where(p => p.IsTestProject).Should().Contain(p => p.Name.Contains("Tests"));
    }

    [Fact]
    public async Task ScanAsync_ProjectHasCorrectReferences()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };

        // Act
        var result = await _scanner.ScanAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var agentsProject = result.Projects.FirstOrDefault(p => p.Name == "nirmata.Agents");
        if (agentsProject != null)
        {
            agentsProject.PackageReferences.Should().NotBeNull();
            agentsProject.ProjectReferences.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ScanAsync_ProducesDeterministicOutput()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };

        // Act
        var result1 = await _scanner.ScanAsync(request);
        var result2 = await _scanner.ScanAsync(request);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();

        // Same repository state should produce same counts
        result1.Statistics.TotalFiles.Should().Be(result2.Statistics.TotalFiles);
        result1.Statistics.ProjectCount.Should().Be(result2.Statistics.ProjectCount);
        result1.Solutions.Count.Should().Be(result2.Solutions.Count);
        result1.Projects.Count.Should().Be(result2.Projects.Count);
    }

    private static string GetRepositoryRoot()
    {
        // Start from current directory and walk up to find repo root
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);

        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, "nirmata.slnx")) ||
                File.Exists(Path.Combine(dir.FullName, "nirmata.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        // Fallback to current directory
        return currentDir;
    }
}
