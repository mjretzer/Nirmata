# Implementation Tasks: Add Backlog Capture & Triage Plane

## 1. Workspace Contracts & Schemas
- [x] 1.1 Define `TODO-*.json` schema (id, description, source, capturedAt, priority, status)
- [x] 1.2 Define `ISS-*.json` schema (id, title, description, severity, status, routingDecision)
- [x] 1.3 Define triage/capture event schemas for `.aos/state/events.ndjson`
- [x] 1.4 Add schema constants to `Gmsd.Aos` schema registry

## 2. DeferredIssuesCurator
- [x] 2.1 Create `IDeferredIssuesCurator` interface
- [x] 2.2 Implement issue scanning from `.aos/spec/issues/`
- [x] 2.3 Implement severity/priority assessment logic
- [x] 2.4 Implement routing recommendation (main loop vs defer)
- [x] 2.5 Write triage events to `.aos/state/events.ndjson`
- [x] 2.6 Update `ISS-*.json` with triage decisions
- [x] 2.7 Unit tests: urgent issue routes to main loop, non-urgent stays deferred

## 3. TodoCapturer
- [x] 3.1 Create `ITodoCapturer` interface
- [x] 3.2 Implement TODO capture from execution context
- [x] 3.3 Generate `TODO-*.json` files in `.aos/context/todos/`
- [x] 3.4 Write capture events to `.aos/state/events.ndjson`
- [x] 3.5 Ensure cursor remains unaffected on capture
- [x] 3.6 Unit tests: capture produces valid TODO file, cursor unchanged

## 4. TodoReviewer
- [x] 4.1 Create `ITodoReviewer` interface
- [x] 4.2 Implement TODO listing/selection from `.aos/context/todos/`
- [x] 4.3 Implement promotion to task (creates task spec)
- [x] 4.4 Implement promotion to roadmap phase (inserts phase)
- [x] 4.5 Implement discard/archive path
- [x] 4.6 Write review events to `.aos/state/events.ndjson`
- [x] 4.7 Unit tests: promotion creates task/phase, discard removes from active TODOs

## 5. Handler Integration
- [x] 5.1 Create `DeferredIssuesCuratorHandler` for orchestrator integration
- [x] 5.2 Create `TodoCapturerHandler` for orchestrator integration
- [x] 5.3 Create `TodoReviewerHandler` for orchestrator integration
- [x] 5.4 Register handlers in `Gmsd.Agents` composition root
- [x] 5.5 Integration tests: handlers work with orchestrator gating

## 6. Validation & Tooling
- [x] 6.1 Add workspace validation rules for TODO/issue files
- [x] 6.2 Add CLI command `openspec consider-issues` (triage)
- [x] 6.3 Add CLI command `openspec add-todo` (capture)
- [x] 6.4 Add CLI command `openspec check-todos` (review)
- [x] 6.5 Run `openspec validate --strict` and fix issues
