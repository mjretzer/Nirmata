## Context

The frontend AOS UI currently depends on mock data returned from `nirmata.frontend/src/app/hooks/useAosData.ts`. This blocks validating real backend behavior and creates divergence between UI and production data.

A key constraint is that UI pages/components must remain unchanged, and data fetching must not occur inside pages/components. The wiring work must therefore happen at the hooks layer (and/or a small API helper module used by those hooks) while keeping return keys stable for existing consumers.

## Goals / Non-Goals

**Goals:**
- Replace mock/placeholder data sources used by `useAosData.ts` with real, endpoint-backed calls.
- Centralize network requests in the hooks layer and keep pages/components as passive consumers.
- Preserve existing hook return keys so UI code does not need refactoring.
- Provide consistent loading and error semantics across updated hooks.
- Replace the mock-derived virtual filesystem with a real filesystem data source exposed via the existing `useFileSystem()` entry point.

**Non-Goals:**
- Refactor UI pages/components to a new data architecture.
- Redesign API contracts or introduce new backend endpoints (this change assumes the necessary endpoints already exist or will be provided separately).
- Change user-facing UI behavior beyond removing mock discrepancies and surfacing real errors/loading states.

## Decisions

- Centralize API access behind a typed helper used by hooks.
  - Rationale: Keeps pages/components unchanged and creates a single place to handle base URL, headers, auth, and response typing.
  - Alternative: Inline `fetch` in each hook. Rejected because it increases duplication and makes consistent error/loading handling harder.

- Preserve existing return keys while adding standardized state.
  - Rationale: The roadmap requirement is “wire without refactoring pages”. Hooks can maintain their current shape and introduce `{ isLoading, error }` in a non-breaking way (or map to the existing shape used by the UI).
  - Alternative: Switch to a new query library pattern and update all consumers. Rejected as it violates the “no page changes” constraint.

- Replace the virtual filesystem provider with real data while keeping `useFileSystem()` as the integration point.
  - Rationale: This isolates change to hook/data layer and avoids cascading UI changes.
  - Alternative: Keep mocks and only partially wire endpoints. Rejected because it leaves the UI in a mixed state and undermines end-to-end testing.

## Risks / Trade-offs

- Backend endpoint availability or contract mismatch → Ensure hooks surface errors clearly and keep mock fallback disabled so issues are visible during integration.
- Increased latency vs. mocks → Provide loading state and avoid blocking UI rendering on synchronous mock generation.
- Data shape changes from real endpoints → Add response mapping/adapters within hooks to keep the outward-facing shape stable.
- Auth/session requirements for endpoints → Ensure API helper centralizes headers/credentials and uses existing frontend auth mechanisms.

## Migration Plan

- Implement hooks wiring behind the existing exported hook APIs (no UI changes).
- Validate locally by exercising pages that consume `useAosData.ts` and confirming network calls occur and mocks are no longer referenced.
- Rollback is simply reverting the hook/API helper changes (since no schema or UI changes are required).

## Open Questions

- Which exact backend endpoints and response shapes correspond to each hook in `useAosData.ts`?
- What is the expected authentication mechanism for these endpoints in the frontend runtime (cookies, bearer token, etc.)?
- Should the API helper live in `useAosData.ts` or a dedicated `nirmata.frontend/src/app/api/` module for reuse?
