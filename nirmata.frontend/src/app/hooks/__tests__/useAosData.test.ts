/**
 * Tests for useAosData hooks — validates the thin service layer
 * returns expected shapes and applies filters correctly.
 */
import { describe, it, expect } from "vitest";
import { renderHook } from "@testing-library/react";
import {
  useWorkspace,
  useWorkspaces,
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
  useHostConsole,
  useDiagnostics,
  useCodebaseIntel,
  useOrchestratorState,
} from "../useAosData";

describe("useWorkspace", () => {
  it("returns a workspace and isLoading=false", () => {
    const { result } = renderHook(() => useWorkspace());
    expect(result.current.isLoading).toBe(false);
    expect(result.current.workspace).toBeDefined();
    expect(result.current.workspace.projectName).toBeTruthy();
  });

  it("returns default workspace for unknown id", () => {
    const { result } = renderHook(() => useWorkspace("nonexistent-ws-999"));
    expect(result.current.workspace).toBeDefined();
  });
});

describe("useWorkspaces", () => {
  it("returns a non-empty array", () => {
    const { result } = renderHook(() => useWorkspaces());
    expect(result.current.workspaces.length).toBeGreaterThan(0);
  });
});

describe("useTasks", () => {
  it("returns all tasks without filters", () => {
    const { result } = renderHook(() => useTasks());
    expect(result.current.tasks.length).toBeGreaterThan(0);
    expect(result.current.isLoading).toBe(false);
  });

  it("filters by phaseId", () => {
    const { result } = renderHook(() => useTasks({ phaseId: "PH-0001" }));
    result.current.tasks.forEach((t) => {
      expect(t.phaseId).toBe("PH-0001");
    });
  });

  it("filters by status", () => {
    const { result } = renderHook(() => useTasks({ status: "completed" }));
    result.current.tasks.forEach((t) => {
      expect(t.status).toBe("completed");
    });
  });
});

describe("usePhases", () => {
  it("returns all phases without filter", () => {
    const { result } = renderHook(() => usePhases());
    expect(result.current.phases.length).toBeGreaterThan(0);
  });

  it("filters by milestoneId", () => {
    const { result } = renderHook(() => usePhases("MS-0001"));
    result.current.phases.forEach((p) => {
      expect(p.milestoneId).toBe("MS-0001");
    });
  });
});

describe("useRuns", () => {
  it("returns all runs", () => {
    const { result } = renderHook(() => useRuns());
    expect(result.current.runs.length).toBeGreaterThan(0);
  });
});

describe("useIssues", () => {
  it("returns issues", () => {
    const { result } = renderHook(() => useIssues());
    expect(result.current.issues.length).toBeGreaterThan(0);
  });
});

describe("useMilestones", () => {
  it("returns milestones", () => {
    const { result } = renderHook(() => useMilestones());
    expect(result.current.milestones.length).toBeGreaterThan(0);
  });
});

describe("useTaskPlans", () => {
  it("returns task plans", () => {
    const { result } = renderHook(() => useTaskPlans());
    expect(result.current.plans.length).toBeGreaterThan(0);
  });
});

describe("useProjectSpec", () => {
  it("returns a spec object", () => {
    const { result } = renderHook(() => useProjectSpec());
    expect(result.current.spec).toBeDefined();
    expect(result.current.spec.name).toBeTruthy();
  });
});

describe("useCheckpoints", () => {
  it("returns checkpoints", () => {
    const { result } = renderHook(() => useCheckpoints());
    expect(result.current.checkpoints).toBeDefined();
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
  it("returns a file system tree and findNode function", () => {
    const { result } = renderHook(() => useFileSystem());
    expect(result.current.fileSystem.length).toBeGreaterThan(0);
    expect(typeof result.current.findNode).toBe("function");
  });

  it("finds a node by path segments", () => {
    const { result } = renderHook(() => useFileSystem());
    const root = result.current.findNode([".aos"]);
    expect(root).not.toBeNull();
  });
});

describe("useVerificationState", () => {
  it("returns derived verification context", () => {
    const { result } = renderHook(() => useVerificationState());
    expect(result.current.isLoading).toBe(false);
    expect(result.current.verification.uatItems).toBeDefined();
    expect(result.current.verification.fixItems).toBeDefined();
    expect(result.current.verification.totalUAT).toBeGreaterThan(0);
  });
});

// ── Host / Diagnostics / Codebase hooks ───────────────────────

describe("useHostConsole", () => {
  it("returns logs and surfaces", () => {
    const { result } = renderHook(() => useHostConsole());
    expect(result.current.isLoading).toBe(false);
    expect(result.current.logs.length).toBeGreaterThan(0);
    expect(result.current.surfaces.length).toBeGreaterThan(0);
  });

  it("logs have required fields", () => {
    const { result } = renderHook(() => useHostConsole());
    const log = result.current.logs[0];
    expect(log).toHaveProperty("id");
    expect(log).toHaveProperty("ts");
    expect(log).toHaveProperty("level");
    expect(log).toHaveProperty("msg");
  });

  it("surfaces have required fields", () => {
    const { result } = renderHook(() => useHostConsole());
    const surface = result.current.surfaces[0];
    expect(surface).toHaveProperty("name");
    expect(surface).toHaveProperty("path");
    expect(typeof surface.ok).toBe("boolean");
  });
});

describe("useDiagnostics", () => {
  it("returns all diagnostic data", () => {
    const { result } = renderHook(() => useDiagnostics());
    expect(result.current.isLoading).toBe(false);
    expect(result.current.logs).toBeDefined();
    expect(result.current.artifacts).toBeDefined();
    expect(result.current.locks).toBeDefined();
    expect(result.current.cacheEntries).toBeDefined();
  });

  it("logs have error/warning counts", () => {
    const { result } = renderHook(() => useDiagnostics());
    const log = result.current.logs[0];
    expect(log).toHaveProperty("label");
    expect(typeof log.errors).toBe("number");
    expect(typeof log.warnings).toBe("number");
  });

  it("cache entries have stale flag", () => {
    const { result } = renderHook(() => useDiagnostics());
    result.current.cacheEntries.forEach((entry) => {
      expect(typeof entry.stale).toBe("boolean");
    });
  });
});

describe("useCodebaseIntel", () => {
  it("returns artifacts, languages, and stack", () => {
    const { result } = renderHook(() => useCodebaseIntel());
    expect(result.current.isLoading).toBe(false);
    expect(result.current.artifacts.length).toBeGreaterThan(0);
    expect(result.current.languages.length).toBeGreaterThan(0);
    expect(result.current.stack.length).toBeGreaterThan(0);
  });

  it("artifacts have required fields", () => {
    const { result } = renderHook(() => useCodebaseIntel());
    const artifact = result.current.artifacts[0];
    expect(artifact).toHaveProperty("id");
    expect(artifact).toHaveProperty("name");
    expect(artifact).toHaveProperty("type");
    expect(artifact).toHaveProperty("status");
    expect(artifact).toHaveProperty("path");
  });

  it("languages sum to roughly 100%", () => {
    const { result } = renderHook(() => useCodebaseIntel());
    const total = result.current.languages.reduce((sum, l) => sum + l.pct, 0);
    expect(total).toBeGreaterThan(99);
    expect(total).toBeLessThan(101);
  });
});

describe("useOrchestratorState", () => {
  it("returns blocked and runnable gate presets", () => {
    const { result } = renderHook(() => useOrchestratorState());
    expect(result.current.isLoading).toBe(false);
    expect(result.current.blockedGate.runnable).toBe(false);
    expect(result.current.runnableGate.runnable).toBe(true);
  });

  it("blocked gate has failing checks", () => {
    const { result } = renderHook(() => useOrchestratorState());
    const failCount = result.current.blockedGate.checks.filter(c => c.status === "fail").length;
    expect(failCount).toBeGreaterThan(0);
  });

  it("runnable gate has all passing checks", () => {
    const { result } = renderHook(() => useOrchestratorState());
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