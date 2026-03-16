# rename-command-to-orchestrator — Tasks

## 1. File Structure Changes

### 1.1 Rename Command Page Directory
- [x] Rename `Pages/Command/` directory to `Pages/Orchestrator/`
- [x] Rename `Index.cshtml` → `Index.cshtml` (in new directory)
- [x] Rename `Index.cshtml.cs` → `Index.cshtml.cs` (in new directory)
- [x] Update namespace from `nirmata.Web.Pages.Command` to `nirmata.Web.Pages.Orchestrator`

### 1.2 Update Route Configuration
- [x] Add route redirect from `/Command` to `/Orchestrator` in page model
- [x] Update `@page` directive if needed for explicit route
- [x] Verify route pattern registration in startup/configuration

## 2. Page Model Refactoring

### 2.1 Update Page Model Class
- [x] Rename `IndexModel` references if class name changes (keep as `IndexModel` within `Orchestrator` namespace)
- [x] Update `ViewData["Title"]` from "Command Center" to "Orchestrator"
- [x] Verify `WorkflowClassifier` injection and usage

### 2.2 Refactor Command Processing
- [x] Update `ProcessCommand()` to handle CLI commands (`/help`, `/status`, etc.)
- [x] Ensure freeform text flows through `ExecuteViaOrchestrator()`
- [x] Verify `NormalizeInput()` handles both formats correctly
- [x] Update `ExecuteViaOrchestrator()` call signature if needed

### 2.3 Update Local Commands
- [x] Verify `/help` returns orchestrator-specific command list
- [x] Verify `/status` displays workspace status correctly
- [x] Verify `/validate` runs workspace validation
- [x] Verify `/init` initializes AOS workspace

## 3. UI Updates

### 3.1 Update Page Header and Labels
- [x] Change `<h1>Command Center</h1>` to `<h1>Orchestrator</h1>`
- [x] Update welcome message text
- [x] Change "Welcome to Command Center" to "Welcome to the Orchestrator"
- [x] Update any help text referencing "Command Center"

### 3.2 Update CSS Classes (if needed)
- [x] Search for `.command-center` usage, consider renaming to `.orchestrator-page`
- [x] Update `.command-header`, `.command-form` if class names change
- [x] Ensure backward compatibility with existing styles

### 3.3 Update JavaScript References
- [x] Check for any JS files referencing `Command` page elements
- [x] Update element IDs if they are page-specific
- [x] Verify event handlers still attach correctly

## 4. Navigation and Links

### 4.1 Update Navigation Bar
- [x] Change nav link from "Command" to "Orchestrator"
- [x] Update `href` from `/Command` to `/Orchestrator`
- [x] Update icon/text if applicable

### 4.2 Update Cross-Page Links
- [x] Search for all references to `/Command` in codebase
- [x] Update links in Workspace page, Runs page, etc.
- [x] Update any hardcoded URLs in JavaScript files

### 4.3 Update Redirects
- [x] Add redirect handler for legacy `/Command` URLs
- [x] Return 301/302 redirect to `/Orchestrator`

## 5. Integration Verification

### 5.1 WorkflowClassifier Integration
- [x] Verify `WorkflowClassifier` is properly injected
- [x] Test `ExecuteAsync()` with various command types
- [x] Verify correlation ID generation and logging
- [x] Confirm error handling displays correctly in UI

### 5.2 Subagent Orchestration Flow
- [x] Test freeform text input flows to orchestrator
- [x] Test CLI command input flows to orchestrator
- [x] Verify `WorkflowIntent` is constructed correctly
- [x] Confirm `IOrchestrator.ExecuteAsync()` receives proper input

### 5.3 Message History
- [x] Verify message history saves to correct location
- [x] Update history file path from `command-history.json` to `orchestrator-history.json`
- [x] Ensure backward compatibility or migration for existing history

## 6. Testing

### 6.1 Unit Tests
- [x] Update any tests referencing `Command` page namespace
- [x] Update tests for `WorkflowClassifier` integration
- [x] Add tests for redirect from `/Command` to `/Orchestrator`

### 6.2 Integration Tests
- [x] Verify page loads correctly at `/Orchestrator`
- [x] Verify redirect works from `/Command`
- [x] Test all slash commands still function
- [x] Test freeform input flows through correctly

### 6.3 Manual Verification
- [x] Navigate to `/Orchestrator` and verify UI loads
- [x] Submit `/status` command and verify response
- [x] Submit freeform text and verify normalization
- [x] Check workspace context displays correctly
- [x] Verify message history persists after reload

## 7. Documentation

### 7.1 Update README/Documentation
- [x] Update any documentation referencing "Command Center"
- [x] Change references to "Orchestrator page"
- [x] Update navigation instructions

### 7.2 Code Comments
- [x] Update XML comments referencing old page name
- [x] Update inline comments explaining orchestrator flow

## Completion Criteria

- [x] All files renamed and moved to `Pages/Orchestrator/`
- [x] `/Command` URL redirects to `/Orchestrator`
- [x] Navigation updated to show "Orchestrator" instead of "Command"
- [x] All CLI commands (`/help`, `/status`, etc.) work correctly
- [x] Freeform text input flows through `WorkflowClassifier` to `IOrchestrator`
- [x] UI displays correctly with updated titles and labels
- [x] Message history persists correctly
- [x] Integration tests pass
- [x] No broken links or references remain
