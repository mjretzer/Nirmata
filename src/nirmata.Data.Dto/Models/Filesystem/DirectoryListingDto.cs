namespace nirmata.Data.Dto.Models.Filesystem;

public sealed class DirectoryListingDto
{
    /// <summary>Workspace-relative path of the listed directory, using forward slashes.</summary>
    public required string Path { get; init; }

    public required IReadOnlyList<DirectoryEntryDto> Entries { get; init; }
}
