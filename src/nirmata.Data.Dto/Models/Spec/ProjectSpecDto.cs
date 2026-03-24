namespace nirmata.Data.Dto.Models.Spec;

public sealed class ProjectSpecDto
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Version { get; init; }
    public string? Owner { get; init; }
    public string? Repo { get; init; }
    public IReadOnlyList<string> Milestones { get; init; } = [];
    public IReadOnlyList<string> Constraints { get; init; } = [];
    public IReadOnlyList<string> Tags { get; init; } = [];
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}
