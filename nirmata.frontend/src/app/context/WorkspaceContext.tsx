/**
 * Global Workspace Context
 *
 * Provides engine status, daemon connection, workspace selection,
 * and git state to the entire app. Layout.tsx writes these values;
 * child pages like Orchestrator and Continuity can read them
 * without prop-drilling.
 */

import {
  createContext,
  useContext,
  useState,
  useEffect,
  useMemo,
  type ReactNode,
} from "react";

// ── Types ─────────────────────────────────────────────────────

export type EngineStatus = "idle" | "running" | "paused" | "waiting";

export interface GitState {
  branch: string;
  dirty: number;
  ahead: number;
  behind: number;
}

interface WorkspaceContextValue {
  // Active workspace
  activeWorkspaceId: string;
  setActiveWorkspaceId: (id: string) => void;

  // Engine
  engineStatus: EngineStatus;
  setEngineStatus: (status: EngineStatus) => void;

  // Daemon
  daemonConnected: boolean;
  setDaemonConnected: (connected: boolean) => void;

  // Git
  gitState: GitState;
  setGitState: (state: GitState) => void;
}

const defaultGitState: GitState = {
  branch: "main",
  dirty: 3,
  ahead: 1,
  behind: 0,
};

const WorkspaceContext = createContext<WorkspaceContextValue>({
  activeWorkspaceId: "my-app",
  setActiveWorkspaceId: () => {},
  engineStatus: "idle",
  setEngineStatus: () => {},
  daemonConnected: false,          // honest default — unknown until first ping
  setDaemonConnected: () => {},
  gitState: defaultGitState,
  setGitState: () => {},
});

// ── Constants ─────────────────────────────────────────────────

/** Override with VITE_DAEMON_URL in .env.local when the real daemon ships. */
const DAEMON_BASE_URL =
  typeof import.meta !== "undefined" && (import.meta as { env?: Record<string, string> }).env?.VITE_DAEMON_URL
    ? (import.meta as { env?: Record<string, string> }).env!.VITE_DAEMON_URL
    : "http://localhost:9000";

const HEALTH_ENDPOINT = `${DAEMON_BASE_URL}/api/v1/health`;
const POLL_INTERVAL_MS = 30_000;
const PING_TIMEOUT_MS  = 3_000;

// ── Provider ──────────────────────────────────────────────────

export function WorkspaceProvider({ children }: { children: ReactNode }) {
  const [activeWorkspaceId, setActiveWorkspaceId] = useState("my-app");
  const [engineStatus, setEngineStatus] = useState<EngineStatus>("idle");
  const [daemonConnected, setDaemonConnected] = useState(false); // false until proven otherwise
  const [gitState, setGitState] = useState<GitState>(defaultGitState);

  // ── Daemon health poll ───────────────────────────────────────
  useEffect(() => {
    let cancelled = false;

    async function ping() {
      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), PING_TIMEOUT_MS);
      try {
        const res = await fetch(HEALTH_ENDPOINT, {
          signal: controller.signal,
          // no credentials / no cookies — plain reachability check
          cache: "no-store",
        });
        if (!cancelled) setDaemonConnected(res.ok);
      } catch {
        if (!cancelled) setDaemonConnected(false);
      } finally {
        clearTimeout(timer);
      }
    }

    // Fire immediately, then on a fixed cadence
    ping();
    const id = setInterval(ping, POLL_INTERVAL_MS);

    return () => {
      cancelled = true;
      clearInterval(id);
    };
  }, []);

  const value = useMemo<WorkspaceContextValue>(
    () => ({
      activeWorkspaceId,
      setActiveWorkspaceId,
      engineStatus,
      setEngineStatus,
      daemonConnected,
      setDaemonConnected,
      gitState,
      setGitState,
    }),
    [activeWorkspaceId, engineStatus, daemonConnected, gitState]
  );

  return (
    <WorkspaceContext.Provider value={value}>
      {children}
    </WorkspaceContext.Provider>
  );
}

// ── Hook ──────────────────────────────────────────────────────

export function useWorkspaceContext() {
  return useContext(WorkspaceContext);
}