namespace nirmata.Agents.Execution.Brownfield.CodebaseScanner;

/// <summary>
/// Result of a codebase scan operation.
/// </summary>
public sealed class CodebaseScanResult
{
    /// <summary>
    /// Whether the scan completed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the scan failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The detected repository root path.
    /// </summary>
    public string RepositoryRoot { get; init; } = "";

    /// <summary>
    /// The detected repository name.
    /// </summary>
    public string RepositoryName { get; init; } = "";

    /// <summary>
    /// List of discovered solution files.
    /// </summary>
    public IReadOnlyList<SolutionInfo> Solutions { get; init; } = Array.Empty<SolutionInfo>();

    /// <summary>
    /// List of discovered project files.
    /// </summary>
    public IReadOnlyList<ProjectInfo> Projects { get; init; } = Array.Empty<ProjectInfo>();

    /// <summary>
    /// Directory structure of the repository.
    /// </summary>
    public DirectoryNode RootDirectory { get; init; } = new();

    /// <summary>
    /// Timestamp when the scan completed.
    /// </summary>
    public DateTimeOffset ScanTimestamp { get; init; }

    /// <summary>
    /// Statistics about the scanned repository.
    /// </summary>
    public RepositoryStatistics Statistics { get; init; } = new();

    /// <summary>
    /// Detected technology stack (languages, frameworks, build tools, package managers).
    /// </summary>
    public TechnologyStack TechnologyStack { get; init; } = new();
}

/// <summary>
/// Information about a discovered solution file.
/// </summary>
public sealed class SolutionInfo
{
    /// <summary>
    /// Full path to the solution file.
    /// </summary>
    public string Path { get; init; } = "";

    /// <summary>
    /// Solution name (without extension).
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Relative path from repository root.
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    /// Projects referenced by this solution.
    /// </summary>
    public IReadOnlyList<string> ProjectPaths { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Information about a discovered project file.
/// </summary>
public sealed class ProjectInfo
{
    /// <summary>
    /// Full path to the project file.
    /// </summary>
    public string Path { get; init; } = "";

    /// <summary>
    /// Project name (without extension).
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Relative path from repository root.
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    /// Project type (e.g., "csharp", "fsharp", "vb").
    /// </summary>
    public string ProjectType { get; init; } = "";

    /// <summary>
    /// Target framework(s) from project file.
    /// </summary>
    public IReadOnlyList<string> TargetFrameworks { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Project references.
    /// </summary>
    public IReadOnlyList<string> ProjectReferences { get; init; } = Array.Empty<string>();

    /// <summary>
    /// NuGet package references.
    /// </summary>
    public IReadOnlyList<PackageReference> PackageReferences { get; init; } = Array.Empty<PackageReference>();

    /// <summary>
    /// Whether this is a test project.
    /// </summary>
    public bool IsTestProject { get; init; }

    /// <summary>
    /// Source files in the project.
    /// </summary>
    public IReadOnlyList<string> SourceFiles { get; init; } = Array.Empty<string>();
}

/// <summary>
/// NuGet package reference information.
/// </summary>
public sealed class PackageReference
{
    /// <summary>
    /// Package name.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Package version.
    /// </summary>
    public string Version { get; init; } = "";
}

/// <summary>
/// Represents a directory node in the repository structure.
/// </summary>
public sealed class DirectoryNode
{
    /// <summary>
    /// Directory name.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Full path to the directory.
    /// </summary>
    public string Path { get; init; } = "";

    /// <summary>
    /// Relative path from repository root.
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    /// Child directories.
    /// </summary>
    public IReadOnlyList<DirectoryNode> Directories { get; init; } = Array.Empty<DirectoryNode>();

    /// <summary>
    /// Files in this directory.
    /// </summary>
    public IReadOnlyList<FileInfo> Files { get; init; } = Array.Empty<FileInfo>();
}

/// <summary>
/// File information in the directory structure.
/// </summary>
public sealed class FileInfo
{
    /// <summary>
    /// File name.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Relative path from repository root.
    /// </summary>
    public string RelativePath { get; init; } = "";

    /// <summary>
    /// File extension.
    /// </summary>
    public string Extension { get; init; } = "";

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// Last modified timestamp.
    /// </summary>
    public DateTimeOffset LastModified { get; init; }

    /// <summary>
    /// File classification (code, test, config, etc.).
    /// </summary>
    public string Classification { get; init; } = "";
}

/// <summary>
/// Repository scan statistics.
/// </summary>
public sealed class RepositoryStatistics
{
    /// <summary>
    /// Total number of files scanned.
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    /// Total number of directories scanned.
    /// </summary>
    public int TotalDirectories { get; init; }

    /// <summary>
    /// Total size in bytes.
    /// </summary>
    public long TotalSizeBytes { get; init; }

    /// <summary>
    /// Number of solution files found.
    /// </summary>
    public int SolutionCount { get; init; }

    /// <summary>
    /// Number of project files found.
    /// </summary>
    public int ProjectCount { get; init; }

    /// <summary>
    /// Number of source code files.
    /// </summary>
    public int SourceFileCount { get; init; }

    /// <summary>
    /// Number of test files.
    /// </summary>
    public int TestFileCount { get; init; }

    /// <summary>
    /// Number of configuration files.
    /// </summary>
    public int ConfigFileCount { get; init; }
}

/// <summary>
/// Detected technology stack for the repository.
/// </summary>
public sealed class TechnologyStack
{
    /// <summary>
    /// Detected programming languages with file counts.
    /// </summary>
    public IReadOnlyList<LanguageInfo> Languages { get; init; } = Array.Empty<LanguageInfo>();

    /// <summary>
    /// Detected frameworks (e.g., .NET versions, ASP.NET, etc.).
    /// </summary>
    public IReadOnlyList<FrameworkInfo> Frameworks { get; init; } = Array.Empty<FrameworkInfo>();

    /// <summary>
    /// Detected build tools (e.g., MSBuild, dotnet CLI).
    /// </summary>
    public IReadOnlyList<BuildToolInfo> BuildTools { get; init; } = Array.Empty<BuildToolInfo>();

    /// <summary>
    /// Detected package managers (e.g., NuGet).
    /// </summary>
    public IReadOnlyList<PackageManagerInfo> PackageManagers { get; init; } = Array.Empty<PackageManagerInfo>();
}

/// <summary>
/// Information about a detected programming language.
/// </summary>
public sealed class LanguageInfo
{
    /// <summary>
    /// Language name (e.g., "csharp", "javascript", "python").
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// File extension associated with this language.
    /// </summary>
    public string Extension { get; init; } = "";

    /// <summary>
    /// Number of files of this language type.
    /// </summary>
    public int FileCount { get; init; }

    /// <summary>
    /// Total size in bytes of files of this language.
    /// </summary>
    public long TotalSizeBytes { get; init; }
}

/// <summary>
/// Information about a detected framework.
/// </summary>
public sealed class FrameworkInfo
{
    /// <summary>
    /// Framework name (e.g., ".NET", "ASP.NET Core", "xUnit").
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Framework version or target framework moniker.
    /// </summary>
    public string Version { get; init; } = "";

    /// <summary>
    /// Type of framework (runtime, testing, web, etc.).
    /// </summary>
    public string Type { get; init; } = "";

    /// <summary>
    /// Projects using this framework.
    /// </summary>
    public IReadOnlyList<string> ProjectPaths { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Information about a detected build tool.
/// </summary>
public sealed class BuildToolInfo
{
    /// <summary>
    /// Build tool name (e.g., "MSBuild", "dotnet", "Docker").
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Build tool version if detected.
    /// </summary>
    public string Version { get; init; } = "";

    /// <summary>
    /// Type of build tool (compiler, orchestrator, container).
    /// </summary>
    public string Type { get; init; } = "";

    /// <summary>
    /// Files associated with this build tool.
    /// </summary>
    public IReadOnlyList<string> ConfigFiles { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Information about a detected package manager.
/// </summary>
public sealed class PackageManagerInfo
{
    /// <summary>
    /// Package manager name (e.g., "NuGet", "npm").
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Number of packages referenced.
    /// </summary>
    public int PackageCount { get; init; }

    /// <summary>
    /// Unique package names referenced.
    /// </summary>
    public IReadOnlyList<string> Packages { get; init; } = Array.Empty<string>();
}
