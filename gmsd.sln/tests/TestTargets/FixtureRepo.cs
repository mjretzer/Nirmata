namespace Gmsd.TestTargets;

using System;
using System.IO;

/// <summary>
/// Creates and manages disposable fixture repositories for E2E tests.
/// Each fixture is created in a temp folder under %TEMP%/fixture-{guid}/
/// and automatically cleaned up when disposed.
/// </summary>
public sealed class FixtureRepo : IDisposable
{
    private bool _disposed;

    private FixtureRepo(string rootPath)
    {
        RootPath = rootPath;
    }

    /// <summary>
    /// The root path of the fixture repository.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Creates a new fixture repository with the specified template.
    /// </summary>
    /// <param name="templateName">The name of the template to use. Default is "minimal".</param>
    /// <returns>A new FixtureRepo instance.</returns>
    public static FixtureRepo Create(string templateName = "minimal")
    {
        var guid = Guid.NewGuid().ToString("N")[..8];
        var rootPath = Path.Combine(Path.GetTempPath(), $"fixture-{guid}");
        
        Directory.CreateDirectory(rootPath);
        CopyTemplate(templateName, rootPath);

        return new FixtureRepo(rootPath);
    }

    /// <summary>
    /// Disposes the fixture repository, deleting the temp folder and all contents.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup - ignore errors
        }
    }

    private static void CopyTemplate(string templateName, string targetPath)
    {
        var templateDir = Path.Combine(
            AppContext.BaseDirectory,
            "Templates",
            templateName);

        // Fallback for when running from source
        if (!Directory.Exists(templateDir))
        {
            var sourceDir = Path.GetDirectoryName(typeof(FixtureRepo).Assembly.Location);
            while (sourceDir != null && !Directory.Exists(Path.Combine(sourceDir, "Templates")))
            {
                sourceDir = Directory.GetParent(sourceDir)?.FullName;
            }
            
            if (sourceDir != null)
            {
                templateDir = Path.Combine(sourceDir, "Templates", templateName);
            }
        }

        if (!Directory.Exists(templateDir))
        {
            // Create minimal files if template not found
            CreateMinimalFiles(targetPath);
            return;
        }

        foreach (var file in Directory.GetFiles(templateDir, "*.template"))
        {
            var fileName = Path.GetFileName(file).Replace(".template", "");
            var targetFile = Path.Combine(targetPath, fileName);
            File.Copy(file, targetFile, overwrite: true);
        }
    }

    private static void CreateMinimalFiles(string targetPath)
    {
        var csproj = @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>";

        var programCs = "Console.WriteLine(\"Hello, World!\");";

        File.WriteAllText(Path.Combine(targetPath, "Project.csproj"), csproj);
        File.WriteAllText(Path.Combine(targetPath, "Program.cs"), programCs);
    }
}
