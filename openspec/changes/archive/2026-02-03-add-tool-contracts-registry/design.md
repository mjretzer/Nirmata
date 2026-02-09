## Context
The engine requires a unified tool abstraction layer to support:
1. Internal tools (filesystem, process, git operations)
2. External tools via MCP (Model Context Protocol)
3. Future LLM provider integrations

This change establishes the contract boundary between the execution plane and tool implementations, ensuring consistent evidence capture via the existing `aos-provider-tool-abstractions` call envelope system.

## Goals
- Define stable `ITool` contract for all tool implementations
- Enable tool registration and discovery via deterministic catalog
- Support MCP tool integration without leaking MCP specifics into engine
- Maintain evidence auditability through existing call envelope infrastructure

## Non-Goals
- Full MCP client implementation (only adapter boundary)
- Tool execution scheduling or queuing
- Tool versioning/migration mechanics
- Remote tool distribution/network transport

## Decisions

### Decision: Single interface vs. abstract base
**Chosen:** `ITool` interface with explicit request/result shapes
**Rationale:** Keeps implementations flexible; enables proxy/wrapper patterns for MCP, caching, retry
**Alternatives:** Abstract base class (rejected - too restrictive for cross-cutting concerns)

### Decision: Registry as in-memory with deterministic ordering
**Chosen:** In-memory registry with sorted enumeration by ToolId
**Rationale:** Tools are registered at composition time; deterministic ordering supports stable test assertions and CLI output
**Alternatives:** Persistent registry (rejected - unnecessary complexity for compile-time tool sets)

### Decision: Separate MCP adapter project/namespace
**Chosen:** `Gmsd.Aos/Mcp/**` namespace for MCP-specific adapter types
**Rationale:** MCP is an external protocol; adapter isolation prevents protocol specifics from leaking into core tool abstractions
**Alternatives:** Inline MCP handling (rejected - violates separation of concerns)

### Decision: ToolResult discriminated by success/failure
**Chosen:** Single `ToolResult` type with `IsSuccess` flag and separate `Data`/`Error` properties
**Rationale:** Matches existing error patterns in Gmsd.Common; serializable for evidence capture
**Alternatives:** Exception-based failure (rejected - exceptions don't serialize cleanly for replay)

## Risks / Trade-offs
- **Registry singleton lifetime** → Mitigation: Scoped to workspace/run scope; no static singleton
- **MCP adapter dependency** → Mitigation: Adapter is stubbable; MCP client can be mocked
- **Tool ID collision** → Mitigation: ToolIds catalog uses `gmsd:aos:tool:` prefix; external tools use `mcp:` prefix

## Open Questions
- None identified at proposal stage
