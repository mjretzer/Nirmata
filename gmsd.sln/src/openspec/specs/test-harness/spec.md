# test-harness Specification

## Purpose

Defines test harness conventions and fixtures used for validating the platform.

- **Lives in:** `tests/*`
- **Owns:** Test fixtures, categories, and repeatable verification patterns
- **Does not own:** Production runtime behavior
## Requirements
### Requirement: TST-HRN-001 â€” Disposable fixture repositories

The test infrastructure MUST provide disposable, isolated fixture repositories for E2E tests that are created in temp folders and automatically cleaned up.

#### Scenario: E2E tests need isolated, deterministic repositories

- **Given** a test requires a fresh repository
- **When** the test calls `FixtureRepo.Create()`
- **Then** a temp folder `%TEMP%/fixture-{guid}/` is created with template files
- **And** the folder is cleaned up when the test disposes the fixture

#### Scenario: Fixture templates are minimal and fast

- **Given** the "minimal" template is used
- **When** the fixture is created
- **Then** it contains a valid `.csproj` and `.cs` file
- **And** the project can be built with `dotnet build`

### Requirement: TST-HRN-002 â€” AOS test harness API

The test harness MUST provide an API for running AOS commands, validating workspace layout, and reading state files from `.aos/` directories.

#### Scenario: Tests can run AOS commands

- **Given** a test harness initialized with a repo root
- **When** `RunAsync("init")` is called
- **Then** the command executes against that repo
- **And** a `RunResult` with exit code and output is returned

#### Scenario: Tests can validate AOS workspace layout

- **Given** a repo where `aos init` has run
- **When** `AssertLayout()` is called
- **Then** it asserts all 6 layers exist: schemas, spec, state, evidence, context, codebase, cache

#### Scenario: Tests can read state files

- **Given** a state file at `.aos/state/events.jsonl`
- **When** `ReadEventsTail(10)` is called
- **Then** the last 10 events are returned as typed objects

### Requirement: TST-HRN-003 â€” CLI and in-proc execution modes

The test harness MUST support both CLI subprocess execution and in-process command routing for flexibility and speed.

#### Scenario: Tests can run via CLI subprocess

- **Given** CLI mode is selected
- **When** a command is run
- **Then** the `aos` executable is spawned as a subprocess
- **And** stdin/stdout/stderr are captured

#### Scenario: Tests can run in-process for speed

- **Given** in-proc mode is selected
- **When** a command is run
- **Then** the command is routed through `ICommandRouter` directly
- **And** no subprocess is created

