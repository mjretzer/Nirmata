## ADDED Requirements

### Requirement: Fix Planner interface exists
The system SHALL provide an `IFixPlanner` interface in `nirmata.Agents.Execution.FixPlanner` that generates fix task plans from UAT failure issues.

The interface MUST define:
- `PlanFixAsync(FixPlannerRequest request, CancellationToken ct)` → returns `Task<FixPlannerResult>`

`FixPlannerRequest` MUST include:
- `IssueIds` (array): List of issue IDs to analyze (ISS-###### format)
- `WorkspaceRoot` (string): Path to `.aos/` workspace root
- `ParentTaskId` (string): The original task ID that failed verification (TSK-###### format)
- `ContextPackId` (string|null): Optional context pack for grounded analysis

`FixPlannerResult` MUST include:
- `IsSuccess` (bool): Whether planning completed successfully
- `FixTaskIds` (array): IDs of created fix tasks (TSK-###### format), limited to 2-3 tasks
- `IssueAnalysis` (array): Per-issue analysis with scope determination and root cause
- `ErrorMessage` (string|null): Error details if planning failed

#### Scenario: Planner generates fix tasks from single issue
- **GIVEN** a single issue ISS-0001 from failed UAT on TSK-0001
- **WHEN** `PlanFixAsync` is called with the issue ID
- **THEN** it returns 1-2 fix task IDs with scoped plans addressing the issue

#### Scenario: Planner generates fix tasks from multiple issues
- **GIVEN** 3 issues (ISS-0001, ISS-0002, ISS-0003) from failed UAT on TSK-0001
- **WHEN** `PlanFixAsync` is called with all issue IDs
- **THEN** it returns 2-3 fix task IDs, consolidating related issues where appropriate

#### Scenario: Planner fails when issues not found
- **GIVEN** a request referencing non-existent issue ISS-9999
- **WHEN** `PlanFixAsync` is called
- **THEN** it returns `IsSuccess: false` with error indicating missing issue

### Requirement: Fix task plans include explicit scope
The system SHALL generate fix task plans with explicit `fileScopes` arrays limiting modifications to affected files.

Each fix task plan MUST:
- Derive `fileScopes` from issue `scope` field and codebase analysis
- Include only files that need modification to resolve the issue(s)
- Reference related files (tests, interfaces) when verification requires them
- Document scope rationale in plan metadata

#### Scenario: Issue with single file scope generates focused plan
- **GIVEN** issue ISS-0001 with `scope: ["src/Services/AuthService.cs"]`
- **WHEN** fix plan is generated
- **THEN** the plan's `fileScopes` contains `src/Services/AuthService.cs` and related test file

#### Scenario: Multiple issues with overlapping scope are consolidated
- **GIVEN** issues ISS-0001 (`scope: ["src/Models/User.cs"]`) and ISS-0002 (`scope: ["src/Models/User.cs", "src/Services/UserService.cs"]`)
- **WHEN** fix plan is generated
- **THEN** a single fix task is created with `fileScopes: ["src/Models/User.cs", "src/Services/UserService.cs"]`

### Requirement: Fix task plans include verification steps
The system SHALL generate fix task plans with explicit `acceptanceCriteria` arrays defining verification checks.

Each fix task plan MUST include:
- `acceptanceCriteria` array with 1-3 checks per task
- Check types from: `file-exists`, `content-contains`, `build-succeeds`, `test-passes`
- Checks that validate the specific fix (not generic validation)
- Expected values that confirm the issue is resolved

#### Scenario: Fix plan includes issue-specific verification
- **GIVEN** issue ISS-0001 about missing validation in `src/Models/Order.cs`
- **WHEN** fix task plan is generated
- **THEN** the plan includes `content-contains` check for validation code pattern and `test-passes` check for validation test

#### Scenario: Fix plan includes build verification for compilation issues
- **GIVEN** issue ISS-0002 about build failure with missing namespace
- **WHEN** fix task plan is generated
- **THEN** the plan includes `build-succeeds` check and `content-contains` check for the namespace declaration

### Requirement: Fix task artifacts written to spec store
The system SHALL write fix task artifacts to `.aos/spec/tasks/<task-id>/` following the spec store schema.

Each fix task MUST have three files:
- `task.json`: Task metadata (id, type, status, createdAt, updatedAt, parentTaskId, issueIds)
- `plan.json`: Execution plan with steps, fileScopes, acceptanceCriteria
- `links.json`: Links to parent task, issues, and related evidence

#### Scenario: Successful planning creates complete task artifacts
- **GIVEN** a successful fix planning for issue ISS-0001
- **WHEN** planning completes
- **THEN** `.aos/spec/tasks/TSK-0002/task.json`, `plan.json`, and `links.json` exist with valid content

#### Scenario: Task metadata references parent task and issues
- **GIVEN** fix task TSK-0002 generated from TSK-0001 failure with issue ISS-0001
- **WHEN** examining `task.json`
- **THEN** it contains `parentTaskId: "TSK-0001"` and `issueIds: ["ISS-0001"]`

### Requirement: State cursor indicates ready-to-execute-fix
The system SHALL update the workspace state cursor to indicate fix tasks are ready for execution.

The state update MUST:
- Set cursor phase to `FixPlannerComplete` or equivalent ready state
- Include references to created fix task IDs in cursor context
- Append a `fix-planned` event to `events.ndjson`
- Preserve existing cursor position for roadmap/phase context

#### Scenario: Planning completion updates cursor for execution
- **GIVEN** fix planning completed with task TSK-0002 created
- **WHEN** examining `.aos/state/state.json`
- **THEN** cursor indicates ready state with reference to TSK-0002

#### Scenario: Events recorded for fix planning lifecycle
- **GIVEN** fix planning for issues ISS-0001, ISS-0002
- **WHEN** planning completes
- **THEN** `events.ndjson` contains events: `fix-planning-started`, `fix-task-created` (per task), `fix-planning-completed`

### Requirement: FixPlannerHandler integrates with orchestrator
The system SHALL provide a `FixPlannerHandler` in `nirmata.Agents.Execution.ControlPlane` that implements the orchestrator handler pattern.

The handler MUST:
- Accept fix planning intent via `HandleAsync` method
- Delegate to `IFixPlanner` for analysis and planning
- Return `HandlerResult` with appropriate next phase routing
- On success: return `NextPhase: "TaskExecutor"` with fix task references
- On failure: return error result with diagnostic information

#### Scenario: Handler routes to TaskExecutor on successful planning
- **GIVEN** a fix planning intent for failed verification with 2 issues
- **WHEN** the FixPlannerHandler completes successfully
- **THEN** the result indicates `NextPhase: "TaskExecutor"` with fix task IDs

#### Scenario: Handler returns error on planning failure
- **GIVEN** a fix planning intent with non-existent issue references
- **WHEN** the FixPlannerHandler fails
- **THEN** the result indicates failure with diagnostic error message

### Requirement: Gating engine routes verification failures to FixPlanner
The system SHALL update `IGatingEngine` to route from `Verifier` phase to `FixPlanner` when verification fails.

The gating logic MUST:
- Check for verification failure state (via state store or evidence)
- When verification failed with issues: return `TargetPhase: "FixPlanner"` with issue references
- Include `ContextData` with issue IDs and parent task ID for the FixPlanner
- Preserve the original task context for fix task parent references

#### Scenario: Gating routes to FixPlanner after failed verification
- **GIVEN** a workspace where UAT verification returned `IsPassed: false` with issues ISS-0001, ISS-0002
- **WHEN** `EvaluateAsync` is called on the gating engine
- **THEN** the result indicates `TargetPhase: FixPlanner` with issue IDs and parent task ID in context

### Requirement: Fix Planner provides deterministic results
The system SHALL ensure fix planning produces deterministic results for identical inputs.

The implementation MUST:
- Use deterministic ordering for issue analysis
- Generate consistent task IDs based on input hash and sequence
- Produce identical fileScopes for identical issues
- Use deterministic JSON serialization for all outputs

#### Scenario: Repeated planning produces identical outputs
- **GIVEN** identical issue set ISS-0001, ISS-0002 analyzed twice
- **WHEN** `PlanFixAsync` is called both times
- **THEN** the same fix task IDs are generated with identical plan content
