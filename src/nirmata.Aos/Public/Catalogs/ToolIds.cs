namespace nirmata.Aos.Public.Catalogs;

/// <summary>
/// Stable tool ID constants for the engine.
/// Uses prefixed format to avoid collisions: "nirmata:aos:tool:{category}:{operation}" for internal tools,
/// "mcp:{server}:{tool}" for MCP tools.
/// </summary>
public static class ToolIds
{
    // Prefix constants
    public const string InternalPrefix = "nirmata:aos:tool:";
    public const string McpPrefix = "mcp:";

    // Filesystem tools
    public static class Filesystem
    {
        public const string ReadFile = InternalPrefix + "filesystem:read";
        public const string WriteFile = InternalPrefix + "filesystem:write";
        public const string DeleteFile = InternalPrefix + "filesystem:delete";
        public const string ListDirectory = InternalPrefix + "filesystem:list";
        public const string FileExists = InternalPrefix + "filesystem:exists";
    }

    // Process tools
    public static class Process
    {
        public const string Execute = InternalPrefix + "process:execute";
        public const string ExecuteShell = InternalPrefix + "process:execute-shell";
    }

    // Git tools
    public static class Git
    {
        public const string Status = InternalPrefix + "git:status";
        public const string Diff = InternalPrefix + "git:diff";
        public const string Log = InternalPrefix + "git:log";
        public const string Branch = InternalPrefix + "git:branch";
    }

    // Search tools
    public static class Search
    {
        public const string Grep = InternalPrefix + "search:grep";
        public const string FindFiles = InternalPrefix + "search:find-files";
    }

    /// <summary>
    /// Generates an MCP tool ID from server name and tool name.
    /// </summary>
    public static string McpToolId(string serverName, string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return $"{McpPrefix}{serverName}:{toolName}";
    }

    /// <summary>
    /// Checks if a tool ID is an MCP tool.
    /// </summary>
    public static bool IsMcpTool(string toolId)
    {
        return !string.IsNullOrWhiteSpace(toolId) && toolId.StartsWith(McpPrefix, StringComparison.Ordinal);
    }
}
