using Microsoft.AspNetCore.Mvc;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class FilesystemNode
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty; // "file" | "directory"
    public long? SizeBytes { get; init; }
    public DateTime? LastModified { get; init; }
    public IReadOnlyList<FilesystemNode> Children { get; init; } = [];
}

[ApiController]
[Route("api/v1/filesystem")]
public class FilesystemController : ControllerBase
{
    /// <summary>
    /// Returns a virtual filesystem view of the AOS workspace (.aos/) rooted at the given path.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(FilesystemNode), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetTree([FromQuery] string? path)
    {
        var root = new FilesystemNode
        {
            Name = ".aos",
            Path = path ?? ".aos",
            Kind = "directory",
            Children = []
        };

        return Ok(root);
    }
}
