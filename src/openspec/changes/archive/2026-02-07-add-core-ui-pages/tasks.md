## Implementation Tasks

### 1. Workspace Picker Page (`/Workspace`)
- [x] 1.1 Create `Pages/Workspace/Index.cshtml` with path input, browse button, recent workspaces list
- [x] 1.2 Create `Pages/Workspace/Index.cshtml.cs` with workspace path validation and health checks
- [x] 1.3 Implement `.aos` directory initialization logic
- [x] 1.4 Add workspace validation (schemas, locks) with error reporting
- [x] 1.5 Create configuration persistence for selected workspace
- [x] 1.6 Add path traversal protection
- [x] 1.7 Write unit tests for workspace health checks

### 2. Dashboard Page (`/Dashboard`)
- [x] 2.1 Create `Pages/Dashboard/Index.cshtml` with cursor display, blockers, quick actions
- [x] 2.2 Create `Pages/Dashboard/Index.cshtml.cs` with state.json parsing
- [x] 2.3 Implement cursor position display (milestone/phase/task/step)
- [x] 2.4 Add blockers/issues summary from `spec/issues/`
- [x] 2.5 Create quick action handlers (Validate, Checkpoint, Pause, Resume)
- [x] 2.6 Add latest run card with status and evidence links
- [x] 2.7 Implement HTMX polling for live updates
- [x] 2.8 Write integration tests for dashboard data loading

### 3. Command Center Page (`/Command`)
- [x] 3.1 Create `Pages/Command/Index.cshtml` with chat UI layout
- [x] 3.2 Create `Pages/Command/Index.cshtml.cs` with command processing
- [x] 3.3 Implement message thread display (user + system messages)
- [x] 3.4 Add slash command autocomplete (/init, /status, /validate, /spec, /run, /codebase, /pack, /checkpoint)
- [x] 3.5 Create run summary card component
- [x] 3.6 Implement inline JSON/validation report rendering with syntax highlighting
- [x] 3.7 Add safety rails display (scope, touched files)
- [x] 3.8 Implement attachment handling for evidence files
- [x] 3.9 Write tests for command parsing and execution

### 4. Specs Explorer Page (`/Specs`)
- [x] 4.1 Create `Pages/Specs/Index.cshtml` with tree view and search
- [x] 4.2 Create `Pages/Specs/Index.cshtml.cs` with file tree traversal
- [x] 4.3 Implement spec directory tree visualization (project, roadmap, milestones, phases, tasks, issues, uat)
- [x] 4.4 Add search across spec files (filename + content)
- [x] 4.5 Create dual-mode editor (Form + Raw JSON) with toggle
- [x] 4.6 Implement schema validation for JSON specs
- [x] 4.7 Add "Open on disk path" link generation
- [x] 4.8 Create diff viewer component (current vs git history)
- [x] 4.9 Write tests for spec file operations

### 5. Project Spec Page (`/Specs/Project`)
- [x] 5.1 Create `Pages/Specs/Project.cshtml` with form editor for project.json
- [x] 5.2 Create `Pages/Specs/Project.cshtml.cs` with project spec CRUD operations
- [x] 5.3 Implement form fields: Project Name, Description, Constraints, Success Criteria
- [x] 5.4 Add Interview Mode UI with sequential question flow
- [x] 5.5 Implement JSON schema validation with error display
- [x] 5.6 Create import/export functionality (file upload/download)
- [x] 5.7 Add auto-save draft to localStorage
- [x] 5.8 Write tests for project spec validation and persistence

### 6. Navigation & Layout Updates
- [x] 6.1 Update `Pages/Shared/_Layout.cshtml` with navigation links to new pages
- [x] 6.2 Add active state styling for current page
- [x] 6.3 Ensure responsive layout for all new pages

### 7. Infrastructure & Dependencies
- [x] 7.1 Add HTMX library to `wwwroot/lib/` or CDN reference
- [x] 7.2 Add syntax highlighting library (Prism.js or highlight.js) for JSON display
- [x] 7.3 Create shared components: RunCard, ValidationReport, JsonViewer
- [x] 7.4 Ensure AOS file contracts are accessible from `nirmata.Web`

### 8. Validation & Testing
- [x] 8.1 Verify all 5 pages render without errors
- [x] 8.2 Test navigation between pages works correctly
- [x] 8.3 Run `openspec validate add-core-ui-pages --strict` and fix issues
- [x] 8.4 Write E2E tests covering key user flows
