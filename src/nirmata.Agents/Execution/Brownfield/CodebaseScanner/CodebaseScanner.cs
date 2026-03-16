using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace nirmata.Agents.Execution.Brownfield.CodebaseScanner;

/// <summary>
/// Implementation of the Codebase Scanner workflow.
/// Scans repository structure and builds codebase intelligence.
/// </summary>
public sealed class CodebaseScanner : ICodebaseScanner
{
    private static readonly HashSet<string> RepositoryRootMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".hg",              // Mercurial
        ".svn",             // Subversion
        ".bzr",             // Bazaar
        "package.json",     // Node.js
        "Cargo.toml",       // Rust
        "pom.xml",          // Maven
        "build.gradle",     // Gradle
        "build.gradle.kts", // Gradle Kotlin
        "pom.xml",          // Maven
        "pyproject.toml",   // Python
        "setup.py",         // Python
        "requirements.txt", // Python
        "go.mod",           // Go
        "Gemfile",          // Ruby
        "composer.json",    // PHP
        ".gitignore",
        ".gitattributes",
        "README.md",
        "README.rst",
        "LICENSE",
        "LICENSE.txt",
        "LICENSE.md"
    };

    private static readonly HashSet<string> SolutionExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sln",
        ".slnx"
    };

    private static readonly HashSet<string> ProjectExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj",
        ".vbproj",
        ".fsproj",
        ".vcxproj",
        ".shproj",
        ".esproj",
        ".msbuildproj"
    };

    private static readonly HashSet<string> TestProjectIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tests",
        ".test",
        "tests.",
        "test.",
        ".spec",
        ".unittest",
        ".integrationtest",
        "unittest",
        "integrationtest"
    };

    /// <summary>
    /// Cache entry for file metadata to support incremental scanning.
    /// </summary>
    private sealed class FileCacheEntry
    {
        public string Path { get; set; } = "";
        public long SizeBytes { get; set; }
        public DateTimeOffset LastModified { get; set; }
        public string ContentHash { get; set; } = "";
    }

    /// <summary>
    /// Cache for storing intermediate scan results.
    /// </summary>
    private sealed class ScanCache
    {
        private readonly string _cacheDir;
        private readonly bool _enabled;

        public ScanCache(string? cacheDir, bool enabled)
        {
            _enabled = enabled && !string.IsNullOrEmpty(cacheDir);
            _cacheDir = cacheDir ?? Path.Combine(Path.GetTempPath(), "nirmata-codebase-scan-cache");
            if (_enabled)
            {
                Directory.CreateDirectory(_cacheDir);
            }
        }

        public bool IsEnabled => _enabled;

        public string GetCacheFilePath(string key)
        {
            var safeKey = string.Concat(key.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_cacheDir, $"{safeKey}.json");
        }

        public async Task<T?> LoadAsync<T>(string key) where T : class
        {
            if (!_enabled) return null;
            var path = GetCacheFilePath(key);
            if (!File.Exists(path)) return null;
            try
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveAsync<T>(string key, T value) where T : class
        {
            if (!_enabled) return;
            var path = GetCacheFilePath(key);
            var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false });
            await File.WriteAllTextAsync(path, json);
        }

        public bool IsFileChanged(FileCacheEntry? cached, string filePath)
        {
            if (!_enabled || cached == null) return true;
            try
            {
                var info = new System.IO.FileInfo(filePath);
                if (!info.Exists) return true;
                return info.LastWriteTimeUtc > cached.LastModified.UtcDateTime || info.Length != cached.SizeBytes;
            }
            catch
            {
                return true;
            }
        }
    }

    /// <inheritdoc />
    public async Task<CodebaseScanResult> ScanAsync(CodebaseScanRequest request, IProgress<CodebaseScanProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        const int TotalSteps = 6;
        var currentStep = 0;
        var cache = new ScanCache(request.Options.CacheDirectoryPath, request.Options.EnableCaching);

        try
        {
            // Step 1: Detect repository root
            ReportProgress(progress, CodebaseScanPhase.DetectingRepository, ++currentStep, TotalSteps, "Detecting repository root...");
            var startPath = ResolveStartPath(request.RepositoryPath);
            var repositoryRoot = DetectRepositoryRoot(startPath);

            if (string.IsNullOrEmpty(repositoryRoot))
            {
                ReportProgress(progress, CodebaseScanPhase.Failed, currentStep, TotalSteps, "Failed to detect repository root");
                return new CodebaseScanResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Could not detect repository root. No version control or project markers found."
                };
            }

            var scanTimestamp = DateTimeOffset.UtcNow;
            var repositoryName = Path.GetFileName(repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // Step 2: Discover solutions and projects
            ReportProgress(progress, CodebaseScanPhase.DiscoveringSolutions, ++currentStep, TotalSteps, "Discovering solution files...");
            var (solutions, projects) = await DiscoverSolutionsAndProjectsAsync(repositoryRoot, request.Options, cache, progress, currentStep, TotalSteps, ct);

            // Step 3: Build directory structure
            ReportProgress(progress, CodebaseScanPhase.BuildingStructure, ++currentStep, TotalSteps, "Building directory structure...", 0, 0);
            var rootDirectory = await BuildDirectoryStructureAsync(repositoryRoot, repositoryRoot, request.Options, cache, progress, currentStep, TotalSteps, ct);

            // Step 4: Calculate statistics
            ReportProgress(progress, CodebaseScanPhase.CalculatingStatistics, ++currentStep, TotalSteps, "Calculating repository statistics...");
            var statistics = CalculateStatistics(rootDirectory, solutions, projects);

            // Step 5: Detect technology stack
            ReportProgress(progress, CodebaseScanPhase.AnalyzingStack, ++currentStep, TotalSteps, "Analyzing technology stack...");
            var technologyStack = DetectTechnologyStack(projects, rootDirectory);

            // Step 6: Complete
            ReportProgress(progress, CodebaseScanPhase.Completed, ++currentStep, TotalSteps,
                $"Scan completed. Found {solutions.Count} solution(s), {projects.Count} project(s), {statistics.TotalFiles} file(s).");

            return new CodebaseScanResult
            {
                IsSuccess = true,
                RepositoryRoot = repositoryRoot,
                RepositoryName = repositoryName,
                Solutions = solutions,
                Projects = projects,
                RootDirectory = rootDirectory,
                ScanTimestamp = scanTimestamp,
                Statistics = statistics,
                TechnologyStack = technologyStack
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ReportProgress(progress, CodebaseScanPhase.Failed, currentStep, TotalSteps, $"Scan failed: {ex.Message}");
            return new CodebaseScanResult
            {
                IsSuccess = false,
                ErrorMessage = $"Codebase scan failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Reports progress to the progress handler if available.
    /// </summary>
    private static void ReportProgress(
        IProgress<CodebaseScanProgress>? progress,
        CodebaseScanPhase phase,
        int stepNumber,
        int totalSteps,
        string message,
        int itemsProcessed = 0,
        int totalItems = 0)
    {
        progress?.Report(new CodebaseScanProgress
        {
            Phase = phase,
            StepNumber = stepNumber,
            TotalSteps = totalSteps,
            PercentComplete = totalSteps > 0 ? (stepNumber * 100) / totalSteps : 0,
            Message = message,
            ItemsProcessed = itemsProcessed,
            TotalItems = totalItems
        });
    }

    /// <summary>
    /// Resolves the starting path for repository detection.
    /// </summary>
    private static string ResolveStartPath(string? providedPath)
    {
        if (!string.IsNullOrEmpty(providedPath))
        {
            var fullPath = Path.GetFullPath(providedPath);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
            throw new DirectoryNotFoundException($"Provided path does not exist: {providedPath}");
        }

        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Detects the repository root by looking for version control or project markers.
    /// Walks up the directory tree until a marker is found or root is reached.
    /// </summary>
    private static string? DetectRepositoryRoot(string startPath)
    {
        var currentPath = startPath;

        while (!string.IsNullOrEmpty(currentPath))
        {
            // Check for directory markers
            foreach (var marker in RepositoryRootMarkers.Where(m => !m.Contains('.')))
            {
                var markerPath = Path.Combine(currentPath, marker);
                if (Directory.Exists(markerPath))
                {
                    return currentPath;
                }
            }

            // Check for file markers
            foreach (var marker in RepositoryRootMarkers.Where(m => m.Contains('.')))
            {
                var markerPath = Path.Combine(currentPath, marker);
                if (File.Exists(markerPath))
                {
                    return currentPath;
                }
            }

            // Also check for solution files as fallback
            if (Directory.GetFiles(currentPath, "*.sln").Length > 0 ||
                Directory.GetFiles(currentPath, "*.slnx").Length > 0)
            {
                return currentPath;
            }

            // Move up one directory
            var parentPath = Directory.GetParent(currentPath)?.FullName;
            if (parentPath == null || parentPath == currentPath)
            {
                break;
            }
            currentPath = parentPath;
        }

        // If no root found, return the start path as a fallback
        return startPath;
    }

    /// <summary>
    /// Discovers all solution files and their referenced projects.
    /// </summary>
    private async Task<(IReadOnlyList<SolutionInfo> Solutions, IReadOnlyList<ProjectInfo> Projects)> DiscoverSolutionsAndProjectsAsync(
        string repositoryRoot,
        CodebaseScanOptions options,
        ScanCache cache,
        IProgress<CodebaseScanProgress>? progress,
        int currentStep,
        int totalSteps,
        CancellationToken ct)
    {
        var solutions = new List<SolutionInfo>();
        var allProjects = new ConcurrentDictionary<string, ProjectInfo>(StringComparer.OrdinalIgnoreCase);

        // Find all solution files
        var solutionFiles = new List<string>();
        foreach (var extension in SolutionExtensions)
        {
            solutionFiles.AddRange(Directory.GetFiles(repositoryRoot, $"*{extension}", SearchOption.AllDirectories));
        }

        // Filter out excluded patterns
        solutionFiles = solutionFiles
            .Where(f => !IsExcluded(f, options.ExcludePatterns))
            .ToList();

        int processedCount = 0;
        int totalCount = solutionFiles.Count;

        // Process solutions in parallel if enabled
        var solutionBag = new ConcurrentBag<SolutionInfo>();
        if (options.EnableParallelProcessing && solutionFiles.Count > 1)
        {
            await Parallel.ForEachAsync(solutionFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct }, async (solutionPath, pct) =>
            {
                var localProcessed = Interlocked.Increment(ref processedCount);
                ReportProgress(progress, CodebaseScanPhase.DiscoveringSolutions, currentStep, totalSteps,
                    $"Loading solution {localProcessed} of {totalCount}: {Path.GetFileName(solutionPath)}", localProcessed, totalCount);

                var projectPaths = await ExtractProjectsFromSolutionAsync(solutionPath, pct);
                var solution = new SolutionInfo
                {
                    Path = solutionPath,
                    Name = Path.GetFileNameWithoutExtension(solutionPath),
                    RelativePath = Path.GetRelativePath(repositoryRoot, solutionPath),
                    ProjectPaths = projectPaths
                };
                solutionBag.Add(solution);

                // Queue project paths for discovery with caching
                foreach (var projectPath in projectPaths)
                {
                    await LoadProjectInfoWithCacheAsync(projectPath, repositoryRoot, allProjects, cache, pct);
                }
            });
            solutions = solutionBag.ToList();
        }
        else
        {
            foreach (var solutionPath in solutionFiles)
            {
                ct.ThrowIfCancellationRequested();

                ReportProgress(progress, CodebaseScanPhase.DiscoveringSolutions, currentStep, totalSteps,
                    $"Loading solution {++processedCount} of {totalCount}: {Path.GetFileName(solutionPath)}", processedCount, totalCount);

                var projectPaths = await ExtractProjectsFromSolutionAsync(solutionPath, ct);
                var solution = new SolutionInfo
                {
                    Path = solutionPath,
                    Name = Path.GetFileNameWithoutExtension(solutionPath),
                    RelativePath = Path.GetRelativePath(repositoryRoot, solutionPath),
                    ProjectPaths = projectPaths
                };
                solutions.Add(solution);

                // Queue project paths for discovery
                foreach (var projectPath in projectPaths)
                {
                    await LoadProjectInfoWithCacheAsync(projectPath, repositoryRoot, allProjects, cache, ct);
                }
            }
        }

        // Also discover orphaned projects not in any solution
        ReportProgress(progress, CodebaseScanPhase.DiscoveringSolutions, currentStep, totalSteps,
            $"Discovering orphaned projects...", processedCount, totalCount);
        var orphanedProjects = await DiscoverOrphanedProjectsAsync(repositoryRoot, allProjects.Keys, options, cache, progress, currentStep, totalSteps, ct);
        foreach (var project in orphanedProjects)
        {
            allProjects[project.Path] = project;
        }

        return (solutions.AsReadOnly(), allProjects.Values.ToList().AsReadOnly());
    }

    /// <summary>
    /// Loads project info with caching support.
    /// </summary>
    private async Task LoadProjectInfoWithCacheAsync(
        string projectPath,
        string repositoryRoot,
        ConcurrentDictionary<string, ProjectInfo> allProjects,
        ScanCache cache,
        CancellationToken ct)
    {
        if (allProjects.ContainsKey(projectPath)) return;

        // Check incremental scan - skip if file hasn't changed
        if (cache.IsEnabled)
        {
            var cacheKey = $"project:{projectPath}";
            var cached = await cache.LoadAsync<FileCacheEntry>(cacheKey);
            if (!cache.IsFileChanged(cached, projectPath))
            {
                // Load from cache
                var cachedProject = await cache.LoadAsync<ProjectInfo>(cacheKey + ":data");
                if (cachedProject != null)
                {
                    allProjects.TryAdd(projectPath, cachedProject);
                    return;
                }
            }
        }

        var projectInfo = await LoadProjectInfoAsync(projectPath, repositoryRoot, ct);
        if (projectInfo != null)
        {
            allProjects.TryAdd(projectPath, projectInfo);

            // Save to cache
            if (cache.IsEnabled)
            {
                var cacheKey = $"project:{projectPath}";
                var fileInfo = new System.IO.FileInfo(projectPath);
                await cache.SaveAsync(cacheKey, new FileCacheEntry
                {
                    Path = projectPath,
                    SizeBytes = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTimeUtc
                });
                await cache.SaveAsync(cacheKey + ":data", projectInfo);
            }
        }
    }

    /// <summary>
    /// Extracts project paths from a solution file.
    /// </summary>
    private async Task<IReadOnlyList<string>> ExtractProjectsFromSolutionAsync(string solutionPath, CancellationToken ct)
    {
        var projectPaths = new List<string>();
        var solutionDir = Path.GetDirectoryName(solutionPath) ?? "";

        try
        {
            var content = await File.ReadAllTextAsync(solutionPath, ct);
            var lines = content.Split('\n');

            // Parse traditional .sln files
            if (solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                // Pattern: Project("{GUID}") = "Name", "Path", "{GUID}"
                var projectPattern = new Regex(@"Project\(""\{[^}]+\}""\)\s*=\s*""[^""]+""\s*,\s*""([^""]+)""", RegexOptions.Compiled);

                foreach (var line in lines)
                {
                    var match = projectPattern.Match(line);
                    if (match.Success)
                    {
                        var relativePath = match.Groups[1].Value.Trim();
                        var fullPath = Path.GetFullPath(Path.Combine(solutionDir, relativePath));
                        if (File.Exists(fullPath))
                        {
                            projectPaths.Add(fullPath);
                        }
                    }
                }
            }
            // Parse .slnx XML files
            else if (solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var doc = XDocument.Parse(content);
                    var projectElements = doc.Descendants("Project");
                    foreach (var projectElement in projectElements)
                    {
                        var pathAttr = projectElement.Attribute("Path");
                        if (pathAttr != null)
                        {
                            var relativePath = pathAttr.Value;
                            var fullPath = Path.GetFullPath(Path.Combine(solutionDir, relativePath));
                            if (File.Exists(fullPath))
                            {
                                projectPaths.Add(fullPath);
                            }
                        }
                    }
                }
                catch
                {
                    // If XML parsing fails, skip this solution
                }
            }
        }
        catch
        {
            // If reading fails, return empty list
        }

        return projectPaths.AsReadOnly();
    }

    /// <summary>
    /// Loads project information from a project file.
    /// </summary>
    private async Task<ProjectInfo?> LoadProjectInfoAsync(string projectPath, string repositoryRoot, CancellationToken ct)
    {
        try
        {
            var content = await File.ReadAllTextAsync(projectPath, ct);
            var doc = XDocument.Parse(content);

            // Get project name
            var name = Path.GetFileNameWithoutExtension(projectPath);

            // Determine project type from extension
            var extension = Path.GetExtension(projectPath).ToLowerInvariant();
            var projectType = extension switch
            {
                ".csproj" => "csharp",
                ".vbproj" => "vb",
                ".fsproj" => "fsharp",
                ".vcxproj" => "cpp",
                ".shproj" => "shared",
                ".esproj" => "javascript",
                ".msbuildproj" => "msbuild",
                _ => "unknown"
            };

            // Extract target frameworks
            var targetFrameworks = new List<string>();
            var tfmElements = doc.Descendants("TargetFramework")
                .Concat(doc.Descendants("TargetFrameworks"));
            foreach (var tfm in tfmElements)
            {
                var value = tfm.Value;
                if (!string.IsNullOrEmpty(value))
                {
                    // Handle multiple TFMs separated by semicolons
                    targetFrameworks.AddRange(value.Split(';', StringSplitOptions.RemoveEmptyEntries));
                }
            }

            // Extract project references
            var projectReferences = doc.Descendants("ProjectReference")
                .Select(pr => pr.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, v!)))
                .ToList();

            // Extract package references
            var packageReferences = doc.Descendants("PackageReference")
                .Select(pr => new PackageReference
                {
                    Name = pr.Attribute("Include")?.Value ?? pr.Attribute("Update")?.Value ?? "",
                    Version = pr.Attribute("Version")?.Value ?? pr.Element("Version")?.Value ?? ""
                })
                .Where(pr => !string.IsNullOrEmpty(pr.Name))
                .ToList();

            // Check if test project
            var isTestProject = IsTestProject(name, packageReferences);

            // Find source files
            var projectDir = Path.GetDirectoryName(projectPath)!;
            var sourceFiles = FindSourceFiles(projectDir, projectType);

            return new ProjectInfo
            {
                Path = projectPath,
                Name = name,
                RelativePath = Path.GetRelativePath(repositoryRoot, projectPath),
                ProjectType = projectType,
                TargetFrameworks = targetFrameworks.AsReadOnly(),
                ProjectReferences = projectReferences.AsReadOnly(),
                PackageReferences = packageReferences.AsReadOnly(),
                IsTestProject = isTestProject,
                SourceFiles = sourceFiles
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Discovers project files not referenced by any solution.
    /// </summary>
    private async Task<IReadOnlyList<ProjectInfo>> DiscoverOrphanedProjectsAsync(
        string repositoryRoot,
        IEnumerable<string> knownProjectPaths,
        CodebaseScanOptions options,
        ScanCache cache,
        IProgress<CodebaseScanProgress>? progress,
        int currentStep,
        int totalSteps,
        CancellationToken ct)
    {
        var knownSet = new HashSet<string>(knownProjectPaths, StringComparer.OrdinalIgnoreCase);
        var orphanedProjects = new ConcurrentBag<ProjectInfo>();

        foreach (var extension in ProjectExtensions)
        {
            var projectFiles = Directory.GetFiles(repositoryRoot, $"*{extension}", SearchOption.AllDirectories);
            var filteredFiles = projectFiles
                .Where(p => !knownSet.Contains(p) && !IsExcluded(p, options.ExcludePatterns))
                .ToList();

            int processedCount = 0;
            int totalCount = filteredFiles.Count;

            if (options.EnableParallelProcessing && filteredFiles.Count > 1)
            {
                await Parallel.ForEachAsync(filteredFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct }, async (projectPath, pct) =>
                {
                    var localProcessed = Interlocked.Increment(ref processedCount);
                    if (localProcessed % 10 == 0 || localProcessed == totalCount)
                    {
                        ReportProgress(progress, CodebaseScanPhase.LoadingProjects, currentStep, totalSteps,
                            $"Loading orphaned project {localProcessed} of {totalCount}...", localProcessed, totalCount);
                    }

                    var projectInfo = await LoadProjectInfoWithIncrementalCheckAsync(projectPath, repositoryRoot, cache, options, pct);
                    if (projectInfo != null)
                    {
                        orphanedProjects.Add(projectInfo);
                    }
                });
            }
            else
            {
                foreach (var projectPath in filteredFiles)
                {
                    ct.ThrowIfCancellationRequested();

                    processedCount++;
                    if (processedCount % 10 == 0 || processedCount == totalCount)
                    {
                        ReportProgress(progress, CodebaseScanPhase.LoadingProjects, currentStep, totalSteps,
                            $"Loading orphaned project {processedCount} of {totalCount}...", processedCount, totalCount);
                    }

                    var projectInfo = await LoadProjectInfoWithIncrementalCheckAsync(projectPath, repositoryRoot, cache, options, ct);
                    if (projectInfo != null)
                    {
                        orphanedProjects.Add(projectInfo);
                    }
                }
            }
        }

        return orphanedProjects.ToList().AsReadOnly();
    }

    /// <summary>
    /// Loads project info with incremental scan support.
    /// </summary>
    private async Task<ProjectInfo?> LoadProjectInfoWithIncrementalCheckAsync(
        string projectPath,
        string repositoryRoot,
        ScanCache cache,
        CodebaseScanOptions options,
        CancellationToken ct)
    {
        // Check incremental scan
        if (options.EnableIncrementalScan && options.PreviousScanTimestamp.HasValue)
        {
            try
            {
                var lastModified = File.GetLastWriteTimeUtc(projectPath);
                if (lastModified < options.PreviousScanTimestamp.Value.UtcDateTime)
                {
                    // Try to load from cache
                    if (cache.IsEnabled)
                    {
                        var cached = await cache.LoadAsync<ProjectInfo>($"project:{projectPath}:data");
                        if (cached != null)
                        {
                            return cached;
                        }
                    }
                }
            }
            catch
            {
                // Fall through to load fresh
            }
        }

        var projectInfo = await LoadProjectInfoAsync(projectPath, repositoryRoot, ct);
        if (projectInfo != null && cache.IsEnabled)
        {
            var cacheKey = $"project:{projectPath}";
            var fileInfo = new System.IO.FileInfo(projectPath);
            await cache.SaveAsync(cacheKey, new FileCacheEntry
            {
                Path = projectPath,
                SizeBytes = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc
            });
            await cache.SaveAsync(cacheKey + ":data", projectInfo);
        }
        return projectInfo;
    }

    /// <summary>
    /// Builds the directory structure tree.
    /// </summary>
    private async Task<DirectoryNode> BuildDirectoryStructureAsync(
        string directoryPath,
        string repositoryRoot,
        CodebaseScanOptions options,
        ScanCache cache,
        IProgress<CodebaseScanProgress>? progress,
        int currentStep,
        int totalSteps,
        CancellationToken ct,
        int currentDepth = 0)
    {
        var dirName = Path.GetFileName(directoryPath) ?? "";
        var relativePath = Path.GetRelativePath(repositoryRoot, directoryPath);
        if (relativePath == ".") relativePath = "";

        var files = new ConcurrentBag<FileInfo>();
        var directories = new ConcurrentBag<DirectoryNode>();

        try
        {
            // Process files
            var fileEntries = Directory.GetFiles(directoryPath);

            // Use parallel processing for files if enabled and there are many files
            if (options.EnableParallelProcessing && fileEntries.Length > 10)
            {
                await Parallel.ForEachAsync(fileEntries, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct }, async (filePath, pct) =>
                {
                    if (IsExcluded(filePath, options.ExcludePatterns))
                    {
                        return;
                    }

                    // Check incremental scan
                    if (options.EnableIncrementalScan && options.PreviousScanTimestamp.HasValue)
                    {
                        try
                        {
                            var lastModified = System.IO.File.GetLastWriteTimeUtc(filePath);
                            if (lastModified < options.PreviousScanTimestamp.Value.UtcDateTime)
                            {
                                // Try to load from cache
                                if (cache.IsEnabled)
                                {
                                    var cached = await cache.LoadAsync<FileInfo>($"file:{filePath}");
                                    if (cached != null)
                                    {
                                        files.Add(cached);
                                        return;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Fall through to scan fresh
                        }
                    }

                    var fileInfo = await CreateFileInfoAsync(filePath, repositoryRoot);
                    files.Add(fileInfo);

                    // Save to cache
                    if (cache.IsEnabled)
                    {
                        await cache.SaveAsync($"file:{filePath}", fileInfo);
                    }
                });
            }
            else
            {
                foreach (var filePath in fileEntries)
                {
                    ct.ThrowIfCancellationRequested();

                    if (IsExcluded(filePath, options.ExcludePatterns))
                    {
                        continue;
                    }

                    // Check incremental scan
                    if (options.EnableIncrementalScan && options.PreviousScanTimestamp.HasValue)
                    {
                        try
                        {
                            var lastModified = System.IO.File.GetLastWriteTimeUtc(filePath);
                            if (lastModified < options.PreviousScanTimestamp.Value.UtcDateTime)
                            {
                                // Try to load from cache
                                if (cache.IsEnabled)
                                {
                                    var cached = await cache.LoadAsync<FileInfo>($"file:{filePath}");
                                    if (cached != null)
                                    {
                                        files.Add(cached);
                                        continue;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Fall through to scan fresh
                        }
                    }

                    var fileInfo = await CreateFileInfoAsync(filePath, repositoryRoot);
                    files.Add(fileInfo);

                    // Save to cache
                    if (cache.IsEnabled)
                    {
                        await cache.SaveAsync($"file:{filePath}", fileInfo);
                    }
                }
            }

            // Report progress for current directory
            if (!string.IsNullOrEmpty(relativePath) && progress != null)
            {
                ReportProgress(progress, CodebaseScanPhase.BuildingStructure, currentStep, totalSteps,
                    $"Scanning directory: {relativePath}", 0, 0);
            }

            // Process subdirectories
            if (options.MaxDepth == 0 || currentDepth < options.MaxDepth)
            {
                var dirEntries = Directory.GetDirectories(directoryPath);

                // Use parallel processing for subdirectories if enabled
                if (options.EnableParallelProcessing && dirEntries.Length > 3 && currentDepth < 2)
                {
                    await Parallel.ForEachAsync(dirEntries, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct }, async (subDirPath, pct) =>
                    {
                        var subDirName = Path.GetFileName(subDirPath);

                        // Skip hidden directories unless explicitly included
                        if (!options.IncludeHiddenDirectories && subDirName.StartsWith('.'))
                        {
                            return;
                        }

                        if (IsExcluded(subDirPath, options.ExcludePatterns))
                        {
                            return;
                        }

                        var subDirNode = await BuildDirectoryStructureAsync(
                            subDirPath, repositoryRoot, options, cache, progress, currentStep, totalSteps, pct, currentDepth + 1);
                        directories.Add(subDirNode);
                    });
                }
                else
                {
                    foreach (var subDirPath in dirEntries)
                    {
                        ct.ThrowIfCancellationRequested();

                        var subDirName = Path.GetFileName(subDirPath);

                        // Skip hidden directories unless explicitly included
                        if (!options.IncludeHiddenDirectories && subDirName.StartsWith('.'))
                        {
                            continue;
                        }

                        if (IsExcluded(subDirPath, options.ExcludePatterns))
                        {
                            continue;
                        }

                        var subDirNode = await BuildDirectoryStructureAsync(
                            subDirPath, repositoryRoot, options, cache, progress, currentStep, totalSteps, ct, currentDepth + 1);
                        directories.Add(subDirNode);
                    }
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }

        return new DirectoryNode
        {
            Name = dirName,
            Path = directoryPath,
            RelativePath = relativePath,
            Files = files.OrderBy(f => f.Name).ToList().AsReadOnly(),
            Directories = directories.OrderBy(d => d.Name).ToList().AsReadOnly()
        };
    }

    /// <summary>
    /// Creates a FileInfo object asynchronously.
    /// </summary>
    private static async Task<FileInfo> CreateFileInfoAsync(string filePath, string repositoryRoot)
    {
        var fileInfo = new System.IO.FileInfo(filePath);
        return new FileInfo
        {
            Name = Path.GetFileName(filePath),
            RelativePath = Path.GetRelativePath(repositoryRoot, filePath),
            Extension = Path.GetExtension(filePath).ToLowerInvariant(),
            SizeBytes = fileInfo.Length,
            LastModified = fileInfo.LastWriteTimeUtc,
            Classification = ClassifyFile(filePath)
        };
    }

    /// <summary>
    /// Determines if a project is a test project based on name or package references.
    /// </summary>
    private static bool IsTestProject(string projectName, IReadOnlyList<PackageReference> packages)
    {
        var nameLower = projectName.ToLowerInvariant();

        // Check name indicators
        foreach (var indicator in TestProjectIndicators)
        {
            if (nameLower.Contains(indicator))
            {
                return true;
            }
        }

        // Check for common test packages
        var testPackages = new[] { "xunit", "nunit", "mstest", "microsoft.net.test.sdk" };
        foreach (var package in packages)
        {
            var packageNameLower = package.Name.ToLowerInvariant();
            foreach (var testPackage in testPackages)
            {
                if (packageNameLower.Contains(testPackage))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Finds source files in a project directory.
    /// </summary>
    private static IReadOnlyList<string> FindSourceFiles(string projectDir, string projectType)
    {
        var extensions = projectType switch
        {
            "csharp" => new[] { ".cs" },
            "vb" => new[] { ".vb" },
            "fsharp" => new[] { ".fs" },
            "cpp" => new[] { ".cpp", ".h", ".hpp", ".c" },
            "javascript" => new[] { ".js", ".ts", ".jsx", ".tsx" },
            _ => Array.Empty<string>()
        };

        var sourceFiles = new List<string>();
        foreach (var extension in extensions)
        {
            try
            {
                var files = Directory.GetFiles(projectDir, $"*{extension}", SearchOption.AllDirectories);
                sourceFiles.AddRange(files);
            }
            catch
            {
                // Skip if we can't access subdirectories
            }
        }

        return sourceFiles.AsReadOnly();
    }

    /// <summary>
    /// Classifies a file by type.
    /// </summary>
    private static string ClassifyFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var name = Path.GetFileName(filePath).ToLowerInvariant();

        // Code files
        var codeExtensions = new[] { ".cs", ".vb", ".fs", ".cpp", ".h", ".hpp", ".c", ".js", ".ts", ".jsx", ".tsx", ".py", ".go", ".rs", ".java", ".rb", ".php" };
        if (codeExtensions.Contains(extension))
        {
            return "code";
        }

        // Test files
        if (name.Contains("test") || name.Contains("spec"))
        {
            return "test";
        }

        // Config files
        var configExtensions = new[] { ".json", ".xml", ".yaml", ".yml", ".config", ".ini", ".toml" };
        var configNames = new[] { ".gitignore", ".gitattributes", ".editorconfig", "appsettings", "web.config" };
        if (configExtensions.Contains(extension) || configNames.Any(n => name.Contains(n)))
        {
            return "config";
        }

        // Documentation
        var docExtensions = new[] { ".md", ".rst", ".txt" };
        if (docExtensions.Contains(extension) && (name.StartsWith("readme") || name.StartsWith("license") || name.StartsWith("changelog")))
        {
            return "documentation";
        }

        // Build/CI files
        var buildNames = new[] { "dockerfile", "makefile", "jenkinsfile", ".yml", ".yaml" };
        if (buildNames.Any(n => name.Contains(n)) || name.EndsWith(".ps1") || name.EndsWith(".sh"))
        {
            return "build";
        }

        return "other";
    }

    /// <summary>
    /// Checks if a path matches any exclusion pattern.
    /// </summary>
    private static bool IsExcluded(string path, IReadOnlyList<string> patterns)
    {
        var pathLower = path.ToLowerInvariant();
        return patterns.Any(pattern =>
        {
            var patternLower = pattern.ToLowerInvariant();
            if (patternLower.EndsWith('/'))
            {
                // Directory pattern
                return pathLower.Contains(patternLower.TrimEnd('/') + "/") ||
                       pathLower.EndsWith("/" + patternLower.TrimEnd('/'));
            }
            if (patternLower.StartsWith("*."))
            {
                // Extension pattern
                return pathLower.EndsWith(patternLower.TrimStart('*'));
            }
            return pathLower.Contains(patternLower);
        });
    }

    /// <summary>
    /// Calculates repository statistics from the scanned structure.
    /// </summary>
    private static RepositoryStatistics CalculateStatistics(
        DirectoryNode root,
        IReadOnlyList<SolutionInfo> solutions,
        IReadOnlyList<ProjectInfo> projects)
    {
        var (fileCount, dirCount, totalSize, sourceCount, testCount, configCount) = CalculateDirectoryStats(root);

        return new RepositoryStatistics
        {
            TotalFiles = fileCount,
            TotalDirectories = dirCount,
            TotalSizeBytes = totalSize,
            SolutionCount = solutions.Count,
            ProjectCount = projects.Count,
            SourceFileCount = sourceCount,
            TestFileCount = testCount,
            ConfigFileCount = configCount
        };
    }

    /// <summary>
    /// Recursively calculates statistics from directory tree.
    /// </summary>
    private static (int files, int dirs, long size, int source, int test, int config) CalculateDirectoryStats(DirectoryNode node)
    {
        var fileCount = node.Files.Count;
        var dirCount = node.Directories.Count;
        var totalSize = node.Files.Sum(f => f.SizeBytes);
        var sourceCount = node.Files.Count(f => f.Classification == "code");
        var testCount = node.Files.Count(f => f.Classification == "test");
        var configCount = node.Files.Count(f => f.Classification == "config");

        foreach (var subDir in node.Directories)
        {
            var (f, d, s, src, tst, cfg) = CalculateDirectoryStats(subDir);
            fileCount += f;
            dirCount += d;
            totalSize += s;
            sourceCount += src;
            testCount += tst;
            configCount += cfg;
        }

        return (fileCount, dirCount, totalSize, sourceCount, testCount, configCount);
    }

    /// <summary>
    /// Detects the technology stack from projects and directory structure.
    /// </summary>
    private static TechnologyStack DetectTechnologyStack(
        IReadOnlyList<ProjectInfo> projects,
        DirectoryNode rootDirectory)
    {
        var languages = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        var frameworks = new Dictionary<string, FrameworkInfo>(StringComparer.OrdinalIgnoreCase);
        var buildTools = new List<BuildToolInfo>();
        var packageManagers = new Dictionary<string, PackageManagerInfo>(StringComparer.OrdinalIgnoreCase);

        // Aggregate language info from all files in directory structure
        CollectLanguageStats(rootDirectory, languages);

        // Analyze projects for frameworks and package managers
        foreach (var project in projects)
        {
            // Detect frameworks from target frameworks
            foreach (var tfm in project.TargetFrameworks)
            {
                var frameworkKey = $"{project.ProjectType}:{tfm}";
                if (!frameworks.TryGetValue(frameworkKey, out var framework))
                {
                    framework = new FrameworkInfo
                    {
                        Name = InferFrameworkName(tfm, project.ProjectType),
                        Version = tfm,
                        Type = InferFrameworkType(tfm),
                        ProjectPaths = new List<string>()
                    };
                    frameworks[frameworkKey] = framework;
                }
                ((List<string>)framework.ProjectPaths).Add(project.RelativePath);
            }

            // Detect test frameworks from package references
            foreach (var package in project.PackageReferences)
            {
                var testFrameworkName = DetectTestFramework(package.Name);
                if (!string.IsNullOrEmpty(testFrameworkName))
                {
                    var frameworkKey = $"test:{testFrameworkName}";
                    if (!frameworks.TryGetValue(frameworkKey, out var framework))
                    {
                        framework = new FrameworkInfo
                        {
                            Name = testFrameworkName,
                            Version = package.Version,
                            Type = "testing",
                            ProjectPaths = new List<string>()
                        };
                        frameworks[frameworkKey] = framework;
                    }
                    ((List<string>)framework.ProjectPaths).Add(project.RelativePath);
                }
            }

            // Track NuGet package manager
            if (project.PackageReferences.Count > 0)
            {
                if (!packageManagers.TryGetValue("nuget", out var nuget))
                {
                    nuget = new PackageManagerInfo
                    {
                        Name = "NuGet",
                        PackageCount = 0,
                        Packages = new List<string>()
                    };
                    packageManagers["nuget"] = nuget;
                }
                ((List<string>)nuget.Packages).AddRange(project.PackageReferences.Select(p => p.Name));
            }
        }

        // Detect build tools from files
        DetectBuildTools(rootDirectory, buildTools);

        // Always add MSBuild if we have .NET projects
        if (projects.Any(p => p.ProjectType is "csharp" or "vb" or "fsharp" or "cpp"))
        {
            buildTools.Add(new BuildToolInfo
            {
                Name = "MSBuild",
                Type = "build",
                ConfigFiles = Array.Empty<string>()
            });
        }

        // Deduplicate and count packages
        foreach (var pm in packageManagers.Values)
        {
            var uniquePackages = pm.Packages.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            ((List<string>)pm.Packages).Clear();
            ((List<string>)pm.Packages).AddRange(uniquePackages.OrderBy(p => p));
        }

        return new TechnologyStack
        {
            Languages = languages.Values.OrderBy(l => l.Name).ToList().AsReadOnly(),
            Frameworks = frameworks.Values.OrderBy(f => f.Name).ToList().AsReadOnly(),
            BuildTools = buildTools.OrderBy(b => b.Name).ToList().AsReadOnly(),
            PackageManagers = packageManagers.Values.OrderBy(pm => pm.Name).ToList().AsReadOnly()
        };
    }

    /// <summary>
    /// Collects language statistics from directory tree.
    /// </summary>
    private static void CollectLanguageStats(DirectoryNode node, Dictionary<string, LanguageInfo> languages)
    {
        foreach (var file in node.Files.Where(f => f.Classification == "code"))
        {
            var languageName = InferLanguageFromExtension(file.Extension);
            if (string.IsNullOrEmpty(languageName))
            {
                continue;
            }

            if (!languages.TryGetValue(languageName, out var lang))
            {
                lang = new LanguageInfo
                {
                    Name = languageName,
                    Extension = file.Extension,
                    FileCount = 0,
                    TotalSizeBytes = 0
                };
                languages[languageName] = lang;
            }

            // Update counts (LanguageInfo is immutable, create new)
            languages[languageName] = new LanguageInfo
            {
                Name = lang.Name,
                Extension = lang.Extension,
                FileCount = lang.FileCount + 1,
                TotalSizeBytes = lang.TotalSizeBytes + file.SizeBytes
            };
        }

        foreach (var subDir in node.Directories)
        {
            CollectLanguageStats(subDir, languages);
        }
    }

    /// <summary>
    /// Infers language name from file extension.
    /// </summary>
    private static string InferLanguageFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".vb" => "vb",
            ".fs" => "fsharp",
            ".cpp" or ".cxx" or ".cc" => "cpp",
            ".c" => "c",
            ".h" or ".hpp" => "cpp",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".jsx" => "javascript",
            ".tsx" => "typescript",
            ".py" => "python",
            ".go" => "go",
            ".rs" => "rust",
            ".java" => "java",
            ".rb" => "ruby",
            ".php" => "php",
            _ => ""
        };
    }

    /// <summary>
    /// Infers framework name from target framework moniker.
    /// </summary>
    private static string InferFrameworkName(string tfm, string projectType)
    {
        var tfmLower = tfm.ToLowerInvariant();

        // .NET Core / .NET 5+
        if (tfmLower.StartsWith("netcoreapp"))
        {
            return ".NET Core";
        }
        if (tfmLower.StartsWith("net") && char.IsDigit(tfmLower[3]))
        {
            return ".NET";
        }
        if (tfmLower.StartsWith("netstandard"))
        {
            return ".NET Standard";
        }
        if (tfmLower.StartsWith("net4") || tfmLower.StartsWith("net3") || tfmLower.StartsWith("net2"))
        {
            return ".NET Framework";
        }

        return projectType switch
        {
            "csharp" => ".NET",
            "vb" => ".NET",
            "fsharp" => ".NET",
            "cpp" => "C++",
            "javascript" => "Node.js",
            _ => projectType
        };
    }

    /// <summary>
    /// Infers framework type from target framework moniker.
    /// </summary>
    private static string InferFrameworkType(string tfm)
    {
        var tfmLower = tfm.ToLowerInvariant();

        if (tfmLower.Contains("web"))
        {
            return "web";
        }
        if (tfmLower.Contains("windows"))
        {
            return "desktop";
        }
        if (tfmLower.StartsWith("netstandard") || tfmLower.StartsWith("netcoreapp"))
        {
            return "runtime";
        }

        return "runtime";
    }

    /// <summary>
    /// Detects test framework from package name.
    /// </summary>
    private static string DetectTestFramework(string packageName)
    {
        var nameLower = packageName.ToLowerInvariant();

        if (nameLower.Contains("xunit"))
        {
            return "xUnit";
        }
        if (nameLower.Contains("nunit"))
        {
            return "NUnit";
        }
        if (nameLower.Contains("mstest"))
        {
            return "MSTest";
        }
        if (nameLower.Contains("microsoft.net.test.sdk"))
        {
            return "Microsoft.Test.SDK";
        }

        return "";
    }

    /// <summary>
    /// Detects build tools from directory structure.
    /// </summary>
    private static void DetectBuildTools(DirectoryNode node, List<BuildToolInfo> buildTools)
    {
        foreach (var file in node.Files)
        {
            var nameLower = file.Name.ToLowerInvariant();

            // Docker
            if (nameLower.Contains("dockerfile") || nameLower == ".dockerignore")
            {
                if (!buildTools.Any(b => b.Name == "Docker"))
                {
                    buildTools.Add(new BuildToolInfo
                    {
                        Name = "Docker",
                        Type = "container",
                        ConfigFiles = new List<string> { file.RelativePath }
                    });
                }
            }

            // PowerShell scripts
            if (nameLower.EndsWith(".ps1") && nameLower.Contains("build") || nameLower.Contains("deploy"))
            {
                if (!buildTools.Any(b => b.Name == "PowerShell"))
                {
                    buildTools.Add(new BuildToolInfo
                    {
                        Name = "PowerShell",
                        Type = "script",
                        ConfigFiles = new List<string>()
                    });
                }
            }

            // CI/CD files
            if (nameLower.EndsWith(".yml") || nameLower.EndsWith(".yaml"))
            {
                if (nameLower.Contains(".github/workflows"))
                {
                    if (!buildTools.Any(b => b.Name == "GitHub Actions"))
                    {
                        buildTools.Add(new BuildToolInfo
                        {
                            Name = "GitHub Actions",
                            Type = "ci",
                            ConfigFiles = new List<string>()
                        });
                    }
                }
            }
        }

        foreach (var subDir in node.Directories)
        {
            DetectBuildTools(subDir, buildTools);
        }
    }
}
