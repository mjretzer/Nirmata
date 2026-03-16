using System;
using System.ComponentModel.DataAnnotations;

namespace nirmata.Data.Entities.Workspaces;

public class Workspace
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string Path { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset? LastOpenedAt { get; set; }

    public DateTimeOffset? LastValidatedAt { get; set; }

    public string? HealthStatus { get; set; }
}
