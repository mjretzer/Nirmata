/**
 * API Routing Map — single source of truth for all API base URLs.
 *
 * Routing rule:
 *   Daemon/host/service lifecycle features  → daemonClient  (VITE_DAEMON_URL,  default https://localhost:9000)
 *   Domain data features (workspaces/spec/tasks/runs/issues) → domainClient (VITE_DOMAIN_URL)
 *
 * Configure via .env.local:
 *   VITE_DAEMON_URL=https://localhost:9000
 *   VITE_DOMAIN_URL=https://localhost:7138
 *
 * Import from here — never hardcode base URLs inline.
 */

const _env: Record<string, string> =
  typeof import.meta !== "undefined"
    ? ((import.meta as { env?: Record<string, string> }).env ?? {})
    : {};

const _isSecureBrowserOrigin =
  typeof window !== "undefined" && window.location.protocol === "https:";

function resolveBrowserBaseUrl(envValue: string | undefined, secureFallback: string): string {
  if (!envValue) {
    return secureFallback;
  }

  if (_isSecureBrowserOrigin && envValue.startsWith("http://")) {
    return secureFallback;
  }

  return envValue;
}

/** Base URL for the Agent Manager daemon API (nirmata.Windows.Service.Api). */
export const DAEMON_BASE_URL: string = resolveBrowserBaseUrl(_env["VITE_DAEMON_URL"], "https://localhost:9000");

/** Base URL for the workspace data API (nirmata.Api). */
export const DOMAIN_BASE_URL: string = resolveBrowserBaseUrl(_env["VITE_DOMAIN_URL"], "https://localhost:7138");

/**
 * Authoritative routing map.
 *
 * Import this to see (or log) where each client routes at runtime.
 * Always reads from the env-resolved constants above.
 */
export const API_ROUTING = {
  daemon: {
    baseUrl: DAEMON_BASE_URL,
    envVar: "VITE_DAEMON_URL",
    description:
      "Agent Manager daemon (nirmata.Windows.Service.Api) — health poll, host/service lifecycle, engine commands",
    devDefault: "https://localhost:9000",
  },
  domain: {
    baseUrl: DOMAIN_BASE_URL,
    envVar: "VITE_DOMAIN_URL",
    description:
      "Workspace data API (nirmata.Api) — workspaces, spec, milestones, phases, tasks, runs, issues",
    devDefault: "https://localhost:7138",
  },
} as const;
