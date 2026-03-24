## Why

Verification and fix workflows are still disconnected from the real workspace issue and UAT artifacts, so `VerificationPage` cannot show authoritative failures and `FixPage` cannot derive concrete follow-up work. This blocks Phase 6 validation and leaves the UI dependent on mock state instead of the data already being written under `.aos/spec/issues/` and `.aos/spec/uat/`.

## What Changes

- Add workspace-scoped issue services and controllers so issue records can be listed, created, updated, deleted, and filtered by status, severity, phase, and task.
- Add workspace-scoped UAT services and controllers so the API can return UAT records plus derived pass/fail summaries per task and phase.
- Replace frontend issue and verification mocks with real workspace endpoints.
- Wire issue creation from failed UAT flows into the real workspace issue endpoint.
- Register `FixPage` in the router so the fix workflow is reachable from the app.

## Capabilities

### New Capabilities
- `verification-feedback`: workspace-scoped issue records and UAT-derived verification summaries used by verification and fix workflows.

### Modified Capabilities
- `api`: expand issue tracking from read-only retrieval to full CRUD and status updates, and add a workspace-scoped UAT summary endpoint.

## Impact

- `nirmata.Api` gains new workspace-scoped services, controllers, and response shapes for issues and UAT summaries.
- Frontend hooks on `VerificationPage` and `FixPage` switch from mocked data to real API-backed data.
- Workspace artifacts under `.aos/spec/issues/` and `.aos/spec/uat/` become the source of truth for verification visibility.
- Existing agent-side verification writers remain the producer side; the API becomes the read/summary surface for the UI.
