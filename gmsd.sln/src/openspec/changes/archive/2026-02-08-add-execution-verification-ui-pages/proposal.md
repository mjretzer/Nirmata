# Change: Add Execution & Verification UI Pages

## Why
The GMSD platform needs a comprehensive web interface for execution tracking, verification management, and issue triage. Currently, the system has basic project and runs pages, but lacks the ability to view and interact with the roadmap, milestones, phases, tasks, UAT verification, and issues that are core to the agent orchestration workflow. This change delivers the UI surface needed for users to monitor and manage the complete spec-driven development lifecycle.

## What Changes

- **New Roadmap Page (`/Roadmap`)**: Timeline visualization of milestones and phases with add/insert/remove controls, "discuss phase" and "plan phase" entry points, and alignment warnings between roadmap and state cursor
- **New Milestones Page (`/Milestones`)**: List and detail views for milestones with phases, status tracking, and completion gates
- **New Phases Page (`/Phases`)**: Phase detail with goals/outcomes, assumptions, research tracking, and task generation
- **New Tasks Page (`/Tasks`)**: Task list with filters, detail view with tabs for task.json/plan.json/uat.json/links.json, execution and verification actions
- **Enhanced Runs Page**: Already exists; will verify it meets spec requirements for run detail with commands, logs, artifacts, and commit tracking
- **New UAT Page (`/Uat`)**: "Verify work" wizard building checklists from acceptance criteria, recording pass/fail with repro notes, linking to issues and runs
- **New Issues Page (`/Issues`)**: Issue list with filtering by status/type/severity, detail view with repro steps, actions to route to fix plan or mark resolved/deferred

## Impact

- **Affected Projects:** `Gmsd.Web`
- **Affected Code Paths:**
  - `Gmsd.Web/Pages/Roadmap/**`
  - `Gmsd.Web/Pages/Milestones/**`
  - `Gmsd.Web/Pages/Phases/**`
  - `Gmsd.Web/Pages/Tasks/**`
  - `Gmsd.Web/Pages/Uat/**`
  - `Gmsd.Web/Pages/Issues/**`
- **New Spec Capabilities:**
  - `web-roadmap-page`
  - `web-milestones-page`
  - `web-phases-page`
  - `web-tasks-page`
  - `web-uat-page`
  - `web-issues-page`
- **Dependencies:**
  - `web-razor-pages` (existing infrastructure)
  - `web-runs-dashboard` (existing runs pages)
  - `aos-spec-store` (for reading spec artifacts)
  - `aos-state-store` (for cursor and status)
  - `agents-roadmapper-workflow` (milestone/phase structure)
  - `scope-management` (insert/remove phases)
  - `phase-planning` (task generation)
  - `agents-task-executor` (task execution)
  - `agents-uat-verifier` (verification)
  - `backlog-triage` (issue management)
