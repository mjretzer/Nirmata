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
  useRef,
  useCallback,
  type ReactNode,
} from "react";
import { DAEMON_BASE_URL } from "../api/routing";

// ── Types ─────────────────────────────────────────────────────

interface DaemonHealthResponse {
  ok: boolean;
  version: string;
  uptimeMs: number;
}

export type EngineStatus = "idle" | "running" | "paused" | "waiting";

export interface GitState {
  branch: string;
  dirty: number;
  ahead: number;
  behind: number;
}

export type DaemonConnectionState = "connecting" | "connected" | "disconnected";

export type WorkspaceBootstrapError = "not-found" | "error" | null;

interface WorkspaceContextValue {
  // Active workspace
  activeWorkspaceId: string;
  setActiveWorkspaceId: (id: string) => void;

  // Workspace bootstrap error — set by useWorkspace on lookup failure, cleared on success or ID change
  workspaceBootstrapError: WorkspaceBootstrapError;
  setWorkspaceBootstrapError: (error: WorkspaceBootstrapError) => void;

  // Engine
  engineStatus: EngineStatus;
  setEngineStatus: (status: EngineStatus) => void;

  // Daemon
  daemonConnected: boolean;
  setDaemonConnected: (connected: boolean) => void;
  daemonConnectionState: DaemonConnectionState;
  daemonPollingActive: boolean;
  reconnect: () => void;

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
  workspaceBootstrapError: null,
  setWorkspaceBootstrapError: () => {},
  engineStatus: "idle",
  setEngineStatus: () => {},
  daemonConnected: false,          // honest default — unknown until first ping
  setDaemonConnected: () => {},
  daemonConnectionState: "connecting",
  daemonPollingActive: true,
  reconnect: () => {},
  gitState: defaultGitState,
  setGitState: () => {},
});

// ── Constants ─────────────────────────────────────────────────

// DAEMON_BASE_URL is resolved from VITE_DAEMON_URL via routing.ts (single source of truth)

const HEALTH_ENDPOINT = `${DAEMON_BASE_URL}/api/v1/health`;
const POLL_INTERVAL_MS = 30_000;
const PING_TIMEOUT_MS  = 3_000;

// After this many consecutive failures the poll stops to avoid console spam.
// Call reconnect() to restart.
const MAX_CONSECUTIVE_FAILURES = 1;

// ── Provider ──────────────────────────────────────────────────

export function WorkspaceProvider({
  children,
  initialWorkspaceId = "my-app",
}: {
  children: ReactNode;
  initialWorkspaceId?: string;
}) {
  const [activeWorkspaceId, setActiveWorkspaceId] = useState(initialWorkspaceId);

  const [workspaceBootstrapError, setWorkspaceBootstrapError] = useState<WorkspaceBootstrapError>(null);
  const [engineStatus, setEngineStatus] = useState<EngineStatus>("idle");
  const [daemonConnected, setDaemonConnected] = useState(false); // false until proven otherwise
  const [daemonConnectionState, setDaemonConnectionState] = useState<DaemonConnectionState>("connecting");
  const [daemonPollingActive, setDaemonPollingActive] = useState(true);
  const [gitState, setGitState] = useState<GitState>(defaultGitState);

  // Tracks consecutive ping failures — not state because we don't need re-renders per failure.
  const consecutiveFailures = useRef(0);

  // Clear bootstrap error whenever the active workspace changes so a new lookup is allowed to succeed or fail fresh.
  useEffect(() => {
    setWorkspaceBootstrapError(null);
  }, [activeWorkspaceId]);

  // ── Daemon health poll ───────────────────────────────────────
  // Gated on daemonPollingActive. When MAX_CONSECUTIVE_FAILURES is reached the poll
  // sets daemonPollingActive=false, which triggers cleanup (clears the interval) and
  // prevents the effect from restarting until reconnect() is called.
  useEffect(() => {
    if (!daemonPollingActive) return;

    let cancelled = false;

    async function ping() {
      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), PING_TIMEOUT_MS);
      try {
        const res = await fetch(HEALTH_ENDPOINT, {
          signal: controller.signal,
          cache: "no-store",
        });
        if (res.ok) {
          const body: DaemonHealthResponse = await res.json();
          if (!cancelled) {
            if (body.ok) {
              setDaemonConnected(true);
              setDaemonConnectionState("connected");
              consecutiveFailures.current = 0;
            } else {
              setDaemonConnected(false);
              setDaemonConnectionState("disconnected");
              consecutiveFailures.current += 1;
              if (consecutiveFailures.current >= MAX_CONSECUTIVE_FAILURES) {
                setDaemonPollingActive(false);
              }
            }
          }
        } else {
          if (!cancelled) {
            consecutiveFailures.current += 1;
            setDaemonConnected(false);
            setDaemonConnectionState("disconnected");
            if (consecutiveFailures.current >= MAX_CONSECUTIVE_FAILURES) {
              setDaemonPollingActive(false);
            }
          }
        }
      } catch {
        if (!cancelled) {
          consecutiveFailures.current += 1;
          setDaemonConnected(false);
          setDaemonConnectionState("disconnected");
          if (consecutiveFailures.current >= MAX_CONSECUTIVE_FAILURES) {
            setDaemonPollingActive(false);
          }
        }
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
  }, [daemonPollingActive]);

  // Resets the failure counter and restarts polling. Call this after fixing daemon configuration.
  const reconnect = useCallback(() => {
    consecutiveFailures.current = 0;
    setDaemonPollingActive(true);
    setDaemonConnectionState("connecting");
  }, []);

  const value = useMemo<WorkspaceContextValue>(
    () => ({
      activeWorkspaceId,
      setActiveWorkspaceId,
      workspaceBootstrapError,
      setWorkspaceBootstrapError,
      engineStatus,
      setEngineStatus,
      daemonConnected,
      setDaemonConnected,
      daemonConnectionState,
      daemonPollingActive,
      reconnect,
      gitState,
      setGitState,
    }),
    [activeWorkspaceId, workspaceBootstrapError, engineStatus, daemonConnected, daemonConnectionState, daemonPollingActive, reconnect, gitState]
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