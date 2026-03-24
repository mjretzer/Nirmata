using Microsoft.Extensions.Logging;
using nirmata.Common.Exceptions;
using nirmata.Data.Dto.Models.Filesystem;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

/// <summary>
/// Provides workspace-scoped filesystem access with strict path gating.
/// All operations resolve the requested path through <see cref="ValidateAndNormalizePath"/>
/// before touching the disk, ensuring no access escapes the registered workspace root.
/// </summary>
public sealed class FileSystemService : IFileSystemService
{
    private readonly ILogger<FileSystemService> _logger;

    public FileSystemService(ILogger<FileSystemService> logger)
    {
        _logger = logger;
    }

    /// <summary>Maximum file size that will be read and returned (10 MB).</summary>
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    // Sensible content-type defaults.
    private static readonly Dictionary<string, string> ContentTypeMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { ".json",   "application/json" },
            { ".ndjson", "application/x-ndjson" },
            { ".xml",    "application/xml" },
            { ".yaml",   "application/yaml" },
            { ".yml",    "application/yaml" },
            { ".toml",   "application/toml" },
            { ".md",     "text/markdown; charset=utf-8" },
            { ".txt",    "text/plain; charset=utf-8" },
            { ".log",    "text/plain; charset=utf-8" },
            { ".cs",     "text/plain; charset=utf-8" },
            { ".ts",     "text/plain; charset=utf-8" },
            { ".tsx",    "text/plain; charset=utf-8" },
            { ".js",     "text/javascript; charset=utf-8" },
            { ".jsx",    "text/javascript; charset=utf-8" },
            { ".html",   "text/html; charset=utf-8" },
            { ".css",    "text/css; charset=utf-8" },
            { ".svg",    "image/svg+xml" },
            { ".png",    "image/png" },
            { ".jpg",    "image/jpeg" },
            { ".jpeg",   "image/jpeg" },
            { ".gif",    "image/gif" },
            { ".pdf",    "application/pdf" },
            { ".zip",    "application/zip" },
        };

    /// <inheritdoc/>
    public async Task<DirectoryListingDto> GetDirectoryListingAsync(
        string workspaceRoot, string relativePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ValidateAndNormalizePath(workspaceRoot, relativePath);

        if (!Directory.Exists(absolutePath))
            throw new NotFoundException($"Directory not found: {relativePath}");

        var normalizedRoot = Path.GetFullPath(workspaceRoot);
        var entries = new List<DirectoryEntryDto>();

        foreach (var entry in Directory.EnumerateFileSystemEntries(absolutePath).Order())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isDir = Directory.Exists(entry);
            var entryRelative = ToForwardSlashes(Path.GetRelativePath(normalizedRoot, entry));

            entries.Add(new DirectoryEntryDto
            {
                Name = Path.GetFileName(entry),
                Path = entryRelative,
                Type = isDir ? "directory" : "file",
                SizeBytes = isDir ? null : new FileInfo(entry).Length,
                Children = isDir ? [] : null,
            });
        }

        var listingRelative = ToForwardSlashes(Path.GetRelativePath(normalizedRoot, absolutePath));
        return await Task.FromResult(new DirectoryListingDto
        {
            Path = listingRelative,
            Entries = entries,
        });
    }

    /// <inheritdoc/>
    public async Task<(byte[] Content, string ContentType)> GetFileContentAsync(
        string workspaceRoot, string relativePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ValidateAndNormalizePath(workspaceRoot, relativePath);

        if (!File.Exists(absolutePath))
            throw new NotFoundException($"File not found: {relativePath}");

        var fileInfo = new FileInfo(absolutePath);
        if (fileInfo.Length > MaxFileSizeBytes)
            throw new FileTooLargeException(
                $"File '{relativePath}' is {fileInfo.Length:N0} bytes, which exceeds the {MaxFileSizeBytes:N0}-byte read limit.");

        var content = await File.ReadAllBytesAsync(absolutePath, cancellationToken);
        var ext = Path.GetExtension(absolutePath);
        var contentType = ContentTypeMap.TryGetValue(ext, out var ct) ? ct : "application/octet-stream";

        return (content, contentType);
    }

    /// <inheritdoc/>
    public string ValidateAndNormalizePath(string workspaceRoot, string requestedPath)
    {
        // Normalize workspace root (strip trailing separators so the prefix check is unambiguous).
        var normalizedRoot = Path.GetFullPath(
            workspaceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        // Step 1: normalize to forward-slash (handles backslash injection in URL path inputs).
        // Step 2: convert to OS separators before Path.GetFullPath (on Windows '/' → '\').
        var osSeparated = requestedPath
            .Replace('\\', '/')
            .Replace('/', Path.DirectorySeparatorChar);
        var combined = Path.Combine(normalizedRoot, osSeparated.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var resolved = Path.GetFullPath(combined);

        // Containment check: resolved path must be the root itself or a descendant.
        var rootWithSep = normalizedRoot + Path.DirectorySeparatorChar;
        if (!resolved.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
            !resolved.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Filesystem path rejected: '{RequestedPath}' resolves to '{Resolved}' which escapes workspace root '{Root}'",
                requestedPath, resolved, normalizedRoot);
            throw new ForbiddenException($"Path '{requestedPath}' escapes the workspace root.");
        }

        return resolved;
    }

    private static string ToForwardSlashes(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/');
}
