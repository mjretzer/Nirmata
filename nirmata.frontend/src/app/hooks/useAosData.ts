/**
 * Data Access Hooks for AOS
 *
 * Thin service layer that calls the typed API helper (domainClient / daemonClient).
 * All hooks expose { data, isLoading } and start with empty initial state.
 * When the backend is unavailable the hook silently stays empty — the UI
 * renders its own empty/loading treatment.
 *
 * Usage:
 *   const { workspace, isLoading } = useWorkspace("my-app");
 *   const { tasks } = useTasks({ phaseId: "PH-0002" });
 */

import { useMemo, useState, useCallback, useEffect } from "react";
import {
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
  type HandoffSnapshot,
  type Event,
  type ContextPack,
  type ContinuityState,
} from "../data/mockContinuityData";
const WORKSPACE_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export function isGuidWorkspaceId(value?: string): value is string {
  return typeof value === "string" && WORKSPACE_ID_PATTERN.test(value);
}

export interface FileSystemNode {
  id: string;
  name: string;
  type: "directory" | "file";
  path?: string;
  sizeBytes?: number;
  lastModified?: string;
  children?: FileSystemNode[];
  status?: "valid" | "warning" | "error";
  meta?: unknown;
}

function findNodeByPath(root: FileSystemNode[], pathParts: string[]): FileSystemNode | null {
  if (pathParts.length === 0) return null;
  const [current, ...rest] = pathParts;
  const match = root.find((node) => node.name === current);
  if (!match) return null;
  if (rest.length === 0) return match;
  if (match.type === "directory" && match.children) {
    return findNodeByPath(match.children, rest);
  }
  return null;
}
import {
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
  type ChatMessage,
  type ChatRole,
  type AgentId,
  type ChatArtifactRef,
  type CommandSuggestion,
  type QuickAction,
} from "../data/mockChatData";
import { useWorkspaceContext } from "../context/WorkspaceContext";
import {
  daemonClient,
  domainClient,
  DaemonClient,
  ApiError,
  formatApiFailureDiagnostic,
  getApiFailureDiagnostic,
  type AosApiError,
  type FilesystemNode as ApiFilesystemNode,
  type MilestoneSummary,
  type PhaseSummary,
  type ProjectSpecResponse,
  type CheckpointSummary,
  type ApiHostLogLine,
  type ApiSurfaceStatus,
  type ApiDiagLogEntry,
  type ApiDiagArtifactEntry,
  type ApiDiagLockEntry,
  type ApiDiagCacheEntry,
  type ApiCodebaseArtifact,
  type ApiLanguageBreakdown,
  type ApiStackEntry,
  type WorkspaceCreateRequest,
  type WorkspaceContinuityState,
  type WorkspaceHandoff,
  type WorkspaceEvent,
  type WorkspaceContextPack,
  type WorkspaceCheckpointSummary,
  type WorkspaceRunSummary,
  type WorkspaceIssueDto,
  type WorkspaceUatSummaryDto,
  type WorkspaceUatRecord,
  type CodebaseArtifactDto,
  type ChatApiMessage,
} from "../utils/apiClient";
export type { AosApiError };
import { toast } from "sonner";
import {
  deriveVerificationState,
  type VerificationContext,
} from "../components/verification/verificationState";

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

// ── Private empty defaults ─────────────────────────────────────

const emptyWorkspace: Workspace = {
  repoRoot: "",
  projectName: "",
  hasAosDir: false,
  hasProjectSpec: false,
  hasRoadmap: false,
  hasTaskPlans: false,
  hasHandoff: false,
  cursor: { milestone: "", phase: "", task: null },
  lastRun: { id: "", status: "success", timestamp: "" },
  validation: { schemas: "invalid", spec: "invalid", state: "invalid", evidence: "invalid", codebase: "invalid" },
  lastValidationAt: "",
  openIssuesCount: 0,
  openTodosCount: 0,
};

const emptyProjectSpec: ProjectSpec = {
  name: "",
  description: "",
  version: "",
  owner: "",
  repo: "",
  milestones: [],
  createdAt: "",
  updatedAt: "",
  tags: [],
  constraints: [],
};

const emptyContinuityState: ContinuityState = {
  handoff: null,
  events: [],
  packs: [],
  nextCommand: { command: "", reason: "" },
};

const emptyUatSummary: WorkspaceUatSummaryDto = {
  records: [],
  taskSummaries: [],
  phaseSummaries: [],
};

const emptyBlockedGate: NextTaskGate = {
  taskId: "",
  taskName: "",
  phaseId: "",
  phaseTitle: "",
  runnable: false,
  checks: [],
  recommendedAction: "",
};

const emptyRunnableGate: NextTaskGate = {
  taskId: "",
  taskName: "",
  phaseId: "",
  phaseTitle: "",
  runnable: true,
  checks: [],
  recommendedAction: "",
};

// ── Response-shape adapters ────────────────────────────────────

function mapMilestoneSummary(m: MilestoneSummary): Milestone {
  return {
    id: m.id,
    name: m.title,
    description: "",
    phases: m.phaseIds,
    status: m.status as Milestone["status"],
    targetDate: "",
    definitionOfDone: [],
  };
}

function mapPhaseSummary(p: PhaseSummary): Phase {
  return {
    id: p.id,
    milestoneId: p.milestoneId,
    title: p.title,
    summary: "",
    order: p.order,
    status: p.status as Phase["status"],
    brief: { goal: "", priorities: [], nonGoals: [], constraints: [], dependencies: [] },
    deliverables: [],
    acceptance: { criteria: [], uatChecklist: [] },
    links: { roadmapPhaseRef: "", tasks: p.taskIds, artifacts: [] },
    metadata: { tags: [], owner: "", createdAt: "", updatedAt: "" },
  };
}

function mapProjectSpecResponse(p: ProjectSpecResponse): ProjectSpec {
  return {
    name: p.name,
    description: p.description,
    version: p.version,
    owner: p.owner,
    repo: p.repo,
    milestones: p.milestones,
    createdAt: p.createdAt,
    updatedAt: p.updatedAt,
    tags: p.tags,
    constraints: p.constraints,
  };
}

function mapCheckpointSummary(c: CheckpointSummary): Checkpoint {
  return {
    id: c.id,
    timestamp: c.timestamp,
    cursor: { milestone: c.milestoneId, phase: c.phaseId, task: c.taskId },
    description: c.description,
    source: c.source,
  };
}

function mapWorkspaceCheckpointSummary(c: WorkspaceCheckpointSummary): Checkpoint {
  return {
    id: c.id,
    timestamp: c.timestamp ?? "",
    cursor: {
      milestone: c.position?.milestoneId ?? "",
      phase: c.position?.phaseId ?? "",
      task: c.position?.taskId ?? null,
    },
    description: c.position?.status ?? "",
    source: undefined,
  };
}

function normaliseRunStatus(raw: string | null | undefined): Run["status"] {
  switch (raw) {
    case "pass":
    case "success": return "success";
    case "running": return "running";
    default: return "failed";
  }
}

function mapWorkspaceRunSummary(r: WorkspaceRunSummary): Run {
  return {
    id: r.id,
    taskId: r.taskId ?? "",
    command: "",
    status: normaliseRunStatus(r.status),
    startTime: r.timestamp ?? "",
    endTime: undefined,
    artifacts: [],
    logs: [],
    changedFiles: [],
  };
}

function mapHostLogLine(l: ApiHostLogLine): HostLogLine {
  return { id: l.id, ts: l.ts, level: l.level as HostLogLine["level"], msg: l.msg };
}

function mapSurfaceStatus(s: ApiSurfaceStatus): ApiSurface {
  return { name: s.name, path: s.path, ok: s.ok, latencyMs: s.latencyMs, reason: s.reason };
}

function mapDiagLog(l: ApiDiagLogEntry): DiagLogEntry {
  return { label: l.label, lines: l.lines, warnings: l.warnings, errors: l.errors, path: l.path };
}

function mapDiagArtifact(a: ApiDiagArtifactEntry): DiagArtifactEntry {
  return { name: a.name, size: a.size, type: a.type, path: a.path };
}

function mapDiagLock(l: ApiDiagLockEntry): DiagLockEntry {
  return { id: l.id, scope: l.scope, owner: l.owner, acquired: l.acquired, stale: l.stale };
}

function mapDiagCache(c: ApiDiagCacheEntry): DiagCacheEntry {
  return { label: c.label, size: c.size, path: c.path, stale: c.stale };
}

function mapCodebaseArtifact(a: ApiCodebaseArtifact): CodebaseArtifact {
  return {
    id: a.id,
    name: a.name,
    type: a.type as CodebaseArtifact["type"],
    description: a.description,
    status: a.status as CodebaseArtifact["status"],
    lastUpdated: a.lastUpdated,
    size: a.size,
    path: a.path,
  };
}

function mapLanguageBreakdown(l: ApiLanguageBreakdown): LanguageBreakdown {
  return { name: l.name, pct: l.pct, color: l.color };
}

function mapStackEntry(s: ApiStackEntry): StackEntry {
  return { name: s.name, category: s.category, color: s.color };
}

// ── Workspace ─────────────────────────────────────────────────

/** Maps a backend WorkspaceStatus string to the frontend WorkspaceSummary status union. */
function mapWorkspaceStatus(apiStatus: string): WorkspaceSummary["status"] {
  switch (apiStatus) {
    case "initialized":     return "healthy";
    case "not-initialized": return "needs-init";
    case "missing":         return "missing-path";
    case "inaccessible":    return "invalid";
    default:                return "invalid";
  }
}

function formatStartupDiagnostic(
  err: unknown,
  fallback: { endpoint: string; suggestedFix: string },
): string {
  return formatApiFailureDiagnostic(getApiFailureDiagnostic(err, fallback));
}

export function useWorkspace(workspaceId?: string): {
  workspace: Workspace;
  isLoading: boolean;
  notFound?: boolean;
  bootstrapDiagnostic?: string | null;
} {
  const [workspace, setWorkspace] = useState<Workspace>(emptyWorkspace);
  const [isLoading, setIsLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [bootstrapDiagnostic, setBootstrapDiagnostic] = useState<string | null>(null);
  const { setWorkspaceBootstrapError } = useWorkspaceContext();

  useEffect(() => {
    if (!isGuidWorkspaceId(workspaceId)) {
      setWorkspace(emptyWorkspace);
      setNotFound(false);
      setBootstrapDiagnostic(null);
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    setNotFound(false);
    setBootstrapDiagnostic(null);
    domainClient
      .getWorkspace(workspaceId)
      .then((data) => {
        setWorkspaceBootstrapError(null);
        setBootstrapDiagnostic(null);
        setWorkspace((prev) => ({
          ...prev,
          repoRoot: data.path,
          projectName: data.name,
          hasAosDir: data.status === "initialized",
        }));
      })
      .catch((err) => {
        const isNotFound = err instanceof ApiError && err.status === 404;
        setNotFound(isNotFound);
        setWorkspaceBootstrapError(isNotFound ? "not-found" : "error");
        setBootstrapDiagnostic(
          formatStartupDiagnostic(err, {
            endpoint: `GET /v1/workspaces/${encodeURIComponent(workspaceId)}`,
            suggestedFix: isNotFound
              ? "Verify the workspace identifier or path, then reopen or register the workspace."
              : "Check the domain API URL and confirm the workspace exists and is reachable.",
          })
        );
      })
      .finally(() => setIsLoading(false));
  }, [workspaceId, setWorkspaceBootstrapError]);

  return { workspace, isLoading, notFound, bootstrapDiagnostic };
}

export function useWorkspaces(): {
  workspaces: WorkspaceSummary[];
  isLoading: boolean;
  errorDiagnostic?: string | null;
  refresh: () => void;
} {
  const [workspaces, setWorkspaces] = useState<WorkspaceSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorDiagnostic, setErrorDiagnostic] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    setIsLoading(true);
    setErrorDiagnostic(null);
    domainClient
      .getWorkspaces()
      .then((items) => {
        setWorkspaces(
          items.map((w) => ({
            ...emptyWorkspace,
            id: w.id,
            repoRoot: w.path,
            projectName: w.name,
            hasAosDir: w.status === "initialized",
            lastScanned: new Date(w.lastModified).toISOString(),
            lastOpened: new Date(w.lastModified).toISOString(),
            status: mapWorkspaceStatus(w.status),
            pinned: false,
            isGitRepo: false,
          }))
        );
      })
      .catch((err) => {
        setWorkspaces([]);
        setErrorDiagnostic(
          formatStartupDiagnostic(err, {
            endpoint: "GET /v1/workspaces",
            suggestedFix: "Check the domain API URL and confirm the workspace registry is reachable.",
          })
        );
      })
      .finally(() => setIsLoading(false));
  }, [refreshKey]);

  return {
    workspaces,
    isLoading,
    errorDiagnostic,
    refresh: useCallback(() => setRefreshKey((k) => k + 1), []),
  };
}

export function useRegisterWorkspace(): {
  register: (name: string, path: string) => Promise<{ id: string; name: string } | null>;
  isRegistering: boolean;
} {
  const [isRegistering, setIsRegistering] = useState(false);

  const register = useCallback(async (name: string, path: string): Promise<{ id: string; name: string } | null> => {
    setIsRegistering(true);
    try {
      const req: WorkspaceCreateRequest = { name, path };
      const created = await domainClient.createWorkspace(req);
      return { id: created.id, name: created.name };
    } catch (err) {
      toast.error("Failed to register workspace", {
        description: formatStartupDiagnostic(err, {
          endpoint: "POST /v1/workspaces",
          suggestedFix: "Check the domain API URL and confirm the workspace path is valid.",
        }),
      });
      return null;
    } finally {
      setIsRegistering(false);
    }
  }, []);

  return { register, isRegistering };
}

// ── Plan / Spec ───────────────────────────────────────────────

export function useMilestones(): {
  milestones: Milestone[];
  isLoading: boolean;
} {
  const [milestones, setMilestones] = useState<Milestone[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const { activeWorkspaceId, workspaceBootstrapError } = useWorkspaceContext();

  useEffect(() => {
    if (!isGuidWorkspaceId(activeWorkspaceId) || workspaceBootstrapError) {
      setIsLoading(false);
      return;
    }
    domainClient
      .getMilestones(activeWorkspaceId)
      .then((items) => setMilestones(items.map(mapMilestoneSummary)))
      .catch(() => {})
      .finally(() => setIsLoading(false));
  }, [activeWorkspaceId, workspaceBootstrapError]);

  return { milestones, isLoading };
}

export function usePhases(milestoneId?: string): {
  phases: Phase[];
  isLoading: boolean;
} {
  const [phases, setPhases] = useState<Phase[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const { activeWorkspaceId, workspaceBootstrapError } = useWorkspaceContext();

  useEffect(() => {
    if (!isGuidWorkspaceId(activeWorkspaceId) || workspaceBootstrapError) {
      setIsLoading(false);
      return;
    }
    domainClient
      .getPhases(activeWorkspaceId)
      .then((items) => setPhases(items.map(mapPhaseSummary)))
      .catch(() => {})
      .finally(() => setIsLoading(false));
  }, [activeWorkspaceId, milestoneId, workspaceBootstrapError]);

  return { phases, isLoading };
}

export function useProjectSpec(): {
  spec: ProjectSpec;
  isLoading: boolean;
} {
  const [spec, setSpec] = useState<ProjectSpec>(emptyProjectSpec);
  const [isLoading, setIsLoading] = useState(true);
  const { activeWorkspaceId, workspaceBootstrapError } = useWorkspaceContext();

  useEffect(() => {
    if (!isGuidWorkspaceId(activeWorkspaceId) || workspaceBootstrapError) {
      setIsLoading(false);
      return;
    }
    domainClient
      .getProjectSpec(activeWorkspaceId)
      .then((data) => setSpec(mapProjectSpecResponse(data)))
      .catch(() => {})
      .finally(() => setIsLoading(false));
  }, [activeWorkspaceId, workspaceBootstrapError]);

  return { spec, isLoading };
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
  const [tasks, setTasks] = useState<Task[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const { activeWorkspaceId, workspaceBootstrapError } = useWorkspaceContext();

  useEffect(() => {
    if (!isGuidWorkspaceId(activeWorkspaceId) || workspaceBootstrapError) {
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    domainClient
      .getTasks(activeWorkspaceId, { phaseId: filters?.phaseId, milestoneId: filters?.milestoneId, status: filters?.status })
      .then((items) => {
        setTasks(
          items.map((t) => ({
            id: t.id,
            phaseId: t.phaseId,
            milestone: t.milestoneId,
            name: t.title,
            status: t.status as Task["status"],
            assignedTo: "",
          }))
        );
      })
      .catch(() => setTasks([]))
      .finally(() => setIsLoading(false));
  }, [activeWorkspaceId, filters?.phaseId, filters?.status, filters?.milestoneId, workspaceBootstrapError]);

  return { tasks, isLoading };
}

export function useTaskPlans(taskId?: string): {
  plans: TaskPlan[];
  isLoading: boolean;
} {
  const [plans, setPlans] = useState<TaskPlan[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const { activeWorkspaceId } = useWorkspaceContext();

  useEffect(() => {
    if (!activeWorkspaceId) {
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    domainClient
      .getTaskPlans(taskId ? { taskId } : undefined)
      .then((items) => {
        setPlans(
          items.map((p) => ({
            taskId: p.taskId,
            fileScope: [],
            steps: [],
            verification: [],
            definitionOfDone: [],
          }))
        );
      })
      .catch(() => setPlans([]))
      .finally(() => setIsLoading(false));
  }, [activeWorkspaceId, taskId]);

  return { plans, isLoading };
}

// ── Runs ──────────────────────────────────────────────────────

export function useRuns(taskId?: string): {
  runs: Run[];
  isLoading: boolean;
} {
  const { activeWorkspaceId, workspaceBootstrapError } = useWorkspaceContext();
  const [runs, setRuns] = useState<Run[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!isGuidWorkspaceId(activeWorkspaceId) || workspaceBootstrapError) {
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    domainClient
      .getWorkspaceRuns(activeWorkspaceId)
      .then((items) => {
        const mapped = items.map(mapWorkspaceRunSummary);
        setRuns(taskId ? mapped.filter((r) => r.taskId === taskId) : mapped);
      })
      .catch(() => setRuns([]))
      .finally(() => setIsLoading(false));
  }, [activeWorkspaceId, taskId, workspaceBootstrapError]);

  return { runs, isLoading };
}

// ── Issues ────────────────────────────────────────────────────

function mapWorkspaceIssue(i: WorkspaceIssueDto): Issue {
  return {
    id: i.id,
    severity: (i.severity ?? "medium") as Issue["severity"],
    scope: i.scope ?? i.phaseId ?? "",
    description: i.title,
    repro: i.repro ? [i.repro] : [],
    linkedTasks: i.taskId ? [i.taskId] : [],
    status: i.status as Issue["status"],
    impactedFiles: i.impactedFiles,
    ...(i.expected || i.actual
      ? { expectedVsActual: { expected: i.expected ?? "", actual: i.actual ?? "" } }
      : {}),
    links: {
      ...(i.taskId ? { taskId: i.taskId } : {}),
      ...(i.phaseId ? { phaseId: i.phaseId } : {}),
    },
  };
}

export function useIssues(filters?: {
  status?: Issue["status"];
  severity?: Issue["severity"];
  taskId?: string;
  phaseId?: string;
  milestoneId?: string;
}): {
  issues: Issue[];
  isLoading: boolean;
} {
  const { activeWorkspaceId, workspaceBootstrapError } = useWorkspaceContext();
  const [issues, setIssues] = useState<Issue[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!isGuidWorkspaceId(activeWorkspaceId) || workspaceBootstrapError) {
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    domainClient
      .getWorkspaceIssues(activeWorkspaceId, {
        status: filters?.status,
        severity: filters?.severity,
        taskId: filters?.taskId,
        phaseId: filters?.phaseId,
        milestoneId: filters?.milestoneId,
      })
      .then((items) => setIssues(items.map(mapWorkspaceIssue)))
      .catch(() => setIssues([]))
      .finally(() => setIsLoading(false));
  }, [activeWorkspaceId, filters?.status, filters?.severity, filters?.taskId, filters?.phaseId, filters?.milestoneId, workspaceBootstrapError]);

  return { issues, isLoading };
}

// ── Checkpoints ───────────────────────────────────────────────

export function useCheckpoints(): {
  checkpoints: Checkpoint[];
  isLoading: boolean;
} {
  const { activeWorkspaceId, workspaceBootstrapError } = useWorkspaceContext();
  const [checkpoints, setCheckpoints] = useState<Checkpoint[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!isGuidWorkspaceId(activeWorkspaceId) || workspaceBootstrapError) {
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    domainClient
      .getWorkspaceCheckpoints(activeWorkspaceId)
      .then((items) => setCheckpoints(items.map(mapWorkspaceCheckpointSummary)))
      .catch(() => {})
      .finally(() => setIsLoading(false));
  }, [activeWorkspaceId, workspaceBootstrapError]);

  return { checkpoints, isLoading };
}

// ── Continuity / State ────────────────────────────────────────

export function useContinuityState(): {
  state: ContinuityState;
  handoff: HandoffSnapshot | null;
  events: Event[];
  packs: ContextPack[];
  cursor: { milestoneId: string; phaseId: string; taskId: string | null };
  isLoading: boolean;
} {
  const { activeWorkspaceId, workspaceBootstrapError } = useWorkspaceContext();
  const [state, setState] = useState<ContinuityState>(emptyContinuityState);
  const [handoff, setHandoff] = useState<HandoffSnapshot | null>(null);
  const [events, setEvents] = useState<Event[]>([]);
  const [packs, setPacks] = useState<ContextPack[]>([]);
  const [cursor, setCursor] = useState<{ milestoneId: string; phaseId: string; taskId: string | null }>({ milestoneId: "", phaseId: "", taskId: null });
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!isGuidWorkspaceId(activeWorkspaceId) || workspaceBootstrapError) {
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    Promise.allSettled([
      domainClient.getWorkspaceState(activeWorkspaceId),
      domainClient.getWorkspaceHandoff(activeWorkspaceId),
      domainClient.getWorkspaceEvents(activeWorkspaceId),
      domainClient.getWorkspaceStatePacks(activeWorkspaceId),
    ]).then(([stateResult, handoffResult, eventsResult, packsResult]) => {
      if (stateResult.status === "fulfilled" && stateResult.value?.position) {
        const pos = stateResult.value.position;
        setCursor({
          milestoneId: pos.milestoneId ?? "",
          phaseId: pos.phaseId ?? "",
          taskId: pos.taskId ?? null,
        });
      }
      let mappedHandoff: HandoffSnapshot | null = null;
      if (handoffResult.status === "fulfilled" && handoffResult.value) {
        const h = handoffResult.value as WorkspaceHandoff;
        mappedHandoff = {
          timestamp: h.timestamp ?? "",
          cursor: {
            milestone: h.cursor?.milestoneId ?? "",
            phase: h.cursor?.phaseId ?? "",
            task: h.inFlightTask ?? "",
            step: h.inFlightStep !== undefined ? String(h.inFlightStep) : "",
          },
          inFlight: {
            task: h.inFlightTask ?? "",
            step: h.inFlightStep !== undefined ? String(h.inFlightStep) : "",
            files: h.allowedScope ?? [],
          },
          nextCommand: h.nextCommand ?? "",
          integrity: { matchesCursor: true },
        };
      }
      // Always update handoff state — null correctly clears a stale handoff when
      // handoff.json is absent for the current workspace (API returns null or 404).
      setHandoff(mappedHandoff);

      const mappedEvents: Event[] =
        eventsResult.status === "fulfilled"
          ? eventsResult.value.map((e: WorkspaceEvent) => ({
              id: `${e.type ?? "event"}_${e.timestamp ?? Date.now()}`,
              timestamp: e.timestamp ?? "",
              type: e.type as Event["type"],
              summary: e.payload ?? "",
              references: e.references ?? [],
            }))
          : [];
      if (mappedEvents.length > 0) setEvents(mappedEvents);

      const mappedPacks: ContextPack[] =
        packsResult.status === "fulfilled"
          ? packsResult.value.map((p: WorkspaceContextPack) => ({
              id: p.packId,
              name: p.packId,
              size: p.artifactCount > 0 ? `${p.artifactCount} artifacts` : "",
              mode: (p.mode as ContextPack["mode"]) ?? "execute",
              created: "",
              artifacts: [],
            }))
          : [];
      if (mappedPacks.length > 0) setPacks(mappedPacks);

      setState({
        handoff: mappedHandoff,
        events: mappedEvents,
        packs: mappedPacks,
        nextCommand: { command: "", reason: "" },
      });
    }).finally(() => setIsLoading(false));
  }, [activeWorkspaceId, workspaceBootstrapError]);

  return { state, handoff, events, packs, cursor, isLoading };
}

// ── File System ───────────────────────────────────────────────

export function useFileSystem(path?: string): {
  fileSystem: FileSystemNode[];
  node: FileSystemNode | null;
  findNode: (path: string[]) => FileSystemNode | null;
  isLoading: boolean;
} {
  const [fileSystem, setFileSystem] = useState<FileSystemNode[]>([]);
  const [node, setNode] = useState<FileSystemNode | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const { activeWorkspaceId } = useWorkspaceContext();

  useEffect(() => {
    if (!isGuidWorkspaceId(activeWorkspaceId)) {
      setIsLoading(false);
      return;
    }
    setIsLoading(true);

    const mapApiNode = (n: ApiFilesystemNode): FileSystemNode => ({
      id: n.path,
      name: n.name,
      type: n.type === "directory" ? "directory" : "file",
      path: n.path,
      sizeBytes: n.sizeBytes,
      lastModified: n.lastModified,
    });

    domainClient
      .getWorkspaceFiles(activeWorkspaceId, path)
      .then((apiNode) => {
        if (!apiNode) {
          setNode(null);
          setFileSystem([]);
          return;
        }
        const children: FileSystemNode[] =
          apiNode.type === "directory"
            ? [...apiNode.children]
                .sort((a, b) => {
                  if (a.type !== b.type) return a.type === "directory" ? -1 : 1;
                  return a.name.localeCompare(b.name);
                })
                .map(mapApiNode)
            : [];
        const mapped: FileSystemNode = {
          id: apiNode.path,
          name: apiNode.name,
          type: apiNode.type === "directory" ? "directory" : "file",
          path: apiNode.path,
          sizeBytes: apiNode.sizeBytes,
          lastModified: apiNode.lastModified,
          ...(children.length > 0 && { children }),
        };
        setNode(mapped);
        setFileSystem(children);
      })
      .catch(() => {
        setNode(null);
        setFileSystem([]);
      })
      .finally(() => setIsLoading(false));
  }, [activeWorkspaceId, path]);

  return {
    fileSystem,
    node,
    findNode: (segments: string[]) => findNodeByPath(fileSystem, segments),
    isLoading,
  };
}

// ── UAT Summary ───────────────────────────────────────────────

export function useUatSummary(): {
  uatSummary: WorkspaceUatSummaryDto;
  isLoading: boolean;
} {
  const { activeWorkspaceId } = useWorkspaceContext();
  const [uatSummary, setUatSummary] = useState<WorkspaceUatSummaryDto>(emptyUatSummary);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!isGuidWorkspaceId(activeWorkspaceId)) {
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    domainClient
      .getWorkspaceUat(activeWorkspaceId)
      .then((data) => setUatSummary(data))
      .catch(() => setUatSummary(emptyUatSummary))
      .finally(() => setIsLoading(false));
  }, [activeWorkspaceId]);

  return { uatSummary, isLoading };
}

// ── Verification (derived) ────────────────────────────────────

export function useVerificationState(): {
  verification: VerificationContext;
  isLoading: boolean;
} {
  const { tasks, isLoading: tasksLoading } = useTasks();
  const { plans, isLoading: plansLoading } = useTaskPlans();
  const { issues, isLoading: issuesLoading } = useIssues();
  const { runs, isLoading: runsLoading } = useRuns();
  const { phases, isLoading: phasesLoading } = usePhases();
  const { uatSummary, isLoading: uatLoading } = useUatSummary();

  // Enrich task plans with real UAT check results where available.
  // This replaces client-side mock derivation with persisted workspace data.
  const enrichedPlans = useMemo<TaskPlan[]>(() => {
    const uatByTask = new Map<string, WorkspaceUatRecord[]>();
    for (const r of uatSummary.records) {
      if (r.taskId) {
        const list = uatByTask.get(r.taskId) ?? [];
        list.push(r);
        uatByTask.set(r.taskId, list);
      }
    }

    return plans.map((plan) => {
      const uatRecords = uatByTask.get(plan.taskId);
      if (!uatRecords || uatRecords.length === 0) return plan;

      // Use checks from the most recent UAT record for this task.
      const latest = uatRecords[uatRecords.length - 1];
      const verification: TaskPlan["verification"] = latest.checks.map((c) => ({
        command: c.criterionId,
        type: c.checkType === "manual" ? "manual" as const : "automated" as const,
        passed: c.passed,
      }));
      return { ...plan, verification };
    });
  }, [plans, uatSummary.records]);

  const verification = useMemo(
    () => deriveVerificationState({ tasks, taskPlans: enrichedPlans, issues, runs, phases }),
    [tasks, enrichedPlans, issues, runs, phases]
  );

  const isLoading =
    tasksLoading || plansLoading || issuesLoading || runsLoading || phasesLoading || uatLoading;

  return { verification, isLoading };
}

// ── Host Console (Daemon) ─────────────────────────────────────

export function useHostConsole(): {
  logs: HostLogLine[];
  surfaces: ApiSurface[];
  isLoading: boolean;
} {
  const [logs, setLogs] = useState<HostLogLine[]>([]);
  const [surfaces, setSurfaces] = useState<ApiSurface[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    const fetchAll = () =>
      Promise.all([daemonClient.getHostLogs(), daemonClient.getHostSurfaces()])
        .then(([logsData, surfacesData]) => {
          if (cancelled) return;
          if (logsData.length > 0) setLogs(logsData.map(mapHostLogLine));
          if (surfacesData.length > 0) setSurfaces(surfacesData.map(mapSurfaceStatus));
        })
        .catch(() => {})
        .finally(() => {
          if (!cancelled) setIsLoading(false);
        });

    fetchAll();
    const timer = setInterval(fetchAll, 5_000);

    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, []);

  return { logs, surfaces, isLoading };
}

// ── Diagnostics ───────────────────────────────────────────────

export function useDiagnostics(): {
  logs: DiagLogEntry[];
  artifacts: DiagArtifactEntry[];
  locks: DiagLockEntry[];
  cacheEntries: DiagCacheEntry[];
  isLoading: boolean;
} {
  const [logs, setLogs] = useState<DiagLogEntry[]>([]);
  const [artifacts, setArtifacts] = useState<DiagArtifactEntry[]>([]);
  const [locks, setLocks] = useState<DiagLockEntry[]>([]);
  const [cacheEntries, setCacheEntries] = useState<DiagCacheEntry[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    daemonClient
      .getDiagnostics()
      .then((snap) => {
        if (snap.logs.length > 0) setLogs(snap.logs.map(mapDiagLog));
        if (snap.artifacts.length > 0) setArtifacts(snap.artifacts.map(mapDiagArtifact));
        if (snap.locks.length > 0) setLocks(snap.locks.map(mapDiagLock));
        if (snap.cacheEntries.length > 0) setCacheEntries(snap.cacheEntries.map(mapDiagCache));
      })
      .catch(() => {})
      .finally(() => setIsLoading(false));
  }, []);

  return { logs, artifacts, locks, cacheEntries, isLoading };
}

// ── Workspace-scoped DTO mappers ──────────────────────────────

const _cacheArtifactTypes = new Set(["symbols", "file-graph"]);

function mapCodebaseArtifactDto(a: CodebaseArtifactDto): CodebaseArtifact {
  return {
    id: a.id,
    name: a.id,
    type: _cacheArtifactTypes.has(a.type) ? "cache" : "intel",
    description: "",
    status: a.status as CodebaseArtifact["status"],
    lastUpdated: a.lastUpdated ?? "",
    size: "",
    path: a.path,
  };
}

function mapLanguageString(name: string): LanguageBreakdown {
  return { name, pct: 0, color: "" };
}

function mapStackString(name: string): StackEntry {
  return { name, category: "", color: "" };
}

// ── Codebase Intelligence ─────────────────────────────────────

export function useCodebaseIntel(): {
  artifacts: CodebaseArtifact[];
  languages: LanguageBreakdown[];
  stack: StackEntry[];
  isLoading: boolean;
} {
  const [artifacts, setArtifacts] = useState<CodebaseArtifact[]>([]);
  const [languages, setLanguages] = useState<LanguageBreakdown[]>([]);
  const [stack, setStack] = useState<StackEntry[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const { activeWorkspaceId } = useWorkspaceContext();

  useEffect(() => {
    if (!isGuidWorkspaceId(activeWorkspaceId)) {
      setIsLoading(false);
      return;
    }
    domainClient
      .getWorkspaceCodebase(activeWorkspaceId)
      .then((intel) => {
        if (intel.artifacts.length > 0) setArtifacts(intel.artifacts.map(mapCodebaseArtifactDto));
        if (intel.languages.length > 0) setLanguages(intel.languages.map(mapLanguageString));
        if (intel.stack.length > 0) setStack(intel.stack.map(mapStackString));
      })
      .catch(() => {})
      .finally(() => setIsLoading(false));
  }, [activeWorkspaceId]);

  return { artifacts, languages, stack, isLoading };
}

// ── Orchestrator / Gate ───────────────────────────────────────

export function useOrchestratorState(): {
  blockedGate: NextTaskGate;
  runnableGate: NextTaskGate;
  gateKindMeta: Record<GateKind, GateKindMeta>;
  timelineTemplate: TimelineStep[];
  isLoading: boolean;
} {
  const [blockedGate, setBlockedGate] = useState<NextTaskGate>(emptyBlockedGate);
  const [runnableGate, setRunnableGate] = useState<NextTaskGate>(emptyRunnableGate);
  const [timelineTemplate, setTimelineTemplate] = useState<TimelineStep[]>(defaultTimelineSteps);
  const [isLoading, setIsLoading] = useState(true);
  const { activeWorkspaceId } = useWorkspaceContext();

  useEffect(() => {
    if (!isGuidWorkspaceId(activeWorkspaceId)) {
      setIsLoading(false);
      return;
    }
    Promise.all([
      domainClient.getOrchestratorGate(activeWorkspaceId),
      domainClient.getOrchestratorTimeline(activeWorkspaceId),
    ])
      .then(([gateDto, timelineDto]) => {
        const gate: NextTaskGate = {
          taskId: gateDto.taskId ?? "",
          taskName: gateDto.taskTitle ?? "",
          phaseId: "",
          phaseTitle: "",
          runnable: gateDto.runnable,
          recommendedAction: gateDto.recommendedAction ?? "",
          checks: gateDto.checks.map((c) => ({
            id: c.id,
            kind: c.kind as GateKind,
            label: c.label,
            detail: c.detail,
            status: c.status as GateCheckStatus,
          })),
        };
        if (gate.runnable) {
          setRunnableGate(gate);
        } else {
          setBlockedGate(gate);
        }
        if (timelineDto.steps.length > 0) {
          setTimelineTemplate(
            timelineDto.steps.map((s) => ({
              id: s.id,
              label: s.label,
              status: s.status as TimelineStep["status"],
            }))
          );
        }
      })
      .catch(() => {})
      .finally(() => setIsLoading(false));
  }, [activeWorkspaceId]);

  return { blockedGate, runnableGate, gateKindMeta: GATE_KIND_META, timelineTemplate, isLoading };
}

// ── Chat ──────────────────────────────────────────────────────

/** Map a single ChatApiMessage from the backend to the local ChatMessage shape. */
function mapApiMessage(m: ChatApiMessage, idx: number): ChatMessage {
  return {
    id: `msg-${m.timestamp}-${idx}`,
    role: m.role as ChatMessage["role"],
    content: m.content,
    timestamp: new Date(m.timestamp),
    agent: m.agentId as AgentId | undefined,
    runId: m.runId,
    logs: m.logs.length > 0 ? m.logs : undefined,
    artifacts:
      m.artifacts.length > 0
        ? m.artifacts.map((path) => ({
            path,
            label: path.split("/").pop() ?? path,
            action: "referenced" as const,
          }))
        : undefined,
    timeline: m.timeline?.steps.map((s) => ({
      id: s.id,
      label: s.label,
      status: s.status as TimelineStep["status"],
    })),
    nextCommand: m.nextCommand ?? undefined,
  };
}

export function useChatMessages(workspaceId: string | undefined): {
  messages: ChatMessage[];
  commandSuggestions: CommandSuggestion[];
  quickActions: QuickAction[];
  isLoading: boolean;
  isSubmitting: boolean;
  submitTurn: (input: string) => Promise<void>;
  refreshSnapshot: () => void;
} {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [commandSuggestions, setCommandSuggestions] = useState<CommandSuggestion[]>([]);
  const [quickActions, setQuickActions] = useState<QuickAction[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const loadSnapshot = useCallback(() => {
    if (!isGuidWorkspaceId(workspaceId)) {
      setMessages([]);
      setCommandSuggestions([]);
      setQuickActions([]);
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    domainClient
      .getChatSnapshot(workspaceId)
      .then((snapshot) => {
        setMessages(snapshot.messages.map(mapApiMessage));
        setCommandSuggestions(
          snapshot.commandSuggestions.map((s) => ({
            command: s.command,
            description: s.description,
            group: "",
          }))
        );
        setQuickActions(
          snapshot.quickActions.map((a) => ({
            label: a.label,
            command: a.command,
            variant: "default" as const,
          }))
        );
      })
      .catch(() => {})
      .finally(() => setIsLoading(false));
  }, [workspaceId]);

  useEffect(() => {
    loadSnapshot();
  }, [loadSnapshot]);

  const submitTurn = useCallback(
    async (input: string): Promise<void> => {
      if (!isGuidWorkspaceId(workspaceId) || isSubmitting) return;

      // Optimistically append the user message
      const userMsg: ChatMessage = {
        id: `msg-user-${Date.now()}`,
        role: "user",
        content: input,
        timestamp: new Date(),
        command: input.startsWith("aos ") ? input : undefined,
      };
      setMessages((prev) => [...prev, userMsg]);

      // Append a streaming placeholder
      const thinkingId = `msg-thinking-${Date.now()}`;
      setMessages((prev) => [
        ...prev,
        {
          id: thinkingId,
          role: "assistant",
          content: "",
          timestamp: new Date(),
          agent: "orchestrator",
          streaming: true,
        },
      ]);

      setIsSubmitting(true);
      try {
        const apiMsg = await domainClient.postChatTurn(workspaceId, input);
        const responseMsg = mapApiMessage(apiMsg, Date.now());
        setMessages((prev) =>
          prev.filter((m) => m.id !== thinkingId).concat(responseMsg)
        );
        // Refresh suggestions best-effort after each turn
        domainClient
          .getChatSnapshot(workspaceId)
          .then((snapshot) => {
            if (snapshot.commandSuggestions.length > 0) {
              setCommandSuggestions(
                snapshot.commandSuggestions.map((s) => ({
                  command: s.command,
                  description: s.description,
                  group: "",
                }))
              );
            }
            if (snapshot.quickActions.length > 0) {
              setQuickActions(
                snapshot.quickActions.map((a) => ({
                  label: a.label,
                  command: a.command,
                  variant: "default" as const,
                }))
              );
            }
          })
          .catch(() => {});
      } catch {
        toast.error("Failed to send message", {
          description: "Could not reach the API.",
        });
        setMessages((prev) => prev.filter((m) => m.id !== thinkingId));
      } finally {
        setIsSubmitting(false);
      }
    },
    [workspaceId, isSubmitting]
  );

  return {
    messages,
    commandSuggestions,
    quickActions,
    isLoading,
    isSubmitting,
    submitTurn,
    refreshSnapshot: loadSnapshot,
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
    try {
      const res = await daemonClient.submitCommand({ argv });
      const result = { ok: res.ok, output: res.output };
      setLastResult(result);
      return result;
    } catch {
      toast.error("Command failed", { description: "Could not reach the agent daemon." });
      const result = { ok: false, output: "Command failed" };
      setLastResult(result);
      return result;
    } finally {
      setIsRunning(false);
    }
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

  const test = useCallback(async (baseUrl: string): Promise<{ ok: boolean; version: string; latencyMs: number }> => {
    setIsTesting(true);
    try {
      const t0 = Date.now();
      const health = await new DaemonClient(baseUrl).getHealth();
      const result = { ok: health.ok, version: health.version, latencyMs: Date.now() - t0 };
      setLastPing(result);
      return result;
    } catch (err) {
      toast.error("Connection failed", {
        description: formatStartupDiagnostic(err, {
          endpoint: `GET ${baseUrl.replace(/\/$/, "")}/api/v1/health`,
          suggestedFix: "Check that the daemon is running, the base URL matches your dev config, and CORS allows http://localhost:5173.",
        }),
      });
      const result = { ok: false, version: "", latencyMs: 0 };
      setLastPing(result);
      return result;
    } finally {
      setIsTesting(false);
    }
  }, []);

  const save = useCallback(async (profile: { label: string; baseUrl: string; env: string }): Promise<{ ok: boolean }> => {
    setIsSaving(true);
    try {
      await daemonClient.setHostProfile({ hostName: profile.label, workspacePath: profile.env, metadata: { baseUrl: profile.baseUrl } });
      return { ok: true };
    } catch (err) {
      toast.error("Failed to save host profile", {
        description: formatStartupDiagnostic(err, {
          endpoint: "PUT /api/v1/service/host-profile",
          suggestedFix: "Check that the daemon is running, the base URL matches your dev config, and CORS allows http://localhost:5173.",
        }),
      });
      return { ok: false };
    } finally {
      setIsSaving(false);
    }
  }, []);

  return { test, save, isTesting, isSaving, lastPing };
}

/**
 * useWorkspaceInit
 * Stubs for POST /api/v1/commands with argv ["aos", "init"] and ["aos", "validate"].
 * Manages independent loading flags for init vs validate operations.
 */
export function useWorkspaceInit(workspaceId: string | undefined): {
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
    const resolvedRootPath = rootPath.trim();
    setIsIniting(true);
    try {
      const response = await daemonClient.submitCommand({ argv: ["aos", "init"], workingDirectory: resolvedRootPath });
      const aosDir = response.output.match(/^[^:]+:\s*(.+)$/)?.[1]?.trim() ?? "";
      const result = { ok: true, aosDir };
      setInitResult(result);
      return result;
    } catch (err) {
      toast.error("Workspace init failed", {
        description: formatStartupDiagnostic(err, {
          endpoint: "POST /api/v1/commands",
          suggestedFix: "Check that the daemon is running, the base URL matches your dev config, and CORS allows http://localhost:5173.",
        }),
      });
      const result = { ok: false, aosDir: "" };
      setInitResult(result);
      return result;
    } finally {
      setIsIniting(false);
    }
  }, []);

  const validate = useCallback(async (): Promise<ValidationResult> => {
    setIsValidating(true);
    const wp = workspaceId ?? "";
    const targets: (keyof ValidationResult)[] = ["schemas", "spec", "state", "evidence", "codebase"];
    try {
      const settled = await Promise.allSettled(
        targets.map((t) => daemonClient.submitCommand({ argv: ["aos", "validate", t] }))
      );
      const result: ValidationResult = {
        schemas: settled[0].status === "fulfilled" ? "valid" : "invalid",
        spec: settled[1].status === "fulfilled" ? "valid" : "invalid",
        state: settled[2].status === "fulfilled" ? "valid" : "invalid",
        evidence: settled[3].status === "fulfilled" ? "valid" : "invalid",
        codebase: settled[4].status === "fulfilled" ? "valid" : "invalid",
      };
      setValidationResult(result);
      return result;
    } catch (err) {
      toast.error("Validation failed", {
        description: formatStartupDiagnostic(err, {
          endpoint: "POST /api/v1/commands",
          suggestedFix: "Check that the daemon is running, the base URL matches your dev config, and CORS allows http://localhost:5173.",
        }),
      });
      const result: ValidationResult = { schemas: "invalid", spec: "invalid", state: "invalid", evidence: "invalid", codebase: "invalid" };
      setValidationResult(result);
      return result;
    } finally {
      setIsValidating(false);
    }
  }, [workspaceId]);

  return { init, validate, isIniting, isValidating, initResult, validationResult };
}
