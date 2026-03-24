/**
 * 7.7 – Hook shape verification and activeWorkspaceId re-fetch tests.
 *
 * (a) Return shapes — every workspace-scoped hook exposes the documented
 *     keys with the expected primitive types so existing consumers are not
 *     broken when the data layer moves from stubs to the real domain API.
 *
 * (b) Re-fetch on workspace switch — changing `activeWorkspaceId` via
 *     WorkspaceContext causes each workspace-scoped hook (useMilestones,
 *     usePhases, useTasks, useProjectSpec, useFileSystem) to issue a new
 *     API call using the new workspace ID.
 */

import { describe, it, expect, vi, afterEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { WorkspaceProvider, useWorkspaceContext } from "../../context/WorkspaceContext";
import { domainClient } from "../../utils/apiClient";
import {
  useMilestones,
  usePhases,
  useTasks,
  useProjectSpec,
  useFileSystem,
} from "../useAosData";

// ── Helper ────────────────────────────────────────────────────────────────────

/**
 * Returns a custom hook that exposes both the inner hook's result and
 * `setActiveWorkspaceId` from WorkspaceContext.  This allows a single
 * `renderHook` call (wrapped in WorkspaceProvider) to both observe the hook's
 * output and switch the active workspace ID.
 */
function withWorkspaceControl<T>(innerHook: () => T) {
  return function useWithControl() {
    const { setActiveWorkspaceId } = useWorkspaceContext();
    return { hook: innerHook(), setActiveWorkspaceId };
  };
}

afterEach(() => {
  vi.restoreAllMocks();
});

// WorkspaceProvider initialises activeWorkspaceId to "my-app" by default.
// These tests seed a GUID workspace ID so the GUID-only API routes are exercised.
const INITIAL_WS = "550e8400-e29b-41d4-a716-446655440000";
const NEXT_WS    = "550e8400-e29b-41d4-a716-446655440001";

// ── (a) Return shape verification ─────────────────────────────────────────────

describe("7.7a – Hook return shapes are stable for existing consumers", () => {
  it("useMilestones returns { milestones: Milestone[], isLoading: boolean }", async () => {
    const { result } = renderHook(() => useMilestones(), {
      wrapper: WorkspaceProvider,
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(Array.isArray(result.current.milestones)).toBe(true);
    expect(typeof result.current.isLoading).toBe("boolean");
    expect(Object.keys(result.current).sort()).toEqual(["isLoading", "milestones"]);
  });

  it("usePhases returns { phases: Phase[], isLoading: boolean }", async () => {
    const { result } = renderHook(() => usePhases(), {
      wrapper: WorkspaceProvider,
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(Array.isArray(result.current.phases)).toBe(true);
    expect(typeof result.current.isLoading).toBe("boolean");
    expect(Object.keys(result.current).sort()).toEqual(["isLoading", "phases"]);
  });

  it("useTasks returns { tasks: Task[], isLoading: boolean }", async () => {
    const { result } = renderHook(() => useTasks(), {
      wrapper: WorkspaceProvider,
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(Array.isArray(result.current.tasks)).toBe(true);
    expect(typeof result.current.isLoading).toBe("boolean");
    expect(Object.keys(result.current).sort()).toEqual(["isLoading", "tasks"]);
  });

  it("useProjectSpec returns { spec: ProjectSpec, isLoading: boolean }", async () => {
    const { result } = renderHook(() => useProjectSpec(), {
      wrapper: WorkspaceProvider,
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.spec).toBeDefined();
    expect(result.current.spec).not.toBeNull();
    expect(typeof result.current.spec).toBe("object");
    expect(typeof result.current.isLoading).toBe("boolean");
    expect(Object.keys(result.current).sort()).toEqual(["isLoading", "spec"]);
  });

  it("useProjectSpec.spec has all documented string/array fields (empty-default shape)", async () => {
    const { result } = renderHook(() => useProjectSpec(), {
      wrapper: WorkspaceProvider,
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const { spec } = result.current;
    expect(typeof spec.name).toBe("string");
    expect(typeof spec.description).toBe("string");
    expect(typeof spec.version).toBe("string");
    expect(typeof spec.owner).toBe("string");
    expect(typeof spec.repo).toBe("string");
    expect(Array.isArray(spec.milestones)).toBe(true);
    expect(Array.isArray(spec.tags)).toBe(true);
    expect(Array.isArray(spec.constraints)).toBe(true);
  });

  it("useFileSystem returns { fileSystem: [], node: null|object, findNode: fn, isLoading: boolean }", async () => {
    const { result } = renderHook(() => useFileSystem(), {
      wrapper: WorkspaceProvider,
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(Array.isArray(result.current.fileSystem)).toBe(true);
    expect(typeof result.current.findNode).toBe("function");
    expect(typeof result.current.isLoading).toBe("boolean");
    expect(Object.keys(result.current).sort()).toEqual([
      "fileSystem",
      "findNode",
      "isLoading",
      "node",
    ]);
  });

  it("useFileSystem.findNode returns null for a non-existent path (shape is callable)", async () => {
    const { result } = renderHook(() => useFileSystem(), {
      wrapper: WorkspaceProvider,
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.findNode(["no", "such", "path"])).toBeNull();
  });
});

// ── (b) Re-fetch on workspace switch ──────────────────────────────────────────

describe("7.7b – Switching activeWorkspaceId triggers re-fetch in workspace-scoped hooks", () => {
  it("useMilestones re-fetches with the new workspace ID", async () => {
    const spy = vi.spyOn(domainClient, "getMilestones").mockResolvedValue([]);

    const { result } = renderHook(withWorkspaceControl(() => useMilestones()), {
      wrapper: WorkspaceProvider,
    });

    act(() => result.current.setActiveWorkspaceId(INITIAL_WS));
    await waitFor(() => expect(result.current.hook.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith(INITIAL_WS);

    act(() => result.current.setActiveWorkspaceId(NEXT_WS));

    await waitFor(() => expect(spy).toHaveBeenCalledWith(NEXT_WS));
    expect(spy).toHaveBeenCalledTimes(2);
  });

  it("usePhases re-fetches with the new workspace ID", async () => {
    const spy = vi.spyOn(domainClient, "getPhases").mockResolvedValue([]);

    const { result } = renderHook(withWorkspaceControl(() => usePhases()), {
      wrapper: WorkspaceProvider,
    });

    act(() => result.current.setActiveWorkspaceId(INITIAL_WS));
    await waitFor(() => expect(result.current.hook.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith(INITIAL_WS);

    act(() => result.current.setActiveWorkspaceId(NEXT_WS));

    await waitFor(() => expect(spy).toHaveBeenCalledWith(NEXT_WS));
    expect(spy).toHaveBeenCalledTimes(2);
  });

  it("useTasks re-fetches with the new workspace ID", async () => {
    const spy = vi.spyOn(domainClient, "getTasks").mockResolvedValue([]);

    const { result } = renderHook(withWorkspaceControl(() => useTasks()), {
      wrapper: WorkspaceProvider,
    });

    act(() => result.current.setActiveWorkspaceId(INITIAL_WS));
    await waitFor(() => expect(result.current.hook.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith(INITIAL_WS, expect.anything());

    act(() => result.current.setActiveWorkspaceId(NEXT_WS));

    await waitFor(() => expect(spy).toHaveBeenCalledWith(NEXT_WS, expect.anything()));
    expect(spy).toHaveBeenCalledTimes(2);
  });

  it("useProjectSpec re-fetches with the new workspace ID", async () => {
    const mockSpec = {
      name: "test",
      description: "",
      version: "1.0",
      owner: "",
      repo: "",
      milestones: [],
      createdAt: "",
      updatedAt: "",
      tags: [],
      constraints: [],
    };
    const spy = vi.spyOn(domainClient, "getProjectSpec").mockResolvedValue(mockSpec);

    const { result } = renderHook(withWorkspaceControl(() => useProjectSpec()), {
      wrapper: WorkspaceProvider,
    });

    act(() => result.current.setActiveWorkspaceId(INITIAL_WS));
    await waitFor(() => expect(result.current.hook.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith(INITIAL_WS);

    act(() => result.current.setActiveWorkspaceId(NEXT_WS));

    await waitFor(() => expect(spy).toHaveBeenCalledWith(NEXT_WS));
    expect(spy).toHaveBeenCalledTimes(2);
  });

  it("useFileSystem re-fetches with the new workspace ID", async () => {
    const mockNode = { name: "root", path: "/", type: "directory", children: [] };
    const spy = vi
      .spyOn(domainClient, "getWorkspaceFiles")
      .mockResolvedValue(mockNode);

    const { result } = renderHook(withWorkspaceControl(() => useFileSystem()), {
      wrapper: WorkspaceProvider,
    });

    act(() => result.current.setActiveWorkspaceId(INITIAL_WS));
    await waitFor(() => expect(result.current.hook.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith(INITIAL_WS, undefined);

    act(() => result.current.setActiveWorkspaceId(NEXT_WS));

    await waitFor(() => expect(spy).toHaveBeenCalledWith(NEXT_WS, undefined));
    expect(spy).toHaveBeenCalledTimes(2);
  });

  it("useFileSystem passes explicit path on re-fetch", async () => {
    const mockNode = { name: "src", path: "/src", type: "directory", children: [] };
    const spy = vi
      .spyOn(domainClient, "getWorkspaceFiles")
      .mockResolvedValue(mockNode);

    const { result } = renderHook(
      withWorkspaceControl(() => useFileSystem("src")),
      { wrapper: WorkspaceProvider },
    );

    act(() => result.current.setActiveWorkspaceId(INITIAL_WS));
    await waitFor(() => expect(result.current.hook.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith(INITIAL_WS, "src");

    act(() => result.current.setActiveWorkspaceId(NEXT_WS));

    await waitFor(() => expect(spy).toHaveBeenCalledWith(NEXT_WS, "src"));
    expect(spy).toHaveBeenCalledTimes(2);
  });

  it("hooks skip the fetch and settle immediately when activeWorkspaceId changes to empty string", async () => {
    const spy = vi.spyOn(domainClient, "getMilestones").mockResolvedValue([]);

    const { result } = renderHook(withWorkspaceControl(() => useMilestones()), {
      wrapper: WorkspaceProvider,
    });

    // Wait for the initial fetch to complete
    await waitFor(() => expect(result.current.hook.isLoading).toBe(false));
    spy.mockClear(); // only track calls made after the workspace switch

    // Switch to empty string — the early-return guard in useMilestones should fire
    act(() => result.current.setActiveWorkspaceId(""));

    await waitFor(() => expect(result.current.hook.isLoading).toBe(false));

    // getMilestones must NOT have been called with an empty workspace ID
    expect(spy).not.toHaveBeenCalledWith("");
    expect(spy).toHaveBeenCalledTimes(0);
  });
});
