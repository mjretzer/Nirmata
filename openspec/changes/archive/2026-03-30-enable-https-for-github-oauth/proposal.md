## Why

GitHub OAuth callbacks and browser redirects are easiest to configure when the app has a stable HTTPS origin. The browser-facing pieces of Nirmata are now standardizing on HTTPS defaults: the frontend dev server, the daemon API, and the domain API all expose HTTPS endpoints, and the browser clients rely on those secure origins for API calls. That keeps GitHub callback setup predictable and avoids mixed-content failures during the connect flow.

## What Changes

- Keep the browser-facing app stack on stable HTTPS origins so the UI can run securely.
- Update the frontend dev/runtime config so browser requests target HTTPS base URLs instead of mixed HTTP origins.
- Ensure GitHub OAuth callback URLs are derived from the HTTPS origin and can be registered in GitHub without manual workarounds.
- Document the local setup so a developer can run the app securely without hand-editing callback links each time.

## Capabilities

### New Capabilities
- `https-browser-entrypoint`: run the Nirmata frontend and browser-consumed APIs behind an HTTPS origin suitable for GitHub OAuth callbacks.
- `github-oauth-callback-over-https`: complete the GitHub connection flow using an HTTPS callback URL derived from the app origin.

### Modified Capabilities
- `github-workspace-bootstrap`: keep the GitHub connect flow, but make it work against a secure origin.

## Impact

- Frontend dev/runtime configuration in `nirmata.frontend/vite.config.ts` and `nirmata.frontend/src/app/api/routing.ts`.
- Browser API routing in `nirmata.frontend/src/app/utils/apiClient.ts`.
- Daemon hosting and CORS in `src/nirmata.Windows.Service.Api/Program.cs` and `src/nirmata.Windows.Service.Api/appsettings.Development.json`.
- API hosting and callback configuration in `src/nirmata.Api/Program.cs`, `src/nirmata.Api/Properties/launchSettings.json`, and `src/nirmata.Api/appsettings.json`.
- GitHub OAuth config and callback docs in `src/nirmata.Services/Configuration/GitHubOptions.cs`.
- Development/setup documentation and verification tests.
