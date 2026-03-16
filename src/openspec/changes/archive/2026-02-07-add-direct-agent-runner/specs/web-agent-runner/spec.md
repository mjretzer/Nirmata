## ADDED Requirements

### Requirement: Workflow Classifier in Web Project

The `nirmata.Web` project SHALL provide a `WorkflowClassifier` class that enables in-process agent execution without requiring the Windows Service host.

The implementation MUST:
- Accept `IOrchestrator` via constructor injection
- Provide `ExecuteAsync(string inputRaw, CancellationToken)` method
- Generate correlation IDs for each execution
- Normalize inputs into `WorkflowIntent` for the orchestrator
- Return `OrchestratorResult` with run status and artifacts
- Handle execution exceptions gracefully

#### Scenario: Execute no-op command via WorkflowClassifier
- **GIVEN** the Web application is configured with `AddnirmataAgents()`
- **WHEN** `WorkflowClassifier.ExecuteAsync("status")` is called
- **THEN** the orchestrator executes without spawning external processes
- **AND** a valid `OrchestratorResult` is returned with run ID

#### Scenario: WorkflowClassifier generates correlation IDs
- **GIVEN** a call to `ExecuteAsync()` without explicit correlation ID
- **WHEN** execution begins
- **THEN** a unique correlation ID is generated and passed to the orchestrator
- **AND** the correlation ID appears in run evidence

#### Scenario: WorkflowClassifier handles execution failures
- **GIVEN** an orchestrator that throws an exception
- **WHEN** `ExecuteAsync()` is called
- **THEN** the exception is caught and wrapped in a failed `OrchestratorResult`
- **AND** error details are logged
