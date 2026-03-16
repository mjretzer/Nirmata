# Tasks: Extend Engine Public DI Surface

## Phase 1: Foundation Services

### T1: Artifact Path Resolution Service
- [x] Define `IArtifactPathResolver` interface in `nirmata.Aos/Public/`
- [x] Map artifact IDs (MS/PH/TSK/ISS/UAT/PCK/RUN) to canonical paths
- [x] Register as Singleton in `AddnirmataAos()`
- [x] Verify: interface resolves all ID types per `aos-path-routing` spec

### T2: Deterministic JSON Serialization Service
- [x] Define `IDeterministicJsonSerializer` interface
- [x] Include: UTF-8 w/o BOM, LF endings, stable key ordering, atomic writes, no-churn
- [x] Register as Singleton in `AddnirmataAos()`
- [x] Verify: write-read-write yields byte-identical output

### T3: Schema Registry Service
- [x] Define `ISchemaRegistry` interface
- [x] Include: load by `$id`, embedded schema access, local schema pack support
- [x] Register as Singleton in `AddnirmataAos()`
- [x] Verify: registry loads all embedded schemas at startup

## Phase 2: Lifecycle & State Services

### T4: Run Manager Service
- [x] Define `IRunManager` interface
- [x] Include: start/finish run, run index management, packet/result artifacts
- [x] Register as Singleton in `AddnirmataAos()`
- [x] Verify: can create run, finish run, list runs per `aos-run-lifecycle` spec

### T5: Event Store Service
- [x] Define `IEventStore` interface
- [x] Include: append events, tail n events, list with filters
- [x] Register as Singleton in `AddnirmataAos()`
- [x] Verify: append then tail returns expected order per `aos-state-store` spec

### T6: Checkpoint Manager Service
- [x] Define `ICheckpointManager` interface
- [x] Include: create/restore checkpoints, state snapshots
- [x] Register as Singleton in `AddnirmataAos()`
- [x] Verify: checkpoint create/restore per `aos-checkpoints` spec

## Phase 3: Maintenance Services

### T7: Lock Manager Service
- [x] Define `ILockManager` interface
- [x] Include: acquire/release/status, fail-fast on contention
- [x] Register as Singleton in `AddnirmataAos()`
- [x] Verify: lock acquisition fails fast when held per `aos-lock-manager` spec

### T8: Cache Manager Service
- [x] Define `ICacheManager` interface
- [x] Include: clear, prune (days threshold)
- [x] Register as Singleton in `AddnirmataAos()`
- [x] Verify: clear/prune only affects `.aos/cache/**` per `aos-cache-hygiene` spec

## Phase 4: Integration & Validation

### T9: Update DI Registration
- [x] Extend `AddnirmataAos()` to register all 8 new services
- [x] Document lifetime conventions in XML comments
- [x] Ensure singletons have deterministic initialization order

### T10: Build Verification
- [x] `dotnet build nirmata.Aos.csproj` passes
- [x] `dotnet build nirmata.Agents.csproj` passes (can resolve services)
- [x] All existing tests pass (note: 4 pre-existing failures in PromptTemplateLoaderTests unrelated to this change)

### T11: OpenSpec Validation
- [x] Run `openspec validate extend-engine-di-surface --strict`
- [x] Resolve any validation errors
- [x] Ensure all spec deltas cross-reference existing specs
