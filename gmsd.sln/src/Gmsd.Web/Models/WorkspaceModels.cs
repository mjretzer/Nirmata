using System;
using System.ComponentModel.DataAnnotations;

namespace Gmsd.Web.Models;

public record WorkspaceDto(
    Guid Id,
    string Path,
    string Name,
    DateTimeOffset? LastOpenedAt,
    string? HealthStatus);

public record OpenWorkspaceRequest(
    [Required] string Path);

public record InitWorkspaceRequest(
    [Required] string Path,
    string? Name = null);

public record WorkspaceValidationReport(
    string Path,
    bool IsValid,
    List<string> Errors,
    List<string> Warnings,
    DateTimeOffset RunAt);
