/**
 * Data Access Hooks for AOS
 *
 * Thin service layer that currently returns mock data.
 * When real APIs arrive, swap the implementations here —
 * every consumer stays untouched.
 *
 * Usage:
 *   const { workspace, isLoading } = useWorkspace("my-app");
 *   const { tasks } = useTasks({ phaseId: "PH-0002" });
 */

import { useMemo, useState, useCallback } from "react";
import {
  mockWorkspace,
  mockWorkspaces,
  mockPhases,
  mockTasks,
  mockRuns,
  mockIssues,
  mockTaskPlans,
  mockMilestones,
  mockProjectSpec,
  mockCheckpoints,
  type Workspace,
  type WorkspaceSummary,
  type Phase,
  type Task,
  type Run,
  type Issue,
  type TaskPlan,
  type Milestone,
  type ProjectSpec,
  type Checkpoint,
} from "../data/mockData";
import {
  mockHandoff,
  mockEvents,
  mockPacks,
  mockContinuityState,
  type HandoffSnapshot,
  type Event,
  type ContextPack,
  type ContinuityState,
} from "../data/mockContinuityData";
import {
  mockFileSystem,
  findNodeByPath,
  type FileSystemNode,
} from "../data/mockFileSystem";
import {
  deriveVerificationState,
  type VerificationContext,
} from "../components/verification/verificationState";
import {
  mockHostLogs,
  mockApiSurfaces,
  mockDiagLogs,
  mockDiagArtifacts,
  mockDiagLocks,
  mockDiagCacheEntries,
  mockCodebaseArtifacts,
  mockLanguages,
  mockStack,
  type ServiceStatus,
  type ApiSurface,
  type HostLogLine,
  type DiagLogEntry,
  type DiagArtifactEntry,
  type DiagLockEntry,
  type DiagCacheEntry,
  type CodebaseArtifact,
  type LanguageBreakdown,
  type StackEntry,
} from "../data/mockHostData";
import {
  mockBlockedGate,
  mockRunnableGate,
  defaultTimelineSteps,
  GATE_KIND_META,
  type GateKind,
  type GateCheckStatus,
  type GateCheck,
  type NextTaskGate,
  type GateState,
  type OrchestratorMode,
  type TimelineStep,
  type OrchestratorMessage,
  type GateKindMeta,
} from "../data/mockOrchestratorData";
import {
  mockChatMessages,
  mockCommandSuggestions,
  mockQuickActions,
  type ChatMessage,
  type ChatRole,
  type AgentId,
  type ChatArtifactRef,
  type CommandSuggestion,
  type QuickAction,
} from "../data/mockChatData";

// ── Re-exported types (so consumers never import mockData directly) ──

export type {
  Workspace,
  WorkspaceSummary,
  Phase,
  Task,
  Run,
  Issue,
  TaskPlan,
  Milestone,
  ProjectSpec,
  Checkpoint,
  HandoffSnapshot,
  Event,
  ContextPack,
  ContinuityState,
  FileSystemNode,
  // Host / Daemon / Diagnostics / Codebase
  ServiceStatus,
  ApiSurface,
  HostLogLine,
  DiagLogEntry,
  DiagArtifactEntry,
  DiagLockEntry,
  DiagCacheEntry,
  CodebaseArtifact,
  LanguageBreakdown,
  StackEntry,
  // Orchestrator / Gate
  GateKind,
  GateCheckStatus,
  GateCheck,
  NextTaskGate,
  GateState,
  OrchestratorMode,
  TimelineStep,
  OrchestratorMessage,
  GateKindMeta,
  // Chat
  ChatMessage,
  ChatRole,
  AgentId,
  ChatArtifactRef,
  CommandSuggestion,
  QuickAction,
};

// ── Workspace ─────────────────────────────────────────────────

export function useWorkspace(workspaceId?: string): {
  workspace: Workspace;
  isLoading: boolean;
} {
  const workspace = useMemo(() => {
    if (!workspaceId) return mockWorkspace;
    const found = mockWorkspaces.find((ws) => ws.projectName === workspaceId);
    return found ?? mockWorkspace;
  }, [workspaceId]);

  return { workspace, isLoading: false };
}

export function useWorkspaces(): {
  workspaces: WorkspaceSummary[];
  isLoading: boolean;
} {
  return { workspaces: mockWorkspaces, isLoading: false };
}

// ── Plan / Spec ───────────────────────────────────────────────

export function useMilestones(): {
  milestones: Milestone[];
  isLoading: boolean;
} {
  return { milestones: mockMilestones, isLoading: false };
}

export function usePhases(milestoneId?: string): {
  phases: Phase[];
  isLoading: boolean;
} {
  const phases = useMemo(
    () =>
      milestoneId
        ? mockPhases.filter((p) => p.milestoneId === milestoneId)
        : mockPhases,
    [milestoneId]
  );
  return { phases, isLoading: false };
}

export function useProjectSpec(): {
  spec: ProjectSpec;
  isLoading: boolean;
} {
  return { spec: mockProjectSpec, isLoading: false };
}

// ── Tasks ─────────────────────────────────────────────────────

interface TaskFilters {
  phaseId?: string;
  status?: Task["status"];
  milestoneId?: string;
}

export function useTasks(filters?: TaskFilters): {
  tasks: Task[];
  isLoading: boolean;
} {
  const tasks = useMemo(() => {
    let result = mockTasks;
    if (filters?.phaseId) result = result.filter((t) => t.phaseId === filters.phaseId);
    if (filters?.status) result = result.filter((t) => t.status === filters.status);
    if (filters?.milestoneId) result = result.filter((t) => t.milestone === filters.milestoneId);
    return result;
  }, [filters?.phaseId, filters?.status, filters?.milestoneId]);

  return { tasks, isLoading: false };
}

export function useTaskPlans(taskId?: string): {
  plans: TaskPlan[];
  isLoading: boolean;
} {
  const plans = useMemo(
    () =>
      taskId
        ? mockTaskPlans.filter((p) => p.taskId === taskId)
        : mockTaskPlans,
    [taskId]
  );
  return { plans, isLoading: false };
}

// ── Runs ──────────────────────────────────────────────────────

export function useRuns(taskId?: string): {
  runs: Run[];
  isLoading: boolean;
} {
  const runs = useMemo(
    () => (taskId ? mockRuns.filter((r) => r.taskId === taskId) : mockRuns),
    [taskId]
  );
  return { runs, isLoading: false };
}

// ── Issues ────────────────────────────────────────────────────

export function useIssues(filters?: {
  status?: Issue["status"];
  severity?: Issue["severity"];
}): {
  issues: Issue[];
  isLoading: boolean;
} {
  const issues = useMemo(() => {
    let result = mockIssues;
    if (filters?.status) result = result.filter((i) => i.status === filters.status);
    if (filters?.severity) result = result.filter((i) => i.severity === filters.severity);
    return result;
  }, [filters?.status, filters?.severity]);

  return { issues, isLoading: false };
}

// ── Checkpoints ───────────────────────────────────────────────

export function useCheckpoints(): {
  checkpoints: Checkpoint[];
  isLoading: boolean;
} {
  return { checkpoints: mockCheckpoints, isLoading: false };
}

// ── Continuity / State ────────────────────────────────────────

export function useContinuityState(): {
  state: ContinuityState;
  handoff: HandoffSnapshot | null;
  events: Event[];
  packs: ContextPack[];
  isLoading: boolean;
} {
  return {
    state: mockContinuityState,
    handoff: mockHandoff,
    events: mockEvents,
    packs: mockPacks,
    isLoading: false,
  };
}

// ── File System ───────────────────────────────────────────────

export function useFileSystem(): {
  fileSystem: FileSystemNode[];
  findNode: (path: string[]) => FileSystemNode | null;
  isLoading: boolean;
} {
  return {
    fileSystem: mockFileSystem,
    findNode: (segments: string[]) => findNodeByPath(mockFileSystem, segments),
    isLoading: false,
  };
}

// ── Verification (derived) ────────────────────────────────────

export function useVerificationState(): {
  verification: VerificationContext;
  isLoading: boolean;
} {
  const { tasks } = useTasks();
  const { plans } = useTaskPlans();
  const { issues } = useIssues();
  const { runs } = useRuns();
  const { phases } = usePhases();

  const verification = useMemo(
    () => deriveVerificationState({ tasks, taskPlans: plans, issues, runs, phases }),
    [tasks, plans, issues, runs, phases]
  );

  return { verification, isLoading: false };
}

// ── Host Console (Daemon) ─────────────────────────────────────

export function useHostConsole(): {
  logs: HostLogLine[];
  surfaces: ApiSurface[];
  isLoading: boolean;
} {
  return {
    logs: mockHostLogs,
    surfaces: mockApiSurfaces,
    isLoading: false,
  };
}

// ── Diagnostics ───────────────────────────────────────────────

export function useDiagnostics(): {
  logs: DiagLogEntry[];
  artifacts: DiagArtifactEntry[];
  locks: DiagLockEntry[];
  cacheEntries: DiagCacheEntry[];
  isLoading: boolean;
} {
  return {
    logs: mockDiagLogs,
    artifacts: mockDiagArtifacts,
    locks: mockDiagLocks,
    cacheEntries: mockDiagCacheEntries,
    isLoading: false,
  };
}

// ── Codebase Intelligence ─────────────────────────────────────

export function useCodebaseIntel(): {
  artifacts: CodebaseArtifact[];
  languages: LanguageBreakdown[];
  stack: StackEntry[];
  isLoading: boolean;
} {
  return {
    artifacts: mockCodebaseArtifacts,
    languages: mockLanguages,
    stack: mockStack,
    isLoading: false,
  };
}

// ── Orchestrator / Gate ───────────────────────────────────────

export function useOrchestratorState(): {
  blockedGate: NextTaskGate;
  runnableGate: NextTaskGate;
  gateKindMeta: Record<GateKind, GateKindMeta>;
  timelineTemplate: TimelineStep[];
  isLoading: boolean;
} {
  return {
    blockedGate: mockBlockedGate,
    runnableGate: mockRunnableGate,
    gateKindMeta: GATE_KIND_META,
    timelineTemplate: defaultTimelineSteps,
    isLoading: false,
  };
}

// ── Chat ──────────────────────────────────────────────────────

export function useChatMessages(): {
  messages: ChatMessage[];
  commandSuggestions: CommandSuggestion[];
  quickActions: QuickAction[];
  isLoading: boolean;
} {
  return {
    messages: mockChatMessages,
    commandSuggestions: mockCommandSuggestions,
    quickActions: mockQuickActions,
    isLoading: false,
  };
}

// ── Command & Engine Hooks ────────────────────────────────────

// Shared ValidationResult type — exported so consumers can import without touching /data/
export type ValidationResult = {
  schemas: "valid" | "invalid";
  spec: "valid" | "invalid";
  state: "valid" | "invalid" | "warning";
  evidence: "valid" | "invalid";
  codebase: "valid" | "invalid";
};

/**
 * useAosCommand
 * Stub for POST /api/v1/commands on Gmsd.Windows.Service.Api.
 * Executes an argv array and returns structured output.
 */
export function useAosCommand(): {
  execute: (argv: string[]) => Promise<{ ok: boolean; output: string }>;
  isRunning: boolean;
  lastResult: { ok: boolean; output: string } | null;
} {
  const [isRunning, setIsRunning] = useState(false);
  const [lastResult, setLastResult] = useState<{ ok: boolean; output: string } | null>(null);

  const execute = useCallback(async (argv: string[]): Promise<{ ok: boolean; output: string }> => {
    setIsRunning(true);
    await new Promise<void>((resolve) => setTimeout(resolve, 900));
    const result = { ok: true, output: argv.join(" ") + " → OK (mock)" };
    setLastResult(result);
    setIsRunning(false);
    return result;
  }, []);

  return { execute, isRunning, lastResult };
}

/**
 * useEngineConnection
 * Stubs for GET /api/v1/health and PUT /api/v1/service/host-profile.
 * Manages independent loading flags for test vs save operations.
 */
export function useEngineConnection(): {
  test: (baseUrl: string) => Promise<{ ok: boolean; version: string; latencyMs: number }>;
  save: (profile: { label: string; baseUrl: string; env: string }) => Promise<{ ok: boolean }>;
  isTesting: boolean;
  isSaving: boolean;
  lastPing: { ok: boolean; version: string; latencyMs: number } | null;
} {
  const [isTesting, setIsTesting] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [lastPing, setLastPing] = useState<{ ok: boolean; version: string; latencyMs: number } | null>(null);

  const test = useCallback(async (_baseUrl: string): Promise<{ ok: boolean; version: string; latencyMs: number }> => {
    setIsTesting(true);
    await new Promise<void>((resolve) => setTimeout(resolve, 1200));
    const result = { ok: true, version: "v2.4.0-alpha", latencyMs: 4 };
    setLastPing(result);
    setIsTesting(false);
    return result;
  }, []);

  const save = useCallback(async (_profile: { label: string; baseUrl: string; env: string }): Promise<{ ok: boolean }> => {
    setIsSaving(true);
    await new Promise<void>((resolve) => setTimeout(resolve, 600));
    setIsSaving(false);
    return { ok: true };
  }, []);

  return { test, save, isTesting, isSaving, lastPing };
}

/**
 * useWorkspaceInit
 * Stubs for POST /api/v1/commands with argv ["aos", "init"] and ["aos", "validate"].
 * Manages independent loading flags for init vs validate operations.
 */
export function useWorkspaceInit(_workspaceId: string | undefined): {
  init: (rootPath: string) => Promise<{ ok: boolean; aosDir: string }>;
  validate: () => Promise<ValidationResult>;
  isIniting: boolean;
  isValidating: boolean;
  initResult: { ok: boolean; aosDir: string } | null;
  validationResult: ValidationResult | null;
} {
  const [isIniting, setIsIniting] = useState(false);
  const [isValidating, setIsValidating] = useState(false);
  const [initResult, setInitResult] = useState<{ ok: boolean; aosDir: string } | null>(null);
  const [validationResult, setValidationResult] = useState<ValidationResult | null>(null);

  const init = useCallback(async (rootPath: string): Promise<{ ok: boolean; aosDir: string }> => {
    setIsIniting(true);
    await new Promise<void>((resolve) => setTimeout(resolve, 1800));
    const result = { ok: true, aosDir: rootPath + "/.aos" };
    setInitResult(result);
    setIsIniting(false);
    return result;
  }, []);

  const validate = useCallback(async (): Promise<ValidationResult> => {
    setIsValidating(true);
    await new Promise<void>((resolve) => setTimeout(resolve, 1200));
    const result: ValidationResult = {
      schemas: "valid",
      spec: "valid",
      state: "valid",
      evidence: "valid",
      codebase: "valid",
    };
    setValidationResult(result);
    setIsValidating(false);
    return result;
  }, []);

  return { init, validate, isIniting, isValidating, initResult, validationResult };
}