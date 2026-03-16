## 1. Implementation

- [x] 1.1 Create `nirmata.Agents/Tools/` directory with moved files
  - [x] 1.1.1 Move `ToolDescriptor.cs` (update namespace to `nirmata.Agents.Tools`)
  - [x] 1.1.2 Move `ToolCapability.cs` (update namespace to `nirmata.Agents.Tools`)
  - [x] 1.1.3 Move `ToolParameter.cs` (update namespace to `nirmata.Agents.Tools`)
- [x] 1.2 Create `nirmata.Agents/Mcp/` directory with moved files
  - [x] 1.2.1 Move `McpToolAdapter.cs` (update namespace to `nirmata.Agents.Mcp`)
  - [x] 1.2.2 Move `McpToolFactory.cs` (update namespace to `nirmata.Agents.Mcp`)
- [x] 1.3 Create `nirmata.Agents/Registry/` directory with moved files
  - [x] 1.3.1 Move `IToolRegistry.cs` (update namespace to `nirmata.Agents.Registry`)
  - [x] 1.3.2 Move `ToolRegistry.cs` (update namespace to `nirmata.Agents.Registry`)
- [x] 1.4 Update `nirmata.Aos` to remove moved directories
  - [x] 1.4.1 Delete `nirmata.Aos/Tools/` directory
  - [x] 1.4.2 Delete `nirmata.Aos/Mcp/` directory
  - [x] 1.4.3 Delete `nirmata.Aos/Registry/` directory
- [x] 1.5 Update namespace imports in all affected files
  - [x] 1.5.1 Update imports in `nirmata.Agents` files referencing moved types
  - [x] 1.5.2 Update imports in test projects (`nirmata.Aos.Tests`, `nirmata.Agents.Tests`)
- [x] 1.6 Update project references if needed
  - [x] 1.6.1 Ensure `nirmata.Agents` still references `nirmata.Aos` for `ITool` contract
  - [x] 1.6.2 Add `nirmata.Aos.Tests` reference to `nirmata.Agents` for MCP types

## 2. Validation

- [x] 2.1 Build solution successfully (`dotnet build`)
- [x] 2.2 Run tool-related tests (46 tests passed)
- [x] 2.3 Verify `nirmata.Aos/Contracts/Tools/` contains only contracts (ITool, ToolRequest, ToolResult, ToolContext)
- [x] 2.4 Verify `nirmata.Agents` contains tool execution infrastructure (Registry, Tools, Mcp)
- [x] 2.5 Run `openspec validate refactor-move-tool-execution-to-agents --strict`

## 3. Documentation

- [x] 3.1 Update any internal documentation referencing old namespaces
- [x] 3.2 Verify spec deltas are accurate
