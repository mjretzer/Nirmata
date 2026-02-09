# scope-management Specification

## ADDED Requirements

### Requirement: Roadmap Modifier interface contract

The system SHALL provide an `IRoadmapModifier` interface in `Gmsd.Agents.Execution.Planning` that enables safe insertion, removal, and renumbering of roadmap phases.

The interface MUST define:
- `InsertPhaseAsync(InsertPhaseRequest request, CancellationToken ct)` → returns `Task<RoadmapModifyResult>`
- `RemovePhaseAsync(RemovePhaseRequest request, CancellationToken ct)` → returns `Task<RoadmapModifyResult>`
- `RenumberPhasesAsync(RenumberPhasesRequest request, CancellationToken ct)` → returns `Task<RoadmapModifyResult>`

`InsertPhaseRequest` MUST include:
- `TargetPhaseId` (string): The phase ID to insert before/after (e.g., "PH-0002")
- `InsertPosition` (enum): `Before` or `After`
- `PhaseName` (string): Human-readable name for the new phase
- `PhaseDescription` (string): Description of phase objectives
- `Deliverables` (List<string>): Expected deliverables
- `InputArtifacts` (List<string>): Required input artifacts
- `OutputArtifacts` (List<string>): Produced output artifacts

`RemovePhaseRequest` MUST include:
- `PhaseId` (string): The phase ID to remove
- `Force` (bool): When false (default), prevents removal of active phase; when true, allows removal with warning

`RoadmapModifyResult` MUST include:
- `IsSuccess` (bool): Whether the modification succeeded
- `ModifiedRoadmapPath` (string): Path to the modified roadmap.json
- `AffectedPhaseIds` (List<string>): All phase IDs affected by renumbering
- `CreatedIssueId` (string|null): Issue ID if removal was blocked
- `OldCursorPhaseId` (string|null): Previous cursor phase before modification
- `NewCursorPhaseId` (string|null): New cursor phase after modification
- `Error` (string|null): Error message if modification failed

#### Scenario: Insert phase before existing phase

- **GIVEN** a workspace with roadmap containing phases PH-0001, PH-0002, PH-0003
- **WHEN** `InsertPhaseAsync` is called with `TargetPhaseId="PH-0002"`, `InsertPosition=Before`
- **THEN** a new phase is inserted, phases are renumbered to PH-0001, PH-0002 (new), PH-0003 (was PH-0002), PH-0004 (was PH-0003)

#### Scenario: Insert phase after existing phase

- **GIVEN** a workspace with roadmap containing phases PH-0001, PH-0002, PH-0003
- **WHEN** `InsertPhaseAsync` is called with `TargetPhaseId="PH-0002"`, `InsertPosition=After`
- **THEN** a new phase is inserted, phases are renumbered to PH-0001, PH-0002, PH-0003 (new), PH-0004 (was PH-0003)

#### Scenario: Remove phase without force flag

- **GIVEN** a workspace with roadmap containing phases PH-0001, PH-0002, PH-0003 and cursor at PH-0001
- **WHEN** `RemovePhaseAsync` is called with `PhaseId="PH-0002"`, `Force=false`
- **THEN** phase PH-0002 is removed, remaining phases are renumbered to PH-0001, PH-0002 (was PH-0003)

#### Scenario: Remove phase with force flag allows active phase removal

- **GIVEN** a workspace with roadmap containing phases PH-0001, PH-0002, PH-0003 and cursor at PH-0002
- **WHEN** `RemovePhaseAsync` is called with `PhaseId="PH-0002"`, `Force=true`
- **THEN** phase PH-0002 is removed, cursor moves to next phase PH-0003 (renumbered to PH-0002)

### Requirement: Phase Remover safety checks

The system SHALL prevent accidental removal of the currently active phase without explicit force flag.

The safety check MUST:
- Read `state.json` to determine the current cursor phase
- Compare the requested `PhaseId` against `cursor.phaseId`
- If they match and `Force=false`, reject the removal operation
- If they match and `Force=true`, allow removal but emit a warning event
- Never allow removal if it would leave the roadmap with zero phases

#### Scenario: Active phase removal blocked without force flag

- **GIVEN** a workspace with cursor at phase PH-0002
- **WHEN** `RemovePhaseAsync` is called with `PhaseId="PH-0002"`, `Force=false`
- **THEN** the operation fails with `IsSuccess=false`, no state or spec modifications, issue ISS-#### created documenting the blocker

#### Scenario: Cannot remove last remaining phase

- **GIVEN** a workspace with only one phase PH-0001
- **WHEN** `RemovePhaseAsync` is called with `PhaseId="PH-0001"`
- **THEN** the operation fails with `IsSuccess=false` and error message indicating roadmap must have at least one phase

### Requirement: Roadmap Renumberer maintains consistent IDs

The system SHALL provide an `IRoadmapRenumberer` that maintains consistent phase ID sequencing (PH-#### format) after insertions or removals.

The renumberer MUST:
- Accept a roadmap with gaps or non-sequential IDs
- Produce a roadmap with sequential PH-0001, PH-0002, ... ordering
- Preserve all phase metadata (name, description, deliverables, etc.)
- Return a mapping of old IDs to new IDs for cursor update
- Handle up to 9999 phases (PH-0001 through PH-9999)

#### Scenario: Renumberer closes gaps after phase removal

- **GIVEN** a roadmap with phases PH-0001, PH-0002, PH-0004 (PH-0003 was removed)
- **WHEN** `RenumberPhasesAsync` is called
- **THEN** phases are renumbered to PH-0001, PH-0002, PH-0003 with original PH-0004 content now at PH-0003

#### Scenario: Renumberer preserves phase data during renumbering

- **GIVEN** a roadmap where phase PH-0003 has specific deliverables ["item1", "item2"]
- **WHEN** that phase is renumbered to PH-0002 due to prior removal
- **THEN** the deliverables ["item1", "item2"] are preserved at the new PH-0002 ID

### Requirement: Cursor coherence preservation

The system SHALL preserve cursor coherence during all roadmap modifications.

Cursor updates MUST:
- Map the old cursor phase ID to the new ID after renumbering
- If cursor pointed to a removed phase:
  - If removed phase was before cursor: cursor stays at same phase (renumbered)
  - If removed phase was cursor: cursor advances to next phase or previous if no next
  - If removed phase was after cursor: cursor unchanged (renumbering only affects after)
- Write updated cursor to `state.json`
- Append `cursor.updated` event to `events.ndjson`

#### Scenario: Cursor preserved when removing phase before cursor

- **GIVEN** cursor at PH-0003, roadmap has PH-0001, PH-0002, PH-0003, PH-0004
- **WHEN** PH-0001 is removed
- **THEN** phases renumber to PH-0001 (was PH-0002), PH-0002 (was PH-0003), PH-0003 (was PH-0004); cursor updates to PH-0002

#### Scenario: Cursor advances when removing active phase with force

- **GIVEN** cursor at PH-0002, roadmap has PH-0001, PH-0002, PH-0003
- **WHEN** PH-0002 is removed with `Force=true`
- **THEN** phases renumber to PH-0001, PH-0002 (was PH-0003); cursor advances to PH-0002

### Requirement: Issue creation on removal blocker

The system SHALL create an issue document when phase removal is blocked due to active phase protection.

The issue MUST:
- Be written to `.aos/spec/issues/ISS-####.json` with sequential numbering
- Include `blockingPhaseId`: the phase that could not be removed
- Include `reason`: "Active phase removal attempted without force flag"
- Include `suggestedAction`: "Use force flag to remove active phase, or move cursor to different phase"
- Include `createdAt` timestamp

#### Scenario: Blocker issue created on active phase removal attempt

- **GIVEN** cursor at PH-0002, removal attempted without force
- **WHEN** the removal operation fails
- **THEN** `.aos/spec/issues/ISS-0001.json` is created with blockingPhaseId="PH-0002" and appropriate reason

### Requirement: Event capture for modifications

The system SHALL append events to `.aos/state/events.ndjson` for all modification operations.

Events MUST include:
- `roadmap.modified`: emitted on successful insert/remove/renumber
  - `data.operation`: "insert", "remove", or "renumber"
  - `data.affectedPhases`: list of phase IDs affected
  - `data.oldCursor`: cursor phase before operation
  - `data.newCursor`: cursor phase after operation
- `roadmap.blocker`: emitted when removal is blocked
  - `data.blockingPhaseId`: the phase that blocked removal
  - `data.reason`: explanation of why blocked
  - `data.issueId`: reference to created issue

#### Scenario: Modified event written on successful phase insert

- **GIVEN** a successful phase insertion operation
- **WHEN** the operation completes
- **THEN** `events.ndjson` contains a `roadmap.modified` event with operation="insert" and affectedPhases list

#### Scenario: Blocker event written on failed removal

- **GIVEN** an active phase removal attempt without force flag
- **WHEN** the operation is blocked
- **THEN** `events.ndjson` contains a `roadmap.blocker` event with blockingPhaseId and issueId

### Requirement: Roadmap Modifier Handler orchestrator integration

The system SHALL provide a `RoadmapModifierHandler` that integrates with the orchestrator's gating and dispatch system.

The handler MUST:
- Implement the handler pattern used by the orchestrator workflow
- Accept commands for roadmap modification (insert-phase, remove-phase, renumber-phases)
- Delegate to `IRoadmapModifier` methods
- Return handler result indicating success/failure and state changes
- Integrate with `IRunLifecycleManager` for evidence capture

#### Scenario: Handler executes phase insertion command

- **GIVEN** a gating result targeting the RoadmapModifier with command "insert-phase"
- **WHEN** the handler executes
- **THEN** it delegates to `InsertPhaseAsync`, writes all artifacts, and returns success result

#### Scenario: Handler reports modification failures

- **GIVEN** a remove-phase command for active phase without force flag
- **WHEN** the handler executes
- **THEN** it returns failure result with issue reference, no state corruption

### Requirement: Atomic spec and state updates

The system SHALL perform roadmap modifications atomically to prevent partial writes.

Atomic update requirements:
- Write roadmap.json and state.json atomically (both succeed or both fail)
- Use temporary files and atomic rename where supported
- On failure, leave existing files unchanged
- Validate both files against schemas before considering operation successful

#### Scenario: Atomic write prevents partial corruption

- **GIVEN** a modification operation that succeeds writing roadmap.json but would fail writing state.json
- **WHEN** the operation executes
- **THEN** neither file is modified, original files remain intact, operation returns failure
