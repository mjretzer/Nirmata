## 1. Tool Contract Models
- [x] 1.1 Create `Gmsd.Aos/Contracts/Tools/ITool.cs` - core tool invocation interface
- [x] 1.2 Create `Gmsd.Aos/Contracts/Tools/ToolRequest.cs` - standardized request shape
- [x] 1.3 Create `Gmsd.Aos/Contracts/Tools/ToolResult.cs` - standardized result shape with success/fail states
- [x] 1.4 Create `Gmsd.Aos/Contracts/Tools/ToolContext.cs` - execution context (runId, correlation IDs)

## 2. Tool Descriptor and Metadata
- [x] 2.1 Create `Gmsd.Aos/Tools/ToolDescriptor.cs` - metadata model (id, name, description, input/output schema refs)
- [x] 2.2 Create `Gmsd.Aos/Tools/ToolCapability.cs` - capability flags (caching, retry, streaming)
- [x] 2.3 Create `Gmsd.Aos/Tools/ToolParameter.cs` - parameter metadata for introspection

## 3. Tool Registry
- [x] 3.1 Create `Gmsd.Aos/Registry/IToolRegistry.cs` - registry interface
- [x] 3.2 Create `Gmsd.Aos/Registry/ToolRegistry.cs` - in-memory registry implementation
- [x] 3.3 Implement `Register(ToolDescriptor, ITool)` - register tools with their descriptors
- [x] 3.4 Implement `Resolve(string toolId)` - resolve tool by stable ID
- [x] 3.5 Implement `ResolveByName(string name)` - resolve tool by display name
- [x] 3.6 Implement `List()` - enumerable tool catalog with deterministic ordering

## 4. Tool ID Catalog
- [x] 4.1 Create `Gmsd.Aos/Public/Catalogs/ToolIds.cs` - stable tool ID constants class

## 5. MCP Adapter Boundary
- [x] 5.1 Create `Gmsd.Aos/Mcp/McpToolAdapter.cs` - wraps MCP endpoint as `ITool`
- [x] 5.2 Create `Gmsd.Aos/Mcp/McpToolFactory.cs` - factory for MCP tool instances
- [x] 5.3 Implement request translation (ToolRequest → MCP call)
- [x] 5.4 Implement result normalization (MCP response → ToolResult)

## 6. Validation and Tests
- [x] 6.1 Unit tests for registry register/resolve operations
- [x] 6.2 Unit tests for catalog enumeration order stability
- [x] 6.3 Unit tests for MCP adapter stub invocation returning normalized results
- [x] 6.4 Integration test verifying end-to-end tool invocation through registry
