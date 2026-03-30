## Context

GitHub OAuth requires a callback URL that matches a reachable origin. The Nirmata browser stack is now converging on HTTPS defaults: the frontend dev server, daemon API, and domain API all expose HTTPS endpoints, and browser routing uses those secure service URLs directly. That keeps OAuth callback registration straightforward and avoids mixed-content issues when the app is accessed from a secure origin.

## Goals / Non-Goals

**Goals:**
- Provide a stable HTTPS origin for the browser-facing app stack.
- Keep GitHub OAuth callback setup simple for developers and operators.
- Avoid mixed-content browser failures between the frontend and browser-used APIs.
- Preserve the existing local/dev service topology as much as possible.
- Keep the GitHub OAuth secret server-side.

**Non-Goals:**
- Replacing GitHub OAuth with PAT-based auth.
- Building a full production certificate management system.
- Reworking unrelated workspace bootstrap behavior.
- Moving GitHub client credentials into the browser.

## Decisions

1. **Use HTTPS endpoints for the browser-facing stack**
   - The simplest approach is to expose secure origins for the UI and browser-visible APIs directly.
   - That keeps the browser on HTTPS without introducing an extra proxy layer or additional localhost hop.
   - **Chosen implementation:** direct HTTPS hosting on the frontend and backend dev surfaces.
   - **Chosen browser origin:** `https://localhost:8443`.
   - **Alternatives considered:**
     - Frontend-only HTTPS: rejected because browser requests to HTTP APIs would still be mixed content.
     - Reverse proxy / gateway: rejected because the HTTPS backend defaults are already straightforward to expose directly in development.

2. **Derive the OAuth callback from the externally visible HTTPS origin**
   - The backend should continue to own the OAuth callback endpoint, but the configured callback must match the secure origin that the browser uses.
   - When an explicit callback URL is not supplied, the app should build one from the HTTPS origin.
   - **Alternatives considered:**
     - Hardcode a callback URL in appsettings for every environment. Rejected because it is brittle and environment-specific.

3. **Keep browser routing centralized**
   - The frontend already has centralized routing for daemon and domain API base URLs.
   - The HTTPS change updates those base URLs in one place so the browser does not need to know about the underlying transport details.
   - **Alternatives considered:**
     - Scatter new URLs across components. Rejected because it makes future OAuth and origin debugging harder.

4. **Document the dev HTTPS setup rather than hiding it**
   - The user should be able to bootstrap the HTTPS flow without guesswork.
   - A short setup guide should explain which HTTPS origins to use and which callback URL to register in GitHub.

## Risks / Trade-offs

- [Local certificate trust] → Mitigation: use a trusted local certificate or a dev proxy flow that clearly documents trust requirements.
- [Mixed-content regressions] → Mitigation: update browser-facing base URLs and CORS origins together.
- [Callback mismatch] → Mitigation: keep a single source of truth for the externally visible origin.
- [Configuration drift between services] → Mitigation: prefer one HTTPS entrypoint and one documented dev origin.

## Migration Plan

1. Introduce the HTTPS entrypoint and update frontend routing to use it.
2. Update daemon/API origin and CORS settings so browser requests remain same-origin or allowed-cross-origin over HTTPS.
3. Wire the GitHub OAuth callback URL to the secure origin.
4. Add docs and tests for the HTTPS connection flow.
5. Verify the GitHub connect button opens GitHub and returns to the app over HTTPS.

## Open Questions

- Should the default secure origin target `localhost` only, or also support a configurable developer host name?
- Do we want the same HTTPS setup to be used in staging/prod, or only for local development and OAuth testing?
