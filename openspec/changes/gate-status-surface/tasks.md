## 1. Workspace gate summary contract

- [x] 1.1 Inspect the existing workspace-scoped API and service layer to identify the best canonical place to expose a workspace gate summary derived from `.aos/spec`, `.aos/state`, and `.aos/codebase` artifacts.
- [x] 1.2 Implement the workspace gate summary read contract so it returns the current gate, next required step, blocking reason, and brownfield codebase readiness details for a workspace.
- [x] 1.3 Add backend coverage proving the summary changes when canonical artifacts advance the workspace from interview to roadmap/planning and when brownfield preflight is missing or stale.

## 2. Shared UI status surface

- [x] 2.1 Add a shared frontend data hook or adapter that loads the workspace gate summary without duplicating gate inference in page components.
- [ ] 2.2 Build a reusable gate status surface that renders the current gate, blocking reason, next required step, and the primary route-to-action affordance.
- [ ] 2.3 Wire the shared status surface into `WorkspaceDashboard`, `ChatPage`, and `CodebasePage` so each page shows the same underlying workspace gate state.

## 3. Brownfield readiness and routing behavior

- [ ] 3.1 Render explicit brownfield preflight states for missing and stale codebase maps with user-facing copy that distinguishes the two cases.
- [ ] 3.2 Map each supported blocking gate to the correct route or workflow entry point so the primary action takes the user to the page that can resolve it.
- [ ] 3.3 Ensure the status surface degrades clearly for unsupported or unavailable route targets instead of hiding the blocking state.

## 4. Verification

- [ ] 4.1 Add frontend tests proving the dashboard, chat, and codebase pages display the same gate summary for the same workspace state.
- [ ] 4.2 Add frontend tests proving the primary action routes to the expected workflow entry point for brownfield preflight and at least one non-codebase gate.
- [ ] 4.3 Verify end to end that when canonical workspace artifacts change, a subsequent status refresh shows the updated gate and readiness details without page-specific divergence.