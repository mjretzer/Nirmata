/**
 * Focused API payload mapping tests for useCodebaseIntel and useOrchestratorState.
 *
 * These tests mock the API client to verify that the hooks map backend DTOs
 * to the expected frontend types correctly, per the Phase 7 OpenSpec requirements.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { useCodebaseIntel, useOrchestratorState } from "../useAosData";

// ── useCodebaseIntel mapping ───────────────────────────────────────────────

describe("useCodebaseIntel — API payload mapping", () => {
  afterEach(() => vi.restoreAllMocks());

  it("maps artifact id and path from the backend DTO", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getWorkspaceCodebase").mockResolvedValue({
      artifacts: [
        {
          id: "map",
          type: "map",
          status: "ready",
          path: ".aos/codebase/map.json",
          lastUpdated: "2026-03-01T00:00:00Z",
        },
      ],
      languages: [],
      stack: [],
    });

    const { result } = renderHook(() => useCodebaseIntel());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.artifacts).toHaveLength(1);
    const artifact = result.current.artifacts[0];
    expect(artifact.id).toBe("map");
    expect(artifact.path).toBe(".aos/codebase/map.json");
  });

  it("maps artifact status from the backend DTO", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getWorkspaceCodebase").mockResolvedValue({
      artifacts: [
        { id: "stack", type: "stack", status: "stale", path: ".aos/codebase/stack.json", lastUpdated: null },
      ],
      languages: [],
      stack: [],
    });

    const { result } = renderHook(() => useCodebaseIntel());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.artifacts[0].status).toBe("stale");
  });

  it("classifies cache artifacts (symbols, file-graph) as type 'cache'", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getWorkspaceCodebase").mockResolvedValue({
      artifacts: [
        { id: "symbols", type: "symbols", status: "ready", path: ".aos/codebase/cache/symbols.json", lastUpdated: null },
        { id: "file-graph", type: "file-graph", status: "missing", path: ".aos/codebase/cache/file-graph.json", lastUpdated: null },
      ],
      languages: [],
      stack: [],
    });

    const { result } = renderHook(() => useCodebaseIntel());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.artifacts).toHaveLength(2);
    expect(result.current.artifacts[0].type).toBe("cache");
    expect(result.current.artifacts[1].type).toBe("cache");
  });

  it("classifies non-cache artifacts as type 'intel'", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getWorkspaceCodebase").mockResolvedValue({
      artifacts: [
        { id: "map", type: "map", status: "ready", path: ".aos/codebase/map.json", lastUpdated: null },
        { id: "architecture", type: "architecture", status: "ready", path: ".aos/codebase/architecture.json", lastUpdated: null },
      ],
      languages: [],
      stack: [],
    });

    const { result } = renderHook(() => useCodebaseIntel());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.artifacts.every((a) => a.type === "intel")).toBe(true);
  });

  it("maps language strings from the inventory languages array", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getWorkspaceCodebase").mockResolvedValue({
      artifacts: [],
      languages: ["C#", "TypeScript"],
      stack: [],
    });

    const { result } = renderHook(() => useCodebaseIntel());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const langNames = result.current.languages.map((l) => l.name);
    expect(langNames).toContain("C#");
    expect(langNames).toContain("TypeScript");
  });

  it("maps stack strings from the inventory stack array", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getWorkspaceCodebase").mockResolvedValue({
      artifacts: [],
      languages: [],
      stack: [".NET 10", "React 18"],
    });

    const { result } = renderHook(() => useCodebaseIntel());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const stackNames = result.current.stack.map((s) => s.name);
    expect(stackNames).toContain(".NET 10");
    expect(stackNames).toContain("React 18");
  });

  it("remains empty when the API call rejects", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getWorkspaceCodebase").mockRejectedValue(new Error("Network error"));

    const { result } = renderHook(() => useCodebaseIntel());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.artifacts).toHaveLength(0);
    expect(result.current.languages).toHaveLength(0);
    expect(result.current.stack).toHaveLength(0);
  });
});

// ── useOrchestratorState mapping ───────────────────────────────────────────

describe("useOrchestratorState — API payload mapping", () => {
  afterEach(() => vi.restoreAllMocks());

  it("sets blockedGate when gate.runnable is false", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getOrchestratorGate").mockResolvedValue({
      taskId: "TSK-000001",
      taskTitle: "Add service layer",
      runnable: false,
      recommendedAction: "execute-plan",
      checks: [
        { id: "plan.exists", kind: "dependency", label: "Task plan exists", detail: "plan.json is missing", status: "fail" },
      ],
    });
    vi.spyOn(domainClient, "getOrchestratorTimeline").mockResolvedValue({ steps: [] });

    const { result } = renderHook(() => useOrchestratorState());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.blockedGate.runnable).toBe(false);
    expect(result.current.blockedGate.taskId).toBe("TSK-000001");
    expect(result.current.blockedGate.recommendedAction).toBe("execute-plan");
  });

  it("sets runnableGate when gate.runnable is true", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getOrchestratorGate").mockResolvedValue({
      taskId: "TSK-000002",
      taskTitle: "Write tests",
      runnable: true,
      recommendedAction: null,
      checks: [
        { id: "plan.exists", kind: "dependency", label: "Task plan exists", detail: "plan.json is present", status: "pass" },
      ],
    });
    vi.spyOn(domainClient, "getOrchestratorTimeline").mockResolvedValue({ steps: [] });

    const { result } = renderHook(() => useOrchestratorState());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.runnableGate.runnable).toBe(true);
    expect(result.current.runnableGate.taskId).toBe("TSK-000002");
  });

  it("maps gate checks to the frontend check shape", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getOrchestratorGate").mockResolvedValue({
      taskId: "TSK-000001",
      taskTitle: "First task",
      runnable: false,
      recommendedAction: "execute-plan",
      checks: [
        { id: "evidence.run", kind: "evidence", label: "Execution evidence exists", detail: "No evidence found", status: "fail" },
        { id: "uat.status", kind: "uat", label: "UAT verification", detail: "UAT not recorded", status: "fail" },
      ],
    });
    vi.spyOn(domainClient, "getOrchestratorTimeline").mockResolvedValue({ steps: [] });

    const { result } = renderHook(() => useOrchestratorState());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const checks = result.current.blockedGate.checks;
    expect(checks).toHaveLength(2);
    expect(checks[0].id).toBe("evidence.run");
    expect(checks[0].kind).toBe("evidence");
    expect(checks[0].status).toBe("fail");
    expect(checks[1].id).toBe("uat.status");
    expect(checks[1].kind).toBe("uat");
  });

  it("maps timeline steps from the backend DTO", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getOrchestratorGate").mockResolvedValue({
      taskId: null,
      taskTitle: null,
      runnable: false,
      recommendedAction: "new-project",
      checks: [],
    });
    vi.spyOn(domainClient, "getOrchestratorTimeline").mockResolvedValue({
      steps: [
        { id: "PH-0001", label: "Foundation", status: "completed" },
        { id: "PH-0002", label: "Core Logic", status: "active" },
        { id: "PH-0003", label: "Polish", status: "pending" },
      ],
    });

    const { result } = renderHook(() => useOrchestratorState());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.timelineTemplate).toHaveLength(3);
    expect(result.current.timelineTemplate[0].id).toBe("PH-0001");
    expect(result.current.timelineTemplate[0].status).toBe("completed");
    expect(result.current.timelineTemplate[1].status).toBe("active");
    expect(result.current.timelineTemplate[2].status).toBe("pending");
  });

  it("maps timeline step labels from the backend DTO", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getOrchestratorGate").mockResolvedValue({
      taskId: null,
      taskTitle: null,
      runnable: false,
      recommendedAction: "new-project",
      checks: [],
    });
    vi.spyOn(domainClient, "getOrchestratorTimeline").mockResolvedValue({
      steps: [
        { id: "PH-0001", label: "Foundation and Bootstrap", status: "completed" },
      ],
    });

    const { result } = renderHook(() => useOrchestratorState());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.timelineTemplate[0].label).toBe("Foundation and Bootstrap");
  });

  it("keeps empty timeline when API returns no steps", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getOrchestratorGate").mockResolvedValue({
      taskId: null,
      taskTitle: null,
      runnable: false,
      recommendedAction: "new-project",
      checks: [],
    });
    vi.spyOn(domainClient, "getOrchestratorTimeline").mockResolvedValue({ steps: [] });

    const { result } = renderHook(() => useOrchestratorState());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // When steps is empty, the hook falls back to the default template (defaultTimelineSteps).
    // The template has 6 entries and all are pending.
    expect(result.current.timelineTemplate.length).toBeGreaterThan(0);
    expect(result.current.timelineTemplate.every((s) => s.status === "pending")).toBe(true);
  });

  it("remains in default state when both API calls reject", async () => {
    const { domainClient } = await import("../../utils/apiClient");
    vi.spyOn(domainClient, "getOrchestratorGate").mockRejectedValue(new Error("Offline"));
    vi.spyOn(domainClient, "getOrchestratorTimeline").mockRejectedValue(new Error("Offline"));

    const { result } = renderHook(() => useOrchestratorState());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // Defaults: blockedGate has runnable=false, runnableGate has runnable=true.
    expect(result.current.blockedGate.runnable).toBe(false);
    expect(result.current.runnableGate.runnable).toBe(true);
  });
});
