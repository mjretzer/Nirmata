using System.Text;
using System.Text.Json;
using System.Reflection;
using Gmsd.Aos.Public.Catalogs;
using Json.Schema;

namespace Gmsd.Agents.Execution.Brownfield.MapValidator;

/// <summary>
/// Validates codebase map integrity, schema compliance, and cross-file invariants.
/// </summary>
public sealed class MapValidator : IMapValidator
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    // Schema IDs for all codebase artifacts
    private static readonly IReadOnlyDictionary<string, string> ArtifactSchemaMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["map.json"] = SchemaIds.CodebaseMapV1,
        ["stack.json"] = SchemaIds.CodebaseStackV1,
        ["architecture.json"] = SchemaIds.CodebaseArchitectureV1,
        ["structure.json"] = SchemaIds.CodebaseStructureV1,
        ["conventions.json"] = SchemaIds.CodebaseConventionsV1,
        ["testing.json"] = SchemaIds.CodebaseTestingV1,
        ["integrations.json"] = SchemaIds.CodebaseIntegrationsV1,
        ["concerns.json"] = SchemaIds.CodebaseConcernsV1,
        ["cache/symbols.json"] = SchemaIds.CodebaseSymbolsV1,
        ["cache/file-graph.json"] = SchemaIds.CodebaseFileGraphV1,
    };

    private static readonly IReadOnlyList<string> RequiredArtifacts = new[]
    {
        "map.json",
        "stack.json",
        "structure.json",
    };

    /// <inheritdoc />
    public async Task<MapValidationResult> ValidateAsync(MapValidationRequest request, CancellationToken ct = default)
    {
        var issues = new List<MapValidationIssue>();
        var artifactsToValidate = GetArtifactsToValidate(request);
        var codebasePath = Path.Combine(request.RepositoryRootPath, ".aos", "codebase");

        // 1. Check required files exist
        CheckRequiredFilesExist(codebasePath, issues, ct);

        // 2. Schema validation
        if (request.ValidateSchemaCompliance)
        {
            await ValidateSchemaComplianceAsync(codebasePath, artifactsToValidate, issues, ct);
        }

        // 3. Cross-file invariant checks
        if (request.CheckCrossFileInvariants)
        {
            await ValidateCrossFileInvariantsAsync(codebasePath, issues, ct);
        }

        // 4. Determinism validation (hash comparison)
        if (request.ValidateDeterminism)
        {
            await ValidateDeterminismAsync(codebasePath, artifactsToValidate, request.ExpectedHashes, issues, ct);
        }

        var summary = new MapValidationSummary
        {
            ArtifactsValidated = artifactsToValidate.Count,
            ErrorCount = issues.Count(i => i.Severity == ValidationSeverity.Error),
            WarningCount = issues.Count(i => i.Severity == ValidationSeverity.Warning),
            InfoCount = issues.Count(i => i.Severity == ValidationSeverity.Info),
            ValidationTimestamp = DateTimeOffset.UtcNow,
        };

        return new MapValidationResult
        {
            IsValid = summary.ErrorCount == 0,
            Issues = issues,
            Summary = summary,
        };
    }

    private static IReadOnlyList<string> GetArtifactsToValidate(MapValidationRequest request)
    {
        if (request.SpecificArtifacts.Count > 0)
        {
            return request.SpecificArtifacts
                .Where(a => ArtifactSchemaMap.ContainsKey(a))
                .ToList();
        }
        return ArtifactSchemaMap.Keys.ToList();
    }

    private static void CheckRequiredFilesExist(string codebasePath, List<MapValidationIssue> issues, CancellationToken ct)
    {
        foreach (var artifact in RequiredArtifacts)
        {
            ct.ThrowIfCancellationRequested();

            var filePath = Path.Combine(codebasePath, artifact);
            if (!File.Exists(filePath))
            {
                issues.Add(new MapValidationIssue
                {
                    IssueType = "MissingFile",
                    Artifact = artifact,
                    Message = $"Required artifact '{artifact}' is missing from the codebase map.",
                    Severity = ValidationSeverity.Error,
                });
            }
        }
    }

    private static async Task ValidateSchemaComplianceAsync(
        string codebasePath,
        IReadOnlyList<string> artifacts,
        List<MapValidationIssue> issues,
        CancellationToken ct)
    {
        // Build schema validation context from embedded schemas
        var schemaContext = await BuildSchemaValidationContextAsync(ct);
        if (schemaContext is null)
        {
            issues.Add(new MapValidationIssue
            {
                IssueType = "SchemaLoadError",
                Artifact = "",
                Message = "Failed to load validation schemas.",
                Severity = ValidationSeverity.Error,
            });
            return;
        }

        foreach (var artifact in artifacts)
        {
            ct.ThrowIfCancellationRequested();

            var filePath = Path.Combine(codebasePath, artifact);
            if (!File.Exists(filePath))
            {
                continue; // Already reported in required files check
            }

            if (!ArtifactSchemaMap.TryGetValue(artifact, out var schemaId))
            {
                issues.Add(new MapValidationIssue
                {
                    IssueType = "UnknownArtifact",
                    Artifact = artifact,
                    Message = $"No schema defined for artifact '{artifact}'.",
                    Severity = ValidationSeverity.Warning,
                });
                continue;
            }

            var artifactIssues = ValidateJsonFileAgainstSchema(schemaContext, filePath, schemaId, artifact);
            issues.AddRange(artifactIssues);
        }
    }

    private static async Task<SchemaValidationContext?> BuildSchemaValidationContextAsync(CancellationToken ct)
    {
        var byId = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);

        var schemaResources = GetSchemaAssemblies()
            .SelectMany(assembly => assembly.GetManifestResourceNames()
                .Where(n => n.Contains("Resources.Schemas", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase))
                .Select(resourceName => (assembly, resourceName)))
            .ToList();

        if (schemaResources.Count == 0)
        {
            var assemblyNames = string.Join(", ", GetSchemaAssemblies().Select(a => a.FullName));
            throw new InvalidOperationException($"No schema resources found in assemblies: {assemblyNames}");
        }

        var processedResources = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (assembly, resourceName) in schemaResources)
        {
            ct.ThrowIfCancellationRequested();

            var resourceKey = $"{assembly.FullName}:{resourceName}";
            if (!processedResources.Add(resourceKey))
            {
                continue;
            }

            try
            {
                await using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is null) continue;

                using var reader = new StreamReader(stream, Utf8NoBom);
                var json = await reader.ReadToEndAsync(ct);

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("$id", out var idProp) ||
                    idProp.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var schemaId = idProp.GetString();
                if (string.IsNullOrWhiteSpace(schemaId)) continue;

                var schema = JsonSchema.FromText(json);

                // Register for reference resolution
                try
                {
                    var uri = new Uri(schemaId, UriKind.Absolute);
                    if (SchemaRegistry.Global.Get(uri) is null)
                    {
                        SchemaRegistry.Global.Register(uri, schema);
                    }
                }
                catch (UriFormatException)
                {
                    // Continue without registration for malformed URIs
                }

                byId.TryAdd(schemaId, schema);
            }
            catch (Exception ex)
            {
                // Skip schemas that fail to load
                Console.WriteLine($"DEBUG: Failed to load schema from {resourceName}: {ex.Message}");
            }
        }

        return byId.Count > 0 ? new SchemaValidationContext(byId) : null;
    }

    private static IEnumerable<Assembly> GetSchemaAssemblies()
    {
        yield return typeof(SchemaIds).Assembly;

        var validatorAssembly = typeof(MapValidator).Assembly;
        if (!ReferenceEquals(validatorAssembly, typeof(SchemaIds).Assembly))
        {
            yield return validatorAssembly;
        }
    }

    private static IReadOnlyList<MapValidationIssue> ValidateJsonFileAgainstSchema(
        SchemaValidationContext ctx,
        string filePath,
        string schemaId,
        string artifactName)
    {
        if (!ctx.ById.TryGetValue(schemaId, out var schema))
        {
            return new List<MapValidationIssue>
            {
                new()
                {
                    IssueType = "SchemaNotFound",
                    Artifact = artifactName,
                    Message = $"Schema '{schemaId}' not found for artifact '{artifactName}'.",
                    Severity = ValidationSeverity.Error,
                }
            };
        }

        string json;
        try
        {
            json = File.ReadAllText(filePath, Utf8NoBom);
        }
        catch (Exception ex)
        {
            return new List<MapValidationIssue>
            {
                new()
                {
                    IssueType = "FileReadError",
                    Artifact = artifactName,
                    Message = $"Failed to read file: {ex.Message}",
                    Severity = ValidationSeverity.Error,
                }
            };
        }

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return new List<MapValidationIssue>
            {
                new()
                {
                    IssueType = "InvalidJson",
                    Artifact = artifactName,
                    Message = $"Invalid JSON: {ex.Message}",
                    Severity = ValidationSeverity.Error,
                }
            };
        }

        if (doc is null)
        {
            return new List<MapValidationIssue>
            {
                new()
                {
                    IssueType = "InvalidJson",
                    Artifact = artifactName,
                    Message = "Invalid JSON: parsed to null root node.",
                    Severity = ValidationSeverity.Error,
                }
            };
        }

        using (doc)
        {
            var options = new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            };

            var result = schema.Evaluate(doc.RootElement, options);
            if (result.IsValid)
            {
                return Array.Empty<MapValidationIssue>();
            }

            var issues = new List<MapValidationIssue>();
            CollectSchemaValidationIssues(result, issues, artifactName);
            return issues;
        }
    }

    private static void CollectSchemaValidationIssues(EvaluationResults result, List<MapValidationIssue> issues, string artifactName)
    {
        if (result.Errors is not null)
        {
            var pointer = result.InstanceLocation.ToString() ?? "";
            foreach (var kvp in result.Errors)
            {
                var msg = kvp.Value ?? "Schema validation failed.";
                issues.Add(new MapValidationIssue
                {
                    IssueType = "SchemaViolation",
                    Artifact = artifactName,
                    Path = pointer,
                    Message = msg,
                    Severity = ValidationSeverity.Error,
                });
            }
        }

        if (result.Details is null) return;

        foreach (var child in result.Details)
        {
            if (child is null) continue;
            CollectSchemaValidationIssues(child, issues, artifactName);
        }
    }

    private static async Task ValidateCrossFileInvariantsAsync(
        string codebasePath,
        List<MapValidationIssue> issues,
        CancellationToken ct)
    {
        // Load map.json for reference data
        var mapPath = Path.Combine(codebasePath, "map.json");
        if (!File.Exists(mapPath))
        {
            return; // Already reported as missing file
        }

        JsonDocument? mapDoc = null;
        try
        {
            var mapJson = await File.ReadAllTextAsync(mapPath, Utf8NoBom, ct);
            mapDoc = JsonDocument.Parse(mapJson);
        }
        catch
        {
            return; // Already reported as JSON error
        }

        using (mapDoc)
        {
            // Check scan timestamp consistency across files
            await ValidateTimestampConsistencyAsync(codebasePath, mapDoc, issues, ct);

            // Check repository root consistency
            ValidateRepositoryRootConsistency(mapDoc, issues);

            // Check structure.json consistency with map.json summary
            await ValidateStructureConsistencyAsync(codebasePath, mapDoc, issues, ct);

            // Check symbols.json references valid files
            await ValidateSymbolFileReferencesAsync(codebasePath, issues, ct);

            // Check file-graph.json references valid files
            await ValidateFileGraphReferencesAsync(codebasePath, issues, ct);
        }
    }

    private static async Task ValidateTimestampConsistencyAsync(
        string codebasePath,
        JsonDocument mapDoc,
        List<MapValidationIssue> issues,
        CancellationToken ct)
    {
        if (!mapDoc.RootElement.TryGetProperty("scanTimestamp", out var mapTimestampProp) ||
            mapTimestampProp.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var mapTimestamp = mapTimestampProp.GetString();
        if (string.IsNullOrWhiteSpace(mapTimestamp)) return;

        var artifactsToCheck = new[] { "stack.json", "architecture.json", "structure.json", "conventions.json" };

        foreach (var artifact in artifactsToCheck)
        {
            ct.ThrowIfCancellationRequested();

            var filePath = Path.Combine(codebasePath, artifact);
            if (!File.Exists(filePath)) continue;

            try
            {
                var json = await File.ReadAllTextAsync(filePath, Utf8NoBom, ct);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("scanTimestamp", out var timestampProp) &&
                    timestampProp.ValueKind == JsonValueKind.String)
                {
                    var artifactTimestamp = timestampProp.GetString();
                    if (artifactTimestamp != mapTimestamp)
                    {
                        issues.Add(new MapValidationIssue
                        {
                            IssueType = "CrossFileInvariant",
                            Artifact = artifact,
                            Path = "scanTimestamp",
                            Message = $"scanTimestamp in '{artifact}' does not match map.json. Expected consistency across codebase artifacts.",
                            Severity = ValidationSeverity.Warning,
                        });
                    }
                }
            }
            catch
            {
                // Skip files that can't be parsed
            }
        }
    }

    private static void ValidateRepositoryRootConsistency(JsonDocument mapDoc, List<MapValidationIssue> issues)
    {
        if (!mapDoc.RootElement.TryGetProperty("repository", out var repoProp) ||
            repoProp.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!repoProp.TryGetProperty("root", out var rootProp) ||
            rootProp.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var root = rootProp.GetString();
        if (string.IsNullOrWhiteSpace(root))
        {
            issues.Add(new MapValidationIssue
            {
                IssueType = "CrossFileInvariant",
                Artifact = "map.json",
                Path = "repository.root",
                Message = "Repository root path is empty.",
                Severity = ValidationSeverity.Error,
            });
        }
    }

    private static async Task ValidateStructureConsistencyAsync(
        string codebasePath,
        JsonDocument mapDoc,
        List<MapValidationIssue> issues,
        CancellationToken ct)
    {
        // Extract map.json summary statistics
        if (!mapDoc.RootElement.TryGetProperty("summary", out var summaryProp) ||
            summaryProp.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        int mapTotalFiles = 0;
        int mapProjectCount = 0;

        if (summaryProp.TryGetProperty("totalFiles", out var filesProp))
        {
            mapTotalFiles = filesProp.GetInt32();
        }

        if (summaryProp.TryGetProperty("projectCount", out var projectsProp))
        {
            mapProjectCount = projectsProp.GetInt32();
        }

        // Validate against structure.json
        var structurePath = Path.Combine(codebasePath, "structure.json");
        if (!File.Exists(structurePath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(structurePath, Utf8NoBom, ct);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("statistics", out var statsProp) &&
                statsProp.ValueKind == JsonValueKind.Object)
            {
                if (statsProp.TryGetProperty("totalFiles", out var structFilesProp))
                {
                    var structFiles = structFilesProp.GetInt32();
                    if (structFiles != mapTotalFiles)
                    {
                        issues.Add(new MapValidationIssue
                        {
                            IssueType = "CrossFileInvariant",
                            Artifact = "structure.json",
                            Path = "statistics.totalFiles",
                            Message = $"File count mismatch: map.json reports {mapTotalFiles} files, but structure.json reports {structFiles}.",
                            Severity = ValidationSeverity.Warning,
                        });
                    }
                }
            }
        }
        catch
        {
            // Skip if structure.json can't be parsed
        }
    }

    private static async Task ValidateSymbolFileReferencesAsync(
        string codebasePath,
        List<MapValidationIssue> issues,
        CancellationToken ct)
    {
        var symbolsPath = Path.Combine(codebasePath, "cache", "symbols.json");
        if (!File.Exists(symbolsPath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(symbolsPath, Utf8NoBom, ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("locations", out var locationsProp) ||
                locationsProp.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var referencedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var location in locationsProp.EnumerateArray())
            {
                if (location.TryGetProperty("filePath", out var pathProp) &&
                    pathProp.ValueKind == JsonValueKind.String)
                {
                    var filePath = pathProp.GetString();
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        referencedFiles.Add(filePath);
                    }
                }
            }

            // Check that all referenced files exist
            foreach (var filePath in referencedFiles)
            {
                ct.ThrowIfCancellationRequested();

                if (!File.Exists(filePath))
                {
                    issues.Add(new MapValidationIssue
                    {
                        IssueType = "CrossFileInvariant",
                        Artifact = "cache/symbols.json",
                        Message = $"Symbol references non-existent file: '{filePath}'.",
                        Severity = ValidationSeverity.Warning,
                    });
                }
            }
        }
        catch
        {
            // Skip if symbols.json can't be parsed
        }
    }

    private static async Task ValidateFileGraphReferencesAsync(
        string codebasePath,
        List<MapValidationIssue> issues,
        CancellationToken ct)
    {
        var graphPath = Path.Combine(codebasePath, "cache", "file-graph.json");
        if (!File.Exists(graphPath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(graphPath, Utf8NoBom, ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("nodes", out var nodesProp) ||
                nodesProp.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in nodesProp.EnumerateArray())
            {
                if (node.TryGetProperty("id", out var idProp) &&
                    idProp.ValueKind == JsonValueKind.String)
                {
                    var id = idProp.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        nodeIds.Add(id);
                    }
                }
            }

            // Validate edges reference existing nodes
            if (doc.RootElement.TryGetProperty("edges", out var edgesProp) &&
                edgesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var edge in edgesProp.EnumerateArray())
                {
                    ct.ThrowIfCancellationRequested();

                    if (edge.TryGetProperty("source", out var sourceProp) &&
                        sourceProp.ValueKind == JsonValueKind.String)
                    {
                        var source = sourceProp.GetString();
                        if (!string.IsNullOrWhiteSpace(source) && !nodeIds.Contains(source))
                        {
                            issues.Add(new MapValidationIssue
                            {
                                IssueType = "CrossFileInvariant",
                                Artifact = "cache/file-graph.json",
                                Path = "edges",
                                Message = $"Edge references non-existent source node: '{source}'.",
                                Severity = ValidationSeverity.Warning,
                            });
                        }
                    }

                    if (edge.TryGetProperty("target", out var targetProp) &&
                        targetProp.ValueKind == JsonValueKind.String)
                    {
                        var target = targetProp.GetString();
                        if (!string.IsNullOrWhiteSpace(target) && !nodeIds.Contains(target))
                        {
                            issues.Add(new MapValidationIssue
                            {
                                IssueType = "CrossFileInvariant",
                                Artifact = "cache/file-graph.json",
                                Path = "edges",
                                Message = $"Edge references non-existent target node: '{target}'.",
                                Severity = ValidationSeverity.Warning,
                            });
                        }
                    }
                }
            }
        }
        catch
        {
            // Skip if file-graph.json can't be parsed
        }
    }

    private static async Task ValidateDeterminismAsync(
        string codebasePath,
        IReadOnlyList<string> artifacts,
        IReadOnlyDictionary<string, string>? expectedHashes,
        List<MapValidationIssue> issues,
        CancellationToken ct)
    {
        foreach (var artifact in artifacts)
        {
            ct.ThrowIfCancellationRequested();

            var filePath = Path.Combine(codebasePath, artifact);
            if (!File.Exists(filePath))
            {
                continue; // Already reported in required files check
            }

            try
            {
                var actualHash = await ComputeFileHashAsync(filePath, ct);

                // If expected hashes are provided, compare against them
                if (expectedHashes != null && expectedHashes.TryGetValue(artifact, out var expectedHash))
                {
                    if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new MapValidationIssue
                        {
                            IssueType = "DeterminismViolation",
                            Artifact = artifact,
                            Path = null,
                            Message = $"Hash mismatch for '{artifact}'. Expected: {expectedHash}, Actual: {actualHash}.",
                            Severity = ValidationSeverity.Error,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                issues.Add(new MapValidationIssue
                {
                    IssueType = "HashComputationError",
                    Artifact = artifact,
                    Path = null,
                    Message = $"Failed to compute hash for '{artifact}': {ex.Message}",
                    Severity = ValidationSeverity.Warning,
                });
            }
        }
    }

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record SchemaValidationContext(IReadOnlyDictionary<string, JsonSchema> ById);
}
