using Microsoft.AspNetCore.Mvc;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class CodebaseArtifact
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty; // "intel" | "cache" | "pack"
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty; // "ready" | "stale" | "missing" | "error"
    public string LastUpdated { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
}

public sealed class LanguageBreakdown
{
    public string Name { get; init; } = string.Empty;
    public double Pct { get; init; }
    public string Color { get; init; } = string.Empty;
}

public sealed class StackEntry
{
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
}

public sealed class CodebaseIntel
{
    public IReadOnlyList<CodebaseArtifact> Artifacts { get; init; } = [];
    public IReadOnlyList<LanguageBreakdown> Languages { get; init; } = [];
    public IReadOnlyList<StackEntry> Stack { get; init; } = [];
}

[ApiController]
[Route("api/v1/codebase")]
public class CodebaseController : ControllerBase
{
    /// <summary>
    /// Returns codebase intelligence: artifacts, language breakdown, and stack info.
    /// </summary>
    [HttpGet("intel")]
    [ProducesResponseType(typeof(CodebaseIntel), StatusCodes.Status200OK)]
    public IActionResult GetIntel()
    {
        var intel = new CodebaseIntel
        {
            Artifacts = [],
            Languages = [],
            Stack = []
        };

        return Ok(intel);
    }
}
