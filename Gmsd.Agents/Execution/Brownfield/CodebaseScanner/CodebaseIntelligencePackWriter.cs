using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gmsd.Aos.Engine;

namespace Gmsd.Agents.Execution.Brownfield.CodebaseScanner;

/// <summary>
/// Writes codebase intelligence pack JSON files from a scan result.
/// Produces deterministic, canonical JSON output for all intelligence artifacts.
/// </summary>
public sealed class CodebaseIntelligencePackWriter
{
    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>
    /// Creates a new intelligence pack writer with default serialization options.
    /// </summary>
    public CodebaseIntelligencePackWriter()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Creates a new intelligence pack writer with custom serialization options.
    /// </summary>
    public CodebaseIntelligencePackWriter(JsonSerializerOptions serializerOptions)
    {
        _serializerOptions = serializerOptions ?? throw new ArgumentNullException(nameof(serializerOptions));
    }

    /// <summary>
    /// Writes all intelligence pack files to the specified output directory.
    /// </summary>
    /// <param name="result">The scan result containing repository intelligence.</param>
    /// <param name="outputDirectory">The directory to write files to (typically .aos/codebase/).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paths of all written files.</returns>
    public async Task<IReadOnlyList<string>> WriteAllAsync(CodebaseScanResult result, string outputDirectory, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(outputDirectory);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot write intelligence pack for failed scan: " + result.ErrorMessage);
        }

        var writtenFiles = new List<string>();
        var fileHashes = new Dictionary<string, string>();

        // Ensure output directory exists
        Directory.CreateDirectory(outputDirectory);

        // Create cache subdirectory
        var cacheDirectory = Path.Combine(outputDirectory, "cache");
        Directory.CreateDirectory(cacheDirectory);

        // Write map.json - high-level codebase overview
        ct.ThrowIfCancellationRequested();
        var mapPath = Path.Combine(outputDirectory, "map.json");
        WriteMapJson(result, mapPath);
        writtenFiles.Add(mapPath);
        fileHashes["map.json"] = await ComputeFileHashAsync(mapPath, ct);

        // Write stack.json - technology stack detection
        ct.ThrowIfCancellationRequested();
        var stackPath = Path.Combine(outputDirectory, "stack.json");
        WriteStackJson(result, stackPath);
        writtenFiles.Add(stackPath);
        fileHashes["stack.json"] = await ComputeFileHashAsync(stackPath, ct);

        // Write structure.json - directory/file structure
        ct.ThrowIfCancellationRequested();
        var structurePath = Path.Combine(outputDirectory, "structure.json");
        WriteStructureJson(result, structurePath);
        writtenFiles.Add(structurePath);
        fileHashes["structure.json"] = await ComputeFileHashAsync(structurePath, ct);

        // Write architecture.json - architectural patterns
        ct.ThrowIfCancellationRequested();
        var architecturePath = Path.Combine(outputDirectory, "architecture.json");
        WriteArchitectureJson(result, architecturePath);
        writtenFiles.Add(architecturePath);
        fileHashes["architecture.json"] = await ComputeFileHashAsync(architecturePath, ct);

        // Write conventions.json - coding conventions
        ct.ThrowIfCancellationRequested();
        var conventionsPath = Path.Combine(outputDirectory, "conventions.json");
        WriteConventionsJson(result, conventionsPath);
        writtenFiles.Add(conventionsPath);
        fileHashes["conventions.json"] = await ComputeFileHashAsync(conventionsPath, ct);

        // Write testing.json - test structure
        ct.ThrowIfCancellationRequested();
        var testingPath = Path.Combine(outputDirectory, "testing.json");
        WriteTestingJson(result, testingPath);
        writtenFiles.Add(testingPath);
        fileHashes["testing.json"] = await ComputeFileHashAsync(testingPath, ct);

        // Write integrations.json - external integration points
        ct.ThrowIfCancellationRequested();
        var integrationsPath = Path.Combine(outputDirectory, "integrations.json");
        WriteIntegrationsJson(result, integrationsPath);
        writtenFiles.Add(integrationsPath);
        fileHashes["integrations.json"] = await ComputeFileHashAsync(integrationsPath, ct);

        // Write concerns.json - cross-cutting concerns
        ct.ThrowIfCancellationRequested();
        var concernsPath = Path.Combine(outputDirectory, "concerns.json");
        WriteConcernsJson(result, concernsPath);
        writtenFiles.Add(concernsPath);
        fileHashes["concerns.json"] = await ComputeFileHashAsync(concernsPath, ct);

        // Write cache/symbols.json - symbol index (placeholder for now)
        ct.ThrowIfCancellationRequested();
        var symbolsPath = Path.Combine(cacheDirectory, "symbols.json");
        WriteSymbolsJson(result, symbolsPath);
        writtenFiles.Add(symbolsPath);
        fileHashes["cache/symbols.json"] = await ComputeFileHashAsync(symbolsPath, ct);

        // Write cache/file-graph.json - file dependency graph
        ct.ThrowIfCancellationRequested();
        var fileGraphPath = Path.Combine(cacheDirectory, "file-graph.json");
        WriteFileGraphJson(result, fileGraphPath);
        writtenFiles.Add(fileGraphPath);
        fileHashes["cache/file-graph.json"] = await ComputeFileHashAsync(fileGraphPath, ct);

        // Write hash manifest for determinism verification
        ct.ThrowIfCancellationRequested();
        var manifestPath = Path.Combine(outputDirectory, "hash-manifest.json");
        await WriteHashManifestAsync(manifestPath, fileHashes, result.ScanTimestamp, ct);
        writtenFiles.Add(manifestPath);

        return await Task.FromResult(writtenFiles.AsReadOnly());
    }

    /// <summary>
    /// Computes SHA256 hash of a file.
    /// </summary>
    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Writes the hash manifest file for determinism verification.
    /// </summary>
    private static async Task WriteHashManifestAsync(string path, Dictionary<string, string> hashes, DateTimeOffset timestamp, CancellationToken ct)
    {
        var manifestData = new
        {
            schemaVersion = 1,
            generatedAt = timestamp.ToString("O"),
            algorithm = "SHA256",
            files = hashes.OrderBy(h => h.Key).ToDictionary(h => h.Key, h => h.Value)
        };

        var json = JsonSerializer.Serialize(manifestData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await File.WriteAllTextAsync(path, json, ct);
    }

    /// <summary>
    /// Verifies that all files in the output directory match the stored hash manifest.
    /// </summary>
    /// <param name="outputDirectory">The directory containing the intelligence pack.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if all files match their stored hashes, false otherwise.</returns>
    public static async Task<(bool IsValid, List<string> Mismatches)> VerifyDeterminismAsync(string outputDirectory, CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(outputDirectory, "hash-manifest.json");
        if (!File.Exists(manifestPath))
        {
            return (false, new List<string> { "hash-manifest.json not found" });
        }

        var mismatches = new List<string>();
        var manifestJson = await File.ReadAllTextAsync(manifestPath, ct);
        var manifest = JsonSerializer.Deserialize<HashManifest>(manifestJson);

        if (manifest?.Files == null)
        {
            return (false, new List<string> { "Invalid hash manifest format" });
        }

        foreach (var (relativePath, expectedHash) in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();

            var fullPath = Path.Combine(outputDirectory, relativePath);
            if (!File.Exists(fullPath))
            {
                mismatches.Add($"{relativePath}: file missing");
                continue;
            }

            var actualHash = await ComputeFileHashAsync(fullPath, ct);
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                mismatches.Add($"{relativePath}: hash mismatch (expected {expectedHash[..16]}..., got {actualHash[..16]}...)");
            }
        }

        return (mismatches.Count == 0, mismatches);
    }

    /// <summary>
    /// Hash manifest data structure.
    /// </summary>
    private sealed class HashManifest
    {
        public int SchemaVersion { get; set; }
        public string GeneratedAt { get; set; } = "";
        public string Algorithm { get; set; } = "";
        public Dictionary<string, string> Files { get; set; } = new();
    }

    private void WriteMapJson(CodebaseScanResult result, string path)
    {
        var fileTypeBreakdown = new Dictionary<string, int>();
        CollectFileTypeBreakdown(result.RootDirectory, fileTypeBreakdown);

        var mapData = new
        {
            schemaVersion = 1,
            version = "1.0",
            repository = new
            {
                root = result.RepositoryRoot,
                name = result.RepositoryName,
                type = "git",
                remoteUrl = (string?)null,
                branch = (string?)null,
                commit = (string?)null
            },
            scanTimestamp = result.ScanTimestamp.ToString("O"),
            summary = new
            {
                totalFiles = result.Statistics.TotalFiles,
                totalDirectories = result.Statistics.TotalDirectories,
                totalLinesOfCode = EstimateLinesOfCode(result.RootDirectory),
                projectCount = result.Statistics.ProjectCount,
                fileTypeBreakdown
            }
        };

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(path, mapData, _serializerOptions);
    }

    private void WriteStackJson(CodebaseScanResult result, string path)
    {
        var languages = result.TechnologyStack.Languages.Select(l => new
        {
            name = l.Name,
            version = "", // Not detected at this level
            fileCount = l.FileCount,
            linesOfCode = 0, // Would need line counting
            percentage = result.Statistics.TotalFiles > 0
                ? Math.Round((double)l.FileCount / result.Statistics.TotalFiles * 100, 2)
                : 0
        }).ToList();

        var frameworks = result.TechnologyStack.Frameworks.Select(f => new
        {
            name = f.Name,
            version = f.Version,
            type = InferFrameworkTypeCategory(f.Type)
        }).ToList();

        var buildTools = result.TechnologyStack.BuildTools.Select(b => new
        {
            name = b.Name,
            version = b.Version,
            configurationFiles = b.ConfigFiles
        }).ToList();

        var packageManagers = result.TechnologyStack.PackageManagers.Select(pm => new
        {
            name = pm.Name,
            version = "", // Not detected at this level
            manifestFiles = new List<string>() // Would need to track manifest files
        }).ToList();

        var stackData = new
        {
            schemaVersion = 1,
            languages,
            frameworks,
            buildTools,
            packageManagers
        };

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(path, stackData, _serializerOptions);
    }

    private void WriteStructureJson(CodebaseScanResult result, string path)
    {
        var directories = new List<object>();
        var files = new List<object>();
        var filesByType = new Dictionary<string, int>();
        var filesByExtension = new Dictionary<string, int>();

        CollectStructureData(result.RootDirectory, directories, files, filesByType, filesByExtension, 0);

        // Sort for determinism
        directories = directories.OrderBy(d => ((dynamic)d).path).ToList();
        files = files.OrderBy(f => ((dynamic)f).path).ToList();

        var structureData = new
        {
            schemaVersion = 1,
            directories,
            files,
            metadata = new
            {
                rootFiles = result.RootDirectory.Files.Select(f => f.Name).OrderBy(n => n).ToList(),
                rootDirectories = result.RootDirectory.Directories.Select(d => d.Name).OrderBy(n => n).ToList(),
                ignoredPatterns = new List<string>()
            },
            statistics = new
            {
                totalFiles = result.Statistics.TotalFiles,
                totalDirectories = result.Statistics.TotalDirectories,
                totalLinesOfCode = EstimateLinesOfCode(result.RootDirectory),
                averageFileSize = result.Statistics.TotalFiles > 0
                    ? (double)result.Statistics.TotalSizeBytes / result.Statistics.TotalFiles
                    : 0,
                maxDirectoryDepth = directories.Count > 0
                    ? directories.Max(d => (int)((dynamic)d).depth)
                    : 0,
                filesByType,
                filesByExtension
            }
        };

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(path, structureData, _serializerOptions);
    }

    private void WriteArchitectureJson(CodebaseScanResult result, string path)
    {
        // Infer layers from project structure
        var layers = InferLayers(result);
        var entryPoints = InferEntryPoints(result);
        var patterns = InferPatterns(result);

        var architectureData = new
        {
            schemaVersion = 1,
            layers,
            boundaries = new List<object>(), // Would need dependency analysis
            patterns,
            entryPoints
        };

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(path, architectureData, _serializerOptions);
    }

    private void WriteConventionsJson(CodebaseScanResult result, string path)
    {
        var conventionsData = new
        {
            schemaVersion = 1,
            naming = new
            {
                projects = InferProjectNamingConvention(result),
                namespaces = new
                {
                    pattern = "PascalCase",
                    root = InferRootNamespace(result),
                    examples = new List<string>()
                },
                types = new
                {
                    interfaces = "IPascalCase",
                    classes = "PascalCase",
                    structs = "PascalCase",
                    enums = "PascalCase",
                    exceptions = "PascalCase"
                },
                members = new
                {
                    properties = "PascalCase",
                    methods = "PascalCase",
                    fields = "_camelCase",
                    constants = "PascalCase",
                    parameters = "camelCase",
                    localVariables = "camelCase"
                },
                files = new
                {
                    matchTypeName = true,
                    casing = "PascalCase"
                }
            },
            organization = new
            {
                projectStructure = InferProjectStructure(result),
                namespaceToFolderMapping = true,
                filePerType = true,
                maxFileLines = 500,
                maxTypeMembers = 50
            },
            stylePatterns = new List<object>()
        };

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(path, conventionsData, _serializerOptions);
    }

    private void WriteTestingJson(CodebaseScanResult result, string path)
    {
        var testProjects = result.Projects
            .Where(p => p.IsTestProject)
            .Select(p => new
            {
                name = p.Name,
                path = p.RelativePath,
                type = InferTestType(p.Name),
                targetProject = InferTargetProject(p, result.Projects),
                framework = InferTestFramework(p.PackageReferences),
                testCount = 0, // Would need test discovery
                fileCount = p.SourceFiles.Count,
                linesOfCode = 0 // Would need line counting
            })
            .ToList();

        var frameworks = result.TechnologyStack.Frameworks
            .Where(f => f.Type == "testing")
            .Select(f => new
            {
                name = f.Name,
                version = f.Version,
                type = "unit",
                projects = f.ProjectPaths
            })
            .ToList();

        var testingData = new
        {
            schemaVersion = 1,
            testProjects,
            frameworks,
            coverage = new
            {
                overall = new
                {
                    lineCoverage = (double?)null,
                    branchCoverage = (double?)null,
                    methodCoverage = (double?)null
                },
                byProject = new List<object>(),
                tools = new List<object>()
            },
            patterns = new
            {
                testNaming = "MethodName_StateUnderTest_ExpectedBehavior",
                testOrganization = "Arrange-Act-Assert",
                mockingFramework = InferMockingFramework(result.Projects),
                fixturePattern = ""
            },
            configuration = new
            {
                runSettings = (string?)null,
                parallelExecution = true,
                targetFrameworks = result.Projects
                    .Where(p => p.IsTestProject)
                    .SelectMany(p => p.TargetFrameworks)
                    .Distinct()
                    .ToList(),
                collectors = new List<string> { "coverage" }
            }
        };

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(path, testingData, _serializerOptions);
    }

    private void WriteIntegrationsJson(CodebaseScanResult result, string path)
    {
        // Detect integrations from package references and project structure
        var externalApis = DetectExternalApis(result);
        var databases = DetectDatabases(result);
        var services = DetectServices(result);

        var integrationsData = new
        {
            schemaVersion = 1,
            externalApis,
            databases,
            services
        };

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(path, integrationsData, _serializerOptions);
    }

    private void WriteConcernsJson(CodebaseScanResult result, string path)
    {
        var crossCutting = DetectCrossCuttingConcerns(result);
        var hotspots = IdentifyHotspots(result);

        var concernsData = new
        {
            schemaVersion = 1,
            crossCutting,
            hotspots,
            complexity = new
            {
                averageCyclomaticComplexity = 0, // Would need code analysis
                maxCyclomaticComplexity = 0,
                highComplexityFiles = new List<object>(),
                totalLinesOfCode = EstimateLinesOfCode(result.RootDirectory),
                commentDensity = 0.0
            }
        };

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(path, concernsData, _serializerOptions);
    }

    private void WriteSymbolsJson(CodebaseScanResult result, string path)
    {
        // Placeholder: Symbol extraction requires Roslyn or similar
        var symbolsData = new
        {
            schemaVersion = 1,
            symbols = new List<object>(),
            metadata = new
            {
                generatedAt = result.ScanTimestamp.ToString("O"),
                repository = result.RepositoryName,
                symbolCount = 0
            }
        };

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(path, symbolsData, _serializerOptions);
    }

    private void WriteFileGraphJson(CodebaseScanResult result, string path)
    {
        var nodes = new List<object>();
        var edges = new List<object>();

        // Add project nodes
        foreach (var project in result.Projects)
        {
            nodes.Add(new
            {
                id = project.RelativePath,
                type = "project",
                name = project.Name
            });

            // Add edges for project references
            foreach (var reference in project.ProjectReferences)
            {
                var refRelative = result.Projects
                    .FirstOrDefault(p => p.Path.Equals(reference, StringComparison.OrdinalIgnoreCase))
                    ?.RelativePath;

                if (refRelative != null)
                {
                    edges.Add(new
                    {
                        source = project.RelativePath,
                        target = refRelative,
                        type = "project-reference",
                        weight = 1
                    });
                }
            }
        }

        // Sort for determinism
        nodes = nodes.OrderBy(n => ((dynamic)n).id).ToList();
        edges = edges.OrderBy(e => ((dynamic)e).source).ThenBy(e => ((dynamic)e).target).ToList();

        var fileGraphData = new
        {
            schemaVersion = 1,
            nodes,
            edges,
            metadata = new
            {
                generatedAt = result.ScanTimestamp.ToString("O"),
                repository = result.RepositoryName,
                nodeCount = nodes.Count,
                edgeCount = edges.Count
            }
        };

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(path, fileGraphData, _serializerOptions);
    }

    // Helper methods for data transformation

    private static void CollectFileTypeBreakdown(DirectoryNode node, Dictionary<string, int> breakdown)
    {
        foreach (var file in node.Files)
        {
            var key = string.IsNullOrEmpty(file.Extension) ? "no-extension" : file.Extension.TrimStart('.');
            if (!breakdown.ContainsKey(key))
            {
                breakdown[key] = 0;
            }
            breakdown[key]++;
        }

        foreach (var dir in node.Directories)
        {
            CollectFileTypeBreakdown(dir, breakdown);
        }
    }

    private static int EstimateLinesOfCode(DirectoryNode node)
    {
        // Rough estimation: ~50 lines per code file on average
        var codeFiles = CountFilesByClassification(node, "code");
        return codeFiles * 50;
    }

    private static int CountFilesByClassification(DirectoryNode node, string classification)
    {
        var count = node.Files.Count(f => f.Classification == classification);
        foreach (var dir in node.Directories)
        {
            count += CountFilesByClassification(dir, classification);
        }
        return count;
    }

    private static void CollectStructureData(
        DirectoryNode node,
        List<object> directories,
        List<object> files,
        Dictionary<string, int> filesByType,
        Dictionary<string, int> filesByExtension,
        int depth)
    {
        var dirData = new
        {
            path = node.RelativePath,
            name = node.Name,
            parent = depth > 0 ? Path.GetDirectoryName(node.RelativePath)?.Replace("\\", "/") ?? "" : null,
            depth,
            fileCount = node.Files.Count,
            subdirectoryCount = node.Directories.Count,
            purpose = InferDirectoryPurpose(node)
        };
        directories.Add(dirData);

        foreach (var file in node.Files)
        {
            var fileData = new
            {
                path = file.RelativePath,
                name = file.Name,
                directory = Path.GetDirectoryName(file.RelativePath)?.Replace("\\", "/") ?? "",
                extension = file.Extension.TrimStart('.'),
                type = file.Classification,
                language = InferLanguageFromExtension(file.Extension),
                lines = 0, // Would need line counting
                sizeBytes = file.SizeBytes,
                lastModified = file.LastModified.ToString("O"),
                hash = (string?)null
            };
            files.Add(fileData);

            // Update statistics
            if (!filesByType.ContainsKey(file.Classification))
            {
                filesByType[file.Classification] = 0;
            }
            filesByType[file.Classification]++;

            var ext = string.IsNullOrEmpty(file.Extension) ? "none" : file.Extension.TrimStart('.');
            if (!filesByExtension.ContainsKey(ext))
            {
                filesByExtension[ext] = 0;
            }
            filesByExtension[ext]++;
        }

        foreach (var subDir in node.Directories)
        {
            CollectStructureData(subDir, directories, files, filesByType, filesByExtension, depth + 1);
        }
    }

    private static string InferDirectoryPurpose(DirectoryNode node)
    {
        var nameLower = node.Name.ToLowerInvariant();

        if (nameLower.Contains("test") || nameLower.Contains("spec"))
        {
            return "test";
        }
        if (nameLower.Contains("src") || nameLower.Contains("source"))
        {
            return "source";
        }
        if (nameLower.Contains("docs") || nameLower.Contains("documentation"))
        {
            return "documentation";
        }
        if (nameLower.Contains("build") || nameLower.Contains("scripts"))
        {
            return "build";
        }
        if (nameLower.Contains("config") || nameLower.Contains("settings"))
        {
            return "config";
        }
        if (nameLower.Contains("resources") || nameLower.Contains("assets"))
        {
            return "resources";
        }
        if (nameLower.Contains("tools") || nameLower.Contains("utils"))
        {
            return "tools";
        }

        return "unknown";
    }

    private static string InferLanguageFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".vb" => "vb",
            ".fs" => "fsharp",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".py" => "python",
            ".go" => "go",
            ".rs" => "rust",
            ".java" => "java",
            ".rb" => "ruby",
            ".php" => "php",
            ".cpp" or ".cxx" or ".cc" or ".h" or ".hpp" => "cpp",
            ".c" => "c",
            _ => ""
        };
    }

    private static string InferFrameworkTypeCategory(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "web" => "backend",
            "desktop" => "frontend",
            "testing" => "testing",
            _ => "runtime"
        };
    }

    private static List<object> InferLayers(CodebaseScanResult result)
    {
        var layers = new List<object>();

        // Group projects by naming patterns
        var presentationProjects = result.Projects.Where(p =>
            p.Name.Contains("Web", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Api", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("UI", StringComparison.OrdinalIgnoreCase)).ToList();

        var domainProjects = result.Projects.Where(p =>
            p.Name.Contains("Domain", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)).ToList();

        var infrastructureProjects = result.Projects.Where(p =>
            p.Name.Contains("Data", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Infra", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Persistence", StringComparison.OrdinalIgnoreCase)).ToList();

        if (presentationProjects.Any())
        {
            layers.Add(new
            {
                name = "Presentation",
                responsibility = "User interface and API endpoints",
                projects = presentationProjects.Select(p => p.Name).ToList(),
                dependencies = domainProjects.Any() ? new[] { "Domain" } : Array.Empty<string>()
            });
        }

        if (domainProjects.Any())
        {
            layers.Add(new
            {
                name = "Domain",
                responsibility = "Business logic and domain models",
                projects = domainProjects.Select(p => p.Name).ToList(),
                dependencies = Array.Empty<string>()
            });
        }

        if (infrastructureProjects.Any())
        {
            layers.Add(new
            {
                name = "Infrastructure",
                responsibility = "Data access and external services",
                projects = infrastructureProjects.Select(p => p.Name).ToList(),
                dependencies = domainProjects.Any() ? new[] { "Domain" } : Array.Empty<string>()
            });
        }

        return layers;
    }

    private static List<object> InferEntryPoints(CodebaseScanResult result)
    {
        var entryPoints = new List<object>();

        foreach (var project in result.Projects.Where(p => !p.IsTestProject))
        {
            // Check for Program.cs or similar entry points
            var entryPointFiles = project.SourceFiles
                .Where(f => Path.GetFileName(f).Equals("Program.cs", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var entryFile in entryPointFiles)
            {
                entryPoints.Add(new
                {
                    type = "console",
                    name = project.Name,
                    path = Path.GetRelativePath(result.RepositoryRoot, entryFile).Replace("\\", "/"),
                    project = project.Name
                });
            }
        }

        return entryPoints;
    }

    private static List<object> InferPatterns(CodebaseScanResult result)
    {
        var patterns = new List<object>();

        // Check for common patterns in package references
        var allPackages = result.Projects.SelectMany(p => p.PackageReferences).ToList();

        if (allPackages.Any(p => p.Name.Contains("Microsoft.Extensions.DependencyInjection")))
        {
            patterns.Add(new
            {
                name = "Dependency Injection",
                type = "structural",
                implementation = "Microsoft.Extensions.DependencyInjection",
                files = new List<string>()
            });
        }

        if (allPackages.Any(p => p.Name.Contains("EntityFrameworkCore")))
        {
            patterns.Add(new
            {
                name = "Repository",
                type = "structural",
                implementation = "Entity Framework Core",
                files = new List<string>()
            });
        }

        if (result.Projects.Any(p => p.IsTestProject))
        {
            patterns.Add(new
            {
                name = "Unit Testing",
                type = "architectural",
                implementation = "Test projects with xUnit/NUnit/MSTest",
                files = new List<string>()
            });
        }

        return patterns;
    }

    private static object InferProjectNamingConvention(CodebaseScanResult result)
    {
        var projects = result.Projects.Where(p => !p.IsTestProject).ToList();
        if (!projects.Any())
        {
            return new { pattern = "unknown", prefix = (string?)null, suffix = (string?)null, examples = new List<string>() };
        }

        var commonPrefix = FindCommonPrefix(projects.Select(p => p.Name));
        var examples = projects.Take(3).Select(p => p.Name).ToList();

        return new
        {
            pattern = "PascalCase",
            prefix = string.IsNullOrEmpty(commonPrefix) ? null : commonPrefix,
            suffix = (string?)null,
            examples
        };
    }

    private static string FindCommonPrefix(IEnumerable<string> strings)
    {
        var strs = strings.ToList();
        if (!strs.Any()) return "";

        var prefix = strs[0];
        foreach (var str in strs)
        {
            while (!str.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                prefix = prefix[..^1];
                if (string.IsNullOrEmpty(prefix)) break;
            }
        }

        // Only return if it's a meaningful prefix (ends with . or - or _)
        return string.IsNullOrEmpty(prefix) || char.IsLetterOrDigit(prefix[^1]) ? "" : prefix;
    }

    private static string InferRootNamespace(CodebaseScanResult result)
    {
        var commonPrefix = FindCommonPrefix(result.Projects.Select(p => p.Name));
        return string.IsNullOrEmpty(commonPrefix) ? result.RepositoryName : commonPrefix.TrimEnd('.', '-', '_');
    }

    private static List<object> InferProjectStructure(CodebaseScanResult result)
    {
        var structure = new List<object>();

        // Analyze common folder patterns across projects
        var allSourceFiles = result.Projects.SelectMany(p => p.SourceFiles).ToList();
        var commonFolders = allSourceFiles
            .Select(f => Path.GetDirectoryName(Path.GetRelativePath(result.RepositoryRoot, f)))
            .Where(d => !string.IsNullOrEmpty(d))
            .Select(d => d!.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0])
            .GroupBy(d => d)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        foreach (var folder in commonFolders)
        {
            structure.Add(new
            {
                folder,
                purpose = InferFolderPurpose(folder),
                contents = "",
                required = false
            });
        }

        return structure;
    }

    private static string InferFolderPurpose(string folder)
    {
        var lower = folder.ToLowerInvariant();
        if (lower.Contains("controller")) return "API controllers";
        if (lower.Contains("service")) return "Business services";
        if (lower.Contains("model") || lower.Contains("entity")) return "Domain models";
        if (lower.Contains("data") || lower.Contains("repository")) return "Data access";
        if (lower.Contains("test")) return "Tests";
        return "Source files";
    }

    private static string InferTestType(string projectName)
    {
        var nameLower = projectName.ToLowerInvariant();
        if (nameLower.Contains("integration")) return "integration";
        if (nameLower.Contains("e2e") || nameLower.Contains("endtoend")) return "e2e";
        if (nameLower.Contains("performance") || nameLower.Contains("perf")) return "performance";
        if (nameLower.Contains("acceptance")) return "acceptance";
        if (nameLower.Contains("contract")) return "contract";
        return "unit";
    }

    private static string? InferTargetProject(ProjectInfo testProject, IReadOnlyList<ProjectInfo> allProjects)
    {
        // Try to find matching project by name similarity
        var testName = testProject.Name.ToLowerInvariant()
            .Replace(".tests", "")
            .Replace(".test", "")
            .Replace("tests.", "")
            .Replace("test.", "");

        var target = allProjects
            .FirstOrDefault(p => !p.IsTestProject &&
                p.Name.ToLowerInvariant().Equals(testName, StringComparison.OrdinalIgnoreCase));

        return target?.Name;
    }

    private static string InferTestFramework(IReadOnlyList<PackageReference> packages)
    {
        var packageNames = packages.Select(p => p.Name.ToLowerInvariant()).ToList();

        if (packageNames.Any(n => n.Contains("xunit"))) return "xUnit";
        if (packageNames.Any(n => n.Contains("nunit"))) return "NUnit";
        if (packageNames.Any(n => n.Contains("mstest"))) return "MSTest";

        return "Unknown";
    }

    private static string? InferMockingFramework(IReadOnlyList<ProjectInfo> projects)
    {
        var allPackages = projects.SelectMany(p => p.PackageReferences).ToList();
        var packageNames = allPackages.Select(p => p.Name.ToLowerInvariant()).ToList();

        if (packageNames.Any(n => n.Contains("moq"))) return "Moq";
        if (packageNames.Any(n => n.Contains("nsubstitute"))) return "NSubstitute";
        if (packageNames.Any(n => n.Contains("fakeiteasy"))) return "FakeItEasy";

        return null;
    }

    private static List<object> DetectExternalApis(CodebaseScanResult result)
    {
        var apis = new List<object>();
        var allPackages = result.Projects.SelectMany(p => p.PackageReferences).ToList();

        // Check for HTTP client packages
        if (allPackages.Any(p => p.Name.Contains("HttpClient") || p.Name.Contains("RestSharp")))
        {
            apis.Add(new
            {
                name = "External REST APIs",
                type = "rest",
                baseUrl = (string?)null,
                version = (string?)null,
                authentication = "none",
                clientFiles = new List<string>()
            });
        }

        // Check for gRPC
        if (allPackages.Any(p => p.Name.Contains("Grpc")))
        {
            apis.Add(new
            {
                name = "gRPC Services",
                type = "grpc",
                baseUrl = (string?)null,
                version = (string?)null,
                authentication = "none",
                clientFiles = new List<string>()
            });
        }

        return apis;
    }

    private static List<object> DetectDatabases(CodebaseScanResult result)
    {
        var databases = new List<object>();
        var allPackages = result.Projects.SelectMany(p => p.PackageReferences).ToList();

        if (allPackages.Any(p => p.Name.Contains("EntityFrameworkCore.SqlServer")))
        {
            databases.Add(new
            {
                name = "SQL Server",
                type = "sql-server",
                provider = "EF Core",
                connectionStringKey = (string?)null,
                entities = new List<string>(),
                migrationFiles = new List<string>()
            });
        }

        if (allPackages.Any(p => p.Name.Contains("Npgsql.EntityFrameworkCore")))
        {
            databases.Add(new
            {
                name = "PostgreSQL",
                type = "postgresql",
                provider = "EF Core",
                connectionStringKey = (string?)null,
                entities = new List<string>(),
                migrationFiles = new List<string>()
            });
        }

        if (allPackages.Any(p => p.Name.Contains("Microsoft.EntityFrameworkCore.Sqlite")))
        {
            databases.Add(new
            {
                name = "SQLite",
                type = "sqlite",
                provider = "EF Core",
                connectionStringKey = (string?)null,
                entities = new List<string>(),
                migrationFiles = new List<string>()
            });
        }

        return databases;
    }

    private static List<object> DetectServices(CodebaseScanResult result)
    {
        var services = new List<object>();
        var allPackages = result.Projects.SelectMany(p => p.PackageReferences).ToList();

        // Check for message queue packages
        if (allPackages.Any(p => p.Name.Contains("RabbitMQ") || p.Name.Contains("MassTransit")))
        {
            services.Add(new
            {
                name = "Message Queue",
                type = "message-queue",
                provider = "RabbitMQ",
                integrationFiles = new List<string>()
            });
        }

        // Check for cache
        if (allPackages.Any(p => p.Name.Contains("StackExchange.Redis") || p.Name.Contains("Microsoft.Extensions.Caching")))
        {
            services.Add(new
            {
                name = "Distributed Cache",
                type = "cache",
                provider = "Redis",
                integrationFiles = new List<string>()
            });
        }

        return services;
    }

    private static List<object> DetectCrossCuttingConcerns(CodebaseScanResult result)
    {
        var concerns = new List<object>();
        var allPackages = result.Projects.SelectMany(p => p.PackageReferences).ToList();

        if (allPackages.Any(p => p.Name.Contains("Logging") || p.Name.Contains("Serilog") || p.Name.Contains("NLog")))
        {
            concerns.Add(new
            {
                name = "Logging",
                category = "logging",
                description = "Structured logging throughout the application",
                implementationFiles = new List<string>(),
                affectedFiles = new List<string>()
            });
        }

        if (allPackages.Any(p => p.Name.Contains("Authentication") || p.Name.Contains("Identity")))
        {
            concerns.Add(new
            {
                name = "Authentication",
                category = "security",
                description = "User authentication and authorization",
                implementationFiles = new List<string>(),
                affectedFiles = new List<string>()
            });
        }

        if (allPackages.Any(p => p.Name.Contains("Validation")))
        {
            concerns.Add(new
            {
                name = "Validation",
                category = "validation",
                description = "Input validation and model validation",
                implementationFiles = new List<string>(),
                affectedFiles = new List<string>()
            });
        }

        return concerns;
    }

    private static List<object> IdentifyHotspots(CodebaseScanResult result)
    {
        var hotspots = new List<object>();

        // Identify large files as potential hotspots
        var largeFiles = new List<FileInfo>();
        CollectLargeFiles(result.RootDirectory, largeFiles);

        foreach (var file in largeFiles.OrderByDescending(f => f.SizeBytes).Take(10))
        {
            hotspots.Add(new
            {
                filePath = file.RelativePath,
                score = (int)(file.SizeBytes / 1024), // Score based on KB
                changeFrequency = 0,
                contributorCount = 0,
                reasoning = "Large file that may benefit from refactoring"
            });
        }

        return hotspots;
    }

    private static void CollectLargeFiles(DirectoryNode node, List<FileInfo> largeFiles)
    {
        // Consider files > 50KB as large
        largeFiles.AddRange(node.Files.Where(f => f.SizeBytes > 50 * 1024));

        foreach (var dir in node.Directories)
        {
            CollectLargeFiles(dir, largeFiles);
        }
    }
}
