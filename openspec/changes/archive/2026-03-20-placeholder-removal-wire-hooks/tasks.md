## 1. API wiring foundation

- [x] 1.1 Identify the backend endpoints and response shapes required by each hook in `nirmata.frontend/src/app/hooks/useAosData.ts`
- [x] 1.2 Implement a typed API helper (in `useAosData.ts` or `nirmata.frontend/src/app/api/`) for consistent base URL, headers/credentials, and error handling
- [x] 1.3 Add a consistent error mapping strategy (network error vs non-success response) used by all AOS hooks

## 2. Replace mock returns in `useAosData.ts`

- [x] 2.1 Remove all `mock*` imports at the top of `useAosData.ts`
- [x] 2.2 Update each hook to call the typed API helper instead of returning placeholder/mock data
- [x] 2.3 Ensure each updated hook exposes a consistent loading and error state while preserving existing return keys consumed by the UI
- [x] 2.4 Add minimal response-shape adapters inside hooks (if needed) to keep outward-facing shapes stable

## 3. Replace virtual filesystem mock

- [x] 3.1 Remove reliance on `nirmata.frontend/src/app/data/mockFileSystem.ts` for runtime filesystem data
- [x] 3.2 Update `useFileSystem()` (via `useAosData.ts`) to source filesystem data from backend endpoints
- [x] 3.3 Ensure filesystem data includes stable identifiers and ordering required by existing UI consumers

## 4. Integration and verification

- [x] 4.1 Confirm no AOS UI pages/components perform fetching (network calls occur only from hooks/API helper)
- [x] 4.2 Validate the UI still renders with unchanged pages/components and that all hook return keys are present
- [x] 4.3 Validate error paths by simulating endpoint failures and confirming errors surface through hooks
- [x] 4.4 Validate loading paths by throttling network and confirming loading state is observable and stable

## 5. Cleanup

- [x] 5.1 Delete or orphan-check any unused mock-only exports after wiring is complete
- [x] 5.2 Ensure lint/build passes for the frontend workspace after removing mocks
