/**
 * Resilience tests for useAosData hooks — error and loading-state paths.
 *
 * 4.3  Error paths: spy on domainClient methods to simulate network and
 *      server failures, then confirm each hook settles to its empty default
 *      state with isLoading=false (hooks swallow errors silently so the UI
 *      renders empty rather than crashing).
 *
 * 4.4  Loading paths: control promise resolution with a deferred helper to
 *      confirm isLoading=true is visible before the API call settles and
 *      isLoading=false after it settles (resolve or reject).
 */

import { describe, it, expect, vi, afterEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { createElement, type ReactNode } from "react";
import { domainClient, ApiError } from "../../utils/apiClient";
import { WorkspaceProvider } from "../../context/WorkspaceContext";
import {
  useTasks,
  useWorkspaces,
  useWorkspace,
  useIssues,
  useRuns,
  useTaskPlans,
  useMilestones,
  usePhases,
} from "../useAosData";

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Creates a promise whose resolve/reject can be triggered externally. */
function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

const GUID_WORKSPACE_ID = "550e8400-e29b-41d4-a716-446655440000";

function workspaceWrapper({ children }: { children: ReactNode }) {
  return createElement(WorkspaceProvider, {
    initialWorkspaceId: GUID_WORKSPACE_ID,
    children,
  });
}

function useWorkspaceBootstrapAndMilestones(workspaceId: string) {
  return {
    workspace: useWorkspace(workspaceId),
    milestones: useMilestones(),
  };
}

afterEach(() => {
  vi.restoreAllMocks();
});

// ── 4.3  Error paths ──────────────────────────────────────────────────────────

describe("4.3 Error paths — hooks settle to empty state on failure", () => {
  // ── Network errors (status 0 / no HTTP response) ──────────────────────────

  it("useTasks: network error → tasks=[], isLoading=false", async () => {
    vi.spyOn(domainClient, "getTasks").mockRejectedValueOnce(
      new ApiError(0, "Failed to fetch"),
    );

    const { result } = renderHook(() => useTasks());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.tasks).toEqual([]);
  });

  it("useWorkspaces: network error → workspaces=[], isLoading=false", async () => {
    vi.spyOn(domainClient, "getWorkspaces").mockRejectedValueOnce(
      new ApiError(0, "Network unreachable"),
    );

    const { result } = renderHook(() => useWorkspaces());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.workspaces).toEqual([]);
  });

  it("useRuns: network error → runs=[], isLoading=false", async () => {
    vi.spyOn(domainClient, "getWorkspaceRuns").mockRejectedValueOnce(
      new ApiError(0, "Connection refused"),
    );

    const { result } = renderHook(() => useRuns());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.runs).toEqual([]);
  });

  it("useTaskPlans: network error → plans=[], isLoading=false", async () => {
    vi.spyOn(domainClient, "getTaskPlans").mockRejectedValueOnce(
      new ApiError(0, "Failed to fetch"),
    );

    const { result } = renderHook(() => useTaskPlans());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.plans).toEqual([]);
  });

  // ── Server errors (4xx / 5xx HTTP responses) ──────────────────────────────

  it("useTasks: server 500 → tasks=[], isLoading=false", async () => {
    vi.spyOn(domainClient, "getTasks").mockRejectedValueOnce(
      new ApiError(500, "HTTP 500: Internal Server Error"),
    );

    const { result } = renderHook(() => useTasks());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.tasks).toEqual([]);
  });

  it("useIssues: server 404 → issues=[], isLoading=false", async () => {
    vi.spyOn(domainClient, "getIssues").mockRejectedValueOnce(
      new ApiError(404, "HTTP 404: Not Found"),
    );

    const { result } = renderHook(() => useIssues());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.issues).toEqual([]);
  });

  it("useMilestones: server 503 → milestones=[], isLoading=false", async () => {
    vi.spyOn(domainClient, "getMilestones").mockRejectedValueOnce(
      new ApiError(503, "HTTP 503: Service Unavailable"),
    );

    const { result } = renderHook(() => useMilestones());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.milestones).toEqual([]);
  });

  it("usePhases: server 401 → phases=[], isLoading=false", async () => {
    vi.spyOn(domainClient, "getPhases").mockRejectedValueOnce(
      new ApiError(401, "HTTP 401: Unauthorized"),
    );

    const { result } = renderHook(() => usePhases());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.phases).toEqual([]);
  });

  it("useWorkspace: server error → workspace defined (empty default), isLoading=false", async () => {
    vi.spyOn(domainClient, "getWorkspace").mockRejectedValueOnce(
      new ApiError(500, "HTTP 500: Internal Server Error"),
    );

    const { result } = renderHook(() => useWorkspace(GUID_WORKSPACE_ID), {
      wrapper: WorkspaceProvider,
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // workspace stays at its empty default — not undefined
    expect(result.current.workspace).toBeDefined();
    expect(result.current.workspace.repoRoot).toBe("");
    expect(result.current.notFound).toBe(false);
    expect(result.current.bootstrapDiagnostic).toContain(
      `Endpoint: GET /v1/workspaces/${GUID_WORKSPACE_ID}`,
    );
    expect(result.current.bootstrapDiagnostic).toContain("Status: HTTP 500");
    expect(result.current.bootstrapDiagnostic).toContain("Detail: HTTP 500: Internal Server Error");
    expect(result.current.bootstrapDiagnostic).toContain(
      "Suggested fix: Check the domain API URL and confirm the workspace exists and is reachable.",
    );
  });

  it("useWorkspaces: server error renders a diagnostic with endpoint, status, and fix", async () => {
    vi.spyOn(domainClient, "getWorkspaces").mockRejectedValueOnce(
      new ApiError(503, "HTTP 503: Service Unavailable"),
    );

    const { result } = renderHook(() => useWorkspaces());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.workspaces).toEqual([]);
    expect(result.current.errorDiagnostic).toContain("Endpoint: GET /v1/workspaces");
    expect(result.current.errorDiagnostic).toContain("Status: HTTP 503");
    expect(result.current.errorDiagnostic).toContain("Detail: HTTP 503: Service Unavailable");
    expect(result.current.errorDiagnostic).toContain(
      "Suggested fix: Check the domain API URL and confirm the workspace registry is reachable.",
    );
  });

  it("useWorkspace bootstrap failure suppresses follow-up milestone fetches", async () => {
    vi.spyOn(domainClient, "getWorkspace").mockRejectedValueOnce(
      new ApiError(404, "HTTP 404: Not Found"),
    );
    const milestonesSpy = vi.spyOn(domainClient, "getMilestones").mockResolvedValue([]);

    const { result } = renderHook(
      () => useWorkspaceBootstrapAndMilestones(GUID_WORKSPACE_ID),
      { wrapper: workspaceWrapper },
    );

    await waitFor(() => expect(result.current.workspace.isLoading).toBe(false));
    await waitFor(() => expect(result.current.milestones.isLoading).toBe(false));

    expect(result.current.workspace.notFound).toBe(true);
    expect(result.current.workspace.bootstrapDiagnostic).toContain(
      `Endpoint: GET /v1/workspaces/${GUID_WORKSPACE_ID}`,
    );
    expect(result.current.workspace.bootstrapDiagnostic).toContain("Status: HTTP 404");
    expect(milestonesSpy).toHaveBeenCalledTimes(1);
  });
});

// ── 4.4  Loading paths ────────────────────────────────────────────────────────

describe("4.4 Loading paths — isLoading=true while pending, false after settled", () => {
  it("useTasks: isLoading=true on mount, false after resolve", async () => {
    const d = deferred<[]>();
    vi.spyOn(domainClient, "getTasks").mockReturnValueOnce(d.promise);

    const { result } = renderHook(() => useTasks(), {
      wrapper: workspaceWrapper,
    });

    // Immediately after mount the promise is still pending
    expect(result.current.isLoading).toBe(true);

    // Resolve the deferred and confirm loading clears
    act(() => d.resolve([]));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });

  it("useTasks: isLoading=false after network failure (reject path)", async () => {
    const d = deferred<[]>();
    vi.spyOn(domainClient, "getTasks").mockReturnValueOnce(d.promise);

    const { result } = renderHook(() => useTasks(), {
      wrapper: workspaceWrapper,
    });
    expect(result.current.isLoading).toBe(true);

    act(() => d.reject(new ApiError(0, "Failed to fetch")));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.tasks).toEqual([]);
  });

  it("useWorkspaces: isLoading=true on mount, false after resolve", async () => {
    const d = deferred<[]>();
    vi.spyOn(domainClient, "getWorkspaces").mockReturnValueOnce(d.promise);

    const { result } = renderHook(() => useWorkspaces());
    expect(result.current.isLoading).toBe(true);

    act(() => d.resolve([]));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });

  it("useWorkspaces: isLoading=false after server error (reject path)", async () => {
    const d = deferred<[]>();
    vi.spyOn(domainClient, "getWorkspaces").mockReturnValueOnce(d.promise);

    const { result } = renderHook(() => useWorkspaces());
    expect(result.current.isLoading).toBe(true);

    act(() => d.reject(new ApiError(503, "Service Unavailable")));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });

  it("useWorkspace(undefined): isLoading=false immediately — no fetch when no id", () => {
    // No spy needed — the hook skips the fetch when workspaceId is undefined
    const { result } = renderHook(() => useWorkspace());
    expect(result.current.isLoading).toBe(false);
    expect(result.current.workspace).toBeDefined();
  });

  it("useWorkspace('id'): isLoading=true on mount, false after resolve", async () => {
    const d = deferred<{ id: string; name: string; path: string; status: string; lastModified: string }>();
    vi.spyOn(domainClient, "getWorkspace").mockReturnValueOnce(d.promise);

    const { result } = renderHook(() => useWorkspace(GUID_WORKSPACE_ID), {
      wrapper: WorkspaceProvider,
    });
    expect(result.current.isLoading).toBe(true);

    act(() => d.resolve({ id: "ws-1", name: "ws-1", path: "/repos/ws-1", status: "healthy", lastModified: "2026-01-01" }));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });

  it("useIssues: isLoading=true on mount, false after resolve", async () => {
    const d = deferred<[]>();
    vi.spyOn(domainClient, "getIssues").mockReturnValueOnce(d.promise);

    const { result } = renderHook(() => useIssues(), {
      wrapper: workspaceWrapper,
    });
    expect(result.current.isLoading).toBe(true);

    act(() => d.resolve([]));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });

  it("useRuns: loading state is stable after network error (no spurious re-renders)", async () => {
    const d = deferred<[]>();
    vi.spyOn(domainClient, "getWorkspaceRuns").mockReturnValueOnce(d.promise);

    const { result } = renderHook(() => useRuns(), {
      wrapper: workspaceWrapper,
    });
    expect(result.current.isLoading).toBe(true);

    act(() => d.reject(new ApiError(0, "ECONNREFUSED")));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // Remains false — no bounce back to true
    expect(result.current.isLoading).toBe(false);
  });
});
