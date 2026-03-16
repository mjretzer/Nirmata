## Context

The AOS engine needs to route commands from external callers (CLI hosts, Windows Service API) to the appropriate handler implementations. This requires:

1. A stable public interface (`ICommandRouter`) that external callers can compile against
2. A command catalog that maps `{group, command}` pairs to handlers
3. Concrete handler implementations for core commands (init, status, config, validate, spec, state, run)
4. Structured error responses for routing failures (unknown commands)

The existing `ICommandRouter` and `CommandIds` are stubs—this change provides the full routing infrastructure.

## Goals / Non-Goals

### Goals
- Provide a public routing surface in `nirmata.Aos.Public.Services` that callers can compile against
- Enable handler registration via a catalog pattern
- Implement all core command handlers matching the command catalog
- Generate help output from registered command metadata
- Return structured errors for unknown commands

### Non-Goals
- Full CLI argument parsing (handled by the host/CLI layer)
- Authorization/authentication within the router
- Async streaming command responses
- Command middleware/pipeline extensibility in this phase

## Decisions

### Decision: Public router interface returns task-based results

`ICommandRouter.RouteAsync` will accept a `CommandRequest` and return `Task<CommandRouteResult>`. This allows handlers to perform async I/O (workspace access, evidence writes) while keeping the public surface simple.

```csharp
public interface ICommandRouter
{
    Task<CommandRouteResult> RouteAsync(CommandRequest request, CancellationToken ct = default);
}
```

### Decision: Command identification uses `{group, command}` tuple

Commands are identified by a group (e.g., "spec", "state") and a command name (e.g., "init", "validate"). This matches CLI conventions and provides a namespace for related commands.

### Decision: Handler registration is explicit in composition

Handlers are registered in the `CommandCatalog` during DI composition. The catalog is immutable after initialization to ensure deterministic routing behavior.

### Decision: CommandContext provides workspace + evidence access

Handlers receive a `CommandContext` containing:
- `IWorkspace` - workspace path resolution
- `IEvidenceStore` (optional) - evidence capture when enabled
- `CancellationToken` - cancellation propagation

### Decision: Help is a first-class command

The help system is implemented as a command handler (`HelpCommandHandler`) that reads from the `CommandCatalog`. This ensures help output always matches registered commands.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Handler registration errors surface at runtime | Fail-fast validation during composition; unit tests for registration |
| Public surface couples to command contracts | Use stable contract types in `nirmata.Aos.Contracts.Commands` |
| Help output may become stale | Help generated from catalog metadata at runtime |

## Migration Plan

1. Implement base routing infrastructure (router, catalog, base handler)
2. Implement handlers one command group at a time (init → validate → spec → state → run)
3. Implement help renderer last (depends on catalog population)
4. Add unit tests for each handler
5. Add integration test for init → validate happy path

## Open Questions

- Should command handlers be singleton or scoped per execution? (Decision: scoped per execution for cancellation safety)
- Should the router support command aliases? (Decision: no aliases in this phase)
- How should handlers report partial success? (Decision: use `CommandResult` with `IsSuccess` and `Errors` collection)
