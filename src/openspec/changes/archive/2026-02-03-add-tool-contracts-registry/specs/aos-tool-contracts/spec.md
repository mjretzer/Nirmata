# aos-tool-contracts Specification

## Purpose
Defines the tool invocation contract, descriptors, and registry system for the Agent Orchestration Engine (AOS). Enables consistent, auditable tool calls across internal tools and external MCP endpoints.

## ADDED Requirements

### Requirement: Tool invocation contract is standardized
The system SHALL define an `ITool` interface as the primary contract for tool implementations.

The `ITool` interface MUST specify:
- `Task<ToolResult> InvokeAsync(ToolRequest request, CancellationToken cancellationToken)`
- `ToolDescriptor Descriptor { get; }`

Tool implementations MUST return a `ToolResult` that includes:
- `bool IsSuccess` - operation success indicator
- `JsonElement? Data` - successful result payload
- `string? ErrorCode` - error classification when failed
- `string? ErrorMessage` - human-readable error when failed

#### Scenario: Tool invocation succeeds with data
- **GIVEN** a registered tool implementation
- **WHEN** `InvokeAsync` is called with valid `ToolRequest`
- **THEN** the returned `ToolResult` has `IsSuccess = true` and contains result data

#### Scenario: Tool invocation fails with error details
- **GIVEN** a registered tool implementation that encounters an error
- **WHEN** `InvokeAsync` is called and the tool fails
- **THEN** the returned `ToolResult` has `IsSuccess = false` with `ErrorCode` and `ErrorMessage` populated

### Requirement: Tool requests carry execution context
The system SHALL define a `ToolRequest` model that includes:
- `string ToolId` - the stable identifier of the tool being invoked
- `JsonElement Inputs` - tool-specific input parameters
- `string RunId` - correlation ID linking to current orchestration run
- `string? CallId` - optional unique identifier for this specific invocation
- `Dictionary<string, string> Metadata` - additional contextual key-value pairs

#### Scenario: Tool request contains all required context
- **GIVEN** a tool invocation is being prepared
- **WHEN** a `ToolRequest` is constructed
- **THEN** the request includes `ToolId`, `Inputs`, `RunId`, and any correlation metadata

### Requirement: Tool descriptors expose metadata for introspection
The system SHALL define a `ToolDescriptor` model containing:
- `string Id` - stable unique identifier (e.g., `nirmata:aos:tool:filesystem:read`)
- `string Name` - human-readable display name
- `string Description` - explanation of tool purpose and behavior
- `string? InputSchemaRef` - reference to JSON schema for inputs
- `string? OutputSchemaRef` - reference to JSON schema for outputs
- `ToolCapabilities Capabilities` - flags indicating caching, retry, streaming support

#### Scenario: Tool descriptor provides complete metadata
- **GIVEN** a tool is registered in the system
- **WHEN** its `ToolDescriptor` is retrieved
- **THEN** the descriptor contains stable `Id`, display `Name`, `Description`, and capability flags

### Requirement: Tool registry supports register and resolve operations
The system SHALL provide an `IToolRegistry` interface with capabilities:
- `void Register(ToolDescriptor descriptor, ITool implementation)` - register a tool
- `ITool? Resolve(string toolId)` - resolve tool by stable identifier
- `ITool? ResolveByName(string name)` - resolve tool by display name
- `IReadOnlyList<ToolDescriptor> List()` - enumerate all registered tool descriptors

The registry MUST reject duplicate registrations for the same `toolId`.

#### Scenario: Tool is registered and resolved by ID
- **GIVEN** a tool with descriptor `Id = "test:tool"` is registered
- **WHEN** `Resolve("test:tool")` is called
- **THEN** the correct `ITool` implementation is returned

#### Scenario: Duplicate registration is rejected
- **GIVEN** a tool with descriptor `Id = "test:tool"` is already registered
- **WHEN** another tool attempts registration with the same `Id`
- **THEN** an `InvalidOperationException` is thrown

### Requirement: Tool catalog enumeration is deterministic
The system SHALL ensure `IToolRegistry.List()` returns tools in stable order.

Enumeration MUST be sorted by `ToolDescriptor.Id` using ordinal string comparison.

#### Scenario: Multiple catalog enumerations return consistent order
- **GIVEN** three tools registered with IDs `z-tool`, `a-tool`, `m-tool`
- **WHEN** `List()` is called multiple times
- **THEN** the order is consistently `a-tool`, `m-tool`, `z-tool` (ordinal sort)

### Requirement: Stable tool IDs are cataloged
The system SHALL maintain a `ToolIds` catalog class in `nirmata.Aos.Public.Catalogs` containing constants for built-in tool identifiers.

Tool ID constants MUST follow the pattern: `nirmata:aos:tool:{category}:{name}:{version}`

#### Scenario: Built-in tool IDs are discoverable
- **GIVEN** a developer needs to reference a built-in tool
- **WHEN` they access `ToolIds.FileSystemReadV1`
- **THEN** they receive the stable identifier `"nirmata:aos:tool:filesystem:read:v1"`

### Requirement: MCP adapter wraps external tools as ITool
The system SHALL provide an `McpToolAdapter` class that implements `ITool` by delegating to an MCP endpoint.

The adapter MUST:
- Translate `ToolRequest.Inputs` to MCP call parameters
- Normalize MCP responses to `ToolResult` format
- Capture invocation evidence via existing call envelope infrastructure
- Report `Capabilities` indicating external tool nature

#### Scenario: MCP tool invocation returns normalized result
- **GIVEN** an MCP endpoint is configured and wrapped by `McpToolAdapter`
- **WHEN` the adapter's `InvokeAsync` is called
- **THEN** the result is a `ToolResult` with normalized data or error structure

#### Scenario: MCP adapter stub returns normalized result
- **GIVEN** an `McpToolAdapter` is configured with a stub/mock MCP client
- **WHEN` the adapter processes a test request
- **THEN** the returned `ToolResult` follows the normalized success/failure contract
