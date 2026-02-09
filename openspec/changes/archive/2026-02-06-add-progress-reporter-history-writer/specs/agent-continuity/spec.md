## ADDED Requirements

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
