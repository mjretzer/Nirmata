# AOS Command Console — Guidelines

## Architecture rules

* **Data access pattern**: All pages and components consume data exclusively through hooks exported from `/src/app/hooks/useAosData.ts`. Pages must never import from `/src/app/data/` files directly. When the daemon API arrives, only the hook internals change — consumers stay untouched.
* **Mock data is intentional**: Mock files in `/src/app/data/` are the runtime data source for now. Do not remove or replace them until the daemon API layer is built.
* **Reserved React props**: Never use `ref` as a component prop name — rename it to `gitRef` (or similar) to avoid shadowing React's reserved `ref`.
* **Dashboard folder**: `components/dashboards/` is the sole canonical folder for dashboard-level view components (e.g. `RunsDashboard`, `FolderDashboard`, `UATDashboard`).
* **Lucide icons**: All lucide-react icons must be explicitly imported at the top of each file that uses them. Never rely on implicit/inherited imports.
* **Verification separation**: `verificationState.ts` exports a pure `deriveVerificationState(data)` function. `useAosData.ts` exposes `useVerificationState()` which wraps it. Keep these two files separate — one is pure logic, the other is a React hook.
* **Config model**: `workspace-config-panel.tsx` houses the central `configRegistry` using the real `aos config` key/value model consumed via `resolveConfig()`. The shared `config-category-accordion.tsx` renders config categories. Settings pages use `useConfigState()` to manage edits, validation, apply/discard lifecycle.

## Import safety

* `fast_apply_tool` tends to silently drop existing imports (especially `useState`/`useEffect`/`useRef` and lucide-react icons). After any edit, verify that **all imports at the top of the file are still present**.
* When editing a file, always re-check the first 30 lines of the file post-edit to confirm import integrity.

## Code style

* Prefer extracting shared helpers to `/src/app/utils/` (e.g. `relativeTime` in `format.ts`, `copyToClipboard` in `clipboard.ts`).
* Prefer extracting shared UI snippets to `/src/app/components/` (e.g. `WorkspaceStatusBadge.tsx`).
* Prefix unused state variables with `_` to satisfy the `@typescript-eslint/no-unused-vars` rule (e.g. `_runUntil`, `_alwaysVerify`).
* Replace `console.log` stubs with `toast.info()` via sonner for user-facing feedback.
* Use `useMemo` for React context provider values to prevent unnecessary re-renders.

## Tooling

* **Lint**: `pnpm lint` — ESLint flat config with `typescript-eslint` + `react-hooks/rules-of-hooks` as error.
* **Test**: `pnpm test` — Vitest with jsdom, 71 test cases across hooks (including `useTimers`), utils, and verification logic.
* **Type check**: `pnpm typecheck` — strict TypeScript with `tsc --noEmit`.

## Naming conventions

* Pages: PascalCase in `/src/app/pages/` (e.g. `WorkspaceDashboard.tsx`, `ContinuityPage.tsx`).
* Components: PascalCase in `/src/app/components/` (e.g. `WorkspaceStatusBadge.tsx`).
* Hooks: camelCase prefixed with `use` in `/src/app/hooks/` (e.g. `useAosData.ts`).
* Utils: camelCase in `/src/app/utils/` (e.g. `format.ts`, `clipboard.ts`).
* Mock data: camelCase prefixed with `mock` in `/src/app/data/` (e.g. `mockData.ts`, `mockHostData.ts`).