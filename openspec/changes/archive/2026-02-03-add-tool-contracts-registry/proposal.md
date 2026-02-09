# Change: Add Tool Contracts and Registry

## Why
The engine requires a formal tool invocation system to support extensible, auditable tool calls. Currently, there is no unified contract for tool descriptors, no registry for tool resolution, and no MCP boundary adapter. This change establishes the foundation for tool abstraction, enabling the engine to invoke tools (both internal and external via MCP) with consistent contracts and evidence capture.

## What Changes
- **ADDED** `ITool` interface defining the tool invocation contract
- **ADDED** Tool request/result shape models for type-safe tool interactions
- **ADDED** Tool descriptor/metadata models for tool registration
- **ADDED** Tool registry with register/resolve capabilities and stable enumeration
- **ADDED** `ToolIds` catalog for stable tool identification
- **ADDED** MCP adapter boundary to wrap MCP endpoints as `ITool` implementations

## Impact
- **Affected specs:** `aos-provider-tool-abstractions` (complements), new `aos-tool-contracts`
- **Affected code paths:**
  - `Gmsd.Aos/Contracts/Tools/**` - tool contract interfaces and models
  - `Gmsd.Aos/Tools/**` - tool descriptor and metadata implementations
  - `Gmsd.Aos/Registry/**` - tool registry implementation
  - `Gmsd.Aos/Public/Catalogs/**` - `ToolIds.cs` for stable tool IDs
  - `Gmsd.Aos/Mcp/**` - MCP adapter implementation

## Dependencies
- `aos-provider-tool-abstractions` (existing) - provides call envelope foundation for tool invocation evidence
