using Gmsd.Agents.Execution.Execution.AtomicGitCommitter;
using Xunit;
using Xunit.Abstractions;

namespace Gmsd.Agents.Tests.Execution.AtomicGitCommitter;

public class StagingIntersectionTests
{
    private readonly ITestOutputHelper _output;

    public StagingIntersectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Compute Tests

    [Fact]
    public void Compute_EmptyChangedFiles_ReturnsEmpty()
    {
        var changedFiles = Array.Empty<string>();
        var fileScopes = new[] { "src/**/*.cs" };

        var result = StagingIntersection.Compute(changedFiles, fileScopes);

        Assert.Empty(result);
    }

    [Fact]
    public void Compute_EmptyFileScopes_ReturnsEmpty()
    {
        var changedFiles = new[] { "src/Foo.cs" };
        var fileScopes = Array.Empty<string>();

        var result = StagingIntersection.Compute(changedFiles, fileScopes);

        Assert.Empty(result);
    }

    [Fact]
    public void Compute_ExactMatch_ReturnsMatchedFile()
    {
        var changedFiles = new[] { "src/Foo.cs" };
        var fileScopes = new[] { "src/Foo.cs" };

        var result = StagingIntersection.Compute(changedFiles, fileScopes);

        Assert.Single(result);
        Assert.Equal("src/Foo.cs", result[0]);
    }

    [Fact]
    public void Compute_SubsetMatch_ReturnsOnlyMatchingFiles()
    {
        var changedFiles = new[] { "src/Foo.cs", "src/Bar.cs", "tests/Baz.cs" };
        var fileScopes = new[] { "src/**/*.cs" };

        var result = StagingIntersection.Compute(changedFiles, fileScopes);

        Assert.Equal(2, result.Count);
        Assert.Contains("src/Foo.cs", result);
        Assert.Contains("src/Bar.cs", result);
        Assert.DoesNotContain("tests/Baz.cs", result);
    }

    [Fact]
    public void Compute_NoMatch_ReturnsEmpty()
    {
        var changedFiles = new[] { "tests/Foo.cs" };
        var fileScopes = new[] { "src/**/*.cs" };

        var result = StagingIntersection.Compute(changedFiles, fileScopes);

        Assert.Empty(result);
    }

    [Fact]
    public void Compute_GlobStarStar_MatchesNestedDirectories()
    {
        var changedFiles = new[]
        {
            "src/Foo.cs",
            "src/Utils/Helpers.cs",
            "src/Deep/Nested/File.cs",
            "tests/Test.cs"
        };
        var fileScopes = new[] { "src/**/*.cs" };

        var result = StagingIntersection.Compute(changedFiles, fileScopes);

        Assert.Equal(3, result.Count);
        Assert.Contains("src/Foo.cs", result);
        Assert.Contains("src/Utils/Helpers.cs", result);
        Assert.Contains("src/Deep/Nested/File.cs", result);
        Assert.DoesNotContain("tests/Test.cs", result);
    }

    [Fact]
    public void Compute_GlobStar_MatchesSingleDirectoryLevel()
    {
        var changedFiles = new[]
        {
            "src/Foo.cs",
            "src/Bar.cs",
            "src/Nested/Baz.cs"
        };
        var fileScopes = new[] { "src/*.cs" };

        var result = StagingIntersection.Compute(changedFiles, fileScopes);

        _output.WriteLine($"Changed files: {string.Join(", ", changedFiles)}");
        _output.WriteLine($"Pattern: {fileScopes[0]}");
        _output.WriteLine($"Result ({result.Count} files): {string.Join(", ", result)}");

        Assert.Equal(2, result.Count);
        Assert.Contains("src/Foo.cs", result);
        Assert.Contains("src/Bar.cs", result);
        Assert.DoesNotContain("src/Nested/Baz.cs", result);
    }

    [Fact]
    public void Compute_MultipleScopes_MatchesAnyScope()
    {
        var changedFiles = new[]
        {
            "src/Foo.cs",
            "tests/Bar.cs",
            "docs/README.md"
        };
        var fileScopes = new[] { "src/**/*.cs", "tests/**/*.cs" };

        var result = StagingIntersection.Compute(changedFiles, fileScopes);

        Assert.Equal(2, result.Count);
        Assert.Contains("src/Foo.cs", result);
        Assert.Contains("tests/Bar.cs", result);
        Assert.DoesNotContain("docs/README.md", result);
    }

    [Fact]
    public void Compute_ReturnsDeterministicallyOrderedResults()
    {
        var changedFiles = new[] { "src/Zebra.cs", "src/Alpha.cs", "src/Beta.cs" };
        var fileScopes = new[] { "src/**/*.cs" };

        var result1 = StagingIntersection.Compute(changedFiles, fileScopes);
        var result2 = StagingIntersection.Compute(changedFiles, fileScopes);

        Assert.Equal(result1, result2);
        Assert.Equal(new[] { "src/Alpha.cs", "src/Beta.cs", "src/Zebra.cs" }, result1);
    }

    [Fact]
    public void Compute_HandlesBackslashPaths()
    {
        var changedFiles = new[] { "src\\Foo.cs", "src\\Bar.cs" };
        var fileScopes = new[] { "src/**/*.cs" };

        var result = StagingIntersection.Compute(changedFiles, fileScopes);

        Assert.Equal(2, result.Count);
        Assert.Contains("src\\Foo.cs", result);
        Assert.Contains("src\\Bar.cs", result);
    }

    [Fact]
    public void Compute_CaseInsensitiveMatching()
    {
        var changedFiles = new[] { "SRC/Foo.CS" };
        var fileScopes = new[] { "src/**/*.cs" };

        var result = StagingIntersection.Compute(changedFiles, fileScopes);

        Assert.Single(result);
        Assert.Equal("SRC/Foo.CS", result[0]);
    }

    #endregion

    #region ComputeWithDetails Tests

    [Fact]
    public void ComputeWithDetails_ReturnsFilesToStage()
    {
        var changedFiles = new[] { "src/Foo.cs", "src/Bar.cs" };
        var fileScopes = new[] { "src/**/*.cs" };

        var result = StagingIntersection.ComputeWithDetails(changedFiles, fileScopes);

        Assert.Equal(2, result.FilesToStage.Count);
        Assert.Empty(result.ExcludedFiles);
    }

    [Fact]
    public void ComputeWithDetails_ReturnsExcludedFilesWithReason()
    {
        var changedFiles = new[] { "src/Foo.cs", "tests/Bar.cs" };
        var fileScopes = new[] { "src/**/*.cs" };

        var result = StagingIntersection.ComputeWithDetails(changedFiles, fileScopes);

        Assert.Single(result.FilesToStage);
        Assert.Equal("src/Foo.cs", result.FilesToStage[0]);

        Assert.Single(result.ExcludedFiles);
        Assert.Equal("tests/Bar.cs", result.ExcludedFiles[0].FilePath);
        Assert.Equal("out of scope", result.ExcludedFiles[0].Reason);
    }

    [Fact]
    public void ComputeWithDetails_EmptyChangedFiles_MarksAllAsExcluded()
    {
        var changedFiles = Array.Empty<string>();
        var fileScopes = new[] { "src/**/*.cs" };

        var result = StagingIntersection.ComputeWithDetails(changedFiles, fileScopes);

        Assert.Empty(result.FilesToStage);
        Assert.Empty(result.ExcludedFiles);
    }

    [Fact]
    public void ComputeWithDetails_AllFilesExcluded_WhenNoMatch()
    {
        var changedFiles = new[] { "tests/Foo.cs", "docs/Bar.md" };
        var fileScopes = new[] { "src/**/*.cs" };

        var result = StagingIntersection.ComputeWithDetails(changedFiles, fileScopes);

        Assert.Empty(result.FilesToStage);
        Assert.Equal(2, result.ExcludedFiles.Count);
        Assert.All(result.ExcludedFiles, ef => Assert.Equal("out of scope", ef.Reason));
    }

    [Fact]
    public void ComputeWithDetails_ReturnsDeterministicallyOrderedExcludedFiles()
    {
        var changedFiles = new[] { "tests/Zebra.cs", "tests/Alpha.cs", "src/Foo.cs" };
        var fileScopes = new[] { "src/**/*.cs" };

        var result = StagingIntersection.ComputeWithDetails(changedFiles, fileScopes);

        Assert.Single(result.FilesToStage);
        Assert.Equal(2, result.ExcludedFiles.Count);
        Assert.Equal("tests/Alpha.cs", result.ExcludedFiles[0].FilePath);
        Assert.Equal("tests/Zebra.cs", result.ExcludedFiles[1].FilePath);
    }

    #endregion
}
