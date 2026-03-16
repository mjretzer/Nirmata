# aos-command-routing Specification

## Purpose

Defines canonical AOS workspace contracts and behavioral semantics for $capabilityId.

- **Lives in:** `nirmata.Aos/*`, `.aos/**`
- **Owns:** Engine-level artifact contracts, validation, and deterministic IO semantics for this capability
- **Does not own:** Plane/orchestrator workflows (owned by `agents-*` capabilities)
## Requirements
### Requirement: Public command router interface

The system SHALL provide a public `ICommandRouter` interface in `nirmata.Aos.Public.Services` that accepts a `CommandRequest` and returns a `Task<CommandRouteResult>`.

The interface MUST support cancellation via `CancellationToken`.

#### Scenario: Caller routes a valid command
- **GIVEN** a registered command `{group: "spec", command: "init"}`
- **WHEN** `RouteAsync` is called with a matching `CommandRequest`
- **THEN** the corresponding handler executes and returns a `CommandRouteResult`

#### Scenario: Caller cancels a command
- **GIVEN** a long-running command handler
- **WHEN** the cancellation token is triggered
- **THEN** the handler receives the cancellation signal and the router returns a cancelled result

### Requirement: Command catalog registration

The system SHALL provide a `CommandCatalog` in `nirmata.Aos.Public.Catalogs` that allows registration of command handlers by `{group, command}` tuple.

The catalog MUST reject duplicate registrations for the same `{group, command}` combination.

#### Scenario: Register a command handler
- **GIVEN** an `ICommandHandler` implementation for `{group: "spec", command: "init"}`
- **WHEN** the handler is registered in the catalog
- **THEN** the catalog contains the handler and its metadata

#### Scenario: Prevent duplicate registration
- **GIVEN** a handler already registered for `{group: "spec", command: "init"}`
- **WHEN** attempting to register another handler for the same tuple
- **THEN** the catalog throws an `InvalidOperationException`

### Requirement: Command identification catalog

The system SHALL provide stable command identifiers in `nirmata.Aos.Public.Catalogs.CommandIds` for all core commands:

- Base commands: `init`, `status`, `help`
- Spec commands: `spec.init`, `spec.validate`
- State commands: `state.show`, `state.reset`
- Config commands: `config.get`, `config.set`
- Validate commands: `validate.spec`, `validate.state`
- Run commands: `run.execute`, `run.resume`

#### Scenario: Consumer references stable command ID
- **WHEN** a consumer references `CommandIds.Spec.Init`
- **THEN** it receives the stable string identifier `"spec.init"`

### Requirement: Unknown command handling

The system SHALL return a structured error when `RouteAsync` is called with an unregistered `{group, command}` tuple.

The error MUST include:
- Error code: `COMMAND_UNKNOWN`
- Message indicating the command was not found
- Available groups/commands for discovery

#### Scenario: Unknown command returns structured error
- **GIVEN** no handler registered for `{group: "unknown", command: "test"}`
- **WHEN** `RouteAsync` is called with that request
- **THEN** the result indicates failure with error code `COMMAND_UNKNOWN`

### Requirement: Base command handler interface

The system SHALL provide an `ICommandHandler` interface in `nirmata.Aos.Engine.Commands.Base` that all command handlers implement.

The interface MUST define:
- `Group` property (string)
- `Command` property (string)
- `HandleAsync(CommandContext, CancellationToken)` method returning `Task<CommandResult>`

#### Scenario: Handler implements base interface
- **GIVEN** a class implementing `ICommandHandler`
- **WHEN** the handler is invoked via the router
- **THEN** the `HandleAsync` method executes with the provided context

### Requirement: Command context provides workspace access

The system SHALL provide a `CommandContext` class that handlers use to access:
- `IWorkspace` for path resolution
- `IEvidenceStore` for evidence capture (when enabled)
- `CancellationToken` for cancellation

#### Scenario: Handler accesses workspace
- **GIVEN** a handler that needs to read `.aos/spec/project.json`
- **WHEN** the handler accesses `context.Workspace.SpecStore.GetProjectPath()`
- **THEN** it receives the canonical path without referencing internal engine types directly

### Requirement: Init command handler

The system SHALL provide an `InitCommandHandler` that initializes a new AOS workspace.

The handler MUST:
- Create `.aos/` directory structure
- Write initial `spec/project.json` if not present
- Return success with workspace root path

#### Scenario: Init creates workspace structure
- **GIVEN** a directory without `.aos/`
- **WHEN** `init` command executes
- **THEN** `.aos/{spec,state,evidence,context,cache}/` directories exist

### Requirement: Validate command handlers

The system SHALL provide `ValidateSpecCommandHandler` and `ValidateStateCommandHandler` that validate workspace artifacts.

The handlers MUST:
- Run schema validation against the respective layer
- Return structured validation results (pass/fail with errors)
- Write validation evidence when evidence is enabled

#### Scenario: Validate spec returns structured result
- **GIVEN** a workspace with `.aos/spec/project.json`
- **WHEN** `validate.spec` command executes
- **THEN** the result indicates pass/fail with specific validation errors if any

### Requirement: Status command handler

The system SHALL provide a `StatusCommandHandler` that reports workspace status.

The handler MUST return:
- Workspace existence indicator
- Current phase/task from state (if available)
- Validation status summary

#### Scenario: Status reports workspace state
- **GIVEN** an initialized workspace at phase "PH-ENG-0011"
- **WHEN** `status` command executes
- **THEN** the result includes current phase and workspace health

### Requirement: Help command handler

The system SHALL provide a `HelpCommandHandler` that generates help output from the command catalog.

The handler MUST:
- List all registered commands grouped by category
- Include command descriptions from metadata
- Support optional filter by group

#### Scenario: Help lists all commands
- **GIVEN** handlers registered for init, spec, state, validate groups
- **WHEN** `help` command executes
- **THEN** the output includes all commands organized by group

#### Scenario: Help filters by group
- **GIVEN** the `help` command with argument `--group spec`
- **WHEN** the handler executes
- **THEN** only commands in the "spec" group are listed

### Requirement: Evidence capture for commands

The system SHALL write command execution metadata to `.aos/evidence/runs/RUN-*/commands.json` when evidence is enabled.

The metadata MUST include:
- Command `{group, command}` tuple
- Execution timestamp
- Success/failure status
- Error details (if failed)

#### Scenario: Command execution recorded in evidence
- **GIVEN** evidence enabled for a run
- **WHEN** a command executes via the router
- **THEN** the command appears in `commands.json` with timestamp and status

