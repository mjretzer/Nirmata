## 1. Infrastructure & Shared Components
- [x] 1.1 Create shared navigation links in `_Layout.cshtml` for new pages (Roadmap, Milestones, Phases, Tasks, UAT, Issues)
- [x] 1.2 Create shared models for spec artifact display (MilestoneViewModel, PhaseViewModel, TaskViewModel, IssueViewModel)
- [x] 1.3 Create shared partial views for status badges and empty states
- [x] 1.4 Add CSS styling for timeline, tab interfaces, and wizard flows

## 2. Roadmap Page Implementation
- [x] 2.1 Create `Gmsd.Web/Pages/Roadmap/Index.cshtml` with timeline visualization
- [x] 2.2 Create `Gmsd.Web/Pages/Roadmap/Index.cshtml.cs` with roadmap data loading from `.aos/spec/roadmap.json`
- [x] 2.3 Implement Add/Insert/Remove phase controls with validation
- [x] 2.4 Implement "Discuss phase" button (opens phase detail with notes)
- [x] 2.5 Implement "Plan phase" button (triggers task generation)
- [x] 2.6 Implement roadmap ↔ state cursor alignment warnings display
- [x] 2.7 Add integration tests for roadmap page rendering

## 3. Milestones Page Implementation
- [x] 3.1 Create `Gmsd.Web/Pages/Milestones/Index.cshtml` with milestone list table
- [x] 3.2 Create `Gmsd.Web/Pages/Milestones/Index.cshtml.cs` loading from `.aos/spec/milestones/`
- [x] 3.3 Create `Gmsd.Web/Pages/Milestones/Details.cshtml` with phase listing
- [x] 3.4 Create `Gmsd.Web/Pages/Milestones/Details.cshtml.cs` with milestone detail loading
- [x] 3.5 Implement "New milestone" action
- [x] 3.6 Implement "Complete current" action with completion gate validation
- [x] 3.7 Add status indicators for each milestone

## 4. Phases Page Implementation
- [x] 4.1 Create `Gmsd.Web/Pages/Phases/Index.cshtml` with phase list
- [x] 4.2 Create `Gmsd.Web/Pages/Phases/Index.cshtml.cs` loading from `.aos/spec/phases/`
- [x] 4.3 Create `Gmsd.Web/Pages/Phases/Details.cshtml` with tabbed interface
- [x] 4.4 Create `Gmsd.Web/Pages/Phases/Details.cshtml.cs` with goals/outcomes, assumptions, research
- [x] 4.5 Implement "List assumptions" action that persists to spec
- [x] 4.6 Implement "Set research" action that persists to spec
- [x] 4.7 Implement "Plan phase" action that generates 2-3 atomic tasks + plans
- [x] 4.8 Display phase constraints pulled from state decisions/blockers

## 5. Tasks Page Implementation
- [x] 5.1 Create `Gmsd.Web/Pages/Tasks/Index.cshtml` with filtered task list
- [x] 5.2 Create `Gmsd.Web/Pages/Tasks/Index.cshtml.cs` with filtering by phase/milestone/status
- [x] 5.3 Create `Gmsd.Web/Pages/Tasks/Details.cshtml` with tabbed detail view
- [x] 5.4 Create `Gmsd.Web/Pages/Tasks/Details.cshtml.cs` loading task.json, plan.json, uat.json, links.json
- [x] 5.5 Implement "Execute plan" action that triggers task execution
- [x] 5.6 Implement "View evidence" action linking to latest run evidence
- [x] 5.7 Implement "Mark status" action for manual status updates

## 6. UAT Page Implementation
- [x] 6.1 Create `Gmsd.Web/Pages/Uat/Index.cshtml` with UAT list
- [x] 6.2 Create `Gmsd.Web/Pages/Uat/Index.cshtml.cs` loading from `.aos/spec/uat/`
- [x] 6.3 Create `Gmsd.Web/Pages/Uat/Verify.cshtml` with "Verify work" wizard
- [x] 6.4 Create `Gmsd.Web/Pages/Uat/Verify.cshtml.cs` building checklist from task acceptance criteria
- [x] 6.5 Implement pass/fail recording with repro notes
- [x] 6.6 Implement issue emission on failed verification
- [x] 6.7 Implement re-run verification against same checks
- [x] 6.8 Add links: UAT ↔ issues ↔ runs evidence

## 7. Issues Page Implementation
- [x] 7.1 Create `Gmsd.Web/Pages/Issues/Index.cshtml` with filtered issue list
- [x] 7.2 Create `Gmsd.Web/Pages/Issues/Index.cshtml.cs` with filtering by status/type/severity/task/phase/milestone
- [x] 7.3 Create `Gmsd.Web/Pages/Issues/Details.cshtml` with repro steps, expected vs actual
- [x] 7.4 Create `Gmsd.Web/Pages/Issues/Details.cshtml.cs` loading issue details
- [x] 7.5 Implement "Route to fix plan" action
- [x] 7.6 Implement "Mark resolved/deferred" actions
- [x] 7.7 Implement linking to task/phase/milestone

## 8. Integration & Testing
- [x] 8.1 Wire up navigation between all new pages
- [x] 8.2 Add integration tests for each page controller
- [x] 8.3 Add E2E tests for critical user flows (roadmap → phase → task → UAT → issue)
- [x] 8.4 Verify all pages render spec artifacts correctly
- [x] 8.5 Verify actions are wired to backend services

## 9. Documentation
- [x] 9.1 Update `Gmsd.Web` README with new page documentation
- [x] 9.2 Document the UI workflow: Roadmap → Milestones → Phases → Tasks → Runs → UAT → Issues
