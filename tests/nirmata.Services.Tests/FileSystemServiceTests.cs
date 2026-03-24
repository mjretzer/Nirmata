using Microsoft.Extensions.Logging.Abstractions;
using nirmata.Common.Exceptions;
using nirmata.Services.Implementations;
using Xunit;

namespace nirmata.Services.Tests;

public class FileSystemServiceTests
{
    private readonly FileSystemService _sut = new(NullLogger<FileSystemService>.Instance);

    // Use a fixed absolute root path for normalization tests; no files need to exist.
    private static readonly string Root = OperatingSystem.IsWindows()
        ? @"C:\workspace\project"
        : "/workspace/project";

    [Fact]
    public void ValidateAndNormalizePath_SimpleRelativePath_ResolvesInsideRoot()
    {
        var result = _sut.ValidateAndNormalizePath(Root, "subdir/file.txt");

        Assert.StartsWith(Root, result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file.txt", result);
    }

    [Fact]
    public void ValidateAndNormalizePath_EmptyPath_ResolvesToRoot()
    {
        var result = _sut.ValidateAndNormalizePath(Root, "");

        Assert.Equal(Path.GetFullPath(Root), result, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndNormalizePath_DotPath_ResolvesToRoot()
    {
        var result = _sut.ValidateAndNormalizePath(Root, ".");

        Assert.Equal(Path.GetFullPath(Root), result, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndNormalizePath_NestedForwardSlashes_ResolvesInsideRoot()
    {
        var result = _sut.ValidateAndNormalizePath(Root, "a/b/c/file.json");

        Assert.StartsWith(Path.GetFullPath(Root), result, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("file.json", result);
    }

    [Fact]
    public void ValidateAndNormalizePath_BackslashSeparators_NormalizedAndAccepted()
    {
        // Input uses backslashes (Windows-style) — must be normalized and accepted.
        var result = _sut.ValidateAndNormalizePath(Root, @"subdir\file.txt");

        Assert.StartsWith(Path.GetFullPath(Root), result, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("file.txt", result);
    }

    [Fact]
    public void ValidateAndNormalizePath_MixedSeparators_NormalizedAndAccepted()
    {
        var result = _sut.ValidateAndNormalizePath(Root, @"a/b\c/file.txt");

        Assert.StartsWith(Path.GetFullPath(Root), result, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("file.txt", result);
    }

    [Fact]
    public void ValidateAndNormalizePath_DotDotTraversal_Rejected()
    {
        Assert.Throws<ValidationFailedException>(() =>
            _sut.ValidateAndNormalizePath(Root, "../../etc/passwd"));
    }

    [Fact]
    public void ValidateAndNormalizePath_DotDotWithBackslash_Rejected()
    {
        Assert.Throws<ValidationFailedException>(() =>
            _sut.ValidateAndNormalizePath(Root, @"..\..\etc\passwd"));
    }

    [Fact]
    public void ValidateAndNormalizePath_DotDotMixedSeparators_Rejected()
    {
        Assert.Throws<ValidationFailedException>(() =>
            _sut.ValidateAndNormalizePath(Root, @"..\../etc/passwd"));
    }

    [Fact]
    public void ValidateAndNormalizePath_DotDotInsideSubdir_StillInsideRoot_Accepted()
    {
        // subdir/../file.txt resolves to root/file.txt — still within root.
        var result = _sut.ValidateAndNormalizePath(Root, "subdir/../file.txt");

        Assert.Equal(
            Path.GetFullPath(Path.Combine(Root, "file.txt")),
            result,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndNormalizePath_LeadingForwardSlash_StrippedAndAccepted()
    {
        // URL paths may carry a leading '/'; it must be stripped, not treated as absolute.
        var result = _sut.ValidateAndNormalizePath(Root, "/subdir/file.txt");

        Assert.StartsWith(Path.GetFullPath(Root), result, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("file.txt", result);
    }

    [Fact]
    public void ValidateAndNormalizePath_LeadingBackslash_StrippedAndAccepted()
    {
        var result = _sut.ValidateAndNormalizePath(Root, @"\subdir\file.txt");

        Assert.StartsWith(Path.GetFullPath(Root), result, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("file.txt", result);
    }

    [Fact]
    public void ValidateAndNormalizePath_RootWithTrailingSeparator_Accepted()
    {
        // Workspace root may arrive with a trailing separator — must not affect gating.
        var rootWithSep = Root + Path.DirectorySeparatorChar;
        var result = _sut.ValidateAndNormalizePath(rootWithSep, "file.txt");

        Assert.StartsWith(Path.GetFullPath(Root), result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void ValidateAndNormalizePath_AbsoluteWindowsPath_Rejected()
    {
        // An absolute Windows path injected as the relative path must be rejected.
        Assert.Throws<ValidationFailedException>(() =>
            _sut.ValidateAndNormalizePath(Root, @"C:\Windows\System32"));
    }
}
