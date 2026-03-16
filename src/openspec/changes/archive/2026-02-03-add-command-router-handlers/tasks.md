## 1. Public Surface - Command Routing Interface
- [x] 1.1 Define `ICommandRouter` interface with `RouteAsync` method in `nirmata.Aos/Public/Services/ICommandRouter.cs`
- [x] 1.2 Define `CommandRouteResult` contract in `nirmata.Aos/Contracts/Commands/CommandRouteResult.cs`
- [x] 1.3 Define `CommandRequest` contract in `nirmata.Aos/Contracts/Commands/CommandRequest.cs`

## 2. Public Surface - Command Catalog
- [x] 2.1 Populate `CommandIds` with stable identifiers for all core commands in `nirmata.Aos/Public/Catalogs/CommandIds.cs`
- [x] 2.2 Create `CommandCatalog` class for handler registration in `nirmata.Aos/Public/Catalogs/CommandCatalog.cs`
- [x] 2.3 Add command metadata contracts (name, group, description) in `nirmata.Aos/Contracts/Commands/CommandMetadata.cs`

## 3. Engine - Command Base Infrastructure
- [x] 3.1 Create `ICommandHandler` interface in `nirmata.Aos/Engine/Commands/Base/ICommandHandler.cs`
- [x] 3.2 Create `CommandContext` class in `nirmata.Aos/Engine/Commands/Base/CommandContext.cs`
- [x] 3.3 Create `CommandResult` base class in `nirmata.Aos/Engine/Commands/Base/CommandResult.cs`
- [x] 3.4 Create `CommandError` class for structured errors in `nirmata.Aos/Contracts/Commands/CommandError.cs`

## 4. Engine - Command Router Implementation
- [x] 4.1 Implement `CommandRouter` class in `nirmata.Aos/Engine/Commands/CommandRouter.cs`
- [x] 4.2 Wire `CommandRouter` to use `CommandCatalog` for handler resolution
- [x] 4.3 Implement unknown command handling with structured error response
- [x] 4.4 Register `ICommandRouter` in DI composition

## 5. Engine - Base Command Group Handlers
- [x] 5.1 Implement `InitCommandHandler` in `nirmata.Aos/Engine/Commands/Init/InitCommandHandler.cs`
- [x] 5.2 Implement `StatusCommandHandler` in `nirmata.Aos/Engine/Commands/Status/StatusCommandHandler.cs`
- [x] 5.3 Implement `ConfigCommandHandler` in `nirmata.Aos/Engine/Commands/Config/ConfigCommandHandler.cs`

## 6. Engine - Spec/State/Run Command Handlers
- [x] 6.1 Implement `ValidateCommandHandler` in `nirmata.Aos/Engine/Commands/Validate/ValidateCommandHandler.cs`
- [x] 6.2 Implement `SpecCommandHandler` in `nirmata.Aos/Engine/Commands/Spec/SpecCommandHandler.cs`
- [x] 6.3 Implement `StateCommandHandler` in `nirmata.Aos/Engine/Commands/State/StateCommandHandler.cs`
- [x] 6.4 Implement `RunCommandHandler` in `nirmata.Aos/Engine/Commands/Runs/RunCommandHandler.cs`

## 7. Engine - Help Renderer
- [x] 7.1 Implement `HelpCommandHandler` in `nirmata.Aos/Engine/Commands/Help/HelpCommandHandler.cs`
- [x] 7.2 Create help output formatter using command catalog metadata
- [x] 7.3 Generate help text for all registered commands

## 8. Tests
- [x] 8.1 Unit test: Unknown command returns structured error
- [x] 8.2 Unit test: Command catalog resolves registered handlers
- [x] 8.3 Integration test: Init → Validate happy path in test harness
- [x] 8.4 Unit test: Help output includes all core commands
- [x] 8.5 Unit test: CommandRouter implements ICommandRouter interface

## 9. Evidence Integration
- [x] 9.1 Ensure commands write to `.aos/evidence/runs/RUN-*/commands.json` when evidence enabled
- [x] 9.2 Add command execution metadata to evidence
