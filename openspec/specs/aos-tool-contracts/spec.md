# aos-tool-contracts Specification

## Purpose
TBD - created by archiving change add-tool-contracts-registry. Update Purpose after archive.
## Requirements
### Requirement: Tool invocation contract is standardized
The system SHALL define an `ITool` interface as the primary contract for tool implementations.

The `ITool` interface MUST specify:
- `Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken)`
- `ToolDescriptor Descriptor { get; }`

Tool implementations MUST return a `ToolResult` that includes:
- `bool IsSuccess` - operation success indicator
- `object? Data` - successful result payload
- `string? ErrorCode` - error classification when failed
- `string? ErrorMessage` - human-readable error when failed

**Note**: `ToolDescriptor` is defined in `Gmsd.Agents.Tools` namespace as it is a registration-time metadata type.

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
- `Dictionary<string, object?> Parameters` - tool-specific input parameters
- `string? CorrelationId` - optional correlation ID for tracing

#### Scenario: Tool request contains all required context
- **GIVEN** a tool invocation is being prepared
- **WHEN** a `ToolRequest` is constructed
- **THEN** the request includes `ToolId`, `Parameters`, and any correlation metadata

### Requirement: Tool results provide standardized output
The system SHALL define a `ToolResult` model that provides:
- Factory method `Success(object? data)` for creating successful results
- Factory method `Failure(string errorCode, string errorMessage)` for failed results
- `bool IsSuccess` - operation success indicator
- `object? Data` - successful result payload
- `string? ErrorCode` - error classification when failed
- `string? ErrorMessage` - human-readable error when failed

#### Scenario: Tool result indicates success
- **GIVEN** a tool execution completes successfully
- **WHEN** `ToolResult.Success(data)` is called
- **THEN** the result has `IsSuccess = true` and contains the data payload

#### Scenario: Tool result indicates failure
- **GIVEN** a tool execution fails
- **WHEN** `ToolResult.Failure(code, message)` is called
- **THEN** the result has `IsSuccess = false` with error details populated

### Requirement: Tool context provides execution metadata
The system SHALL define a `ToolContext` model containing:
- `string RunId` - correlation ID linking to current orchestration run
- `string? CallId` - optional unique identifier for this specific invocation
- `Dictionary<string, string> Metadata` - additional contextual key-value pairs

#### Scenario: Tool context carries run correlation
- **GIVEN** a tool invocation within an orchestration run
- **WHEN** `ToolContext` is constructed with run ID
- **THEN** the context includes `RunId` for correlation and tracing

