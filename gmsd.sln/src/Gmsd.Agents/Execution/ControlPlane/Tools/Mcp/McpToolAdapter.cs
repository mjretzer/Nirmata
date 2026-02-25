using Gmsd.Aos.Contracts.Tools;
using Gmsd.Agents.Execution.ControlPlane.Tools.Contracts;

namespace Gmsd.Agents.Execution.ControlPlane.Tools.Mcp;

/// <summary>
/// Adapter that wraps an MCP endpoint as an ITool implementation.
/// Translates ToolRequest to MCP calls and normalizes responses to ToolResult.
/// </summary>
public sealed class McpToolAdapter : ITool
{
    private readonly string _serverName;
    private readonly string _toolName;
    private readonly IMcpClient _client;

    /// <summary>
    /// Descriptor for this MCP tool.
    /// </summary>
    public ToolDescriptor Descriptor { get; }

    /// <summary>
    /// Creates a new MCP tool adapter.
    /// </summary>
    public McpToolAdapter(string serverName, string toolName, ToolDescriptor descriptor, IMcpClient client)
    {
        _serverName = serverName ?? throw new ArgumentNullException(nameof(serverName));
        _toolName = toolName ?? throw new ArgumentNullException(nameof(toolName));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    public async Task<ToolResult> InvokeAsync(
        ToolRequest request,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            // Translate ToolRequest to MCP call
            var mcpRequest = TranslateToMcpRequest(request);

            // Execute MCP call
            var mcpResponse = await _client.CallToolAsync(
                _serverName,
                _toolName,
                mcpRequest,
                cancellationToken);

            // Normalize MCP response to ToolResult
            return TranslateToToolResult(mcpResponse);
        }
        catch (McpException ex)
        {
            return ToolResult.Failure("MCP_ERROR", $"MCP call failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolResult.Failure("INVOCATION_ERROR", $"Tool invocation failed: {ex.Message}");
        }
    }

    private static Dictionary<string, object?> TranslateToMcpRequest(ToolRequest request)
    {
        return request.Parameters.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value);
    }

    private static ToolResult TranslateToToolResult(McpResponse response)
    {
        if (!response.IsSuccess)
        {
            return ToolResult.Failure(
                response.ErrorCode ?? "MCP_ERROR",
                response.ErrorMessage ?? "Unknown MCP error");
        }

        return ToolResult.Success(response.Data);
    }
}

/// <summary>
/// Interface for MCP client operations (stub for adapter boundary).
/// </summary>
public interface IMcpClient
{
    Task<McpResponse> CallToolAsync(
        string serverName,
        string toolName,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// MCP response structure.
/// </summary>
public sealed class McpResponse
{
    public bool IsSuccess { get; init; }
    public object? Data { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// MCP-specific exception.
/// </summary>
public class McpException : Exception
{
    public McpException(string message) : base(message) { }
    public McpException(string message, Exception innerException) : base(message, innerException) { }
}
