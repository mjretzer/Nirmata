# Gmsd.Web

The GMSD Web Interface provides a comprehensive Razor Pages application for managing the spec-driven development lifecycle. It enables users to view and interact with roadmaps, milestones, phases, tasks, UAT verification, and issue triage.

## Overview

Gmsd.Web is the primary user interface for the GMSD platform, providing:

- **Project Dashboard** - Overview of active projects and runs
- **Roadmap Visualization** - Timeline view of milestones and phases
- **Milestone Management** - Track progress through major deliverables
- **Phase Planning** - Define goals, assumptions, and research
- **Task Execution** - Execute plans and track verification
- **UAT Verification** - Verify work against acceptance criteria
- **Issue Tracking** - Triage and route issues to fix plans

## Pages

### Roadmap (`/Roadmap`)
Timeline visualization of milestones and phases with:
- Add/Insert/Remove phase controls
- "Discuss phase" and "Plan phase" entry points
- Alignment warnings between roadmap and state cursor

### Milestones (`/Milestones`)
List and detail views for milestones:
- Phase listings within each milestone
- Status tracking and completion gates
- "New milestone" and "Complete current" actions

### Phases (`/Phases`)
Phase detail view with tabbed interface:
- **Overview** - Goals, outcomes, and status
- **Assumptions** - List and manage phase assumptions
- **Research** - Track research topics and findings
- **Tasks** - View tasks belonging to the phase

Actions:
- `POST /Phases/Details/{id}?handler=AddAssumption` - Add assumption to phase
- `POST /Phases/Details/{id}?handler=SetResearch` - Set research topic
- `POST /Phases/Details/{id}?handler=PlanPhase` - Generate tasks via orchestrator

### Tasks (`/Tasks`)
Task list and detail views:
- Filter by phase, milestone, or status
- Tabbed detail view: task.json, plan.json, uat.json, links.json
- Execute plans through the agent orchestrator

Actions:
- `POST /Tasks/Details/{id}?handler=ExecutePlan` - Execute task via WorkflowClassifier
- `POST /Tasks/Details/{id}?handler=MarkStatus` - Update task status

### UAT (`/Uat`)
User Acceptance Testing verification:
- "Verify work" wizard building checklists from acceptance criteria
- Pass/fail recording with reproduction notes
- Issue emission on failed verification
- Links between UAT sessions, issues, and run evidence

Actions:
- `POST /Uat/Verify?handler=PassCheck` - Mark check as passed
- `POST /Uat/Verify?handler=FailCheck` - Mark check as failed, create issue
- `POST /Uat/Verify?handler=CompleteVerification` - Complete UAT session
- `POST /Uat/Verify?handler=RestartVerification` - Restart verification

### Issues (`/Issues`)
Issue list and detail views:
- Filter by status, type, severity, task, phase, or milestone
- Detail view with reproduction steps, expected vs actual behavior
- Route to fix plans via orchestrator

Actions:
- `POST /Issues/Details/{issueId}?handler=RouteToFixPlan` - Create fix plan via WorkflowClassifier
- `POST /Issues/Details/{issueId}?handler=MarkStatus` - Update issue status

### Runs (`/Runs`)
Existing runs dashboard with:
- List of all runs with status
- Detail view with commands, logs, artifacts
- Commit tracking for each run

## Architecture

### Backend Service Integration

Pages integrate with backend services via `WorkflowClassifier`:

```csharp
private readonly WorkflowClassifier _agentRunner;

public async Task<IActionResult> OnPostExecutePlanAsync(string id)
{
    var result = await _agentRunner.ExecuteAsync($"run execute --task-id {id}");
    // Handle result...
}
```

Registered services:
- `IOrchestrator` - Main workflow orchestration
- `ITaskExecutor` - Task execution
- `IUatVerifier` - UAT verification
- `IPhasePlanner` - Phase planning
- `IRunRepository` - Run persistence
- `IStateStore` - State management

### Spec Artifact Storage

Pages read from and write to the AOS spec store:

- `.aos/spec/roadmap.json` - Roadmap data
- `.aos/spec/milestones/` - Milestone files
- `.aos/spec/phases/` - Phase files
- `.aos/spec/tasks/` - Task directories
- `.aos/spec/uat/` - UAT session files
- `.aos/spec/issues/` - Issue files
- `.aos/state/state.json` - State and status tracking
- `.aos/evidence/runs/` - Run evidence

### Workspace Configuration

The web app uses workspace configuration stored at:
`%LocalAppData%/Gmsd/workspace-config.json`

```json
{
  "SelectedWorkspacePath": "C:/Projects/MyProject"
}
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=sqllitedb/gmsd.db"
  },
  "GmsdAgents": {
    "WorkspacePath": "C:/Gmsd/Workspace"
  }
}
```

### Service Registration

```csharp
// Program.cs
builder.Services.AddGmsdAgents(builder.Configuration);
builder.Services.AddGmsdServices();
```

## Development

### Running the Application

```bash
cd Gmsd.Web
dotnet run
```

### Running Tests

```bash
dotnet test tests/Gmsd.Web.Tests
```

## Dependencies

- `Gmsd.Agents` - Agent orchestration and execution
- `Gmsd.Aos` - AOS (Agent Operating System) spec and state management
- `Gmsd.Data` - Database context and repositories
- `Gmsd.Services` - Business services

## UI Workflow

The UI supports the complete spec-driven development workflow:

### 1. Roadmap
**Path:** `/Roadmap`

The roadmap provides a timeline visualization of the entire project lifecycle. Users can:
- View milestones and phases in chronological order
- Add, insert, or remove phases
- Click "Discuss phase" to open phase detail with notes
- Click "Plan phase" to trigger task generation
- See alignment warnings between roadmap and state cursor

**Data Source:** `.aos/spec/roadmap.json`

### 2. Milestones
**Path:** `/Milestones`

Milestones represent major deliverables in the project. Users can:
- View list of all milestones with status indicators
- Drill into milestone details to see associated phases
- Create new milestones
- Mark milestones as complete (with validation gates)

**Data Source:** `.aos/spec/milestones/`

### 3. Phases
**Path:** `/Phases`

Phases define the work within each milestone. The detail view has tabs for:
- **Overview:** Goals, outcomes, current status
- **Assumptions:** List assumptions that affect the phase
- **Research:** Track research topics and findings
- **Notes:** Discussion notes for the phase

**Actions:**
- Add assumptions (persisted to state)
- Set research topics (persisted to state)
- Plan phase (generates 2-3 atomic tasks via orchestrator)

**Data Source:** `.aos/spec/phases/`, `.aos/state/state.json`

### 4. Tasks
**Path:** `/Tasks`

Tasks are atomic work units generated from phase planning. Users can:
- Filter tasks by phase, milestone, or status
- View task detail with tabs for task.json, plan.json, uat.json, links.json
- Execute plans through the agent orchestrator
- View evidence from latest run
- Manually mark task status

**Actions:**
- Execute plan (triggers task execution via WorkflowClassifier)
- View evidence (links to run evidence)
- Mark status (manual status updates)

**Data Source:** `.aos/spec/tasks/`

### 5. Runs
**Path:** `/Runs`

The Runs page (existing) tracks execution evidence:
- List all runs with status and timestamps
- View run details with commands executed
- Browse logs and artifacts
- See files changed and commits made

**Data Source:** `.aos/evidence/runs/`

### 6. UAT (User Acceptance Testing)
**Path:** `/Uat`

UAT verification ensures tasks meet acceptance criteria:
- "Verify work" wizard builds checklist from task acceptance criteria
- Pass/fail each criterion with reproduction notes
- Failed checks automatically emit issues
- Re-run verification against same checks
- Links to related issues and run evidence

**Actions:**
- Pass check (record verification)
- Fail check (create issue automatically)
- Complete verification (mark task as verified)
- Restart verification (reset all checks)

**Data Source:** `.aos/spec/uat/`

### 7. Issues
**Path:** `/Issues`

Issue tracking for failed verifications and bugs:
- Filter by status, type, severity, task, phase, or milestone
- Detail view shows reproduction steps, expected vs actual behavior
- Route to fix plan (creates fix task via orchestrator)
- Mark resolved or deferred with resolution notes

**Actions:**
- Route to fix plan (creates fix task via WorkflowClassifier)
- Mark resolved/deferred (with resolution notes)

**Data Source:** `.aos/spec/issues/`

### Workflow Flow

```
Roadmap (define structure)
    ↓
Milestones (track deliverables)
    ↓
Phases (plan work)
    ↓
Tasks (execute via agent)
    ↓
Runs (capture evidence)
    ↓
UAT (verify acceptance)
    ↓
Issues (triage failures) → Route to fix plan → Back to Tasks
```

This workflow creates a closed loop where issues found during verification are routed back to the task system for resolution, ensuring complete traceability from roadmap definition through issue resolution.
