## Why

The workspace initialization flow looks enabled in the UI, but the path typed into the Workspace settings page is not yet the authoritative location used by `aos init`. As a result, `.aos/` can be created in the daemon's current working directory or host profile workspace instead of the exact folder the user entered.

## What Changes

- Make the entered workspace root path the authoritative path for `aos init`.
- Pass the explicit root path from the frontend to the daemon command request instead of relying on daemon process state.
- Keep the existing bootstrapper as the source of truth for creating `.aos/` and its baseline structure.
- Preserve the existing host-profile fallback for other command flows, but do not depend on it for workspace initialization.
- Add verification coverage so init creates `.aos/` in the exact folder entered in the UI, even when the path contains spaces.

## Impact

- `nirmata.frontend` will send the selected root path through the init flow instead of only issuing `aos init` with no location context.
- `nirmata.Windows.Service.Api` will honor an explicit root path for command execution during initialization.
- `nirmata.Aos` should continue to create `.aos/` idempotently at the resolved repository root.
- Users will get a predictable init experience that matches the folder they typed in the UI.
