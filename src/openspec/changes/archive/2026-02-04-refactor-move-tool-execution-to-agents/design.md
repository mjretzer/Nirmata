## Context

The nirmata solution has a clear architectural separation between:

- **Engine (`nirmata.Aos`)**: Workspace primitives, contracts, deterministic I/O, schema validation
- **Plane (`nirmata.Agents`)**: Workflow orchestration, LLM providers, tool execution

Currently, tool execution infrastructure (ToolRegistry, ToolDescriptor, MCP adapters) resides in `nirmata.Aos`, violating the architectural boundary. This creates a dependency where the Engine owns tool orchestration concerns that should belong to the Plane.

## Goals

- Enforce clean architectural boundary between Engine and Plane
- Move tool *execution* infrastructure to Agents (where LLM orchestration lives)
- Keep tool *contracts* (ITool interface) in AOS (the primitive)
- Enable Agents to own the complete tool lifecycle (registration, discovery, MCP wrapping)

## Non-Goals

- Changing ITool interface semantics
- Changing ToolResult/ToolRequest/ToolsContext contracts
- Adding new tool capabilities (just moving existing ones)
- Modifying MCP protocol implementation details

## Decisions

### Decision: ToolRegistry moves to Agents

**Rationale**: ToolRegistry is about tool *discovery* and *resolution* at runtime - an orchestration concern. Agents orchestrates tool calls during workflow execution.

**Alternative considered**: Keep registry in AOS as a "primitive service"
- Rejected: Registry is not a workspace primitive; it's runtime state management

### Decision: ToolDescriptor moves to Agents

**Rationale**: ToolDescriptor is metadata for tool *registration* and *discovery*. The Engine only needs the stable ITool contract for invocation.

**Alternative considered**: Keep descriptor in AOS as part of the contract
- Rejected: Descriptor is consumed by the registry (now in Agents) and contains registration-time metadata, not invocation-time contracts

### Decision: MCP adapters move to Agents

**Rationale**: MCP is a provider abstraction, similar to LLM providers. Agents owns all provider adapters (Anthropic, AzureOpenAI, Ollama, MCP).

**Alternative considered**: Keep MCP in AOS as "external tool contract"
- Rejected: MCP is a specific provider implementation, not a generic contract

## Migration Plan

1. **Phase 1**: Copy files to new locations with updated namespaces
2. **Phase 2**: Update all imports in Agents and test projects
3. **Phase 3**: Remove old files from AOS
4. **Phase 4**: Verify builds and tests pass

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Namespace churn in active code | Coordinated change; update all imports in single commit |
| Test project breakage | Run full test suite before committing |
| Cross-project references | Verify nirmata.Agents still compiles against nirmata.Aos contracts |

## Open Questions

- None identified
