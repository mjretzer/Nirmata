# Design: Extend Engine Public DI Surface

## Context

The Engine (`nirmata.Aos`) owns the `.aos/` workspace contract, deterministic IO, schema validation, and lifecycle management. While the existing specs define the required behavior for these capabilities, they are not uniformly exposed as DI services. This design formalizes 8 additional service abstractions that Plane workflows need.

## Design Principles

1. **Service interfaces live in `nirmata.Aos/Public/`** — the only stable surface other projects compile against
2. **Implementations live in `nirmata.Aos/Engine/`** — internal to the engine, swappable
3. **All services are Singleton** — they manage workspace-wide state that must be consistent
4. **Specs already define behavior** — these interfaces formalize what specs require

## Service Taxonomy

### Group 1: Workspace Primitives

| Interface | Purpose | Key Operations |
|-----------|---------|----------------|
| `IArtifactPathResolver` | ID → canonical file path mapping | `ResolvePath(ArtifactId)`, `GetContractPath(WellKnownPath)` |
| `IDeterministicJsonSerializer` | Deterministic read/write | `Serialize<T>()`, `Deserialize<T>()`, `WriteAtomic()` |
| `IAosConfigStore` | Config get/set operations | `Get(string key)`, `Set(string key, string value)` |

### Group 2: Schema & Validation

| Interface | Purpose | Key Operations |
|-----------|---------|----------------|
| `ISchemaRegistry` | Load schemas by `$id` | `GetSchema(string schemaId)`, `ListSchemaIds()` |

### Group 3: Lifecycle Management

| Interface | Purpose | Key Operations |
|-----------|---------|----------------|
| `IRunManager` | Run start/finish/index | `StartRun()`, `FinishRun(string runId)`, `ListRuns()` |
| `IEventStore` | Event append/tail | `AppendEvent(AosEvent)`, `Tail(int n)`, `ListEvents(filter?)` |
| `ICheckpointManager` | Checkpoint create/restore | `CreateCheckpoint()`, `RestoreCheckpoint(string checkpointId)` |

### Group 4: Maintenance

| Interface | Purpose | Key Operations |
|-----------|---------|----------------|
| `ILockManager` | Workspace lock control | `Acquire()`, `Release()`, `GetStatus()` |
| `ICacheManager` | Cache hygiene | `Clear()`, `Prune(TimeSpan ageThreshold)` |

## Lifetime Strategy

All services are **Singleton** because:
- They manage workspace-wide resources (locks, indexes, registry caches)
- Multiple instances would create coordination problems (lock files, index corruption)
- They wrap filesystem operations that are naturally singleton-per-workspace

```csharp
// In AddnirmataAos():
services.AddSingleton<IArtifactPathResolver, ArtifactPathResolver>();
services.AddSingleton<IDeterministicJsonSerializer, DeterministicJsonSerializer>();
services.AddSingleton<ISchemaRegistry, SchemaRegistry>();
services.AddSingleton<IRunManager, RunManager>();
services.AddSingleton<IEventStore, EventStore>();
services.AddSingleton<ICheckpointManager, CheckpointManager>();
services.AddSingleton<ILockManager, LockManager>();
services.AddSingleton<ICacheManager, CacheManager>();
```

## Dependency Graph

```
IWorkspace (root)
  ├── IArtifactPathResolver (uses IWorkspace.AosRootPath)
  ├── IDeterministicJsonSerializer (stateless, shared)
  ├── ISchemaRegistry (embedded resource access)
  ├── ISpecStore (uses IArtifactPathResolver + IDeterministicJsonSerializer)
  ├── IStateStore (uses IArtifactPathResolver + IDeterministicJsonSerializer)
  ├── IEvidenceStore (uses IArtifactPathResolver + IDeterministicJsonSerializer)
  ├── IRunManager (uses IEvidenceStore + IEventStore)
  ├── IEventStore (uses IArtifactPathResolver)
  ├── ICheckpointManager (uses IStateStore + IArtifactPathResolver)
  ├── ILockManager (uses IArtifactPathResolver)
  ├── ICacheManager (uses IArtifactPathResolver)
  └── IValidator (uses ISchemaRegistry + ISpecStore + IStateStore)
```

## Cross-Cutting: Deterministic JSON

All file-writing services delegate to `IDeterministicJsonSerializer` for:
- UTF-8 encoding (no BOM)
- LF line endings
- Stable recursive key ordering
- Atomic write (temp + replace)
- No-churn semantics (skip write if bytes unchanged)

This ensures every `.aos/` artifact follows the same deterministic format regardless of which service writes it.

## CLI Integration

Existing CLI commands will be refactored to inject services rather than instantiate helpers:

```csharp
// Before (ad-hoc):
var runId = RunLifecycleHelper.StartRun(workspace);

// After (DI):
var runId = _runManager.StartRun();
```

Commands become thin adapters over services:
- `aos run start` → `_runManager.StartRun()`
- `aos lock acquire` → `_lockManager.Acquire()`
- `aos cache clear` → `_cacheManager.Clear()`

## Validation Approach

Each service will have:
1. **Interface definition** in `nirmata.Aos/Public/`
2. **Stub/Fake implementation** in test projects for Plane unit tests
3. **Real implementation** in `nirmata.Aos/Engine/` (may be minimal/stub initially)
4. **DI registration** in `AddnirmataAos()`

Real implementations can be minimal initially; the goal is establishing the contract boundary.

## Open Questions

1. **Should `IRunManager` depend on `IEventStore` or emit events via a separate mechanism?**
   - Proposal: `IRunManager` appends events via `IEventStore` for consistency

2. **Should `ICheckpointManager` support listing checkpoints?**
   - Proposal: Yes, `ListCheckpoints()` returns checkpoint metadata

3. **Should `ISchemaRegistry` cache loaded schemas?**
   - Proposal: Yes, in-memory cache as schemas are immutable once loaded

## Trade-offs Considered

| Alternative | Rejected Because |
|-------------|------------------|
| Scoped lifetime for some services | Lock/cache coordination becomes complex |
| Merging IRunManager into IEvidenceStore | Run lifecycle is distinct from evidence storage |
| Merging IEventStore into IStateStore | Events are append-only; state is mutable derived view |
| Static helpers instead of services | Prevents mocking/testing; violates DI pattern |

## Backwards Compatibility

- Existing 7 services remain unchanged
- New services are additive only
- No breaking changes to `AddnirmataAos()` signature (overloads acceptable)
