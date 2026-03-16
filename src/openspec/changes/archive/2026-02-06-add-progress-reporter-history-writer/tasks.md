# Tasks: Add Progress Reporter and History Writer Workflows

## 1. Design and Scaffolding
- [x] 1.1 Define progress report output contract (cursor, blockers, next command)
- [x] 1.2 Define history entry schema (RUN/TSK key, timestamp, verification proof, commit hash, evidence pointers)
- [x] 1.3 Create `nirmata.Agents/Execution/Continuity/ProgressReporter/` directory structure
- [x] 1.4 Create `nirmata.Agents/Execution/Continuity/HistoryWriter/` directory structure
- [x] 1.5 Define `IProgressReporter` interface with `ReportAsync()` method
- [x] 1.6 Define `IHistoryWriter` interface with `AppendAsync()` method

## 2. Core Progress Reporter Implementation
- [x] 2.1 Implement `ProgressReporter` with state reading logic
- [x] 2.2 Integrate with `IStateStore` to read cursor position and roadmap state
- [x] 2.3 Implement blocker detection from current state and task evidence
- [x] 2.4 Implement next command recommendation logic
- [x] 2.5 Add deterministic JSON output formatting for progress reports

## 3. Core History Writer Implementation
- [x] 3.1 Implement `HistoryWriter` with evidence reading logic
- [x] 3.2 Integrate with `IEvidenceStore` to locate task evidence artifacts
- [x] 3.3 Implement summary.md file append logic with safe concurrent access
- [x] 3.4 Implement evidence pointer generation (links to `.aos/evidence/runs/RUN-*/summary.json`)
- [x] 3.5 Add commit hash capture when available from git context

## 4. Command Handlers
- [x] 4.1 Implement `report-progress` command handler
- [x] 4.2 Implement `write-history` command handler
- [x] 4.3 Wire handlers into command routing infrastructure
- [x] 4.4 Add command result contracts for progress and history outputs

## 5. Integration and Testing
- [x] 5.1 Unit tests for `ProgressReporter` state reading and report generation
- [x] 5.2 Unit tests for `HistoryWriter` evidence linking and summary append
- [x] 5.3 Integration tests for `report-progress` → deterministic output
- [x] 5.4 Integration tests for `write-history` → summary.md append with evidence links
- [x] 5.5 Verify progress output matches state/roadmap deterministically

## 6. Validation
- [x] 6.1 Run `openspec validate add-progress-reporter-history-writer --strict`
- [x] 6.2 Manual test: report-progress outputs valid cursor and next command
- [x] 6.3 Manual test: write-history appends entry with evidence pointers
- [x] 6.4 Verify summary entries include commit hashes when available
