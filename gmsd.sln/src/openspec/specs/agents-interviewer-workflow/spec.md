# agents-interviewer-workflow Specification

## Purpose

Defines orchestration-plane workflow semantics for $capabilityId.

- **Lives in:** `Gmsd.Agents/*`
- **Owns:** Control-plane routing/gating and workflow orchestration for this capability
- **Does not own:** Engine contract storage/serialization and product domain behavior
## Requirements
### Requirement: New-Project Interviewer interface

The system SHALL provide an `INewProjectInterviewer` interface in `Gmsd.Agents.Execution.Planning.NewProjectInterviewer` that conducts structured interviews to produce a valid project specification.

The interface MUST define:
- `ConductInterviewAsync(InterviewContext context, CancellationToken ct)` â†’ returns `Task<InterviewResult>`

`InterviewContext` MUST carry:
- `RunId` (string): Current run identifier for evidence association
- `WorkspacePath` (string): Absolute path to workspace root
- `UserInput` (string|null): Optional initial user input to seed the interview

`InterviewResult` MUST include:
- `IsSuccess` (bool): Whether the interview produced a valid project spec
- `ProjectSpec` (object|null): The normalized project specification document
- `Transcript` (string): Full interview Q&A transcript in Markdown format
- `Summary` (string): Human-readable summary of key decisions and requirements
- `Error` (string|null): Error message if interview failed

#### Scenario: Interview produces valid project spec

- **GIVEN** a workspace without `.aos/spec/project.json`
- **WHEN** `ConductInterviewAsync` is called with a valid context
- **THEN** the method returns `IsSuccess=true` with a `ProjectSpec` containing `name` and `description`

#### Scenario: Interview fails gracefully on LLM error

- **GIVEN** a workspace where the LLM provider is unavailable
- **WHEN** `ConductInterviewAsync` is called
- **THEN** the method returns `IsSuccess=false` with a descriptive error message

### Requirement: Interview session model tracks Q&A state

The system SHALL provide an `InterviewSession` model that tracks the state of an ongoing interview.

The model MUST include:
- `SessionId` (string): Unique identifier for this interview session
- `Questions` (list): Ordered list of questions asked with timestamps
- `Answers` (list): Ordered list of user/LLM responses with timestamps
- `CurrentPhase` (string): One of: "discovery", "clarification", "confirmation", "completed"
- `DraftSpec` (object|null): Accumulated project specification being built

#### Scenario: Session tracks interview progression

- **GIVEN** an interview session in "discovery" phase with 2 questions answered
- **WHEN** the interviewer moves to "clarification" phase
- **THEN** `CurrentPhase` updates to "clarification" and the phase transition is recorded

### Requirement: Project spec generator normalizes interview output

The system SHALL provide a `ProjectSpecGenerator` that converts interview session data into a valid `project.json` document conforming to schema `gmsd:aos:schema:project:v1`.

The generator MUST:
- Extract `name` from interview answers (required, non-empty string)
- Extract `description` from interview answers (required, non-empty string)
- Set `schemaVersion` to `1`
- Validate output against the project schema before returning

#### Scenario: Generator produces schema-compliant project.json

- **GIVEN** an interview session with answers containing "Project Name" and "Project Description"
- **WHEN** `GenerateAsync` is called
- **THEN** the output validates against `gmsd:aos:schema:project:v1` with the extracted values

#### Scenario: Generator fails on missing required fields

- **GIVEN** an interview session without a clear project name extracted
- **WHEN** `GenerateAsync` is called
- **THEN** the method throws `ValidationFailedException` with details about missing required fields

### Requirement: Interview evidence capture to run artifacts

The system SHALL write interview evidence to `.aos/evidence/runs/RUN-*/artifacts/` with two files:
- `interview.transcript.md`: Full Q&A transcript in chronological order
- `interview.summary.md`: Condensed summary of requirements and decisions

Both files MUST:
- Use UTF-8 encoding with LF line endings
- Be written via `IRunLifecycleManager.AttachArtifactAsync` for proper run association
- Include a frontmatter header with `runId`, `sessionId`, and `timestamp`

#### Scenario: Transcript captures full interview Q&A

- **GIVEN** an interview session with 5 questions and 5 answers
- **WHEN** evidence capture is triggered
- **THEN** `interview.transcript.md` contains all 10 entries in chronological order with timestamps

#### Scenario: Summary captures key decisions

- **GIVEN** an interview that established project name "MyApp" and description "A task tracker"
- **WHEN** evidence capture is triggered
- **THEN** `interview.summary.md` includes these key decisions in a readable format

### Requirement: Interviewer integrates with orchestrator phase dispatch

The system SHALL provide an `InterviewerPhaseHandler` implementing the phase handler contract used by the orchestrator.

The handler MUST:
- Accept gating result with `TargetPhase: Interviewer`
- Delegate to `INewProjectInterviewer.ConductInterviewAsync`
- On success: write `.aos/spec/project.json` via `ISpecStore`
- Return a phase result indicating success/failure and next gating context

#### Scenario: Handler executes interview and writes project.json

- **GIVEN** a gating result with `TargetPhase: Interviewer` and `HasProject: false`
- **WHEN** the handler executes
- **THEN** it conducts the interview and writes `.aos/spec/project.json`

#### Scenario: Handler updates gating context after success

- **GIVEN** a successful interview that produced a valid project spec
- **WHEN** the handler completes
- **THEN** the returned phase result indicates `HasProject: true` for subsequent gating

