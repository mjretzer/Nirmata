## 1. Frontend init flow

- [x] 1.1 Update the workspace init hook so it sends the selected root path through the daemon request instead of calling `aos init` without location context.
- [x] 1.2 Ensure the workspace settings page continues to pass the saved root path into the init flow and surfaces any validation error before dispatch.
- [x] 1.3 Stop fabricating the `.aos/` path locally in the UI; use the backend response as the source of truth for where initialization occurred.

## 2. Backend command execution

- [x] 2.1 Extend the daemon command request contract to accept an explicit repository root or working-directory field for initialization.
- [x] 2.2 Update the command controller to prefer the explicit path for `aos init`, while preserving existing fallback behavior for other flows.
- [x] 2.3 Confirm the command executor runs in the requested directory so the bootstrapper receives the correct repository root.

## 3. AOS bootstrap verification

- [x] 3.1 Confirm the existing workspace bootstrapper remains the authoritative `.aos/` creator and stays idempotent.
- [x] 3.2 Add or update tests to prove `.aos/` is created in the exact folder entered in the UI.
- [x] 3.3 Add coverage for paths with spaces and for repeated init calls on an already initialized workspace.

## 4. End-to-end validation

- [x] 4.1 Verify the UI, daemon, and bootstrapper agree on the same repository root path.
- [x] 4.2 Verify a fresh workspace initializes successfully without manual folder creation.
- [x] 4.3 Verify existing workspace initialization remains non-destructive.
