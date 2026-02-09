# agents-composition-root Specification

## Purpose
TBD - created by archiving change scaffold-agents-di-baseline. Update Purpose after archive.
## Requirements
### Requirement: AddGmsdAgents extension method exists
The system SHALL provide an `AddGmsdAgents` extension method on `IServiceCollection` that registers all Plane-layer services and delegates to Engine composition.

`AddGmsdAgents(IServiceCollection, IConfiguration)` MUST:
- Call `AddGmsdAos()` to register Engine services (stores, validators, router, etc.)
- Register `ILlmProvider` via `AddLlmProvider()` based on configuration
- Register `IPromptTemplateLoader` as singleton
- Register Plane-specific options (`AgentsOptions`)
- Return `IServiceCollection` for fluent chaining

#### Scenario: Host wires up complete Plane services
- **GIVEN** a host application with `IServiceCollection` and `IConfiguration`
- **WHEN** `services.AddGmsdAgents(configuration)` is called
- **THEN** all Engine services (AOS) and Plane services (Agents) are registered in the DI container
- **AND** `ICommandRouter`, `IRunManager`, `ILlmProvider`, and `IPromptTemplateLoader` can be resolved

#### Scenario: Configuration binds AgentsOptions
- **GIVEN** configuration section `Agents` contains settings
- **WHEN** `AddGmsdAgents` is called
- **THEN** `IOptions<AgentsOptions>` is registered and bound to the configuration section

### Requirement: AgentsOptions defines Plane configuration
The system SHALL provide `AgentsOptions` as the configuration class for Plane-specific settings.

`AgentsOptions` MUST include:
- `string DefaultLlmModel` — fallback model when not specified per-request
- `int MaxConcurrentRuns` — maximum parallel runs allowed (default: 5)
- `bool EnableEvidenceCapture` — global toggle for evidence capture (default: true)

#### Scenario: Options bind from appsettings.json
- **GIVEN** `appsettings.json` contains `Agents:DefaultLlmModel = "gpt-4o"`
- **WHEN** `IOptions<AgentsOptions>` is resolved from DI
- **THEN** `Value.DefaultLlmModel` equals `"gpt-4o"`

### Requirement: Runtime models encapsulate run lifecycle
The system SHALL define runtime models for run initiation, execution context, and results.

`RunRequest` MUST include:
- `string RunId` — unique identifier for the run (assigned by caller if provided, else generated)
- `string? CorrelationId` — optional correlation ID for tracing
- `string Intent` — high-level description of run purpose
- `Dictionary<string, JsonElement> Context` — additional contextual data

`RunResponse` MUST include:
- `string RunId` — the run identifier
- `RunStatus Status` — enum: Pending, Running, Completed, Failed
- `string? ErrorMessage` — populated if Status is Failed
- `DateTimeOffset CompletedAt` — timestamp of completion

`RunContext` MUST include:
- `string RunId` — the run identifier
- `string CorrelationId` — RUN-* formatted correlation ID
- `DateTimeOffset StartedAt` — timestamp of run start
- `CancellationToken CancellationToken` — cancellation token for the run

#### Scenario: RunRequest is created with generated ID
- **GIVEN** no RunId is provided
- **WHEN** `RunRequest.Create()` is called
- **THEN** a new RunId is generated (GUID-based, 22-char base64url)

#### Scenario: RunContext provides correlation ID
- **GIVEN** a run is initiated with RunId "abc123"
- **WHEN** `RunContext` is created for the run
- **THEN** `CorrelationId` equals "RUN-abc123"

### Requirement: Persistence abstractions wrap Engine stores
The system SHALL provide `IRunRepository` as a Plane-level abstraction over Engine stores.

`IRunRepository` MUST specify:
- `Task<RunRecord?> GetAsync(string runId, CancellationToken ct)` — retrieve run by ID
- `Task SaveAsync(RunRecord run, CancellationToken ct)` — persist run record
- `Task<IReadOnlyList<RunRecord>> ListActiveAsync(CancellationToken ct)` — list non-terminal runs

`RunRecord` MUST include:
- `string RunId` — unique identifier
- `RunStatus Status` — current status
- `DateTimeOffset CreatedAt` — creation timestamp
- `DateTimeOffset? CompletedAt` — completion timestamp (null if active)
- `string? ErrorDetails` — error information if failed

#### Scenario: Run is persisted via repository
- **GIVEN** a `RunRecord` with RunId "run-123" and Status Running
- **WHEN** `SaveAsync` is called
- **THEN** the record is persisted via Engine stores (IEvidenceStore, IStateStore as appropriate)
- **AND** `GetAsync("run-123")` returns the saved record

### Requirement: Observability provides correlation ID formatting
The system SHALL provide `ICorrelationIdProvider` for standardized correlation ID generation.

`ICorrelationIdProvider` MUST specify:
- `string Generate(string runId)` — returns correlation ID for a run
- `string Format { get; }` — the format prefix (e.g., "RUN-")

`RunCorrelationIdProvider` implementation MUST:
- Return correlation IDs in format "RUN-{runId}"
- Support runIds up to 64 characters

`AgentsLoggerExtensions` MUST provide:
- `BeginRunScope(this ILogger, string runId, string correlationId)` — adds run context to logs
- Structured logging scope includes `RunId` and `CorrelationId` properties

#### Scenario: Correlation ID is formatted for run
- **GIVEN** runId "abc-def-123"
- **WHEN** `Generate("abc-def-123")` is called
- **THEN** the result is "RUN-abc-def-123"

#### Scenario: Log scope includes correlation ID
- **GIVEN** an `ILogger` and runId "run-456"
- **WHEN** `logger.BeginRunScope("run-456", "RUN-run-456")` is used in a `using` block
- **THEN** all logs within the scope include `RunId` and `CorrelationId` properties
- **AND** rendered logs display the correlation ID in RUN-* format

