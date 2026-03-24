## Context

Phase 6 connects the verification UI to real workspace data instead of mocks. The backend already has agent-side writers for issue artifacts and UAT results, while the API layer still needs workspace-scoped read/write services and controllers that surface issue and UAT data to the frontend. The main constraint is to align the new API surface with the existing `.aos/spec/issues/` and `.aos/spec/uat/` layout without changing the producer-side artifact formats unless required by the new read models.

## Goals / Non-Goals

**Goals:**
- Expose workspace-scoped issue CRUD and status updates backed by `.aos/spec/issues/`.
- Expose workspace-scoped UAT summaries backed by `.aos/spec/uat/` with derived pass/fail per task and phase.
- Replace verification and issue mocks in the frontend with API-backed hooks and routing.
- Keep agent-side issue creation compatible with the new API read model.

**Non-Goals:**
- Redesign the underlying UAT execution engine.
- Change the agent-side verification contract unless required for API compatibility.
- Introduce a new persistence technology; the filesystem-backed spec store remains the source of truth.

## Decisions

- **Filesystem-backed services instead of in-memory controllers**: `IssueService` and `UatService` should read from `.aos/spec/**` so the API reflects persisted workspace state. This matches the rest of the Phase 5/6 architecture and avoids diverging from the agent-produced artifacts. Alternative considered: keep controller-local static lists. Rejected because it would not persist across requests and would not reflect the workspace.
- **Workspace-scoped endpoints under `nirmata.Api`**: use `/v1/workspaces/{wsId}/issues` and `/v1/workspaces/{wsId}/uat` so the data is tied to a registered workspace and follows the existing domain API pattern. Alternative considered: keep using `/api/v1/issues` and add query params for workspace. Rejected because the rest of the domain data surface is workspace-scoped and this change needs workspace validation.
- **Derived UAT summaries at the API boundary**: the UAT service should aggregate raw UAT records into task/phase pass-fail summaries rather than forcing the frontend to infer them. Alternative considered: expose only raw artifact files and let the frontend derive state. Rejected because verification pages need a stable contract and the derivation logic belongs with the authoritative data source.
- **Frontend hooks should consume the new endpoints directly**: `useIssues` and `useVerificationState` should stop relying on local mock state and use the workspace-scoped API plus existing tasks/runs data. Alternative considered: keep derived state entirely client-side. Rejected because the user-facing pages need a consistent server-backed view of issue status and UAT outcomes.
- **Keep issue creation aligned with existing agent writer output**: the API write model should preserve the same field semantics as `IssueWriter` so failed UAT checks and manual issue creation converge on the same workspace issue store. Alternative considered: introduce a separate API-only issue schema. Rejected because it would fragment the workflow and complicate `FixPage`.

## Risks / Trade-offs

- **[Schema drift between agent-written issues and API DTOs]** → Mitigate by reusing shared field names and adding tests that read agent-produced issue files through the API service.
- **[UAT derivation logic may be ambiguous across phases/tasks]** → Mitigate by defining deterministic summary rules in the spec and design, then covering them with fixture-based tests.
- **[Frontend migration may temporarily diverge from current mock assumptions]** → Mitigate by keeping response shapes close to existing hook contracts and adding adapter code only where necessary.
- **[Workspace root validation failures]** → Mitigate by validating all workspace IDs and file paths before reading from `.aos/spec/**`.

## Migration Plan

1. Add backend issue and UAT services plus controllers behind the existing API host.
2. Add or update tests for workspace validation, issue filtering, and UAT summary derivation.
3. Update frontend data hooks to consume the new endpoints and preserve the current UI flow.
4. Register `FixPage` in the router and confirm `VerificationPage` can seed fix workflows from failed UATs.
5. Validate the end-to-end flow against real workspace artifacts.

Rollback strategy: remove the new controllers/services from DI and restore the previous frontend mocks if the new endpoints produce incompatible results.

## Open Questions

- Should manual issue creation use the same filesystem schema as agent-generated issues, or a dedicated write DTO that is normalized into the same file format?
- Should UAT summaries include only the current task and phase cursor, or all historical records for a workspace?
- Should issue status updates mutate only the on-disk JSON record, or also emit a corresponding event for continuity history?
