# Tasks: Add Pause/Resume Manager Workflow

## 1. Design and Scaffolding
- [x] 1.1 Define handoff state schema (cursor, task context, scope, next command)
- [x] 1.2 Define `IPauseResumeManager` interface with PauseAsync/ResumeAsync methods
- [x] 1.3 Create `nirmata.Agents/Execution/Continuity/` directory structure
- [x] 1.4 Define handoff.json schema contract in `nirmata.Aos/Contracts/State/`

## 2. Core Pause/Resume Implementation
- [x] 2.1 Implement `PauseResumeManager` with handoff capture logic
- [x] 2.2 Implement handoff state serialization to `.aos/state/handoff.json`
- [x] 2.3 Implement resume state reconstruction from handoff snapshot
- [x] 2.4 Integrate with `IRunManager` to capture run context during pause

## 3. Command Handlers
- [x] 3.1 Implement `pause-work` command handler
- [x] 3.2 Implement `resume-work` command handler
- [x] 3.3 Implement `resume-task` by RUN ID command handler
- [x] 3.4 Wire handlers into command routing infrastructure

## 4. Integration and Testing
- [x] 4.1 Unit tests for `PauseResumeManager` pause/resume logic
- [x] 4.2 Integration tests for full pause-work → resume-work cycle
- [x] 4.3 Integration tests for resume-task by RUN ID
- [x] 4.4 Verify handoff.json schema compliance via validation tests

## 5. Validation
- [x] 5.1 Run `openspec validate add-pause-resume-manager --strict`
- [x] 5.2 Manual test: pause mid-task, verify handoff.json contents
- [x] 5.3 Manual test: resume from handoff, verify deterministic continuation
- [x] 5.4 Manual test: resume-task by RUN ID, verify scope constraints preserved
