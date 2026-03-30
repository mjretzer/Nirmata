/**
 * Tests for useAosData hooks — validates the thin service layer
 * exposes the correct shape and loading state.
 *
 * All hooks start with empty initial state and resolve via the API.
 * In the test environment (no real backend) hooks settle to empty
 * arrays / empty objects after the failed fetch resolves, so assertions
 * focus on shape and loading flags rather than specific data values.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { createElement, type ReactNode } from "react";
import {
  useWorkspace,
  useWorkspaces,
  useWorkspaceInit,
  usePhases,
  useTasks,
  useRuns,
  useIssues,
  useMilestones,
  useTaskPlans,
  useProjectSpec,
  useCheckpoints,
  useContinuityState,
  useFileSystem,
  useVerificationState,
  useUatSummary,
  useHostConsole,
  useDiagnostics,
  useCodebaseIntel,
  useOrchestratorState,
  useAosCommand,
  useEngineConnection,
} from "../useAosData";
import { daemonClient } from "../../utils/apiClient";
import { WorkspaceProvider } from "../../context/WorkspaceContext";

const GUID_WORKSPACE_ID = "550e8400-e29b-41d4-a716-446655440000";

function guidWorkspaceWrapper({ children }: { children: ReactNode }) {
  return createElement(WorkspaceProvider, { initialWorkspaceId: GUID_WORKSPACE_ID, children });
}

describe("useWorkspace", () => {
  afterEach(() => vi.restoreAllMocks());

  it("skips the API for non-GUID ids", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    const spy = vi.spyOn(domainClient, "getWorkspace");

    const { result } = renderHook(() => useWorkspace("my-app"));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(spy).not.toHaveBeenCalled();
    expect(result.current.notFound).toBe(false);
  });

  it("returns a workspace and isLoading=false when no id given", () => {
    const { result } = renderHook(() => useWorkspace());
    expect(result.current.isLoading).toBe(false);
    expect(result.current.workspace).toBeDefined();
  });

  it("returns notFound=false by default", () => {
    const { result } = renderHook(() => useWorkspace());
    expect(result.current.notFound).toBe(false);
  });

  it("returns a workspace for unknown id", async () => {
    const { result } = renderHook(() => useWorkspace("nonexistent-ws-999"));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.workspace).toBeDefined();
  });

  it("uses the active workspace id from context when a route token is not a GUID", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    const spy = vi.spyOn(domainClient, "getWorkspace").mockResolvedValue({
      id: GUID_WORKSPACE_ID,
      name: "Workspace Alpha",
      path: "C:\\Users\\James Lestler\\Desktop\\Projects\\Nirmata",
      status: "initialized",
      lastModified: "2026-01-01T00:00:00Z",
    });

    const { result } = renderHook(() => useWorkspace("workspace-alpha"), {
      wrapper: guidWorkspaceWrapper,
    });

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(spy).toHaveBeenCalledWith(GUID_WORKSPACE_ID);
    expect(result.current.workspace.repoRoot).toBe("C:\\Users\\James Lestler\\Desktop\\Projects\\Nirmata");
    expect(result.current.workspace.projectName).toBe("Workspace Alpha");
  });

  it("returns notFound=true when API returns 404", async () => {
    const { domainClient, ApiError } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getWorkspace").mockRejectedValue(new ApiError(404, "Not Found"));

    const { result } = renderHook(() => useWorkspace("550e8400-e29b-41d4-a716-446655440000"));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.notFound).toBe(true);
  });

  it("returns notFound=false when API returns a non-404 error", async () => {
    const { domainClient, ApiError } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getWorkspace").mockRejectedValue(new ApiError(500, "Internal Server Error"));

    const { result } = renderHook(() => useWorkspace("550e8400-e29b-41d4-a716-446655440001"));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.notFound).toBe(false);
  });
});

describe("useWorkspaceInit", () => {
  afterEach(() => vi.restoreAllMocks());

  it("forwards the selected root path as the daemon working directory", async () => {
    const submitSpy = vi.spyOn(daemonClient, "submitCommand").mockResolvedValue({
      ok: true,
      output: "Created: C:\\Users\\James Lestler\\Desktop\\Projects\\test project\\.aos",
    });

    const { result } = renderHook(() => useWorkspaceInit("550e8400-e29b-41d4-a716-446655440000"));

    await act(async () => {
      await result.current.init("C:\\Users\\James Lestler\\Desktop\\Projects\\test project  ");
    });

    expect(submitSpy).toHaveBeenCalledWith({
      argv: ["aos", "init"],
      workingDirectory: "C:\\Users\\James Lestler\\Desktop\\Projects\\test project",
    });
    expect(result.current.initResult).toEqual({
      ok: true,
      aosDir: "C:\\Users\\James Lestler\\Desktop\\Projects\\test project\\.aos",
    });
  });
});

describe("useWorkspaces", () => {
  it("returns workspaces array and loading state", async () => {
    const { result } = renderHook(() => useWorkspaces());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Array.isArray(result.current.workspaces)).toBe(true);
  });
});

describe("useTasks", () => {
  it("returns tasks array after loading", async () => {
    const { result } = renderHook(() => useTasks());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Array.isArray(result.current.tasks)).toBe(true);
  });

  it("filters by phaseId (passes on any array)", async () => {
    const { result } = renderHook(() => useTasks({ phaseId: "PH-0001" }));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    result.current.tasks.forEach((t) => {
      expect(t.phaseId).toBe("PH-0001");
    });
  });

  it("filters by status (passes on any array)", async () => {
    const { result } = renderHook(() => useTasks({ status: "completed" }));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    result.current.tasks.forEach((t) => {
      expect(t.status).toBe("completed");
    });
  });
});

describe("usePhases", () => {
  it("returns phases array after loading", async () => {
    const { result } = renderHook(() => usePhases());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Array.isArray(result.current.phases)).toBe(true);
  });

  it("filters by milestoneId (passes on any array)", async () => {
    const { result } = renderHook(() => usePhases("MS-0001"));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    result.current.phases.forEach((p) => {
      expect(p.milestoneId).toBe("MS-0001");
    });
  });
});

describe("useRuns", () => {
  it("returns runs array after loading", async () => {
    const { result } = renderHook(() => useRuns());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Array.isArray(result.current.runs)).toBe(true);
  });

  describe("status normalisation", () => {
    afterEach(() => vi.restoreAllMocks());

    it("maps backend 'pass' to 'success'", async () => {
      const { domainClient } = await import("../../utils/apiClient");
      vi.spyOn(domainClient, "getWorkspaceRuns").mockResolvedValue([
        { id: "RUN-001", taskId: "TSK-000001", status: "pass", timestamp: "2026-01-01T00:00:00Z" },
      ]);

      const { result } = renderHook(() => useRuns());
      await waitFor(() => expect(result.current.isLoading).toBe(false));

      expect(result.current.runs).toHaveLength(1);
      expect(result.current.runs[0].status).toBe("success");
    });

    it("maps backend 'fail' to 'failed'", async () => {
      const { domainClient } = await import("../../utils/apiClient");
      vi.spyOn(domainClient, "getWorkspaceRuns").mockResolvedValue([
        { id: "RUN-002", taskId: null, status: "fail", timestamp: "2026-01-01T00:00:00Z" },
      ]);

      const { result } = renderHook(() => useRuns());
      await waitFor(() => expect(result.current.isLoading).toBe(false));

      expect(result.current.runs[0].status).toBe("failed");
    });

    it("maps backend 'success' to 'success'", async () => {
      const { domainClient } = await import("../../utils/apiClient");
      vi.spyOn(domainClient, "getWorkspaceRuns").mockResolvedValue([
        { id: "RUN-003", taskId: null, status: "success", timestamp: "2026-01-01T00:00:00Z" },
      ]);

      const { result } = renderHook(() => useRuns());
      await waitFor(() => expect(result.current.isLoading).toBe(false));

      expect(result.current.runs[0].status).toBe("success");
    });

    it("maps backend 'running' to 'running'", async () => {
      const { domainClient } = await import("../../utils/apiClient");
      vi.spyOn(domainClient, "getWorkspaceRuns").mockResolvedValue([
        { id: "RUN-004", taskId: null, status: "running", timestamp: "2026-01-01T00:00:00Z" },
      ]);

      const { result } = renderHook(() => useRuns());
      await waitFor(() => expect(result.current.isLoading).toBe(false));

      expect(result.current.runs[0].status).toBe("running");
    });

    it("maps unknown status to 'failed'", async () => {
      const { domainClient } = await import("../../utils/apiClient");
      vi.spyOn(domainClient, "getWorkspaceRuns").mockResolvedValue([
        { id: "RUN-005", taskId: null, status: null, timestamp: "2026-01-01T00:00:00Z" },
      ]);

      const { result } = renderHook(() => useRuns());
      await waitFor(() => expect(result.current.isLoading).toBe(false));

      expect(result.current.runs[0].status).toBe("failed");
    });

    it("maps run fields correctly from WorkspaceRunSummary", async () => {
      const { domainClient } = await import("../../utils/apiClient");
      vi.spyOn(domainClient, "getWorkspaceRuns").mockResolvedValue([
        { id: "RUN-006", taskId: "TSK-000007", status: "pass", timestamp: "2026-03-01T12:00:00Z" },
      ]);

      const { result } = renderHook(() => useRuns());
      await waitFor(() => expect(result.current.isLoading).toBe(false));

      const run = result.current.runs[0];
      expect(run.id).toBe("RUN-006");
      expect(run.taskId).toBe("TSK-000007");
      expect(run.startTime).toBe("2026-03-01T12:00:00Z");
      expect(Array.isArray(run.artifacts)).toBe(true);
      expect(Array.isArray(run.logs)).toBe(true);
      expect(Array.isArray(run.changedFiles)).toBe(true);
    });
  });
});

describe("useIssues", () => {
  it("returns issues array after loading", async () => {
    const { result } = renderHook(() => useIssues());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Array.isArray(result.current.issues)).toBe(true);
  });
});

describe("useMilestones", () => {
  it("returns milestones array after loading", async () => {
    const { result } = renderHook(() => useMilestones());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Array.isArray(result.current.milestones)).toBe(true);
  });
});

describe("useTaskPlans", () => {
  it("returns task plans array after loading", async () => {
    const { result } = renderHook(() => useTaskPlans());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Array.isArray(result.current.plans)).toBe(true);
  });
});

describe("useProjectSpec", () => {
  it("returns a spec object after loading", async () => {
    const { result } = renderHook(() => useProjectSpec());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.spec).toBeDefined();
    expect(typeof result.current.spec.name).toBe("string");
  });
});

describe("useCheckpoints", () => {
  it("returns checkpoints array after loading", async () => {
    const { result } = renderHook(() => useCheckpoints());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Array.isArray(result.current.checkpoints)).toBe(true);
  });
});

describe("useContinuityState", () => {
  it("returns continuity state, handoff, events, packs", () => {
    const { result } = renderHook(() => useContinuityState());
    expect(result.current.state).toBeDefined();
    expect(result.current.events).toBeDefined();
    expect(result.current.packs).toBeDefined();
  });
});

describe("useFileSystem", () => {
  it("returns a file system array and findNode function", async () => {
    const { result } = renderHook(() => useFileSystem());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Array.isArray(result.current.fileSystem)).toBe(true);
    expect(typeof result.current.findNode).toBe("function");
  });

  it("findNode returns null for a path that does not exist", async () => {
    const { result } = renderHook(() => useFileSystem());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    const node = result.current.findNode(["nonexistent-path-xyz"]);
    expect(node).toBeNull();
  });
});

describe("useUatSummary", () => {
  it("returns empty summary and isLoading=false when no workspace", async () => {
    const { result } = renderHook(() => useUatSummary());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Array.isArray(result.current.uatSummary.records)).toBe(true);
    expect(Array.isArray(result.current.uatSummary.taskSummaries)).toBe(true);
    expect(Array.isArray(result.current.uatSummary.phaseSummaries)).toBe(true);
  });
});

describe("useVerificationState", () => {
  it("returns derived verification context after loading", async () => {
    const { result } = renderHook(() => useVerificationState());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.verification.uatItems).toBeDefined();
    expect(result.current.verification.fixItems).toBeDefined();
    expect(typeof result.current.verification.totalUAT).toBe("number");
  });
});

// ── Host / Diagnostics / Codebase hooks ───────────────────────

describe("useHostConsole", () => {
  it("returns logs and surfaces arrays after loading", async () => {
    const { result } = renderHook(() => useHostConsole());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Array.isArray(result.current.logs)).toBe(true);
    expect(Array.isArray(result.current.surfaces)).toBe(true);
  });
});

describe("useDiagnostics", () => {
  it("returns all diagnostic arrays after loading", async () => {
    const { result } = renderHook(() => useDiagnostics());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Array.isArray(result.current.logs)).toBe(true);
    expect(Array.isArray(result.current.artifacts)).toBe(true);
    expect(Array.isArray(result.current.locks)).toBe(true);
    expect(Array.isArray(result.current.cacheEntries)).toBe(true);
  });

  it("cache entries all have stale flag when present", async () => {
    const { result } = renderHook(() => useDiagnostics());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    result.current.cacheEntries.forEach((entry) => {
      expect(typeof entry.stale).toBe("boolean");
    });
  });
});

describe("useCodebaseIntel", () => {
  it("returns artifacts, languages, and stack arrays after loading", async () => {
    const { result } = renderHook(() => useCodebaseIntel());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(Array.isArray(result.current.artifacts)).toBe(true);
    expect(Array.isArray(result.current.languages)).toBe(true);
    expect(Array.isArray(result.current.stack)).toBe(true);
  });
});

// ── AosCommand / EngineConnection (Daemon) ────────────────────

describe("useAosCommand", () => {
  afterEach(() => vi.restoreAllMocks());

  it("starts with isRunning=false and lastResult=null", () => {
    const { result } = renderHook(() => useAosCommand());
    expect(result.current.isRunning).toBe(false);
    expect(result.current.lastResult).toBeNull();
  });

  it("exposes an execute function", () => {
    const { result } = renderHook(() => useAosCommand());
    expect(typeof result.current.execute).toBe("function");
  });

  it("sets isRunning=false after execute resolves (no backend)", async () => {
    const { result } = renderHook(() => useAosCommand());
    await act(async () => {
      await result.current.execute(["aos", "status"]);
    });
    expect(result.current.isRunning).toBe(false);
  });

  it("sets lastResult after execute completes (no backend returns failure shape)", async () => {
    const { result } = renderHook(() => useAosCommand());
    await act(async () => {
      await result.current.execute(["aos", "status"]);
    });
    expect(result.current.lastResult).not.toBeNull();
    expect(typeof result.current.lastResult!.ok).toBe("boolean");
    expect(typeof result.current.lastResult!.output).toBe("string");
  });

  it("returns ok:false output:'Command failed' when daemon is unreachable", async () => {
    const { result } = renderHook(() => useAosCommand());
    let returnValue: { ok: boolean; output: string } | undefined;
    await act(async () => {
      returnValue = await result.current.execute(["aos", "status"]);
    });
    expect(returnValue?.ok).toBe(false);
    expect(returnValue?.output).toBe("Command failed");
  });
});

describe("useEngineConnection", () => {
  afterEach(() => vi.restoreAllMocks());

  it("starts with isTesting=false, isSaving=false, lastPing=null", () => {
    const { result } = renderHook(() => useEngineConnection());
    expect(result.current.isTesting).toBe(false);
    expect(result.current.isSaving).toBe(false);
    expect(result.current.lastPing).toBeNull();
  });

  it("exposes test and save functions", () => {
    const { result } = renderHook(() => useEngineConnection());
    expect(typeof result.current.test).toBe("function");
    expect(typeof result.current.save).toBe("function");
  });

  it("test() returns failure shape when daemon is unreachable", async () => {
    const { result } = renderHook(() => useEngineConnection());
    let ping: { ok: boolean; version: string; latencyMs: number } | undefined;
    await act(async () => {
      ping = await result.current.test("http://localhost:19999");
    });
    expect(ping?.ok).toBe(false);
    expect(ping?.version).toBe("");
    expect(ping?.latencyMs).toBe(0);
  });

  it("test() sets lastPing after resolution", async () => {
    const { result } = renderHook(() => useEngineConnection());
    await act(async () => {
      await result.current.test("http://localhost:19999");
    });
    expect(result.current.lastPing).not.toBeNull();
    expect(typeof result.current.lastPing!.ok).toBe("boolean");
  });

  it("test() sets isTesting=false after completion", async () => {
    const { result } = renderHook(() => useEngineConnection());
    await act(async () => {
      await result.current.test("http://localhost:19999");
    });
    expect(result.current.isTesting).toBe(false);
  });

  it("save() returns ok:false when daemon is unreachable", async () => {
    const { result } = renderHook(() => useEngineConnection());
    let saveResult: { ok: boolean } | undefined;
    await act(async () => {
      saveResult = await result.current.save({
        label: "test-host",
        baseUrl: "http://localhost:19999",
        env: "/tmp/workspace",
      });
    });
    expect(saveResult?.ok).toBe(false);
  });

  it("save() sets isSaving=false after completion", async () => {
    const { result } = renderHook(() => useEngineConnection());
    await act(async () => {
      await result.current.save({ label: "h", baseUrl: "http://localhost:19999", env: "/x" });
    });
    expect(result.current.isSaving).toBe(false);
  });
});

describe("useOrchestratorState", () => {
  it("blockedGate defaults to runnable=false", async () => {
    const { result } = renderHook(() => useOrchestratorState());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.blockedGate.runnable).toBe(false);
  });

  it("runnableGate defaults to runnable=true", async () => {
    const { result } = renderHook(() => useOrchestratorState());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.runnableGate.runnable).toBe(true);
  });

  it("runnable gate has all passing checks (vacuously true when empty)", async () => {
    const { result } = renderHook(() => useOrchestratorState());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    const allPass = result.current.runnableGate.checks.every(c => c.status === "pass");
    expect(allPass).toBe(true);
  });

  it("returns gate kind metadata", () => {
    const { result } = renderHook(() => useOrchestratorState());
    expect(result.current.gateKindMeta.dependency.label).toBe("Dependency");
    expect(result.current.gateKindMeta.uat.label).toBe("UAT");
    expect(result.current.gateKindMeta.evidence.label).toBe("Evidence");
  });

  it("returns a timeline template with 6 steps", () => {
    const { result } = renderHook(() => useOrchestratorState());
    expect(result.current.timelineTemplate).toHaveLength(6);
    result.current.timelineTemplate.forEach(step => {
      expect(step.status).toBe("pending");
    });
  });
});
