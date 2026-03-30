## 1. HTTPS browser entrypoint

- [x] 1.1 Decide the HTTPS workaround implementation (local reverse proxy, gateway service, or direct HTTPS hosting) and document the chosen origin.
- [x] 1.2 Update the frontend dev/runtime configuration to serve or target the secure origin.
- [x] 1.3 Update browser API routing so daemon and domain requests use HTTPS-compatible base URLs.

## 2. Backend origin and callback wiring

- [x] 2.1 Update daemon and API hosting/CORS settings so the HTTPS origin is accepted in development.
- [x] 2.2 Ensure the GitHub OAuth callback URL resolves to the secure origin used by the browser.
- [x] 2.3 Keep GitHub OAuth secrets server-side and validate missing or mismatched origin configuration with a clear error.

## 3. Docs and verification

- [x] 3.1 Add setup docs explaining how to run the app over HTTPS and which GitHub callback URL to register.
- [x] 3.2 Add or update tests for the HTTPS origin, callback routing, and mixed-content-safe browser requests.
- [x] 3.3 Verify the GitHub connect flow reaches GitHub and returns successfully over HTTPS.
