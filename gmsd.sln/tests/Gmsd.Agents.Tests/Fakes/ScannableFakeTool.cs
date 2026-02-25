using Gmsd.Agents.Execution.ControlPlane.Tools.Contracts;
using Gmsd.Aos.Contracts.Tools;

namespace Gmsd.Agents.Tests.Fakes;

/// <summary>
/// A top-level fake tool for testing assembly scanning registration.
/// Must be public and non-nested to be picked up by the modified scanner.
/// </summary>
public class ScannableFakeTool : ITool
{
    public ToolDescriptor Descriptor => new()
    {
        Id = "scannable-fake-tool",
        Name = "ScannableFakeTool",
        Description = "A fake tool for testing scanning",
        Parameters = new List<ToolParameter>()
    };

    public Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ToolResult.Success("Executed"));
    }
}
