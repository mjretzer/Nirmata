using FluentAssertions;
using System.Text.RegularExpressions;
using Xunit;

namespace nirmata.Agents.Tests.Execution.SymbolCacheBuilder;

public class SymbolCacheBuilderTests
{
    private readonly Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheBuilder _builder = new();

    [Fact]
    public async Task BuildAsync_WithValidRepository_ReturnsSuccessResult()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
        result.RepositoryRoot.Should().NotBeNullOrEmpty();
        result.Symbols.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildAsync_ReturnsCorrectMetadata()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.BuildTimestamp.Should().BeWithin(TimeSpan.FromMinutes(1));
        result.Statistics.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildAsync_ExtractsTypeSymbols()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheRequest
        {
            RepositoryPath = GetRepositoryRoot(),
            SourceFiles = new[] { GetSymbolCacheBuilderFilePath() }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var typeSymbols = result.Symbols.Where(s =>
            s.SymbolType is Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolType.Class or
                           Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolType.Interface);
        typeSymbols.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BuildAsync_ExtractsMethodSymbols()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheRequest
        {
            RepositoryPath = GetRepositoryRoot(),
            SourceFiles = new[] { GetSymbolCacheBuilderFilePath() }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var methodSymbols = result.Symbols.Where(s =>
            s.SymbolType is Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolType.Method or
                           Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolType.Constructor);
        methodSymbols.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BuildAsync_SymbolsHaveCorrectAccessibility()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheRequest
        {
            RepositoryPath = GetRepositoryRoot(),
            SourceFiles = new[] { GetSymbolCacheBuilderFilePath() }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Symbols.Should().Contain(s =>
            s.Accessibility == Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolAccessibility.Public);
    }

    [Fact]
    public async Task BuildAsync_SymbolsHaveLocations()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheRequest
        {
            RepositoryPath = GetRepositoryRoot(),
            SourceFiles = new[] { GetSymbolCacheBuilderFilePath() }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Symbols.Should().AllSatisfy(s =>
        {
            s.Location.FilePath.Should().NotBeNullOrEmpty();
            s.Location.RelativePath.Should().NotBeNullOrEmpty();
            s.Location.LineNumber.Should().BeGreaterThan(0);
            s.Location.ColumnNumber.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public async Task BuildAsync_ProducesDeterministicOutput()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheRequest
        {
            RepositoryPath = GetRepositoryRoot(),
            SourceFiles = new[] { GetSymbolCacheBuilderFilePath() }
        };

        // Act
        var result1 = await _builder.BuildAsync(request);
        var result2 = await _builder.BuildAsync(request);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();

        // Same source should produce same symbol count and order
        result1.Statistics.TotalSymbols.Should().Be(result2.Statistics.TotalSymbols);
        result1.Symbols.Count.Should().Be(result2.Symbols.Count);

        // Symbols should be in deterministic order (by FullName)
        for (var i = 0; i < result1.Symbols.Count; i++)
        {
            result1.Symbols[i].FullName.Should().Be(result2.Symbols[i].FullName);
        }
    }

    [Fact]
    public async Task BuildAsync_WithSpecificFiles_OnlyScansThoseFiles()
    {
        // Arrange
        var specificFile = GetSymbolCacheBuilderFilePath();
        var request = new Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheRequest
        {
            RepositoryPath = GetRepositoryRoot(),
            SourceFiles = new[] { specificFile }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Statistics.SourceFileCount.Should().Be(1);
        result.Symbols.Select(s => s.Location.FilePath).Distinct().Should().ContainSingle()
            .Which.Should().Be(specificFile);
    }

    [Fact]
    public async Task BuildAsync_WithEmptySourceFiles_ScansAllCsFiles()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Statistics.SourceFileCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task BuildAsync_WithCancellationToken_StopsWhenCancelled()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => _builder.BuildAsync(request, cts.Token));
    }

    [Fact]
    public async Task BuildAsync_StatisticsAreCalculated()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheRequest
        {
            RepositoryPath = GetRepositoryRoot(),
            SourceFiles = new[] { GetSymbolCacheBuilderFilePath() }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Statistics.TotalSymbols.Should().BeGreaterThan(0);
        
        var namespaceCount = result.Symbols.Count(s => s.SymbolType == Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolType.Namespace);
        
        result.Statistics.TotalSymbols.Should().Be(
            result.Statistics.TypeCount +
            result.Statistics.MethodCount +
            result.Statistics.PropertyCount +
            result.Statistics.FieldCount +
            namespaceCount);
    }

    [Fact]
    public async Task BuildAsync_SymbolsHaveUniqueIds()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheRequest
        {
            RepositoryPath = GetRepositoryRoot(),
            SourceFiles = new[] { GetSymbolCacheBuilderFilePath() }
        };

        // Act
        var result = await _builder.BuildAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var ids = result.Symbols.Select(s => s.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Test_MethodRegex_Matches_BuildAsync_Signature()
    {
        var line = "    public async Task<SymbolCacheResult> BuildAsync(SymbolCacheRequest request, CancellationToken ct = default)";
        
        var type = typeof(Agents.Execution.Brownfield.SymbolCacheBuilder.SymbolCacheBuilder);
        var field = type.GetField("MethodPattern", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var regex = (Regex)field!.GetValue(null)!;

        var match = regex.Match(line.Trim());
        match.Success.Should().BeTrue($"Regex should match line: '{line}'");
        
        // Group 4 is return type, Group 5 is name (based on my previous edit)
        match.Groups[4].Value.Should().Be("Task<SymbolCacheResult>");
        match.Groups[5].Value.Should().Be("BuildAsync");
    }

    private static string GetRepositoryRoot()
    {
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

        return currentDir;
    }

    private static string GetSymbolCacheBuilderFilePath()
    {
        var root = GetRepositoryRoot();
        return Path.Combine(root, "nirmata.Agents", "Execution", "Brownfield", "SymbolCacheBuilder", "SymbolCacheBuilder.cs");
    }
}
