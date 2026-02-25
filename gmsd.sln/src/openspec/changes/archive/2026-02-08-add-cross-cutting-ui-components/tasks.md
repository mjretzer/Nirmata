## Implementation Tasks

### 1. Global Command Palette
- [x] 1.1 Create `wwwroot/js/command-palette.js` — keyboard shortcut handler (Ctrl+K)
- [x] 1.2 Create command registry with items: pages, artifacts, runs, commands
- [x] 1.3 Create palette UI overlay with search/filter
- [x] 1.4 Add navigation action handlers for selected commands
- [x] 1.5 Register keyboard shortcut in `site.js`
- [x] 1.6 Style palette with CSS (modal, search input, results list)

### 2. Persistent Workspace Badge
- [x] 2.1 Create workspace badge partial in `Pages/Shared/_WorkspaceBadge.cshtml`
- [x] 2.2 Display current workspace path from config
- [x] 2.3 Add `.aos` health indicator (valid/invalid/missing)
- [x] 2.4 Integrate badge into `_Layout.cshtml` header
- [x] 2.5 Add tooltip with workspace details on hover
- [x] 2.6 Style badge with CSS (repo icon, path text, health indicator)

### 3. Unified Artifact Link System
- [x] 3.1 Create `wwwroot/js/artifact-links.js` — link detection and rendering
- [x] 3.2 Support prefixes: TSK, PH, MS, UAT, RUN
- [x] 3.3 Create mapping from prefix to route (TSK → /Tasks/Details/{id})
- [x] 3.4 Add click handlers for artifact navigation
- [x] 3.5 Style artifact links with CSS (prefix badge, hover effects)
- [x] 3.6 Apply link transformation to page content areas

### 4. Toast/Notification System
- [x] 4.1 Create `wwwroot/js/toasts.js` — notification manager
- [x] 4.2 Create toast container in `_Layout.cshtml`
- [x] 4.3 Support toast types: success, error, warning, info
- [x] 4.4 Add auto-dismiss with configurable duration
- [x] 4.5 Add manual dismiss (X button)
- [x] 4.6 Style toasts with CSS (positioning, colors, animations)
- [x] 4.7 Create server-side toast helper for Razor Pages
- [x] 4.8 Integrate with validation failures, run completion, lock conflicts

### 5. Shared Styling
- [x] 5.1 Update `wwwroot/css/site.css` with component styles
- [x] 5.2 Ensure consistent theming across all components
- [x] 5.3 Add CSS variables for component colors
- [x] 5.4 Verify mobile responsiveness

### 6. Testing & Verification
- [x] 6.1 Verify command palette opens with Ctrl+K
- [x] 6.2 Verify palette search filters commands correctly
- [x] 6.3 Verify workspace badge displays correct path
- [x] 6.4 Verify health indicator reflects .aos status
- [x] 6.5 Verify artifact links (TSK/PH/MS/UAT/RUN) work correctly
- [x] 6.6 Verify toasts appear for validation failures
- [x] 6.7 Verify toasts auto-dismiss after timeout
- [x] 6.8 Run `openspec validate add-cross-cutting-ui-components --strict`

### 7. Documentation
- [x] 7.1 Add component documentation comments to JavaScript files
- [x] 7.2 Update `openspec/specs/web-cross-cutting-components/spec.md` with final details
