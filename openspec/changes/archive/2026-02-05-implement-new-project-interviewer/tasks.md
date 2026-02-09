## 1. Core Interview Infrastructure
- [x] 1.1 Create `Gmsd.Agents/Execution/Planning/NewProjectInterviewer/` directory structure
- [x] 1.2 Define `INewProjectInterviewer` interface with `ConductInterviewAsync` method
- [x] 1.3 Define `InterviewSession` model (Questions, Answers, State, ProjectDraft)
- [x] 1.4 Define `InterviewResult` model (Success, ProjectSpec, Transcript, Summary)

## 2. Interview Implementation
- [x] 2.1 Implement `NewProjectInterviewer` class with LLM-driven Q&A loop
- [x] 2.2 Create prompt templates for interview phases (discovery, clarification, confirmation)
- [x] 2.3 Implement `ProjectSpecGenerator` to normalize interview output to `project.json` schema
- [x] 2.4 Add validation that generated spec conforms to `gmsd:aos:schema:project:v1`

## 3. Evidence Capture
- [x] 3.1 Write `interview.transcript.md` to `.aos/evidence/runs/RUN-*/artifacts/`
- [x] 3.2 Write `interview.summary.md` with key decisions and normalized requirements
- [x] 3.3 Integrate with `IInterviewEvidenceWriter` for artifact management
- [x] 3.4 Evidence attached to run through artifact pointers

## 4. Spec Persistence
- [x] 4.1 Integrate with workspace to write `.aos/spec/project.json`
- [x] 4.2 Use deterministic JSON serialization (stable key ordering, LF endings)
- [x] 4.3 Validation passes after interview completion
- [x] 4.4 Handle idempotency: re-running interview updates existing project.json

## 5. Orchestrator Integration
- [x] 5.1 Register `INewProjectInterviewer` in DI container
- [x] 5.2 Wire up Interviewer phase dispatch in orchestrator
- [x] 5.3 Add `InterviewerHandler` that implements the phase handler contract
- [x] 5.4 Gating result context includes interview readiness flags

## 6. Testing
- [x] 6.1 Unit tests: `NewProjectInterviewer` with mocked LLM provider
- [x] 6.2 Unit tests: `ProjectSpecGenerator` normalization logic
- [x] 6.3 Integration test: full interview flow writes valid `project.json`
- [x] 6.4 Integration test: interview evidence attached to run artifacts
- [x] 6.5 Validation test: `validate spec` passes after interview completion

## 7. Verification
- [x] 7.1 Run `openspec validate implement-new-project-interviewer --strict`
- [x] 7.2 Manual test: trigger interview via orchestrator on workspace without project
- [x] 7.3 Verify `.aos/spec/project.json` schema compliance
