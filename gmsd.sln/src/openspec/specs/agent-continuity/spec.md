# agent-continuity Specification

## Purpose

Defines orchestration-plane workflow semantics for $capabilityId.

- **Lives in:** `Gmsd.Agents/*`
- **Owns:** Continuity and run/workflow coordination semantics for this capability
- **Does not own:** Engine contract storage/serialization and product domain behavior
## Requirements
### Requirement: Handoff state capture
The system SHALL provide the ability to capture a complete handoff snapshot during workflow execution that preserves all state necessary for deterministic resumption.

The handoff state SHALL include:
- Current cursor position (phase, milestone, task IDs)
- In-flight task context (task ID, partial results, execution packet)
- Scope constraints (file boundaries, edit restrictions)
- Next pending command with arguments
- Source run ID for provenance tracking

#### Scenario: Pause captures complete execution context
- **GIVEN** an active workflow execution with task TSK-0001 in progress
- **WHEN** `PauseResumeManager.PauseAsync()` is invoked
- **THEN** `.aos/state/handoff.json` is created with cursor, task context, scope, and next command
- **AND** the file uses deterministic JSON serialization (stable key ordering, LF endings)

#### Scenario: Handoff includes scope preservation
- **GIVEN** an execution with scope constraints (e.g., file whitelist: ["src/utils.ts"])
- **WHEN** pause is triggered
- **THEN** handoff.json contains the exact scope constraints
- **AND** resumed execution respects the same scope boundaries

### Requirement: pause-work command
The system SHALL provide a `pause-work` command that creates an interruption-safe handoff snapshot.

The command SHALL:
- Accept optional reason/message for the pause
- Capture current cursor position from `IStateStore`
- Capture in-flight task context from active run
- Write handoff.json to `.aos/state/handoff.json`
- Return handoff metadata including timestamp and captured run ID

#### Scenario: pause-work during active task
- **GIVEN** workflow is executing task TSK-0001 in phase "Implementation"
- **WHEN** `pause-work` command is invoked with reason "user interruption"
- **THEN** handoff.json captures task ID, phase cursor, and scope
- **AND** handoff.json contains `"reason": "user interruption"`

#### Scenario: pause-work with no active run
- **GIVEN** no active workflow execution
- **WHEN** `pause-work` command is invoked
- **THEN** return error indicating no active execution to pause
- **AND** do not create or modify handoff.json

### Requirement: resume-work command
The system SHALL provide a `resume-work` command that reconstructs execution state from the handoff snapshot.

The command SHALL:
- Read `.aos/state/handoff.json`
- Validate handoff schema and freshness (optional TTL check)
- Restore cursor position to `IStateStore`
- Reconstruct execution packet from handoff context
- Resume workflow from the captured next command
- Return resumed run ID and status

#### Scenario: resume-work reconstructs deterministic continuation
- **GIVEN** a valid `.aos/state/handoff.json` from a paused task TSK-0001
- **WHEN** `resume-work` command is invoked
- **THEN** cursor is restored to the saved position
- **AND** the next action is the captured command from handoff
- **AND** resumed task respects the same scope constraints as the original

#### Scenario: resume-work with missing handoff
- **GIVEN** no `.aos/state/handoff.json` exists
- **WHEN** `resume-work` command is invoked
- **THEN** return error indicating no handoff available to resume from
- **AND** suggest creating a new run instead

### Requirement: resume-task by RUN ID
The system SHALL provide a `resume-task` command that can restore execution state from any historical RUN evidence folder.

The command SHALL:
- Accept RUN ID as argument (e.g., `resume-task --run-id RUN-a1b2c3d4`)
- Locate evidence folder at `.aos/evidence/runs/<run-id>/`
- Read `summary.json` and `packet.json` to reconstruct execution context
- Restore applicable state (cursor, scope, task context)
- Start a new continuation run linked to the source run ID

#### Scenario: resume-task locates historical evidence
- **GIVEN** an archived run at `.aos/evidence/runs/RUN-a1b2c3d4/` with task evidence
- **WHEN** `resume-task --run-id RUN-a1b2c3d4` is invoked
- **THEN** the run folder is located and summary.json is parsed
- **AND** execution packet is reconstructed from artifacts/packet.json
- **AND** a new run is started with provenance link to RUN-a1b2c3d4

#### Scenario: resume-task with non-existent RUN ID
- **GIVEN** no evidence folder exists for RUN-xyz123
- **WHEN** `resume-task --run-id RUN-xyz123` is invoked
- **THEN** return error indicating run not found
- **AND** list available runs from `.aos/evidence/runs/index.json`

### Requirement: Deterministic resume semantics
The system SHALL guarantee that resumed execution produces deterministic continuation from the pause point.

Resumed execution SHALL:
- Begin with the exact next command captured at pause time
- Operate on identical scope constraints as original execution
- Produce equivalent outputs (modulo timestamps, run IDs) given same inputs
- Not repeat already-completed work from the original run

#### Scenario: Resume is idempotent to original execution
- **GIVEN** a workflow paused at command C after completing commands A and B
- **WHEN** resumed and allowed to complete
- **THEN** final state matches what would have occurred without pause
- **AND** commands A and B effects are not duplicated

### Requirement: Handoff JSON schema validation
The system SHALL validate handoff.json against a defined schema with embedded schema version.

The schema SHALL include:
- `schemaVersion` field (current: "1.0")
- `timestamp` ISO8601 timestamp of pause
- `sourceRunId` provenance tracking
- `cursor` object with phase, milestone, task IDs
- `taskContext` with task ID, status, partial results
- `scope` object with constraints
- `nextCommand` object with name and arguments

#### Scenario: Handoff passes schema validation
- **GIVEN** a handoff.json generated by PauseResumeManager
- **WHEN** schema validator processes the file
- **THEN** all required fields are present with valid types
- **AND** schemaVersion matches supported versions

#### Scenario: Invalid handoff fails validation
- **GIVEN** a handoff.json with missing required fields (e.g., no cursor)
- **WHEN** schema validator processes the file
- **THEN** validation errors are returned listing missing/invalid fields
- **AND** resume operation is blocked with clear error message

### Requirement: Progress Reporter deterministic output
The system SHALL provide a `ProgressReporter` that generates deterministic progress reports from current execution state.

The progress report SHALL include:
- Current cursor position (phase ID, milestone ID, task ID)
- List of active blockers (if any) with severity and description
- Next recommended command with arguments
- Timestamp of report generation
- Source run ID (if active run exists)

#### Scenario: Report progress during active task
- **GIVEN** workflow is executing task TSK-0001 in phase "Implementation" milestone "M2"
- **WHEN** `report-progress` command is invoked
- **THEN** progress output contains cursor {phase: "Implementation", milestone: "M2", task: "TSK-0001"}
- **AND** blockers list is empty or populated based on current state
- **AND** next recommended command reflects the pending action

#### Scenario: Report progress matches state deterministically
- **GIVEN** identical state in `.aos/state/` and roadmap in `.aos/spec/`
- **WHEN** `report-progress` is invoked multiple times
- **THEN** each report contains identical cursor position and next command
- **AND** only timestamp varies between invocations

#### Scenario: Report progress with blockers
- **GIVEN** active execution with a verification failure on task TSK-0002
- **WHEN** `report-progress` command is invoked
- **THEN** blockers list contains {task: "TSK-0002", type: "verification-failed", severity: "high"}
- **AND** next recommended command suggests fix-plan or manual review

### Requirement: report-progress command
The system SHALL provide a `report-progress` command that outputs current execution status.

The command SHALL:
- Accept optional format argument (json, markdown; default: json)
- Read cursor position from `IStateStore`
- Read roadmap/tasks from spec store
- Read task evidence to detect blockers
- Return progress report as structured output

#### Scenario: report-progress returns JSON
- **GIVEN** no format argument provided
- **WHEN** `report-progress` command is invoked
- **THEN** output is valid JSON with schemaVersion, cursor, blockers, nextCommand, timestamp

#### Scenario: report-progress with markdown format
- **GIVEN** format argument "markdown"
- **WHEN** `report-progress` command is invoked
- **THEN** output is markdown formatted with headers for Cursor, Blockers, Next Action

#### Scenario: report-progress with no active run
- **GIVEN** no active workflow execution and no cursor in state
- **WHEN** `report-progress` command is invoked
- **THEN** report indicates "no active execution"
- **AND** next recommended command suggests starting a new run

### Requirement: History Writer durable narrative
The system SHALL provide a `HistoryWriter` that appends durable narrative entries to `.aos/spec/summary.md`.

Each history entry SHALL include:
- RUN/TSK key (e.g., "RUN-0001/TSK-0003")
- ISO8601 timestamp of entry creation
- Verification proof (pass/fail status, method used)
- Commit hash (when available from git context)
- Evidence pointers (relative paths to evidence artifacts)
- Optional brief narrative description

#### Scenario: History entry for completed task
- **GIVEN** task TSK-0003 in RUN-0001 completed with verification passed
- **WHEN** `write-history` is invoked for the task
- **THEN** `.aos/spec/summary.md` contains entry with RUN/TSK key
- **AND** entry includes verification proof {status: "passed", method: "uat-verifier"}
- **AND** entry includes evidence pointer to `.aos/evidence/runs/RUN-0001/TSK-0003/summary.json`

#### Scenario: History entry includes commit hash
- **GIVEN** task execution resulted in git commit abc1234
- **WHEN** `write-history` appends entry for the task
- **THEN** entry contains `"commitHash": "abc1234"`
- **AND** commit is verifiable via `git log` in repository root

#### Scenario: History entry for failed verification
- **GIVEN** task TSK-0004 failed UAT verification with 2 issues
- **WHEN** `write-history` appends entry for the task
- **THEN** entry includes verification proof {status: "failed", method: "uat-verifier", issues: 2}
- **AND** evidence pointer links to `.aos/evidence/runs/RUN-0002/TSK-0004/verification.json`

### Requirement: write-history command
The system SHALL provide a `write-history` command that appends a history entry for a completed task or run.

The command SHALL:
- Accept RUN ID and optional TSK ID as arguments
- Locate evidence folder at `.aos/evidence/runs/<run-id>/`
- Read task/run summary and verification results
- Extract commit hash from git context if available
- Append formatted entry to `.aos/spec/summary.md`
- Return confirmation with entry timestamp and evidence pointer

#### Scenario: write-history for specific task
- **GIVEN** completed task TSK-0005 in RUN-0003 with evidence
- **WHEN** `write-history --run RUN-0003 --task TSK-0005` is invoked
- **THEN** entry appended to `.aos/spec/summary.md`
- **AND** entry contains RUN/TSK key "RUN-0003/TSK-0005"
- **AND** evidence pointer links to task evidence folder

#### Scenario: write-history for entire run
- **GIVEN** completed run RUN-0004 with multiple tasks
- **WHEN** `write-history --run RUN-0004` is invoked (no task specified)
- **THEN** entry appended with RUN key only
- **AND** evidence pointer links to `.aos/evidence/runs/RUN-0004/summary.json`
- **AND** entry summarizes overall run outcome

#### Scenario: write-history with missing evidence
- **GIVEN** no evidence exists for RUN-xyz999
- **WHEN** `write-history --run RUN-xyz999` is invoked
- **THEN** return error indicating evidence not found
- **AND** do not modify `.aos/spec/summary.md`

### Requirement: Summary.md concurrent access safety
The system SHALL ensure safe concurrent access when multiple history writes append to `.aos/spec/summary.md`.

The implementation SHALL:
- Use atomic file append operations where available
- Or implement advisory locking via `.aos/cache/history.lock`
- Prevent write corruption from concurrent processes
- Maintain entry ordering by append timestamp

#### Scenario: Concurrent history writes do not corrupt file
- **GIVEN** two runs attempt to write history simultaneously
- **WHEN** both `write-history` commands complete
- **THEN** `.aos/spec/summary.md` contains both entries intact
- **AND** no entry is partially written or interleaved

#### Scenario: Lock recovery after crash
- **GIVEN** previous write acquired lock but process crashed before release
- **WHEN** new `write-history` command is invoked
- **THEN** lock is acquired after reasonable timeout (e.g., 30 seconds)
- **AND** write proceeds safely

### Requirement: Evidence pointer format
The system SHALL format evidence pointers as relative paths from workspace root.

Evidence pointers SHALL:
- Use forward slashes for cross-platform compatibility
- Begin with `.aos/evidence/`
- Point to specific artifact files (summary.json, verification.json, etc.)
- Be resolvable by both human readers and tooling

#### Scenario: Evidence pointer path format
- **GIVEN** task TSK-0006 evidence at `.aos/evidence/runs/RUN-0005/TSK-0006/summary.json`
- **WHEN** history entry is written for the task
- **THEN** evidence pointer contains `".aos/evidence/runs/RUN-0005/TSK-0006/summary.json"`
- **AND** path uses forward slashes on all platforms

### Requirement: Progress Reporter integration with existing stores
The system SHALL integrate `ProgressReporter` with existing AOS stores for state reading.

The integration SHALL:
- Use `IStateStore` to read current cursor position
- Use spec store interfaces to read roadmap and task definitions
- Use `IEvidenceStore` to read task evidence for blocker detection
- Not maintain separate state or caching layers

#### Scenario: Progress reads current state on each invocation
- **GIVEN** cursor position changes between two `report-progress` invocations
- **WHEN** second report is generated
- **THEN** report reflects new cursor position
- **AND** no stale or cached state is used

### Requirement: History Writer integration with evidence store
The system SHALL integrate `HistoryWriter` with `IEvidenceStore` to locate artifacts.

The integration SHALL:
- Accept RUN ID and resolve evidence folder path
- Read task summary.json for outcome and metadata
- Read verification.json for proof details when available
- Handle missing evidence gracefully with clear errors

#### Scenario: History Writer resolves evidence path
- **GIVEN** RUN-0006 exists in evidence store
- **WHEN** `write-history --run RUN-0006` is invoked
- **THEN** `IEvidenceStore` resolves path to `.aos/evidence/runs/RUN-0006/`
- **AND** summary.json is read from resolved path

### Requirement: Pause/resume workflow with explicit state transitions
The system SHALL support explicit pause/resume state transitions for long-running workflows.

Pause/resume behavior MUST:
- Be triggered by explicit commands (`aos run pause`, `aos run resume`)
- Update run state in `.aos/state/run.json` with status transitions
- Preserve execution context (cursor, task evidence, logs) during pause
- Allow resumption from exact pause point
- Be visible to user via `report-progress` and UI

#### Scenario: Run is paused explicitly
- **GIVEN** a running workflow
- **WHEN** `aos run pause --run-id <id>` is executed
- **THEN** the workflow stops accepting new task executions
- **AND** run state is updated with `status: paused` and `pausedAtUtc`
- **AND** execution context is preserved

#### Scenario: Run is resumed from pause point
- **GIVEN** a paused workflow
- **WHEN** `aos run resume --run-id <id>` is executed
- **THEN** the workflow resumes from the exact pause point
- **AND** run state is updated with `status: resumed` and `resumedAtUtc`
- **AND** execution context is intact (cursor, evidence, logs)

#### Scenario: Pause is idempotent
- **GIVEN** a paused workflow
- **WHEN** `aos run pause --run-id <id>` is executed again
- **THEN** the command succeeds (no error)
- **AND** status remains `paused`

#### Scenario: Resume requires paused status
- **GIVEN** a running workflow (status `started`)
- **WHEN** `aos run resume --run-id <id>` is executed
- **THEN** the command fails with error indicating run is not paused

### Requirement: Progress reporting includes run status
The system SHALL include run status in progress reports.

Progress reports MUST include:
- Current run status (started/paused/resumed/finished/abandoned)
- Time in current status
- Pause/resume history (if applicable)
- Estimated time to completion (if available)

#### Scenario: Progress report shows paused status
- **GIVEN** a paused workflow
- **WHEN** `report-progress` is invoked
- **THEN** output includes `"status": "paused"`
- **AND** includes `"pausedAtUtc": "2026-02-20T10:30:00Z"`
- **AND** includes time paused (e.g., "5 minutes")

#### Scenario: Progress report shows resumed status
- **GIVEN** a resumed workflow
- **WHEN** `report-progress` is invoked
- **THEN** output includes `"status": "resumed"`
- **AND** includes `"resumedAtUtc": "2026-02-20T10:35:00Z"`

#### Scenario: Progress report shows abandoned status
- **GIVEN** a workflow marked `abandoned`
- **WHEN** `report-progress` is invoked
- **THEN** output includes `"status": "abandoned"`
- **AND** includes `"abandonedAtUtc"` timestamp
- **AND** suggests recovery steps

### Requirement: UI displays pause/resume controls and status
The system SHALL display pause/resume controls and status in the orchestrator UI.

UI MUST:
- Show current run status prominently (started/paused/resumed/finished/abandoned)
- Provide pause button when run is active (started/resumed)
- Provide resume button when run is paused
- Display status timeline (started → paused → resumed → finished)
- Show pause/resume timestamps
- Provide clear recovery guidance for abandoned runs

#### Scenario: UI shows pause button for active run
- **GIVEN** a running workflow
- **WHEN** the orchestrator page is viewed
- **THEN** a "Pause" button is visible and enabled
- **AND** current status is displayed as "Running"

#### Scenario: UI shows resume button for paused run
- **GIVEN** a paused workflow
- **WHEN** the orchestrator page is viewed
- **THEN** a "Resume" button is visible and enabled
- **AND** current status is displayed as "Paused"
- **AND** pause timestamp is shown

#### Scenario: UI shows abandoned run guidance
- **GIVEN** a workflow marked `abandoned`
- **WHEN** the orchestrator page is viewed
- **THEN** status is displayed as "Abandoned"
- **AND** abandonment reason is shown (timeout exceeded)
- **AND** recovery options are suggested (inspect evidence, retry, cleanup)

### Requirement: Continuity plane respects pause/resume state
The system SHALL ensure the continuity plane respects pause/resume state transitions.

Behavior MUST:
- Not execute new tasks while paused
- Resume execution from cursor on resume command
- Preserve all state (cursor, evidence, logs) during pause
- Log pause/resume events for audit trail

#### Scenario: Task executor respects paused state
- **GIVEN** a workflow is paused
- **WHEN** task executor checks for next task
- **THEN** no new tasks are dequeued or executed
- **AND** executor waits for resume command

#### Scenario: Cursor is preserved during pause
- **GIVEN** a workflow paused at task TSK-0005
- **WHEN** workflow is resumed
- **THEN** cursor remains at TSK-0005
- **AND** next task to execute is TSK-0006

#### Scenario: Pause/resume is logged
- **GIVEN** a workflow transitions: started → paused → resumed
- **WHEN** transitions occur
- **THEN** service logs include entries for each transition
- **AND** logs include timestamps and run ID

