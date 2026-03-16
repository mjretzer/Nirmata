# Change: Move Tool Execution Infrastructure from AOS to Agents

## Why

The architectural boundary between `nirmata.Aos` (Engine) and `nirmata.Agents` (Plane) is currently violated by having tool execution infrastructure in the wrong layer. According to `@/openspec/project.md` and `@/openspec/roadmap.md`:

- **AOS (Engine)** owns: Tool *contracts* (ITool, ToolRequest, ToolResult) - the primitives
- **Agents (Plane)** owns: Tool *execution* (ToolRegistry, ToolDescriptor, MCP adapters) - the orchestration

Currently, `nirmata.Aos` contains:
- `ToolRegistry` - tool registration/resolution
- `ToolDescriptor`, `ToolCapability`, `ToolParameter` - tool metadata for discovery
- `McpToolAdapter`, `McpToolFactory` - MCP tool implementations

These should be in `nirmata.Agents` alongside LLM provider abstractions, as they represent tool *execution* infrastructure, not workspace primitives.

## What Changes

### Code Moves

**Move from `nirmata.Aos` to `nirmata.Agents`:**
- `nirmata.Aos/Tools/` → `nirmata.Agents/Tools/`
  - `ToolDescriptor.cs`
  - `ToolCapability.cs` 
  - `ToolParameter.cs`
- `nirmata.Aos/Mcp/` → `nirmata.Agents/Mcp/`
  - `McpToolAdapter.cs`
  - `McpToolFactory.cs`
- `nirmata.Aos/Registry/` → `nirmata.Agents/Registry/`
  - `IToolRegistry.cs`
  - `ToolRegistry.cs`

**Keep in `nirmata.Aos/Contracts/Tools/`:**
- `ITool.cs` - the contract interface
- `ToolRequest.cs` - input contract
- `ToolResult.cs` - output contract
- `ToolContext.cs` - execution context contract

### Namespace Updates

- `nirmata.Aos.Tools` → `nirmata.Agents.Tools`
- `nirmata.Aos.Mcp` → `nirmata.Agents.Mcp`
- `nirmata.Aos.Registry` → `nirmata.Agents.Registry`
- `nirmata.Aos.Contracts.Tools` (unchanged - already correct)

### Spec Updates

- **MODIFY** `aos-tool-contracts`: Clarify that AOS only owns ITool/ToolRequest/ToolResult contracts; move registry/descriptor/MCP requirements to Agents
- **ADD** `agents-tool-registry`: New capability spec for tool registry, descriptors, and MCP adapters in Agents plane

## Impact

- **Affected specs:** `aos-tool-contracts`, new `agents-tool-registry`
- **Affected code:** 
  - `nirmata.Aos/Tools/*` → `nirmata.Agents/Tools/*`
  - `nirmata.Aos/Mcp/*` → `nirmata.Agents/Mcp/*`
  - `nirmata.Aos/Registry/*` → `nirmata.Agents/Registry/*`
- **Breaking changes:** Namespace changes for any consumers of moved types
- **Tests:** All test files referencing moved types need namespace updates
