## Implementation Tasks

### 1. Shared Components
- [x] 1.1 Create `JsonViewerModel` in `nirmata.Web/Models/` for structured JSON display
- [x] 1.2 Create `Shared/_JsonViewer.cshtml` partial for reusable JSON formatting with collapsible sections
- [x] 1.3 Update `Shared/_Layout.cshtml` to add navigation links for new pages (Codebase, Context, State, Checkpoints, Validation, Fix)

### 2. Fix Planning Page
- [x] 2.1 Create `Pages/Fix/Index.cshtml` and `Index.cshtml.cs` — list view for repair loops
- [x] 2.2 Create `Pages/Fix/Details.cshtml` and `Details.cshtml.cs` — show fix plan tasks, loop state
- [x] 2.3 Add actions: Plan Fix, Execute Fix, Re-verify with form handlers
- [x] 2.4 Link to related run and issue detail pages

### 3. Codebase Intelligence Page
- [x] 3.1 Create `Pages/Codebase/Index.cshtml` and `Index.cshtml.cs` — main intelligence dashboard
- [x] 3.2 Add trigger buttons: Scan, Map Build, Symbols Build, Graph Build
- [x] 3.3 Create viewer partials or modals for each artifact type (map, stack, architecture, structure, conventions, testing, integrations, concerns)
- [x] 3.4 Display last built timestamp and validation status per artifact
- [x] 3.5 Integrate `ICodebaseReader` or file system access to `.aos/codebase/`

### 4. Context Packs Page
- [x] 4.1 Create `Pages/Context/Index.cshtml` and `Index.cshtml.cs` — list packs by task/phase
- [x] 4.2 Show pack budget size and metadata (created, related task/phase)
- [x] 4.3 Add actions: Build Pack, Show Pack, Diff Pack since RUN
- [x] 4.4 Create pack content viewer with structured display

### 5. State & Events Page
- [x] 5.1 Create `Pages/State/Index.cshtml` and `Index.cshtml.cs` — state.json viewer
- [x] 5.2 Display cursor, decisions, blockers, gating signals from state
- [x] 5.3 Create events tail section with HTMX polling or manual refresh
- [x] 5.4 Add event type filtering (dropdown or buttons)
- [x] 5.5 Create history summary view with run/task keyed entries
- [x] 5.6 Integrate `IEventStore` and `IStateStore` for data access

### 6. Pause/Resume & Checkpoints Page
- [x] 6.1 Create `Pages/Checkpoints/Index.cshtml` and `Index.cshtml.cs` — checkpoint list
- [x] 6.2 Add Pause action that creates handoff.json snapshot
- [x] 6.3 Add Resume action that validates alignment and continues
- [x] 6.4 Add Create Checkpoint action with description input
- [x] 6.5 Add Show Checkpoint view for metadata and state snapshot
- [x] 6.6 Add Restore Checkpoint action with confirmation
- [x] 6.7 Display lock status (owner, timestamp)
- [x] 6.8 Add Release Lock action with safety checks
- [x] 6.9 Integrate `ICheckpointManager` and `ILockManager`

### 7. Validation & Maintenance Page
- [x] 7.1 Create `Pages/Validation/Index.cshtml` and `Index.cshtml.cs` — validation dashboard
- [x] 7.2 Add validation buttons: Schemas, Spec, State, Evidence, Codebase
- [x] 7.3 Add "Repair Indexes" action button
- [x] 7.4 Add Cache Clear and Cache Prune action buttons
- [x] 7.5 Create validation report display with collapsible sections
- [x] 7.6 Add clickable links from failing validations to offending files
- [x] 7.7 Integrate workspace validation services from `nirmata.Aos`

### 8. Testing & Verification
- [x] 8.1 Verify all new pages render without errors
- [x] 8.2 Verify navigation links work and current page is highlighted
- [x] 8.3 Verify JsonViewer component displays formatted JSON correctly
- [x] 8.4 Test each page action with valid and invalid workspace states
- [x] 8.5 Verify read-only display of sensitive engine state
- [x] 8.6 Run `openspec validate add-advanced-ui-pages --strict` and fix any issues

### 9. Documentation
- [x] 9.1 Update `openspec/specs/web-razor-pages/spec.md` with new page references
- [x] 9.2 Add page documentation comments to code-behind files
