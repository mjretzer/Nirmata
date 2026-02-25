using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Gmsd.Agents.Execution.Brownfield.FileGraphBuilder;

/// <summary>
/// Implementation of the File Graph Builder.
/// Builds file dependency graphs from repository source files.
/// </summary>
public sealed class FileGraphBuilder : IFileGraphBuilder
{
    private static readonly Regex UsingDirectiveRegex = new(
        @"^\s*using\s+([\w.]+)\s*;?$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex NamespaceRegex = new(
        @"namespace\s+([\w.]+)",
        RegexOptions.Compiled);

    private static readonly Regex TypeReferenceRegex = new(
        @"(class|interface|struct|record|enum)\s+(\w+)(?:\s*:\s*([\w<>,\s]+))?",
        RegexOptions.Compiled);

    private static readonly Regex FieldDeclarationRegex = new(
        @"(?:public|private|protected|internal)?\s*(?:readonly\s+)?([\w<>\[\]]+)\s+(\w+)\s*[=;]",
        RegexOptions.Compiled);

    private static readonly Regex MethodParameterRegex = new(
        @"\(\s*([\w<>\[\]]+)\s+\w+",
        RegexOptions.Compiled);

    private static readonly Regex GenericTypeRegex = new(
        @"([\w.]+)<([\w<>,\s]+)>",
        RegexOptions.Compiled);

    /// <summary>
    /// Cache for storing intermediate graph building results.
    /// </summary>
    private sealed class GraphCache
    {
        private readonly string _cacheDir;
        private readonly bool _enabled;

        public GraphCache(string? cacheDir, bool enabled)
        {
            _enabled = enabled && !string.IsNullOrEmpty(cacheDir);
            _cacheDir = cacheDir ?? Path.Combine(Path.GetTempPath(), "gmsd-file-graph-cache");
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
    }

    /// <inheritdoc />
    public async Task<FileGraphResult> BuildAsync(FileGraphRequest request, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var repositoryRoot = Path.GetFullPath(request.RepositoryPath);
        var cache = new GraphCache(request.Options.CacheDirectoryPath, request.Options.EnableCaching);

        try
        {
            var nodes = new ConcurrentBag<FileNode>();
            var edges = new ConcurrentBag<FileEdge>();

            var projectFiles = request.ProjectFiles.Any()
                ? request.ProjectFiles.ToList()
                : Directory.GetFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
                    .Where(p => !IsExcluded(p, request.Options.ExcludePatterns))
                    .ToList();

            var projectGraph = await BuildProjectGraphAsync(projectFiles, repositoryRoot, nodes, edges, request.Options, cache, ct);

            if (request.Options.IncludeImportDependencies)
            {
                await BuildImportDependenciesAsync(projectGraph, repositoryRoot, nodes, edges, request.Options, cache, ct);
            }

            if (request.Options.IncludeIntraProjectEdges)
            {
                await BuildFileToFileRelationshipsAsync(projectGraph, repositoryRoot, nodes, edges, request.Options, cache, ct);
            }

            var nodeList = nodes.OrderBy(n => n.Id).ToList();
            var edgeList = edges.OrderBy(e => e.Id).ToList();

            if (request.Options.CalculateEdgeWeights)
            {
                edgeList = CalculateCouplingWeights(edgeList, nodeList);
            }

            stopwatch.Stop();

            return new FileGraphResult
            {
                IsSuccess = true,
                RepositoryRoot = repositoryRoot,
                BuildTimestamp = DateTimeOffset.UtcNow,
                Nodes = nodeList,
                Edges = edgeList,
                Statistics = CalculateStatistics(nodeList, edgeList, stopwatch.Elapsed)
            };
        }
        catch (Exception ex)
        {
            return new FileGraphResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                RepositoryRoot = repositoryRoot,
                BuildTimestamp = DateTimeOffset.UtcNow
            };
        }
    }

    private async Task<Dictionary<string, ProjectInfo>> BuildProjectGraphAsync(
        List<string> projectFiles,
        string repositoryRoot,
        ConcurrentBag<FileNode> nodes,
        ConcurrentBag<FileEdge> edges,
        FileGraphOptions options,
        GraphCache cache,
        CancellationToken ct)
    {
        var projectGraph = new ConcurrentDictionary<string, ProjectInfo>(StringComparer.OrdinalIgnoreCase);

        if (options.EnableParallelProcessing && projectFiles.Count > 1)
        {
            await Parallel.ForEachAsync(projectFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct }, async (projectPath, pct) =>
            {
                await ProcessProjectAsync(projectPath, repositoryRoot, nodes, edges, options, cache, projectGraph, pct);
            });
        }
        else
        {
            foreach (var projectPath in projectFiles)
            {
                ct.ThrowIfCancellationRequested();
                await ProcessProjectAsync(projectPath, repositoryRoot, nodes, edges, options, cache, projectGraph, ct);
            }
        }

        return projectGraph.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    private async Task ProcessProjectAsync(
        string projectPath,
        string repositoryRoot,
        ConcurrentBag<FileNode> nodes,
        ConcurrentBag<FileEdge> edges,
        FileGraphOptions options,
        GraphCache cache,
        ConcurrentDictionary<string, ProjectInfo> projectGraph,
        CancellationToken ct)
    {
        var relativePath = GetRelativePath(projectPath, repositoryRoot);
        var projectId = relativePath.Replace('\\', '/');
        var projectName = Path.GetFileNameWithoutExtension(projectPath);

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
                        var cached = await cache.LoadAsync<ProjectInfo>($"graph:project:{projectId}");
                        if (cached != null)
                        {
                            projectGraph.TryAdd(projectId, cached);
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Fall through to process fresh
            }
        }

        var node = new FileNode
        {
            Id = projectId,
            FullPath = projectPath,
            RelativePath = relativePath,
            NodeType = NodeType.Project,
            Extension = ".csproj",
            FileSize = new FileInfo(projectPath).Length,
            LastModified = File.GetLastWriteTimeUtc(projectPath)
        };
        nodes.Add(node);

        var projectInfo = new ProjectInfo
        {
            ProjectPath = projectPath,
            ProjectId = projectId,
            ProjectName = projectName,
            SourceFiles = new List<string>()
        };

        if (options.IncludeProjectReferences)
        {
            var references = await ExtractProjectReferencesAsync(projectPath, repositoryRoot, ct);
            projectInfo.ProjectReferences = references;

            foreach (var reference in references)
            {
                var edgeId = $"{projectId} -> {reference.ReferenceProjectId}";
                var edge = new FileEdge
                {
                    Id = edgeId,
                    SourceId = projectId,
                    TargetId = reference.ReferenceProjectId,
                    EdgeType = EdgeType.ProjectReference,
                    Weight = 1.0
                };
                edges.Add(edge);
            }
        }

        if (options.IncludeIntraProjectEdges)
        {
            var projectDir = Path.GetDirectoryName(projectPath);
            if (projectDir != null)
            {
                var sourceFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !IsExcluded(f, options.ExcludePatterns))
                    .ToList();

                projectInfo.SourceFiles = sourceFiles;

                foreach (var sourceFile in sourceFiles)
                {
                    var sourceRelativePath = GetRelativePath(sourceFile, repositoryRoot);
                    var sourceFileId = sourceRelativePath.Replace('\\', '/');

                    var sourceNode = new FileNode
                    {
                        Id = sourceFileId,
                        FullPath = sourceFile,
                        RelativePath = sourceRelativePath,
                        NodeType = NodeType.SourceFile,
                        ProjectId = projectId,
                        Extension = ".cs",
                        FileSize = new FileInfo(sourceFile).Length,
                        LastModified = File.GetLastWriteTimeUtc(sourceFile)
                    };
                    nodes.Add(sourceNode);

                    var includeEdgeId = $"{projectId} includes {sourceFileId}";
                    var includeEdge = new FileEdge
                    {
                        Id = includeEdgeId,
                        SourceId = projectId,
                        TargetId = sourceFileId,
                        EdgeType = EdgeType.FileInclude,
                        Weight = 0.5
                    };
                    edges.Add(includeEdge);
                }
            }
        }

        projectGraph.TryAdd(projectId, projectInfo);

        // Save to cache
        if (cache.IsEnabled)
        {
            await cache.SaveAsync($"graph:project:{projectId}", projectInfo);
        }
    }

    private async Task<List<ProjectReference>> ExtractProjectReferencesAsync(
        string projectPath,
        string repositoryRoot,
        CancellationToken ct)
    {
        var references = new List<ProjectReference>();
        var projectDirectory = Path.GetDirectoryName(projectPath);
        if (projectDirectory == null) return references;

        try
        {
            var doc = await Task.Run(() => XDocument.Load(projectPath), ct);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            var projectReferenceElements = doc.Descendants(ns + "ProjectReference");
            foreach (var element in projectReferenceElements)
            {
                var include = element.Attribute("Include")?.Value;
                if (!string.IsNullOrEmpty(include))
                {
                    var resolvedPath = Path.GetFullPath(Path.Combine(projectDirectory, include));
                    if (File.Exists(resolvedPath))
                    {
                        var relativePath = GetRelativePath(resolvedPath, repositoryRoot);
                        references.Add(new ProjectReference
                        {
                            ReferencePath = resolvedPath,
                            ReferenceProjectId = relativePath.Replace('\\', '/')
                        });
                    }
                }
            }
        }
        catch (Exception)
        {
        }

        return references;
    }

    private async Task BuildImportDependenciesAsync(
        Dictionary<string, ProjectInfo> projectGraph,
        string repositoryRoot,
        ConcurrentBag<FileNode> nodes,
        ConcurrentBag<FileEdge> edges,
        FileGraphOptions options,
        GraphCache cache,
        CancellationToken ct)
    {
        var allSourceFiles = projectGraph.Values.SelectMany(p => p.SourceFiles).ToList();

        if (options.EnableParallelProcessing && allSourceFiles.Count > 10)
        {
            await Parallel.ForEachAsync(allSourceFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct }, async (sourceFile, pct) =>
            {
                await ProcessFileImportDependenciesAsync(sourceFile, projectGraph, repositoryRoot, nodes, edges, options, cache, pct);
            });
        }
        else
        {
            foreach (var project in projectGraph.Values)
            {
                foreach (var sourceFile in project.SourceFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    await ProcessFileImportDependenciesAsync(sourceFile, projectGraph, repositoryRoot, nodes, edges, options, cache, ct);
                }
            }
        }
    }

    private async Task ProcessFileImportDependenciesAsync(
        string sourceFile,
        Dictionary<string, ProjectInfo> projectGraph,
        string repositoryRoot,
        ConcurrentBag<FileNode> nodes,
        ConcurrentBag<FileEdge> edges,
        FileGraphOptions options,
        GraphCache cache,
        CancellationToken ct)
    {
        var sourceRelativePath = GetRelativePath(sourceFile, repositoryRoot);
        var sourceFileId = sourceRelativePath.Replace('\\', '/');
        var project = projectGraph.Values.FirstOrDefault(p => p.SourceFiles.Contains(sourceFile));
        var sourceDirectory = Path.GetDirectoryName(sourceFile);
        if (sourceDirectory == null || project == null) return;

        // Check incremental scan
        if (options.EnableIncrementalScan && options.PreviousScanTimestamp.HasValue)
        {
            try
            {
                var lastModified = File.GetLastWriteTimeUtc(sourceFile);
                if (lastModified < options.PreviousScanTimestamp.Value.UtcDateTime)
                {
                    // Skip processing - dependencies haven't changed
                    return;
                }
            }
            catch
            {
                // Fall through to process fresh
            }
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(sourceFile, ct);
        }
        catch (Exception)
        {
            return;
        }

        var usingMatches = UsingDirectiveRegex.Matches(content);
        foreach (Match match in usingMatches)
        {
            var namespaceName = match.Groups[1].Value;
            var lineNumber = GetLineNumber(content, match.Index);

            var resolvedFile = ResolveNamespaceToFile(namespaceName, sourceDirectory, project.SourceFiles);
            if (resolvedFile != null)
            {
                var targetRelativePath = GetRelativePath(resolvedFile, repositoryRoot);
                var targetFileId = targetRelativePath.Replace('\\', '/');

                if (targetFileId != sourceFileId)
                {
                    var edgeId = $"{sourceFileId} imports {targetFileId} ({namespaceName})";
                    var edge = new FileEdge
                    {
                        Id = edgeId,
                        SourceId = sourceFileId,
                        TargetId = targetFileId,
                        EdgeType = EdgeType.ImportDependency,
                        ImportSymbol = namespaceName,
                        LineNumber = lineNumber,
                        Weight = options.CalculateEdgeWeights ? 0.3 : 1.0
                    };
                    edges.Add(edge);
                }
            }
            else
            {
                var targetProject = FindProjectForNamespace(namespaceName, projectGraph, project.ProjectId);
                if (targetProject != null)
                {
                    var edgeId = $"{sourceFileId} imports {targetProject} ({namespaceName})";
                    var edge = new FileEdge
                    {
                        Id = edgeId,
                        SourceId = sourceFileId,
                        TargetId = targetProject,
                        EdgeType = EdgeType.ImportDependency,
                        ImportSymbol = namespaceName,
                        LineNumber = lineNumber,
                        Weight = options.CalculateEdgeWeights ? 0.5 : 1.0
                    };
                    edges.Add(edge);
                }
            }
        }
    }

    private async Task BuildFileToFileRelationshipsAsync(
        Dictionary<string, ProjectInfo> projectGraph,
        string repositoryRoot,
        ConcurrentBag<FileNode> nodes,
        ConcurrentBag<FileEdge> edges,
        FileGraphOptions options,
        GraphCache cache,
        CancellationToken ct)
    {
        var fileTypeMap = BuildFileTypeMap(projectGraph, repositoryRoot);
        var allSourceFiles = projectGraph.Values.SelectMany(p => p.SourceFiles).ToList();

        if (options.EnableParallelProcessing && allSourceFiles.Count > 10)
        {
            await Parallel.ForEachAsync(allSourceFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct }, async (sourceFile, pct) =>
            {
                await ProcessFileTypeReferencesAsync(sourceFile, fileTypeMap, repositoryRoot, nodes, edges, options, cache, pct);
            });
        }
        else
        {
            foreach (var project in projectGraph.Values)
            {
                foreach (var sourceFile in project.SourceFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    await ProcessFileTypeReferencesAsync(sourceFile, fileTypeMap, repositoryRoot, nodes, edges, options, cache, ct);
                }
            }
        }
    }

    private async Task ProcessFileTypeReferencesAsync(
        string sourceFile,
        Dictionary<string, List<string>> fileTypeMap,
        string repositoryRoot,
        ConcurrentBag<FileNode> nodes,
        ConcurrentBag<FileEdge> edges,
        FileGraphOptions options,
        GraphCache cache,
        CancellationToken ct)
    {
        var sourceRelativePath = GetRelativePath(sourceFile, repositoryRoot);
        var sourceFileId = sourceRelativePath.Replace('\\', '/');

        // Check incremental scan
        if (options.EnableIncrementalScan && options.PreviousScanTimestamp.HasValue)
        {
            try
            {
                var lastModified = File.GetLastWriteTimeUtc(sourceFile);
                if (lastModified < options.PreviousScanTimestamp.Value.UtcDateTime)
                {
                    // Skip processing - references haven't changed
                    return;
                }
            }
            catch
            {
                // Fall through to process fresh
            }
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(sourceFile, ct);
        }
        catch (Exception)
        {
            return;
        }

        var referencedTypes = ExtractTypeReferences(content);

        foreach (var typeName in referencedTypes)
        {
            var targetFile = FindFileForType(typeName, fileTypeMap, sourceFile);
            if (targetFile != null && targetFile != sourceFile)
            {
                var targetRelativePath = GetRelativePath(targetFile, repositoryRoot);
                var targetFileId = targetRelativePath.Replace('\\', '/');

                if (targetFileId != sourceFileId)
                {
                    var edgeId = $"{sourceFileId} refs {targetFileId} ({typeName})";
                    var edge = new FileEdge
                    {
                        Id = edgeId,
                        SourceId = sourceFileId,
                        TargetId = targetFileId,
                        EdgeType = EdgeType.TypeReference,
                        ImportSymbol = typeName,
                        Weight = 1.0
                    };
                    edges.Add(edge);
                }
            }
        }
    }

    private Dictionary<string, List<string>> BuildFileTypeMap(
        Dictionary<string, ProjectInfo> projectGraph,
        string repositoryRoot)
    {
        var fileTypeMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in projectGraph.Values)
        {
            foreach (var sourceFile in project.SourceFiles)
            {
                var types = ExtractDefinedTypes(sourceFile);
                foreach (var type in types)
                {
                    if (!fileTypeMap.ContainsKey(type))
                    {
                        fileTypeMap[type] = new List<string>();
                    }
                    fileTypeMap[type].Add(sourceFile);
                }
            }
        }

        return fileTypeMap;
    }

    private List<string> ExtractDefinedTypes(string filePath)
    {
        var types = new List<string>();

        try
        {
            var content = File.ReadAllText(filePath);
            var matches = TypeReferenceRegex.Matches(content);

            foreach (Match match in matches)
            {
                var typeName = match.Groups[2].Value;
                types.Add(typeName);
            }
        }
        catch (Exception)
        {
        }

        return types;
    }

    private List<string> ExtractTypeReferences(string content)
    {
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var typeMatches = TypeReferenceRegex.Matches(content);
        foreach (Match match in typeMatches)
        {
            if (match.Groups[3].Success)
            {
                var baseTypes = match.Groups[3].Value.Split(',');
                foreach (var baseType in baseTypes)
                {
                    var cleanType = baseType.Trim().Split('<')[0].Trim();
                    if (!string.IsNullOrEmpty(cleanType) && !IsBuiltInType(cleanType))
                    {
                        references.Add(cleanType);
                    }
                }
            }
        }

        var fieldMatches = FieldDeclarationRegex.Matches(content);
        foreach (Match match in fieldMatches)
        {
            var fieldType = match.Groups[1].Value.Trim();
            var cleanType = ExtractBaseTypeName(fieldType);
            if (!string.IsNullOrEmpty(cleanType) && !IsBuiltInType(cleanType))
            {
                references.Add(cleanType);
            }
        }

        var paramMatches = MethodParameterRegex.Matches(content);
        foreach (Match match in paramMatches)
        {
            var paramType = match.Groups[1].Value.Trim();
            var cleanType = ExtractBaseTypeName(paramType);
            if (!string.IsNullOrEmpty(cleanType) && !IsBuiltInType(cleanType))
            {
                references.Add(cleanType);
            }
        }

        return references.ToList();
    }

    private string ExtractBaseTypeName(string typeName)
    {
        var cleanType = typeName.Split('<')[0].Trim().Split('[')[0].Trim();
        return cleanType;
    }

    private bool IsBuiltInType(string typeName)
    {
        var builtInTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "int", "long", "short", "byte", "float", "double", "decimal", "bool", "char", "string",
            "object", "void", "var", "dynamic", "Task", "Task<T>", "IEnumerable", "IEnumerable<T>",
            "List", "List<T>", "Dictionary", "Dictionary<K,V>", "Array", "DateTime", "DateTimeOffset",
            "TimeSpan", "Guid", "CancellationToken", "Exception", "IReadOnlyList", "IReadOnlyList<T>"
        };
        return builtInTypes.Contains(typeName) || typeName.StartsWith("System.", StringComparison.OrdinalIgnoreCase);
    }

    private string? FindFileForType(string typeName, Dictionary<string, List<string>> fileTypeMap, string sourceFile)
    {
        if (fileTypeMap.TryGetValue(typeName, out var files))
        {
            return files.FirstOrDefault();
        }

        var typeWithoutGeneric = typeName.Split('<')[0].Trim();
        if (fileTypeMap.TryGetValue(typeWithoutGeneric, out var filesWithoutGeneric))
        {
            return filesWithoutGeneric.FirstOrDefault();
        }

        return null;
    }

    private List<FileEdge> CalculateCouplingWeights(List<FileEdge> edges, List<FileNode> nodes)
    {
        var nodeEdgeCounts = new Dictionary<string, int>();
        foreach (var edge in edges)
        {
            nodeEdgeCounts[edge.SourceId] = nodeEdgeCounts.GetValueOrDefault(edge.SourceId) + 1;
            nodeEdgeCounts[edge.TargetId] = nodeEdgeCounts.GetValueOrDefault(edge.TargetId) + 1;
        }

        var maxConnections = nodeEdgeCounts.Any() ? nodeEdgeCounts.Values.Max() : 1;
        var fileGraphEdges = edges.Where(e => e.EdgeType != EdgeType.ProjectReference).ToList();

        var updatedEdges = new List<FileEdge>();
        var processedEdges = new HashSet<string>();

        foreach (var edge in edges)
        {
            if (processedEdges.Contains(edge.Id))
            {
                continue;
            }
            processedEdges.Add(edge.Id);

            double weight;
            if (edge.EdgeType == EdgeType.ProjectReference)
            {
                weight = 1.0;
            }
            else if (edge.EdgeType == EdgeType.FileInclude)
            {
                weight = 0.3;
            }
            else if (edge.EdgeType == EdgeType.TypeReference)
            {
                var sourceConnections = nodeEdgeCounts.GetValueOrDefault(edge.SourceId, 1);
                var targetConnections = nodeEdgeCounts.GetValueOrDefault(edge.TargetId, 1);
                var inverseFrequency = Math.Log(maxConnections + 1) - Math.Log(Math.Max(sourceConnections, targetConnections));
                var normalizedWeight = inverseFrequency / Math.Log(maxConnections + 1);
                weight = Math.Max(0.1, Math.Min(0.9, normalizedWeight));
            }
            else
            {
                var sourceConnections = nodeEdgeCounts.GetValueOrDefault(edge.SourceId, 1);
                weight = Math.Max(0.1, 1.0 - (sourceConnections / (double)maxConnections) * 0.5);
            }

            updatedEdges.Add(new FileEdge
            {
                Id = edge.Id,
                SourceId = edge.SourceId,
                TargetId = edge.TargetId,
                EdgeType = edge.EdgeType,
                ImportSymbol = edge.ImportSymbol,
                LineNumber = edge.LineNumber,
                Weight = Math.Round(weight, 3)
            });
        }

        return updatedEdges.OrderBy(e => e.Id).ToList();
    }

    private string? ResolveNamespaceToFile(string namespaceName, string sourceDirectory, List<string> projectFiles)
    {
        var parts = namespaceName.Split('.');
        if (parts.Length == 0) return null;

        foreach (var file in projectFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (parts.Contains(fileName))
            {
                var fileNamespace = TryExtractNamespace(file);
                if (fileNamespace != null && namespaceName.StartsWith(fileNamespace, StringComparison.Ordinal))
                {
                    return file;
                }
            }
        }

        return null;
    }

    private string? TryExtractNamespace(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var match = NamespaceRegex.Match(content);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        catch (Exception)
        {
        }
        return null;
    }

    private string? FindProjectForNamespace(
        string namespaceName,
        Dictionary<string, ProjectInfo> projectGraph,
        string currentProjectId)
    {
        var parts = namespaceName.Split('.');
        if (parts.Length == 0) return null;

        var currentProject = projectGraph.Values.FirstOrDefault(p => p.ProjectId == currentProjectId);
        if (currentProject == null) return null;

        foreach (var reference in currentProject.ProjectReferences)
        {
            var referencedProject = projectGraph.Values.FirstOrDefault(p =>
                p.ProjectId.Equals(reference.ReferenceProjectId, StringComparison.OrdinalIgnoreCase));

            if (referencedProject != null)
            {
                var projectName = referencedProject.ProjectName;
                if (parts.Any(p => p.Equals(projectName, StringComparison.OrdinalIgnoreCase)))
                {
                    return referencedProject.ProjectId;
                }
            }
        }

        return null;
    }

    private int GetLineNumber(string content, int index)
    {
        return content[..index].Count(c => c == '\n') + 1;
    }

    private bool IsExcluded(string path, IReadOnlyList<string> excludePatterns)
    {
        var normalizedPath = path.Replace('\\', '/');
        return excludePatterns.Any(pattern =>
            normalizedPath.Contains(pattern.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
    }

    private string GetRelativePath(string fullPath, string basePath)
    {
        return Path.GetRelativePath(basePath, fullPath).Replace('\\', '/');
    }

    private FileGraphStatistics CalculateStatistics(List<FileNode> nodes, List<FileEdge> edges, TimeSpan duration)
    {
        return new FileGraphStatistics
        {
            TotalNodes = nodes.Count,
            TotalEdges = edges.Count,
            ProjectCount = nodes.Count(n => n.NodeType == NodeType.Project),
            SourceFileCount = nodes.Count(n => n.NodeType == NodeType.SourceFile),
            ProjectReferenceCount = edges.Count(e => e.EdgeType == EdgeType.ProjectReference),
            ImportDependencyCount = edges.Count(e => e.EdgeType == EdgeType.ImportDependency),
            TypeReferenceCount = edges.Count(e => e.EdgeType == EdgeType.TypeReference),
            AverageEdgeWeight = edges.Any() ? edges.Average(e => e.Weight) : 0,
            BuildDuration = duration
        };
    }

    private sealed class ProjectInfo
    {
        public string ProjectPath { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public List<string> SourceFiles { get; set; } = new();
        public List<ProjectReference> ProjectReferences { get; set; } = new();
    }

    private sealed class ProjectReference
    {
        public string ReferencePath { get; set; } = "";
        public string ReferenceProjectId { get; set; } = "";
    }
}
