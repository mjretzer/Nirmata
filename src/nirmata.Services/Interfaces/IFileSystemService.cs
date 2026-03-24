using nirmata.Data.Dto.Models.Filesystem;

namespace nirmata.Services.Interfaces;

public interface IFileSystemService
{
    /// <summary>
    /// Returns a directory listing for the given workspace-relative path.
    /// The path must resolve within <paramref name="workspaceRoot"/>.
    /// </summary>
    Task<DirectoryListingDto> GetDirectoryListingAsync(string workspaceRoot, string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the raw content and content-type for the file at the given workspace-relative path.
    /// The path must resolve within <paramref name="workspaceRoot"/>.
    /// </summary>
    Task<(byte[] Content, string ContentType)> GetFileContentAsync(string workspaceRoot, string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that <paramref name="requestedPath"/> (workspace-relative, forward-slash) resolves within
    /// <paramref name="workspaceRoot"/> and returns the normalized absolute OS path.
    /// Throws <see cref="nirmata.Common.Exceptions.ValidationFailedException"/> if the path escapes the root.
    /// </summary>
    string ValidateAndNormalizePath(string workspaceRoot, string requestedPath);
}
