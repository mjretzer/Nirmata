# Change: add-command-router-handlers

## Why

The AOS engine needs a deterministic command routing system that maps `{group, command}` pairs to concrete handlers. Currently, `ICommandRouter` and `CommandIds` exist as stubs without runtime behavior. This change establishes the routing infrastructure, command catalog, and core handler implementations for init, status, config, validate, spec, state, and run commands—enabling the engine to process commands with structured error responses and evidence capture.

## What Changes

- **ADD** `ICommandRouter` interface with `RouteAsync` method in `Gmsd.Aos/Public/Services/`
- **ADD** `CommandCatalog` for registering and resolving command handlers in `Gmsd.Aos/Public/Catalogs/`
- **POPULATE** `CommandIds` with stable command identifiers for all core commands
- **ADD** `ICommandHandler` base interface and `CommandContext` in `Gmsd.Aos/Engine/Commands/Base/`
- **ADD** Command group handlers in `Gmsd.Aos/Engine/Commands/{Init,Status,Config,Validate,Spec,State,Runs}/`
- **ADD** Help renderer in `Gmsd.Aos/Engine/Commands/Help/` that generates output from command catalog
- **ADD** `CommandRouter` implementation in `Gmsd.Aos/Engine/Commands/`
- **ADD** Structured error responses for unknown commands

## Impact

- **Affected specs:** 
  - `aos-public-api-surface` (extends command routing surface)
  - NEW `aos-command-routing` capability (command dispatch and handler contracts)
- **Affected code:**
  - `Gmsd.Aos/Public/Services/ICommandRouter.cs` (expanded from stub)
  - `Gmsd.Aos/Public/Catalogs/CommandIds.cs` (populated)
  - `Gmsd.Aos/Public/Catalogs/CommandCatalog.cs` (new)
  - `Gmsd.Aos/Engine/Commands/` (new directory tree)
- **Breaking:** None (adds new surface, does not modify existing contracts)
