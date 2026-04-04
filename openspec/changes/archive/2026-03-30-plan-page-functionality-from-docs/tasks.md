## 1. Plan Route and Artifact Resolution

- [x] 1.1 Update `nirmata.frontend/src/app/pages/PlanPage.tsx` route resolution so `.aos/spec` root, `phases/PH-*` directory, and `.json` artifact routes resolve through one deterministic branch order.
- [x] 1.2 Remove synthetic artifact generation from `nirmata.frontend/src/app/pages/PlanPage.tsx` (`getSpecFileContent`) and prevent generated substitute payloads for missing files.
- [x] 1.3 Implement explicit missing-artifact behavior in `nirmata.frontend/src/app/pages/PlanPage.tsx` for unknown phases and missing artifact file paths.
- [x] 1.4 Add explicit loading and error rendering paths in `nirmata.frontend/src/app/pages/PlanPage.tsx` for data-fetch and file-read failures.

## 2. Canonical Data Source Alignment

- [x] 2.1 Update `nirmata.frontend/src/app/components/plan/RoadmapTimeline.tsx` to render milestone/phase/task values from hook data as source of truth.
- [x] 2.2 Update `nirmata.frontend/src/app/components/plan/PhaseTaskList.tsx` to avoid local-only task state as primary render source.
- [x] 2.3 Ensure phase task filtering is strictly based on `phaseId` from canonical task data in `nirmata.frontend/src/app/components/plan/PhaseTaskList.tsx`.

## 3. Tests and Verification

- [x] 3.1 Add `nirmata.frontend/src/app/pages/__tests__/PlanPage.test.tsx` covering root roadmap lens, known phase directory lens, and unknown phase missing-artifact behavior.
- [x] 3.2 Add test cases in `nirmata.frontend/src/app/pages/__tests__/PlanPage.test.tsx` verifying missing artifact routes do not render synthetic JSON fallback.
- [x] 3.3 Add test cases for loading and error states in `nirmata.frontend/src/app/pages/__tests__/PlanPage.test.tsx` using mocked hook states.
- [x] 3.4 Run `npm run test -- PlanPage` from `nirmata.frontend/` and resolve all regressions introduced by this change.

## 4. Implementation Guardrails

- [x] 4.1 Keep route contracts unchanged for `/ws/:workspaceId/files/.aos/spec` and descendants.
- [x] 4.2 Do not introduce new API endpoints; consume existing workspace domain-data endpoints only.
- [x] 4.3 Confirm all changed files are limited to Plan page, plan components, and associated tests.
