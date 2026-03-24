namespace nirmata.Data.Dto.Models.Filesystem;

public sealed class DirectoryEntryDto
{
    public required string Name { get; init; }

    /// <summary>Workspace-relative path using forward slashes.</summary>
    public required string Path { get; init; }

    /// <summary>"file" or "directory".</summary>
    public required string Type { get; init; }

    /// <summary>Present for files; omitted for directories.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>Present only when <see cref="Type"/> is "directory".</summary>
    public IReadOnlyList<DirectoryEntryDto>? Children { get; init; }
}
