using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace nirmata.Agents.Execution.Brownfield.SymbolCacheBuilder;

/// <summary>
/// Implementation of the Symbol Cache Builder.
/// Extracts symbols from C# source files using regex-based parsing.
/// </summary>
public sealed class SymbolCacheBuilder : ISymbolCacheBuilder
{
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
    /// Cache for storing intermediate symbol extraction results.
    /// </summary>
    private sealed class SymbolCache
    {
        private readonly string _cacheDir;
        private readonly bool _enabled;

        public SymbolCache(string? cacheDir, bool enabled)
        {
            _enabled = enabled && !string.IsNullOrEmpty(cacheDir);
            _cacheDir = cacheDir ?? Path.Combine(Path.GetTempPath(), "nirmata-symbol-cache");
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

        public bool IsFileChanged(FileCacheEntry? cached, string filePath, string currentHash)
        {
            if (!_enabled || cached == null) return true;
            try
            {
                var info = new FileInfo(filePath);
                if (!info.Exists) return true;
                return info.LastWriteTimeUtc > cached.LastModified.UtcDateTime || info.Length != cached.SizeBytes || cached.ContentHash != currentHash;
            }
            catch
            {
                return true;
            }
        }
    }

    public async Task<SymbolCacheResult> BuildAsync(SymbolCacheRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var startTime = DateTimeOffset.UtcNow;
        var cache = new SymbolCache(request.Options.CacheDirectoryPath, request.Options.EnableCaching);

        try
        {
            var sourceFiles = GetSourceFiles(request);
            var symbols = new ConcurrentBag<SymbolInfo>();

            if (request.Options.EnableParallelProcessing && sourceFiles.Count > 1)
            {
                await Parallel.ForEachAsync(sourceFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct }, async (file, pct) =>
                {
                    var fileSymbols = await ExtractSymbolsFromFileWithCacheAsync(file, request.RepositoryPath, request.Options, cache, pct);
                    foreach (var symbol in fileSymbols)
                    {
                        symbols.Add(symbol);
                    }
                });
            }
            else
            {
                foreach (var file in sourceFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    var fileSymbols = await ExtractSymbolsFromFileWithCacheAsync(file, request.RepositoryPath, request.Options, cache, ct);
                    foreach (var symbol in fileSymbols)
                    {
                        symbols.Add(symbol);
                    }
                }
            }

            var orderedSymbols = symbols
                .OrderBy(s => s.FullName)
                .ThenBy(s => s.SymbolType)
                .ToList();

            return new SymbolCacheResult
            {
                IsSuccess = true,
                RepositoryRoot = request.RepositoryPath,
                BuildTimestamp = startTime,
                Symbols = orderedSymbols,
                Statistics = CalculateStatistics(orderedSymbols, startTime)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new SymbolCacheResult
            {
                IsSuccess = false,
                ErrorMessage = $"Symbol cache build failed: {ex.Message}",
                RepositoryRoot = request.RepositoryPath,
                BuildTimestamp = startTime
            };
        }
    }

    private async Task<IReadOnlyList<SymbolInfo>> ExtractSymbolsFromFileWithCacheAsync(
        string filePath, string repoRoot, SymbolCacheOptions options, SymbolCache cache, CancellationToken ct)
    {
        // Check incremental scan
        if (options.EnableIncrementalScan && options.PreviousScanTimestamp.HasValue)
        {
            try
            {
                var lastModified = File.GetLastWriteTimeUtc(filePath);
                if (lastModified < options.PreviousScanTimestamp.Value.UtcDateTime)
                {
                    // Try to load from cache
                    if (cache.IsEnabled)
                    {
                        var cached = await cache.LoadAsync<List<SymbolInfo>>($"symbols:{filePath}");
                        if (cached != null)
                        {
                            return cached;
                        }
                    }
                }
            }
            catch
            {
                // Fall through to extract fresh
            }
        }

        var symbols = await ExtractSymbolsFromFileAsync(filePath, repoRoot, options, ct);

        // Save to cache
        if (cache.IsEnabled)
        {
            await cache.SaveAsync($"symbols:{filePath}", symbols.ToList());
        }

        return symbols;
    }

    private static List<string> GetSourceFiles(SymbolCacheRequest request)
    {
        if (request.SourceFiles.Count > 0)
            return request.SourceFiles.ToList();

        return Directory.GetFiles(request.RepositoryPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f, request.Options.ExcludePatterns))
            .OrderBy(f => f)
            .ToList();
    }

    private static bool IsExcluded(string path, IReadOnlyList<string> patterns)
    {
        var pathLower = path.ToLowerInvariant();
        return patterns.Any(p =>
        {
            var pLower = p.ToLowerInvariant();
            if (pLower.EndsWith('/'))
                return pathLower.Contains(pLower.TrimEnd('/') + "/") || pathLower.EndsWith("/" + pLower.TrimEnd('/'));
            if (pLower.StartsWith("*."))
                return pathLower.EndsWith(pLower.TrimStart('*'));
            return pathLower.Contains(pLower);
        });
    }

    private static async Task<IReadOnlyList<SymbolInfo>> ExtractSymbolsFromFileAsync(
        string filePath, string repoRoot, SymbolCacheOptions options, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        var lines = content.Split('\n');
        var symbols = new List<SymbolInfo>();
        var relativePath = Path.GetRelativePath(repoRoot, filePath);
        var currentNamespace = "";
        var lineIndex = 0;

        while (lineIndex < lines.Length)
        {
            var (symbol, newLineIndex) = TryParseSymbol(lines, lineIndex, filePath, relativePath, currentNamespace, options);
            if (symbol != null)
            {
                symbols.Add(symbol);
                if (symbol.SymbolType == SymbolType.Namespace)
                    currentNamespace = symbol.Name;
            }
            lineIndex = newLineIndex;
        }

        return symbols;
    }

    private static (SymbolInfo? symbol, int nextLine) TryParseSymbol(
        string[] lines, int startLine, string filePath, string relativePath, string currentNamespace, SymbolCacheOptions options)
    {
        if (startLine >= lines.Length)
            return (null, startLine + 1);

        var line = lines[startLine];
        var trimmed = line.Trim();

        // Skip empty lines, comments, and using statements
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//") ||
            trimmed.StartsWith("using ") || trimmed.StartsWith("#"))
            return (null, startLine + 1);

        // Parse namespace
        if (trimmed.StartsWith("namespace "))
            return ParseNamespace(lines, startLine, filePath, relativePath);

        // Parse type declarations
        var typeMatch = TypePattern.Match(trimmed);
        if (typeMatch.Success)
            return ParseType(lines, startLine, filePath, relativePath, currentNamespace, typeMatch, options);

        // Parse method
        var methodMatch = MethodPattern.Match(trimmed);
        if (methodMatch.Success && !trimmed.Contains("//"))
            return ParseMethod(lines, startLine, filePath, relativePath, currentNamespace, methodMatch, options);

        // Parse property
        var propMatch = PropertyPattern.Match(trimmed);
        if (propMatch.Success)
            return ParseProperty(lines, startLine, filePath, relativePath, currentNamespace, propMatch, options);

        // Parse field
        var fieldMatch = FieldPattern.Match(trimmed);
        if (fieldMatch.Success && !trimmed.Contains("("))
            return ParseField(lines, startLine, filePath, relativePath, currentNamespace, fieldMatch, options);

        return (null, startLine + 1);
    }

    private static readonly Regex TypePattern = new(
        @"^(\s*)(public|private|internal|protected|file)?\s*((?:(?:static|abstract|sealed|partial|unsafe|new)\s+)*)(class|interface|struct|enum|record)\s+(\w+)",
        RegexOptions.Compiled);

    private static readonly Regex MethodPattern = new(
        @"^(\s*)(public|private|internal|protected)?\s*((?:(?:static|abstract|virtual|override|sealed|async|unsafe|new)\s+)*)([^=]+)\s+([\w<>]+)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex PropertyPattern = new(
        @"^(\s*)(public|private|internal|protected)?\s*((?:(?:static|abstract|virtual|override|sealed|unsafe|new|required)\s+)*)(\S+)\s+(\w+)\s*[{=]",
        RegexOptions.Compiled);

    private static readonly Regex FieldPattern = new(
        @"^(\s*)(public|private|internal|protected|readonly)?\s*((?:(?:static|readonly|const|volatile|unsafe|new)\s+)*)(\S+)\s+(\w+)\s*[;=]",
        RegexOptions.Compiled);

    private static (SymbolInfo? symbol, int nextLine) ParseNamespace(
        string[] lines, int startLine, string filePath, string relativePath)
    {
        var line = lines[startLine];
        var match = Regex.Match(line.Trim(), @"^namespace\s+([\w.]+)");
        if (!match.Success) return (null, startLine + 1);

        var name = match.Groups[1].Value;
        var isBlock = line.Trim().EndsWith("{");
        var endLine = isBlock ? FindClosingBrace(lines, startLine) : lines.Length - 1;

        var symbol = new SymbolInfo
        {
            Id = name,
            Name = name.Split('.').Last(),
            FullName = name,
            SymbolType = SymbolType.Namespace,
            Accessibility = SymbolAccessibility.Public,
            Namespace = "",
            Location = new SymbolLocation
            {
                FilePath = filePath,
                RelativePath = relativePath,
                LineNumber = startLine + 1,
                ColumnNumber = line.IndexOf(name) + 1,
                EndLineNumber = endLine + 1
            }
        };

        // Always enter the namespace body
        return (symbol, startLine + 1);
    }

    private static (SymbolInfo? symbol, int nextLine) ParseType(
        string[] lines, int startLine, string filePath, string relativePath, string ns,
        Match match, SymbolCacheOptions options)
    {
        var accessibility = ParseAccessibility(match.Groups[2].Value);
        var modifiers = match.Groups[3].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var typeKind = match.Groups[4].Value;
        var name = match.Groups[5].Value;

        var endLine = FindClosingBrace(lines, startLine);

        if (!ShouldIncludeSymbol(accessibility, options))
            return (null, endLine + 1); // Skip body if excluded

        var symbolType = typeKind switch
        {
            "class" => SymbolType.Class,
            "interface" => SymbolType.Interface,
            "struct" => SymbolType.Struct,
            "enum" => SymbolType.Enum,
            "record" => SymbolType.Record,
            _ => SymbolType.Unknown
        };

        var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

        var symbol = new SymbolInfo
        {
            Id = fullName,
            Name = name,
            FullName = fullName,
            SymbolType = symbolType,
            Accessibility = accessibility,
            Namespace = ns,
            Modifiers = modifiers.AsReadOnly(),
            Location = new SymbolLocation
            {
                FilePath = filePath,
                RelativePath = relativePath,
                LineNumber = startLine + 1,
                ColumnNumber = lines[startLine].IndexOf(name) + 1,
                EndLineNumber = endLine + 1
            }
        };

        // Enter the type body to find members
        return (symbol, startLine + 1);
    }

    private static (SymbolInfo? symbol, int nextLine) ParseMethod(
        string[] lines, int startLine, string filePath, string relativePath, string ns,
        Match match, SymbolCacheOptions options)
    {
        var accessibility = ParseAccessibility(match.Groups[2].Value);
        var modifiers = match.Groups[3].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var returnType = match.Groups[4].Value;
        var name = match.Groups[5].Value;

        if (!ShouldIncludeSymbol(accessibility, options) || name is "if" or "for" or "while" or "foreach" or "switch" or "catch" or "using")
            return (null, startLine + 1);

        var line = lines[startLine];
        var endLine = FindClosingBrace(lines, startLine);
        var parentType = FindContainingType(lines, startLine);
        var fullName = string.IsNullOrEmpty(parentType) ? name : $"{parentType}.{name}";

        var parameters = ParseParameters(line);
        var isCtor = name == Path.GetFileNameWithoutExtension(filePath);

        var symbol = new SymbolInfo
        {
            Id = $"{fullName}({string.Join(",", parameters.Select(p => p.Type))})",
            Name = name,
            FullName = fullName,
            SymbolType = isCtor ? SymbolType.Constructor : SymbolType.Method,
            Accessibility = accessibility,
            Namespace = ns,
            ParentSymbolId = parentType,
            TypeSignature = returnType,
            Modifiers = modifiers.AsReadOnly(),
            Parameters = parameters.AsReadOnly(),
            Location = new SymbolLocation
            {
                FilePath = filePath,
                RelativePath = relativePath,
                LineNumber = startLine + 1,
                ColumnNumber = line.IndexOf(name) + 1,
                EndLineNumber = endLine + 1
            }
        };

        return (symbol, endLine + 1);
    }

    private static (SymbolInfo? symbol, int nextLine) ParseProperty(
        string[] lines, int startLine, string filePath, string relativePath, string ns,
        Match match, SymbolCacheOptions options)
    {
        var accessibility = ParseAccessibility(match.Groups[2].Value);
        var modifiers = match.Groups[3].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var typeName = match.Groups[4].Value;
        var name = match.Groups[5].Value;

        if (!ShouldIncludeSymbol(accessibility, options))
            return (null, startLine + 1);

        var line = lines[startLine];
        var parentType = FindContainingType(lines, startLine);
        var fullName = string.IsNullOrEmpty(parentType) ? name : $"{parentType}.{name}";
        var isAutoProp = line.Contains("{") && !line.Contains("=>");
        var endLine = isAutoProp ? FindClosingBrace(lines, startLine) : startLine;

        var symbol = new SymbolInfo
        {
            Id = fullName,
            Name = name,
            FullName = fullName,
            SymbolType = SymbolType.Property,
            Accessibility = accessibility,
            Namespace = ns,
            ParentSymbolId = parentType,
            TypeSignature = typeName,
            Modifiers = modifiers.AsReadOnly(),
            Location = new SymbolLocation
            {
                FilePath = filePath,
                RelativePath = relativePath,
                LineNumber = startLine + 1,
                ColumnNumber = line.IndexOf(name) + 1,
                EndLineNumber = endLine + 1
            }
        };

        return (symbol, endLine + 1);
    }

    private static (SymbolInfo? symbol, int nextLine) ParseField(
        string[] lines, int startLine, string filePath, string relativePath, string ns,
        Match match, SymbolCacheOptions options)
    {
        var accessibility = ParseAccessibility(match.Groups[2].Value);
        var modifiers = match.Groups[3].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var typeName = match.Groups[4].Value;
        var name = match.Groups[5].Value;

        if (!ShouldIncludeSymbol(accessibility, options))
            return (null, startLine + 1);

        var line = lines[startLine];
        var parentType = FindContainingType(lines, startLine);
        var fullName = string.IsNullOrEmpty(parentType) ? name : $"{parentType}.{name}";

        var symbol = new SymbolInfo
        {
            Id = fullName,
            Name = name,
            FullName = fullName,
            SymbolType = SymbolType.Field,
            Accessibility = accessibility,
            Namespace = ns,
            ParentSymbolId = parentType,
            TypeSignature = typeName,
            Modifiers = modifiers.AsReadOnly(),
            Location = new SymbolLocation
            {
                FilePath = filePath,
                RelativePath = relativePath,
                LineNumber = startLine + 1,
                ColumnNumber = line.IndexOf(name) + 1,
                EndLineNumber = startLine + 1
            }
        };

        return (symbol, startLine + 1);
    }

    private static SymbolAccessibility ParseAccessibility(string value) => value switch
    {
        "public" => SymbolAccessibility.Public,
        "private" => SymbolAccessibility.Private,
        "internal" => SymbolAccessibility.Internal,
        "protected" => SymbolAccessibility.Protected,
        "protected internal" => SymbolAccessibility.ProtectedInternal,
        "private protected" => SymbolAccessibility.PrivateProtected,
        _ => SymbolAccessibility.Internal
    };

    private static bool ShouldIncludeSymbol(SymbolAccessibility accessibility, SymbolCacheOptions options) => accessibility switch
    {
        SymbolAccessibility.Public => true,
        SymbolAccessibility.Private => options.IncludePrivateSymbols,
        SymbolAccessibility.Internal => options.IncludeInternalSymbols,
        SymbolAccessibility.Protected => true,
        _ => true
    };

    private static List<ParameterInfo> ParseParameters(string line)
    {
        var parameters = new List<ParameterInfo>();
        var parenStart = line.IndexOf('(');
        var parenEnd = line.IndexOf(')');

        if (parenStart < 0 || parenEnd < 0 || parenEnd <= parenStart)
            return parameters;

        var paramStr = line.Substring(parenStart + 1, parenEnd - parenStart - 1);
        if (string.IsNullOrWhiteSpace(paramStr))
            return parameters;

        var paramList = paramStr.Split(',');
        foreach (var p in paramList)
        {
            var trimmed = p.Trim();
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var typeName = string.Join(" ", parts.Take(parts.Length - 1));
                var paramName = parts.Last();
                parameters.Add(new ParameterInfo
                {
                    Name = paramName,
                    Type = typeName,
                    IsOptional = trimmed.Contains("="),
                    IsParams = trimmed.StartsWith("params "),
                    IsOut = typeName.StartsWith("out "),
                    IsRef = typeName.StartsWith("ref ")
                });
            }
        }

        return parameters;
    }

    private static string FindContainingType(string[] lines, int lineIndex)
    {
        for (var i = lineIndex - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("namespace "))
                break;

            var match = Regex.Match(line, @"(?:class|interface|struct|enum|record)\s+(\w+)");
            if (match.Success)
            {
                // Verify this type actually encloses the current line
                var endLine = FindClosingBrace(lines, i);
                if (endLine < lineIndex)
                {
                    // This type closed before our line, keep searching up
                    continue;
                }

                var typeName = match.Groups[1].Value;
                // Find namespace
                var ns = "";
                for (var j = i - 1; j >= 0; j--)
                {
                    var nsMatch = Regex.Match(lines[j].Trim(), @"^namespace\s+([\w.]+)");
                    if (nsMatch.Success)
                    {
                        ns = nsMatch.Groups[1].Value;
                        break;
                    }
                }
                return string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
            }
        }
        return "";
    }

    private static int FindClosingBrace(string[] lines, int startLine)
    {
        var depth = 0;
        var foundOpenBrace = false;
        
        // State flags
        var inString = false;
        var inChar = false;
        var inVerbatimString = false;
        var inSingleLineComment = false;

        for (var i = startLine; i < lines.Length; i++)
        {
            var line = lines[i];
            inSingleLineComment = false; // Reset comment state at new line

            // Check for single-line termination (e.g. interface method or field)
            // Only if we haven't found an open brace yet and we aren't in a multi-line string
            if (!foundOpenBrace && line.Trim().EndsWith(";") && depth == 0 && !inVerbatimString)
                return i;

            for (var j = 0; j < line.Length; j++)
            {
                var c = line[j];
                var next = j + 1 < line.Length ? line[j + 1] : '\0';

                // Handle Comments
                if (!inString && !inChar && !inVerbatimString)
                {
                    if (!inSingleLineComment && c == '/' && next == '/')
                    {
                        inSingleLineComment = true;
                        j++; // Skip next /
                        continue;
                    }
                }

                if (inSingleLineComment) continue;

                // Handle Verbatim Strings @"..."
                if (!inString && !inChar && !inVerbatimString)
                {
                    if (c == '@' && next == '"')
                    {
                        inVerbatimString = true;
                        j++; // Skip "
                        continue;
                    }
                }
                else if (inVerbatimString)
                {
                    if (c == '"')
                    {
                        if (next == '"')
                        {
                            j++; // Skip escaped quote
                        }
                        else
                        {
                            inVerbatimString = false;
                        }
                    }
                    continue; // Skip other chars in verbatim string
                }

                // Handle Normal Strings "..."
                if (!inChar && !inVerbatimString)
                {
                    if (!inString && c == '"')
                    {
                        inString = true;
                        continue;
                    }
                    else if (inString)
                    {
                        if (c == '\\')
                        {
                            j++; // Skip escaped char
                        }
                        else if (c == '"')
                        {
                            inString = false;
                        }
                        continue; // Skip other chars in string
                    }
                }

                // Handle Chars '...'
                if (!inString && !inVerbatimString)
                {
                    if (!inChar && c == '\'')
                    {
                        inChar = true;
                        continue;
                    }
                    else if (inChar)
                    {
                        if (c == '\\')
                        {
                            j++; // Skip escaped char
                        }
                        else if (c == '\'')
                        {
                            inChar = false;
                        }
                        continue; // Skip other chars in char
                    }
                }

                // Handle Braces (only if not in any special block)
                if (c == '{')
                {
                    depth++;
                    foundOpenBrace = true;
                }
                else if (c == '}')
                {
                    depth--;
                }
            }

            if (foundOpenBrace && depth == 0 && !inVerbatimString)
                return i;
        }
        return lines.Length - 1;
    }

    private static SymbolLocation CreateLocation(string filePath, string relativePath, int lineNum, string line, string name)
    {
        return new SymbolLocation
        {
            FilePath = filePath,
            RelativePath = relativePath,
            LineNumber = lineNum + 1,
            ColumnNumber = line.IndexOf(name) + 1,
            EndLineNumber = lineNum + 1
        };
    }

    private static SymbolCacheStatistics CalculateStatistics(List<SymbolInfo> symbols, DateTimeOffset startTime)
    {
        return new SymbolCacheStatistics
        {
            TotalSymbols = symbols.Count,
            TypeCount = symbols.Count(s => s.SymbolType is SymbolType.Class or SymbolType.Interface or SymbolType.Struct or SymbolType.Enum or SymbolType.Record),
            MethodCount = symbols.Count(s => s.SymbolType is SymbolType.Method or SymbolType.Constructor),
            PropertyCount = symbols.Count(s => s.SymbolType == SymbolType.Property),
            FieldCount = symbols.Count(s => s.SymbolType == SymbolType.Field),
            SourceFileCount = symbols.Select(s => s.Location.FilePath).Distinct().Count(),
            BuildDuration = DateTimeOffset.UtcNow - startTime
        };
    }
}
