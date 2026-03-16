# agents-uat-verifier Specification

## Purpose

Defines orchestration-plane workflow semantics for $capabilityId.

- **Lives in:** `nirmata.Agents/*`
- **Owns:** Control-plane routing/gating and workflow orchestration for this capability
- **Does not own:** Engine contract storage/serialization and product domain behavior
## Requirements
### Requirement: UAT Verifier interface exists
The system SHALL provide an `IUatVerifier` interface in `nirmata.Agents.Execution.Verification.UatVerifier` that executes UAT verification against acceptance criteria.

The interface MUST define:
- `VerifyAsync(UatVerificationRequest request, CancellationToken ct)` → returns `Task<UatVerificationResult>`

`UatVerificationRequest` MUST include:
- `TaskId` (string): The task being verified (TSK-###### format)
- `RunId` (string): The run ID containing execution evidence
- `AcceptanceCriteria` (array): List of criteria to verify
- `FileScopes` (array): Allowed file scopes for verification

`UatVerificationResult` MUST include:
- `IsPassed` (bool): True if ALL acceptance criteria pass
- `RunId` (string): The run ID that was verified
- `TaskId` (string): The task ID that was verified
- `Checks` (array): Individual check results with status, message, evidence
- `IssuesCreated` (array): IDs of issues created for failed checks

#### Scenario: Verifier passes when all criteria pass
- **GIVEN** a task TSK-0001 with acceptance criteria that all pass
- **WHEN** `VerifyAsync` is called with the task and run IDs
- **THEN** the result `IsPassed` is true, `Checks` shows all passed

#### Scenario: Verifier fails when any criterion fails
- **GIVEN** a task TSK-0002 with one failing acceptance criterion
- **WHEN** `VerifyAsync` is called
- **THEN** the result `IsPassed` is false, failed check has error details

### Requirement: UAT supports multiple check types
The system SHALL support acceptance criteria check types: `file-exists`, `content-contains`, `build-succeeds`, `test-passes`.

Each check type MUST have:
- `type` (string): One of the four types above
- `target` (string): File path, pattern, or test identifier
- `expected` (object): Type-specific expected values
- `description` (string): Human-readable explanation

#### Scenario: file-exists check passes when file exists
- **GIVEN** a `file-exists` check for `src/Models/User.cs`
- **AND** the file exists in the execution scope
- **WHEN** the check is evaluated
- **THEN** the check result status is `passed`

#### Scenario: content-contains check passes when pattern found
- **GIVEN** a `content-contains` check for file `src/Services/Auth.cs` with pattern `public class AuthService`
- **AND** the file contains the pattern
- **WHEN** the check is evaluated
- **THEN** the check result status is `passed`

#### Scenario: build-succeeds check validates solution builds
- **GIVEN** a `build-succeeds` check with no target (implies solution root)
- **WHEN** the check is evaluated
- **THEN** it runs `dotnet build` and passes if exit code is 0

#### Scenario: test-passes check validates specific test
- **GIVEN** a `test-passes` check with target `MyProject.Tests.Unit.UserServiceTests.CreateUser_Succeeds`
- **WHEN** the check is evaluated
- **THEN** it runs `dotnet test --filter` for that test and passes if it succeeds

### Requirement: UAT artifacts written to evidence store
The system SHALL write UAT results to `.aos/evidence/runs/<run-id>/artifacts/uat-results.json`.

The artifact MUST use deterministic JSON serialization and include:
- `schemaVersion` (integer): Currently 1
- `runId` (string): The verified run ID
- `taskId` (string): The verified task ID
- `status` (string): "passed" or "failed"
- `timestamp` (string): ISO-8601 timestamp
- `checks` (array): Each check with `type`, `target`, `status`, `message`, `durationMs`

#### Scenario: Successful verification writes uat-results.json
- **GIVEN** a verification that passes all checks
- **WHEN** verification completes
- **THEN** `.aos/evidence/runs/RUN-*/artifacts/uat-results.json` exists with status "passed"

#### Scenario: Failed verification captures failure details
- **GIVEN** a verification with 2 passing and 1 failing check
- **WHEN** verification completes
- **THEN** `uat-results.json` has status "failed" and detailed failure information

### Requirement: UAT spec artifacts track acceptance definitions
The system SHALL write UAT specifications to `.aos/spec/uat/UAT-{taskId}.json` when UAT is defined.

The UAT spec artifact MUST include:
- `schemaVersion` (integer): Currently 1
- `taskId` (string): The task ID this UAT belongs to
- `acceptanceCriteria` (array): The defined checks
- `createdAt` (string): ISO-8601 timestamp
- `updatedAt` (string): ISO-8601 timestamp

#### Scenario: UAT definition persisted for task
- **GIVEN** a task TSK-0003 with acceptance criteria
- **WHEN** the UAT is created/updated
- **THEN** `.aos/spec/uat/UAT-TSK-0003.json` exists with the criteria

### Requirement: Failed checks create structured issues
The system SHALL create issue artifacts under `.aos/spec/issues/ISS-{n}.json` for each failed acceptance criterion.

Each issue MUST include:
- `schemaVersion` (integer): Currently 1
- `id` (string): Issue ID in ISS-##### format
- `parentUatId` (string): Reference to UAT-{taskId}
- `scope` (array): Files/areas affected
- `severity` (string): "error", "warning", or "info"
- `repro` (string): Steps to reproduce the failure
- `expected` (string): Expected behavior
- `actual` (string): Actual observed behavior
- `createdAt` (string): ISO-8601 timestamp
- `status` (string): "open" initially

#### Scenario: Failed file-exists check creates issue
- **GIVEN** a `file-exists` check for `src/Models/Order.cs` that fails
- **WHEN** verification completes
- **THEN** an issue ISS-{n} is created with scope `["src/Models/Order.cs"]`, repro details, and "open" status

#### Scenario: Failed build creates issue with compilation details
- **GIVEN** a `build-succeeds` check that fails
- **WHEN** verification completes
- **THEN** an issue is created with scope `["solution"]` and actual containing build error summary

### Requirement: VerifierHandler integrates with orchestrator
The system SHALL provide a `VerifierHandler` in `nirmata.Agents.Execution.ControlPlane` that implements the orchestrator handler pattern.

The handler MUST:
- Accept verification intent via `HandleAsync` method
- Delegate to `IUatVerifier` for verification work
- Return `HandlerResult` with appropriate next phase routing
- On success: return `NextPhase: null` (or next task identifier)
- On failure: return `NextPhase: "FixPlanner"` with issue references

#### Scenario: Handler routes to FixPlanner on verification failure
- **GIVEN** a verification that fails with 2 issues created
- **WHEN** the VerifierHandler completes
- **THEN** the result indicates `NextPhase: "FixPlanner"` and includes issue IDs

#### Scenario: Handler completes on verification success
- **GIVEN** a verification that passes all checks
- **WHEN** the VerifierHandler completes
- **THEN** the result indicates success with no next phase (or reference to next task)

### Requirement: Gating engine routes verification failures to FixPlanner
The system SHALL update `IGatingEngine` to route from `Verifier` phase to `FixPlanner` when verification fails.

The gating logic MUST:
- Check for verification failure state (via state store or evidence)
- When verification failed: return `TargetPhase: "FixPlanner"` with reason "Verification failed, fix planning required"
- Include `ContextData` with issue references for the FixPlanner

#### Scenario: Gating routes to FixPlanner after failed verification
- **GIVEN** a workspace where UAT verification returned `IsPassed: false`
- **WHEN** `EvaluateAsync` is called on the gating engine
- **THEN** the result indicates `TargetPhase: FixPlanner` with issue references in context

