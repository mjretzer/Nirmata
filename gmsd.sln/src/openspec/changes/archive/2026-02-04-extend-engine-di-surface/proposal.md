# Proposal: Extend Engine Public DI Surface with Missing Core Services

## Overview

The Engine (`Gmsd.Aos`) currently exposes 7 services via `AddGmsdAos()`:
- `IWorkspace`, `ISpecStore`, `IStateStore`, `IEvidenceStore`, `IValidator`
- `ICommandRouter` (scoped), `CommandCatalog`

However, several core capabilities defined in existing specs are not yet exposed as formal DI abstractions. This proposal adds the missing service interfaces so Plane workflows can consume Engine capabilities consistently via dependency injection rather than ad-hoc implementations.

## Goals

1. Expose **artifact path mapping** as a service (`IArtifactPathResolver`)
2. Expose **deterministic JSON IO** as a service (`IDeterministicJsonSerializer`)
3. Expose **schema registry** as a service (`ISchemaRegistry`)
4. Expose **run lifecycle management** as a service (`IRunManager`)
5. Expose **event append/tail** as a service (`IEventStore`)
6. Expose **checkpoint management** as a service (`ICheckpointManager`)
7. Expose **lock management** as a service (`ILockManager`)
8. Expose **cache management** as a service (`ICacheManager`)

## Non-Goals

- Implementation of the services (only interface definitions + DI registration)
- Changes to existing service implementations
- New CLI commands (CLI uses these services; services don't add CLI)

## Related Specs

This proposal maps to existing specs that already define the required behavior:

| Service | Spec | Current Status |
|---------|------|----------------|
| `IArtifactPathResolver` | `aos-path-routing` | Spec defined, no service interface |
| `IDeterministicJsonSerializer` | `aos-deterministic-json-serialization` | Spec defined, no service interface |
| `ISchemaRegistry` | `aos-schema-registry` | Spec defined, no service interface |
| `IRunManager` | `aos-run-lifecycle` | Spec defined, implemented ad-hoc in command handlers |
| `IEventStore` | `aos-state-store` | Partial (IStateStore exists), missing event-specific operations |
| `ICheckpointManager` | `aos-checkpoints` | Spec defined, no service interface |
| `ILockManager` | `aos-lock-manager` | Spec defined, no service interface |
| `ICacheManager` | `aos-cache-hygiene` | Spec defined, no service interface |

## Change ID

`extend-engine-di-surface`

## Proposed Directory Structure

```
openspec/changes/extend-engine-di-surface/
├── proposal.md (this file)
├── tasks.md
├── design.md
└── specs/
    ├── engine-artifact-paths/
    │   └── spec.md
    ├── engine-deterministic-json/
    │   └── spec.md
    ├── engine-schema-registry-service/
    │   └── spec.md
    ├── engine-run-manager/
    │   └── spec.md
    ├── engine-event-store/
    │   └── spec.md
    ├── engine-checkpoint-manager/
    │   └── spec.md
    ├── engine-lock-manager/
    │   └── spec.md
    └── engine-cache-manager/
        └── spec.md
```

## Verification Criteria

- All new services are registered in `AddGmsdAos()` with appropriate lifetimes
- `dotnet build Gmsd.Aos.csproj` passes
- Existing tests continue to pass
- Plane (`Gmsd.Agents`) can resolve all services via DI
