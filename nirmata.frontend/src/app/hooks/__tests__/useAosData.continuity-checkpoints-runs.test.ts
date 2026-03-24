/**
 * 4.2 – Frontend hook tests for continuity, checkpoints, and runs data loading.
 *
 * Verifies that the three workspace-scoped hooks introduced in Phase 5 call the
 * correct domain API methods and behave correctly under success, error, and
 * loading-state scenarios.
 *
 * Hook → endpoint mapping:
 *   useRuns(taskId?)       → domainClient.getWorkspaceRuns(wsId)
 *   useCheckpoints()       → domainClient.getWorkspaceCheckpoints(wsId)
 *   useContinuityState()   → domainClient.getWorkspaceState / getWorkspaceHandoff /
 *                             getWorkspaceEvents / getWorkspaceStatePacks  (Promise.allSettled)
 *
 * Test sections:
 *   4.2a – Return shapes are stable
 *   4.2b – Correct workspace-scoped API method is called with the active workspace id
 *   4.2c – Data mapping: API response values flow through to the hook output
 *   4.2d – Error paths: each hook settles to its empty default on failure
 *   4.2e – Loading state: isLoading=true while pending, false after settled
 *   4.2f – Re-fetch: changing activeWorkspaceId triggers a new API call
 *   4.2g – useContinuityState partial-failure resilience (Promise.allSettled paths)
 */

import { describe, it, expect, vi, afterEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { createElement, type ReactNode } from "react";
import { WorkspaceProvider as DefaultWorkspaceProvider, useWorkspaceContext } from "../../context/WorkspaceContext";
import { domainClient, ApiError } from "../../utils/apiClient";
import type {
  WorkspaceRunSummary,
  WorkspaceCheckpointSummary,
  WorkspaceContinuityState,
  WorkspaceHandoff,
  WorkspaceEvent,
  WorkspaceContextPack,
} from "../../utils/apiClient";
import { useRuns, useCheckpoints, useContinuityState } from "../useAosData";

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

/**
 * Returns a combined hook that exposes both the inner hook result and
 * `setActiveWorkspaceId` from WorkspaceContext, allowing workspace switching
 * within a single renderHook call.
 */
function withWorkspaceControl<T>(innerHook: () => T) {
  return function useWithControl() {
    const { setActiveWorkspaceId } = useWorkspaceContext();
    return { hook: innerHook(), setActiveWorkspaceId };
  };
}

// WorkspaceContext default value — no wrapper needed for basic tests.
const DEFAULT_WS = "550e8400-e29b-41d4-a716-446655440010";
const NEXT_WS = "550e8400-e29b-41d4-a716-446655440011";

const WorkspaceProvider = ({ children }: { children: ReactNode }) =>
  createElement(DefaultWorkspaceProvider, { initialWorkspaceId: DEFAULT_WS, children });

afterEach(() => {
  vi.restoreAllMocks();
});

// ── Fixture data ──────────────────────────────────────────────────────────────

const runFixture: WorkspaceRunSummary = {
  id: "RUN-2026-01-13T021500Z",
  taskId: "TSK-000001",
  status: "pass",
  timestamp: "2026-01-13T02:15:00Z",
};

const checkpointFixture: WorkspaceCheckpointSummary = {
  id: "CHK-001",
  position: { milestoneId: "MS-0001", phaseId: "PH-0001", taskId: "TSK-000001", stepIndex: 1, status: "InProgress" },
  timestamp: "2026-01-13T02:15:00Z",
};

const stateFixture: WorkspaceContinuityState = {
  position: {
    milestoneId: "MS-0001",
    phaseId: "PH-0002",
    taskId: "TSK-000003",
    stepIndex: 2,
    status: "InProgress",
  },
  decisions: [],
  blockers: [],
  lastTransition: {
    from: "planned",
    to: "InProgress",
    timestamp: "2026-01-13T02:15:00Z",
    trigger: "execute-plan",
  },
};

const handoffFixture: WorkspaceHandoff = {
  cursor: { milestoneId: "MS-0001", phaseId: "PH-0002", taskId: "TSK-000003" },
  inFlightTask: "TSK-000003",
  inFlightStep: 2,
  allowedScope: ["src/Gmsd.Agents/Workflows/Execution/TaskExecutor/TaskExecutorWorkflow.cs"],
  pendingVerification: false,
  nextCommand: "resume-work",
  timestamp: "2026-01-13T02:15:00Z",
};

const eventsFixture: WorkspaceEvent[] = [
  { type: "phase.planned", timestamp: "2026-01-13T01:00:00Z", payload: "PH-0002 planned", references: ["PH-0002"] },
  { type: "task.resumed",  timestamp: "2026-01-13T02:00:00Z", payload: "TSK-000003 resumed", references: ["TSK-000003"] },
];

const packsFixture: WorkspaceContextPack[] = [
  { packId: "TSK-000003", mode: "execute", budgetTokens: 8000, artifactCount: 3 },
];

// ── 4.2a Return shapes ────────────────────────────────────────────────────────

describe("4.2a – Return shapes are stable", () => {
  it("useRuns returns { runs: Run[], isLoading: boolean }", async () => {
    vi.spyOn(domainClient, "getWorkspaceRuns").mockResolvedValue([]);

    const { result } = renderHook(() => useRuns(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(Array.isArray(result.current.runs)).toBe(true);
    expect(typeof result.current.isLoading).toBe("boolean");
    expect(Object.keys(result.current).sort()).toEqual(["isLoading", "runs"]);
  });

  it("useCheckpoints returns { checkpoints: Checkpoint[], isLoading: boolean }", async () => {
    vi.spyOn(domainClient, "getWorkspaceCheckpoints").mockResolvedValue([]);

    const { result } = renderHook(() => useCheckpoints(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(Array.isArray(result.current.checkpoints)).toBe(true);
    expect(typeof result.current.isLoading).toBe("boolean");
    expect(Object.keys(result.current).sort()).toEqual(["checkpoints", "isLoading"]);
  });

  it("useContinuityState returns { state, handoff, events, packs, cursor, isLoading }", async () => {
    vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue({ decisions: [], blockers: [] });
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockResolvedValue(null);
    vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue([]);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue([]);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.state).toBeDefined();
    expect(typeof result.current.handoff === "object").toBe(true); // null or object
    expect(Array.isArray(result.current.events)).toBe(true);
    expect(Array.isArray(result.current.packs)).toBe(true);
    expect(typeof result.current.cursor).toBe("object");
    expect(typeof result.current.isLoading).toBe("boolean");
    expect(Object.keys(result.current).sort()).toEqual([
      "cursor", "events", "handoff", "isLoading", "packs", "state",
    ]);
  });
});

// ── 4.2b Correct workspace-scoped API method is called ────────────────────────

describe("4.2b – Workspace-scoped API method is called with the active workspace id", () => {
  it("useRuns calls getWorkspaceRuns with the active workspace id", async () => {
    const spy = vi.spyOn(domainClient, "getWorkspaceRuns").mockResolvedValue([]);

    const { result } = renderHook(() => useRuns(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(spy).toHaveBeenCalledOnce();
    expect(spy).toHaveBeenCalledWith(DEFAULT_WS);
  });

  it("useCheckpoints calls getWorkspaceCheckpoints with the active workspace id", async () => {
    const spy = vi.spyOn(domainClient, "getWorkspaceCheckpoints").mockResolvedValue([]);

    const { result } = renderHook(() => useCheckpoints(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(spy).toHaveBeenCalledOnce();
    expect(spy).toHaveBeenCalledWith(DEFAULT_WS);
  });

  it("useContinuityState calls getWorkspaceState, getWorkspaceHandoff, getWorkspaceEvents, getWorkspaceStatePacks", async () => {
    const stateSpy  = vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue({ decisions: [], blockers: [] });
    const handoffSpy = vi.spyOn(domainClient, "getWorkspaceHandoff").mockResolvedValue(null);
    const eventsSpy  = vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue([]);
    const packsSpy   = vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue([]);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(stateSpy).toHaveBeenCalledWith(DEFAULT_WS);
    expect(handoffSpy).toHaveBeenCalledWith(DEFAULT_WS);
    expect(eventsSpy).toHaveBeenCalledWith(DEFAULT_WS);
    expect(packsSpy).toHaveBeenCalledWith(DEFAULT_WS);
  });
});

// ── 4.2c Data mapping ─────────────────────────────────────────────────────────

describe("4.2c – API response values are mapped to hook output", () => {
  it("useRuns: WorkspaceRunSummary fields are mapped to Run shape", async () => {
    vi.spyOn(domainClient, "getWorkspaceRuns").mockResolvedValue([runFixture]);

    const { result } = renderHook(() => useRuns(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.runs).toHaveLength(1);
    const run = result.current.runs[0];
    expect(run.id).toBe(runFixture.id);
    expect(run.taskId).toBe(runFixture.taskId);
    // Backend uses "pass"/"fail"; hook normalises to "success"/"failed"
    expect(run.status).toBe("success");
    expect(run.startTime).toBe(runFixture.timestamp);
    expect(Array.isArray(run.artifacts)).toBe(true);
    expect(Array.isArray(run.logs)).toBe(true);
  });

  it("useRuns: optional taskId filter returns only matching runs", async () => {
    const run2: WorkspaceRunSummary = {
      id: "RUN-002",
      taskId: "TSK-000002",
      status: "fail",
      timestamp: "2026-01-14T00:00:00Z",
    };
    vi.spyOn(domainClient, "getWorkspaceRuns").mockResolvedValue([runFixture, run2]);

    const { result } = renderHook(() => useRuns("TSK-000001"), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.runs).toHaveLength(1);
    expect(result.current.runs[0].taskId).toBe("TSK-000001");
  });

  it("useRuns: no taskId filter returns all runs", async () => {
    const run2: WorkspaceRunSummary = { id: "RUN-002", taskId: "TSK-000002", status: "pass", timestamp: null };
    vi.spyOn(domainClient, "getWorkspaceRuns").mockResolvedValue([runFixture, run2]);

    const { result } = renderHook(() => useRuns(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.runs).toHaveLength(2);
  });

  it("useCheckpoints: WorkspaceCheckpointSummary fields are mapped to Checkpoint shape", async () => {
    vi.spyOn(domainClient, "getWorkspaceCheckpoints").mockResolvedValue([checkpointFixture]);

    const { result } = renderHook(() => useCheckpoints(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.checkpoints).toHaveLength(1);
    const cp = result.current.checkpoints[0];
    expect(cp.id).toBe(checkpointFixture.id);
    expect(cp.timestamp).toBe(checkpointFixture.timestamp);
    expect(cp.cursor.milestone).toBe(checkpointFixture.position!.milestoneId);
    expect(cp.cursor.phase).toBe(checkpointFixture.position!.phaseId);
    expect(cp.cursor.task).toBe(checkpointFixture.position!.taskId);
  });

  it("useContinuityState: cursor is populated from state position", async () => {
    vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue(stateFixture);
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockResolvedValue(null);
    vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue([]);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue([]);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.cursor.milestoneId).toBe(stateFixture.position!.milestoneId);
    expect(result.current.cursor.phaseId).toBe(stateFixture.position!.phaseId);
    expect(result.current.cursor.taskId).toBe(stateFixture.position!.taskId);
  });

  it("useContinuityState: handoff fields are mapped when handoff.json exists", async () => {
    vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue({ decisions: [], blockers: [] });
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockResolvedValue(handoffFixture);
    vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue([]);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue([]);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.handoff).not.toBeNull();
    expect(result.current.handoff!.nextCommand).toBe(handoffFixture.nextCommand);
    expect(result.current.handoff!.inFlight.task).toBe(handoffFixture.inFlightTask);
    expect(result.current.handoff!.inFlight.files).toEqual(handoffFixture.allowedScope);
  });

  it("useContinuityState: handoff is null when endpoint returns null", async () => {
    vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue({ decisions: [], blockers: [] });
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockResolvedValue(null);
    vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue([]);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue([]);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.handoff).toBeNull();
  });

  it("useContinuityState: events are mapped from WorkspaceEvent list", async () => {
    vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue({ decisions: [], blockers: [] });
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockResolvedValue(null);
    vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue(eventsFixture);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue([]);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.events).toHaveLength(2);
    expect(result.current.events[0].type).toBe("phase.planned");
    expect(result.current.events[0].timestamp).toBe("2026-01-13T01:00:00Z");
    expect(result.current.events[1].type).toBe("task.resumed");
  });

  it("useContinuityState: packs are mapped from WorkspaceContextPack list", async () => {
    vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue({ decisions: [], blockers: [] });
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockResolvedValue(null);
    vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue([]);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue(packsFixture);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.packs).toHaveLength(1);
    expect(result.current.packs[0].id).toBe("TSK-000003");
    expect(result.current.packs[0].mode).toBe("execute");
  });
});

// ── 4.2d Error paths ──────────────────────────────────────────────────────────

describe("4.2d – Error paths: hooks settle to empty defaults on failure", () => {
  it("useRuns: network error → runs=[], isLoading=false", async () => {
    vi.spyOn(domainClient, "getWorkspaceRuns").mockRejectedValueOnce(
      new ApiError(0, "Failed to fetch"),
    );

    const { result } = renderHook(() => useRuns(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.runs).toEqual([]);
  });

  it("useRuns: server 500 → runs=[], isLoading=false", async () => {
    vi.spyOn(domainClient, "getWorkspaceRuns").mockRejectedValueOnce(
      new ApiError(500, "HTTP 500: Internal Server Error"),
    );

    const { result } = renderHook(() => useRuns(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.runs).toEqual([]);
  });

  it("useCheckpoints: network error → checkpoints=[], isLoading=false", async () => {
    vi.spyOn(domainClient, "getWorkspaceCheckpoints").mockRejectedValueOnce(
      new ApiError(0, "Connection refused"),
    );

    const { result } = renderHook(() => useCheckpoints(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.checkpoints).toEqual([]);
  });

  it("useCheckpoints: server 503 → checkpoints=[], isLoading=false", async () => {
    vi.spyOn(domainClient, "getWorkspaceCheckpoints").mockRejectedValueOnce(
      new ApiError(503, "HTTP 503: Service Unavailable"),
    );

    const { result } = renderHook(() => useCheckpoints(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.checkpoints).toEqual([]);
  });

  it("useContinuityState: all four endpoints fail → empty defaults, isLoading=false", async () => {
    vi.spyOn(domainClient, "getWorkspaceState").mockRejectedValueOnce(new ApiError(500, "Server Error"));
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockRejectedValueOnce(new ApiError(500, "Server Error"));
    vi.spyOn(domainClient, "getWorkspaceEvents").mockRejectedValueOnce(new ApiError(500, "Server Error"));
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockRejectedValueOnce(new ApiError(500, "Server Error"));

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.handoff).toBeNull();
    expect(result.current.events).toEqual([]);
    expect(result.current.packs).toEqual([]);
    expect(result.current.cursor.milestoneId).toBe("");
    expect(result.current.cursor.phaseId).toBe("");
    expect(result.current.cursor.taskId).toBeNull();
  });
});

// ── 4.2e Loading state ────────────────────────────────────────────────────────

describe("4.2e – Loading state: isLoading=true while pending, false after settled", () => {
  it("useRuns: isLoading=true on mount, false after resolve", async () => {
    const d = deferred<WorkspaceRunSummary[]>();
    vi.spyOn(domainClient, "getWorkspaceRuns").mockReturnValueOnce(d.promise);

    const { result } = renderHook(() => useRuns(), { wrapper: WorkspaceProvider });
    expect(result.current.isLoading).toBe(true);

    act(() => d.resolve([]));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });

  it("useRuns: isLoading=false after network failure (reject path)", async () => {
    const d = deferred<WorkspaceRunSummary[]>();
    vi.spyOn(domainClient, "getWorkspaceRuns").mockReturnValueOnce(d.promise);

    const { result } = renderHook(() => useRuns(), { wrapper: WorkspaceProvider });
    expect(result.current.isLoading).toBe(true);

    act(() => d.reject(new ApiError(0, "ECONNREFUSED")));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.runs).toEqual([]);
  });

  it("useCheckpoints: isLoading=true on mount, false after resolve", async () => {
    const d = deferred<WorkspaceCheckpointSummary[]>();
    vi.spyOn(domainClient, "getWorkspaceCheckpoints").mockReturnValueOnce(d.promise);

    const { result } = renderHook(() => useCheckpoints(), { wrapper: WorkspaceProvider });
    expect(result.current.isLoading).toBe(true);

    act(() => d.resolve([]));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });

  it("useCheckpoints: isLoading=false after network failure (reject path)", async () => {
    const d = deferred<WorkspaceCheckpointSummary[]>();
    vi.spyOn(domainClient, "getWorkspaceCheckpoints").mockReturnValueOnce(d.promise);

    const { result } = renderHook(() => useCheckpoints(), { wrapper: WorkspaceProvider });
    expect(result.current.isLoading).toBe(true);

    act(() => d.reject(new ApiError(0, "ECONNREFUSED")));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.checkpoints).toEqual([]);
  });

  it("useContinuityState: isLoading=true on mount, false after all four settle", async () => {
    const dState  = deferred<WorkspaceContinuityState>();
    const dHandoff = deferred<WorkspaceHandoff | null>();
    const dEvents  = deferred<WorkspaceEvent[]>();
    const dPacks   = deferred<WorkspaceContextPack[]>();

    vi.spyOn(domainClient, "getWorkspaceState").mockReturnValueOnce(dState.promise);
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockReturnValueOnce(dHandoff.promise);
    vi.spyOn(domainClient, "getWorkspaceEvents").mockReturnValueOnce(dEvents.promise);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockReturnValueOnce(dPacks.promise);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    expect(result.current.isLoading).toBe(true);

    act(() => {
      dState.resolve({});
      dHandoff.resolve(null);
      dEvents.resolve([]);
      dPacks.resolve([]);
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
  });

  it("useContinuityState: isLoading stays stable (false) after all reject", async () => {
    const dState   = deferred<WorkspaceContinuityState>();
    const dHandoff = deferred<WorkspaceHandoff | null>();
    const dEvents  = deferred<WorkspaceEvent[]>();
    const dPacks   = deferred<WorkspaceContextPack[]>();

    vi.spyOn(domainClient, "getWorkspaceState").mockReturnValueOnce(dState.promise);
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockReturnValueOnce(dHandoff.promise);
    vi.spyOn(domainClient, "getWorkspaceEvents").mockReturnValueOnce(dEvents.promise);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockReturnValueOnce(dPacks.promise);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    expect(result.current.isLoading).toBe(true);

    act(() => {
      dState.reject(new ApiError(500, "Error"));
      dHandoff.reject(new ApiError(500, "Error"));
      dEvents.reject(new ApiError(500, "Error"));
      dPacks.reject(new ApiError(500, "Error"));
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    // Remains false — no bounce back to true
    expect(result.current.isLoading).toBe(false);
  });
});

// ── 4.2f Re-fetch on workspace switch ─────────────────────────────────────────

describe("4.2f – Re-fetch: changing activeWorkspaceId triggers a new API call", () => {
  it("useRuns re-fetches with the new workspace id", async () => {
    const spy = vi.spyOn(domainClient, "getWorkspaceRuns").mockResolvedValue([]);

    const { result } = renderHook(withWorkspaceControl(() => useRuns()), {
      wrapper: WorkspaceProvider,
    });

    await waitFor(() => expect(result.current.hook.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith(DEFAULT_WS);

    act(() => result.current.setActiveWorkspaceId(NEXT_WS));

    await waitFor(() => expect(spy).toHaveBeenCalledWith(NEXT_WS));
    expect(spy).toHaveBeenCalledTimes(2);
  });

  it("useCheckpoints re-fetches with the new workspace id", async () => {
    const spy = vi.spyOn(domainClient, "getWorkspaceCheckpoints").mockResolvedValue([]);

    const { result } = renderHook(withWorkspaceControl(() => useCheckpoints()), {
      wrapper: WorkspaceProvider,
    });

    await waitFor(() => expect(result.current.hook.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith(DEFAULT_WS);

    act(() => result.current.setActiveWorkspaceId(NEXT_WS));

    await waitFor(() => expect(spy).toHaveBeenCalledWith(NEXT_WS));
    expect(spy).toHaveBeenCalledTimes(2);
  });

  it("useContinuityState re-fetches all four endpoints with the new workspace id", async () => {
    const stateSpy  = vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue({ decisions: [], blockers: [] });
    const handoffSpy = vi.spyOn(domainClient, "getWorkspaceHandoff").mockResolvedValue(null);
    const eventsSpy  = vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue([]);
    const packsSpy   = vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue([]);

    const { result } = renderHook(withWorkspaceControl(() => useContinuityState()), {
      wrapper: WorkspaceProvider,
    });

    await waitFor(() => expect(result.current.hook.isLoading).toBe(false));
    expect(stateSpy).toHaveBeenCalledWith(DEFAULT_WS);

    act(() => result.current.setActiveWorkspaceId(NEXT_WS));

    await waitFor(() => expect(stateSpy).toHaveBeenCalledWith(NEXT_WS));
    expect(stateSpy).toHaveBeenCalledTimes(2);
    expect(handoffSpy).toHaveBeenCalledTimes(2);
    expect(eventsSpy).toHaveBeenCalledTimes(2);
    expect(packsSpy).toHaveBeenCalledTimes(2);
  });
});

// ── 4.2g Partial-failure resilience (useContinuityState) ─────────────────────

describe("4.2g – useContinuityState partial-failure resilience (Promise.allSettled paths)", () => {
  it("state endpoint fails, others succeed — events and packs still populated", async () => {
    vi.spyOn(domainClient, "getWorkspaceState").mockRejectedValueOnce(new ApiError(500, "Error"));
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockResolvedValue(null);
    vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue(eventsFixture);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue(packsFixture);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // cursor stays at empty defaults since state failed
    expect(result.current.cursor.milestoneId).toBe("");
    // but events and packs from successful endpoints are present
    expect(result.current.events).toHaveLength(2);
    expect(result.current.packs).toHaveLength(1);
  });

  it("events endpoint fails, state succeeds — cursor is populated, events stay empty", async () => {
    vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue(stateFixture);
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockResolvedValue(null);
    vi.spyOn(domainClient, "getWorkspaceEvents").mockRejectedValueOnce(new ApiError(500, "Error"));
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue([]);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.cursor.phaseId).toBe(stateFixture.position!.phaseId);
    expect(result.current.events).toEqual([]);
  });

  it("handoff endpoint fails — handoff stays null, other data unaffected", async () => {
    vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue(stateFixture);
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockRejectedValueOnce(new ApiError(404, "Not Found"));
    vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue(eventsFixture);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue(packsFixture);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.handoff).toBeNull();
    expect(result.current.cursor.phaseId).toBe(stateFixture.position!.phaseId);
    expect(result.current.events).toHaveLength(2);
    expect(result.current.packs).toHaveLength(1);
  });
});

// ── 4.5 Pause/resume state reflects handoff.json existence ───────────────────
//
// ContinuityPage seeds its systemStatus from useContinuityState.handoff:
//   - handoff non-null  → status initialises to "paused"
//   - handoff null      → status initialises to "idle"
//
// These tests verify the hook layer that drives that decision.

describe("4.5 – useContinuityState.handoff drives pause/resume state", () => {
  it("handoff is non-null when handoff.json exists — workspace is considered paused", async () => {
    vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue(stateFixture);
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockResolvedValue(handoffFixture);
    vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue([]);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue([]);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // Non-null handoff signals that handoff.json exists → workspace paused
    expect(result.current.handoff).not.toBeNull();
    expect(result.current.handoff!.nextCommand).toBe("resume-work");
    expect(result.current.handoff!.inFlight.task).toBe(handoffFixture.inFlightTask);
  });

  it("handoff is null when handoff.json is absent (404) — workspace is not paused", async () => {
    vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue(stateFixture);
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockResolvedValue(null);
    vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue([]);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue([]);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // Null handoff signals that handoff.json does not exist → workspace not paused
    expect(result.current.handoff).toBeNull();
  });

  it("handoff is null when the handoff endpoint returns 404 — workspace not paused", async () => {
    vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue(stateFixture);
    vi.spyOn(domainClient, "getWorkspaceHandoff").mockRejectedValueOnce(new ApiError(404, "Not Found"));
    vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue([]);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue([]);

    const { result } = renderHook(() => useContinuityState(), { wrapper: WorkspaceProvider });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // 404 from handoff endpoint means handoff.json absent → null → not paused
    expect(result.current.handoff).toBeNull();
  });

  it("handoff transitions from non-null to null after workspace switch — reflects resume", async () => {
    const stateSpy   = vi.spyOn(domainClient, "getWorkspaceState").mockResolvedValue(stateFixture);
    const handoffSpy = vi.spyOn(domainClient, "getWorkspaceHandoff")
      .mockResolvedValueOnce(handoffFixture) // first workspace: paused
      .mockResolvedValueOnce(null);           // second workspace: not paused
    vi.spyOn(domainClient, "getWorkspaceEvents").mockResolvedValue([]);
    vi.spyOn(domainClient, "getWorkspaceStatePacks").mockResolvedValue([]);

    const { result } = renderHook(withWorkspaceControl(() => useContinuityState()), {
      wrapper: WorkspaceProvider,
    });

    await waitFor(() => expect(result.current.hook.isLoading).toBe(false));
    expect(result.current.hook.handoff).not.toBeNull();

    act(() => result.current.setActiveWorkspaceId(NEXT_WS));

    await waitFor(() => expect(stateSpy).toHaveBeenCalledWith(NEXT_WS));
    await waitFor(() => expect(handoffSpy).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(result.current.hook.handoff).toBeNull());
  });
});
