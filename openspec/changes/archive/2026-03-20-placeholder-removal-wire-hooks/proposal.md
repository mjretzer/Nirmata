## Why

The frontend AOS UI is currently wired to mock data, which blocks end-to-end testing and validation against real backend behavior.
This change replaces placeholder/mock returns with real endpoint-backed data while keeping the existing UI pages/components unchanged.

## What Changes

- Remove `mock*` imports and placeholder returns from `nirmata.frontend/src/app/hooks/useAosData.ts`.
- Route existing hooks through a typed API helper (in `useAosData.ts` or a small `nirmata.frontend/src/app/api/` module) that calls real backend endpoints.
- Keep UI pages/components unchanged; they continue consuming the hooks as-is.
- Replace the mock-generated virtual filesystem (`nirmata.frontend/src/app/data/mockFileSystem.ts`) with a real filesystem data source exposed via `useFileSystem()`.
- Standardize hook outputs to include loading and error state (e.g., `{ data, isLoading, error }`) while preserving existing return keys.

## Capabilities

### New Capabilities
- `aos-data-api-wiring`: Provide real, typed, endpoint-backed data for the AOS frontend hooks (including loading/error semantics) without fetching inside pages/components.

### Modified Capabilities
- (none)

## Impact

- Frontend: `nirmata.frontend/src/app/hooks/useAosData.ts` and any consumers of its exported hooks (pages/components should not need changes).
- Frontend data: `nirmata.frontend/src/app/data/mockFileSystem.ts` and the `useFileSystem()` hook behavior.
- Backend/API: Requires the relevant endpoints to be available and accessible from the frontend environment; failures must surface through hook error states.
