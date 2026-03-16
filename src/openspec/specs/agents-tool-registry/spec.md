# agents-tool-registry Specification

## Purpose

Defines orchestration-plane workflow semantics for $capabilityId.

- **Lives in:** `nirmata.Agents/*`
- **Owns:** Control-plane routing/gating and workflow orchestration for this capability
- **Does not own:** Engine contract storage/serialization and product domain behavior
## Requirements
### Requirement: Tool descriptors expose metadata for introspection
The system SHALL define a `ToolDescriptor` model containing:
- `string Id` - stable unique identifier (e.g., `nirmata:aos:tool:filesystem:read`)
- `string Name` - human-readable display name
- `string Description` - explanation of tool purpose and behavior
- `string? InputSchemaRef` - reference to JSON schema for inputs
- `string? OutputSchemaRef` - reference to JSON schema for outputs
- `string Version` - version of the tool descriptor schema (default "1.0")
- `string? Category` - grouping for the tool (e.g., "filesystem", "process", "git")
- `IReadOnlyList<ToolParameter> Parameters` - parameters metadata for introspection
- `ToolCapability Capabilities` - flags indicating caching, retry, streaming support

#### Scenario: Tool descriptor provides complete metadata
- **GIVEN** a tool is registered in the system
- **WHEN** its `ToolDescriptor` is retrieved
- **THEN** the descriptor contains stable `Id`, display `Name`, `Description`, and capability flags

### Requirement: Tool parameters define input schema
The system SHALL define a `ToolParameter` model containing:
- `string Name` - parameter name
- `string Description` - human-readable description
- `string Type` - parameter type (e.g., "string", "number", "boolean")
- `bool Required` - whether the parameter is required (default true)
- `object? DefaultValue` - optional default value
- `IReadOnlyList<string>? EnumValues` - allowed values for enum parameters

#### Scenario: Tool parameter describes input field
- **GIVEN** a tool with input parameters
- **WHEN** its `ToolDescriptor.Parameters` are inspected
- **THEN** each parameter has `Name`, `Description`, `Type`, and `Required` flag

### Requirement: Tool capabilities indicate behavior flags
The system SHALL define a `ToolCapability` flags enum with values:
- `None = 0` - No special capabilities
- `Caching = 1 << 0` - Tool supports caching of results
- `Retry = 1 << 1` - Tool supports retry on failure
- `Streaming = 1 << 2` - Tool supports streaming responses
- `Idempotent = 1 << 3` - Tool is idempotent (safe to retry)
- `SideEffects = 1 << 4` - Tool may have side effects (mutating operation)
- `RequiresAuth = 1 << 5` - Tool requires authentication/authorization

#### Scenario: Tool capability flags are combinable
- **GIVEN** a tool with multiple capabilities
- **WHEN** `ToolDescriptor.Capabilities` is inspected
- **THEN** multiple flags can be set using bitwise OR (e.g., `Caching | Idempotent`)

### Requirement: Tool registry supports register and resolve operations
The system SHALL provide an `IToolRegistry` interface with capabilities:
- `void Register(ToolDescriptor descriptor, ITool implementation)` - register a tool
- `ITool? Resolve(string toolId)` - resolve tool by stable identifier
- `ITool? ResolveByName(string name)` - resolve tool by display name
- `ToolDescriptor? ResolveDescriptor(string toolId)` - resolve descriptor by ID
- `IEnumerable<ToolDescriptor> List()` - enumerate all registered tool descriptors
- `bool IsRegistered(string toolId)` - check if tool is registered

The registry MUST reject duplicate registrations for the same `toolId`.

#### Scenario: Tool is registered and resolved by ID
- **GIVEN** a tool with descriptor `Id = "test:tool"` is registered
- **WHEN** `Resolve("test:tool")` is called
- **THEN** the correct `ITool` implementation is returned

#### Scenario: Duplicate registration is rejected
- **GIVEN** a tool with descriptor `Id = "test:tool"` is already registered
- **WHEN** another tool attempts registration with the same `Id`
- **THEN** an `ArgumentException` is thrown

### Requirement: Tool catalog enumeration is deterministic
The system SHALL ensure `IToolRegistry.List()` returns tools in stable order.

Enumeration MUST be sorted by `ToolDescriptor.Id` using ordinal string comparison.

#### Scenario: Multiple catalog enumerations return consistent order
- **GIVEN** three tools registered with IDs `z-tool`, `a-tool`, `m-tool`
- **WHEN** `List()` is called multiple times
- **THEN** the order is consistently `a-tool`, `m-tool`, `z-tool` (ordinal sort)

### Requirement: Tool registry implementation is thread-safe
The system SHALL provide a `ToolRegistry` implementation that is thread-safe for concurrent registration and resolution operations.

#### Scenario: Concurrent registrations do not corrupt state
- **GIVEN** multiple threads registering tools simultaneously
- **WHEN** all registrations complete
- **THEN** all tools are correctly registered with no data corruption

### Requirement: Stable tool IDs are cataloged
The system SHALL maintain a `ToolIds` catalog class containing constants for built-in tool identifiers.

Tool ID constants MUST follow the pattern: `nirmata:aos:tool:{category}:{name}`

The catalog SHALL provide factory methods:
- `McpToolId(string serverName, string toolName)` - generates MCP tool ID

#### Scenario: Built-in tool IDs are discoverable
- **GIVEN** a developer needs to reference a built-in tool
- **WHEN** they access `ToolIds.FileSystemRead`
- **THEN** they receive the stable identifier `"nirmata:aos:tool:filesystem:read"`

#### Scenario: MCP tool IDs are generated consistently
- **GIVEN** an MCP server named "filesystem" with tool "read"
- **WHEN** `ToolIds.McpToolId("filesystem", "read")` is called
- **THEN** it returns `"nirmata:aos:tool:mcp:filesystem:read"`

### Requirement: MCP adapter wraps external tools as ITool
The system SHALL provide an `McpToolAdapter` class that implements `ITool` by delegating to an MCP endpoint.

The adapter MUST:
- Translate `ToolRequest.Parameters` to MCP call parameters
- Normalize MCP responses to `ToolResult` format
- Handle MCP-specific exceptions and convert to `ToolResult.Failure`

#### Scenario: MCP tool invocation returns normalized result
- **GIVEN** an MCP endpoint is configured and wrapped by `McpToolAdapter`
- **WHEN** the adapter's `InvokeAsync` is called
- **THEN** the result is a `ToolResult` with normalized data or error structure

#### Scenario: MCP failure is normalized to ToolResult
- **GIVEN** an `McpToolAdapter` configured with an MCP client that will fail
- **WHEN** the adapter processes a request that fails
- **THEN** the returned `ToolResult` has `IsSuccess = false` with MCP error details

### Requirement: MCP tool factory creates adapters from metadata
The system SHALL provide an `McpToolFactory` class that creates `McpToolAdapter` instances from MCP server tool metadata.

The factory MUST:
- Generate `ToolDescriptor` from `McpToolMetadata`
- Map MCP parameter metadata to `ToolParameter` models
- Map MCP capabilities to `ToolCapability` flags

#### Scenario: Factory creates adapter from MCP metadata
- **GIVEN** MCP tool metadata describing a tool
- **WHEN** `McpToolFactory.CreateAdapter(serverName, metadata)` is called
- **THEN** a configured `McpToolAdapter` is returned with appropriate `ToolDescriptor`

