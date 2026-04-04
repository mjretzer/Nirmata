/**
 * API Client
 *
 * BaseApiClient  — shared fetch wrapper (timeouts, JSON parsing, error normalization).
 * DaemonClient   — Agent Manager daemon endpoints (nirmata.Windows.Service.Api, port 9000).
 * DomainClient   — Workspace data endpoints (nirmata.Api, separate service/port).
 *
 * Routing rule (enforced at this layer):
 *   Daemon/host/service lifecycle features → DaemonClient (VITE_DAEMON_URL, default https://localhost:9000)
 *   Domain data features (workspaces/spec/tasks/runs/issues) → DomainClient (VITE_DOMAIN_URL, TBD)
 *
 * No third-party HTTP library is needed; native fetch covers all requirements.
 */

import { DAEMON_BASE_URL, DOMAIN_BASE_URL } from "../api/routing";

/**
 * Hook → Endpoint routing manifest
 *
 * ══════════════════════════════════════════════════════════════════════
 * DAEMON API  —  nirmata.Windows.Service.Api  (VITE_DAEMON_URL = https://localhost:9000)
 * ══════════════════════════════════════════════════════════════════════
 *
 * Routed via DaemonClient:
 *   WorkspaceContext (health poll)  GET  /api/v1/health                → reachability check (direct fetch, 30s cadence)
 *   useEngineConnection.test()      GET  /api/v1/health                → ServiceHealthResponse
 *   useEngineConnection.save()      PUT  /api/v1/service/host-profile  → void
 *   useAosCommand.execute()         POST /api/v1/commands              → CommandAcceptedResponse
 *   useWorkspaceInit.init()         POST /api/v1/commands              → CommandAcceptedResponse
 *   useWorkspaceInit.validate()     POST /api/v1/commands (×5)         → CommandAcceptedResponse
 *   useHostConsole()                GET  /api/v1/host/logs?limit       → ApiHostLogLine[]
 *                                   GET  /api/v1/host/surfaces         → ApiSurfaceStatus[]
 *   useDiagnostics()                GET  /api/v1/diagnostics           → DiagnosticsSnapshot
 *   (no hook yet)                   GET  /api/v1/service               → ServiceStatusResponse
 *   (no hook yet)                   POST /api/v1/service/start         → ServiceLifecycleResponse
 *   (no hook yet)                   POST /api/v1/service/stop          → ServiceLifecycleResponse
 *   (no hook yet)                   POST /api/v1/service/restart       → ServiceLifecycleResponse
 *
 * ══════════════════════════════════════════════════════════════════════
 * DOMAIN DATA API  —  nirmata.Api  (VITE_DOMAIN_URL)
 * ══════════════════════════════════════════════════════════════════════
 *
 * Routed via DomainClient:
 *   useWorkspaces()             GET  /v1/workspaces                                        → WorkspaceSummary[]
 *   useWorkspace(id)            GET  /v1/workspaces/:id                                    → WorkspaceSummary
 *   useBootstrapWorkspace()     POST /v1/workspaces/bootstrap                              → WorkspaceBootstrapResult
 *   useGitHubWorkspaceBootstrap() POST /v1/github/bootstrap/start                          → GitHubWorkspaceBootstrapStartResponse
 *   useMilestones(wsId)         GET  /v1/workspaces/:id/spec/milestones                   → MilestoneSummary[]
 *   usePhases(wsId)             GET  /v1/workspaces/:id/spec/phases                       → PhaseSummary[]
 *   useTasks(wsId, filters?)    GET  /v1/workspaces/:id/spec/tasks                        → TaskSummary[]
 *   useProjectSpec(wsId)        GET  /v1/workspaces/:id/spec/project                      → ProjectSpecResponse
 *   useFileSystem(path?)        GET  /v1/workspaces/:id/files/{*path}                     → DirectoryListingDto | file bytes
 *   useTaskPlans(taskId?)       GET  /api/v1/task-plans?taskId&phaseId          → TaskPlanSummary[]
 *   useRuns(taskId?)            GET  /v1/workspaces/:id/runs                    → WorkspaceRunSummary[]
 *   useIssues(filters?)         GET  /v1/workspaces/:id/issues?status&severity&taskId&...  → WorkspaceIssueDto[]
 *   useUatSummary()             GET  /v1/workspaces/:id/uat                      → WorkspaceUatSummaryDto
 *   useContinuityState()        GET  /v1/workspaces/:id/state                    → WorkspaceContinuityState
 *                               GET  /v1/workspaces/:id/state/handoff             → WorkspaceHandoff (404 → null)
 *                               GET  /v1/workspaces/:id/state/events              → WorkspaceEvent[]
 *                               GET  /v1/workspaces/:id/state/packs               → WorkspaceContextPack[]
 *   useCheckpoints()            GET  /v1/workspaces/:id/checkpoints              → WorkspaceCheckpointSummary[]
 *   useCodebaseIntel()          GET  /v1/workspaces/:id/codebase                → CodebaseInventoryDto
 *   useWorkspaceGateSummary()    GET  /v1/workspaces/:id/status                 → WorkspaceGateSummaryDto
 *   useOrchestratorState()      GET  /v1/workspaces/:id/orchestrator/gate       → OrchestratorGateDto
 *                               GET  /v1/workspaces/:id/orchestrator/timeline   → OrchestratorTimelineDto
 *   useChatMessages()           GET  /v1/workspaces/:id/chat                    → ChatSnapshot
 *                               POST /v1/workspaces/:id/chat                    → ChatApiMessage (OrchestratorMessage)
 *
 * DERIVED (no API call — computed from other hooks):
 *   useVerificationState()      derives from useTasks + useTaskPlans + useIssues + useRuns + usePhases
 */

const DEFAULT_TIMEOUT_MS = 10_000;

// ── Response types (mirror backend DTOs) ─────────────────────────────────────

// Daemon

export interface ServiceStatusResponse {
  ok: boolean;
  status: string;
}

export interface ServiceLifecycleResponse {
  ok: boolean;
}

export interface ServiceHealthResponse {
  ok: boolean;
  version: string;
  uptimeMs: number;
}

export interface CommandRequest {
  argv: string[];
  workingDirectory?: string;
}

export interface CommandAcceptedResponse {
  ok: boolean;
  output: string;
}

export interface HostProfileRequest {
  hostName: string;
  workspacePath: string;
  metadata?: Record<string, string>;
}

// Domain

export interface WorkspaceSummary {
  id: string;
  name: string;
  path: string;
  status: string;
  lastModified: string;
}

export interface WorkspaceCreateRequest {
  name: string;
  path: string;
}

export interface WorkspaceUpdateRequest {
  path: string;
}

export interface WorkspaceBootstrapResult {
  success: boolean;
  gitRepositoryCreated: boolean;
  aosScaffoldCreated: boolean;
  originConfigured?: boolean;
  error?: string | null;
  failureKind?: string;
}

export interface GitHubWorkspaceBootstrapStartRequest {
  path: string;
  name: string;
  repositoryName?: string;
  isPrivate?: boolean;
}

export interface GitHubWorkspaceBootstrapStartResponse {
  authorizeUrl: string;
}

export interface TaskSummary {
  id: string;
  phaseId: string;
  milestoneId: string;
  title: string;
  status: string;
}

export interface RunSummary {
  runId: string;
  taskId: string;
  status: string;
  startedAt: string;
  finishedAt?: string;
}

export interface TaskPlanSummary {
  taskId: string;
  phaseId: string;
  milestoneId: string;
  title: string;
  stepCount: number;
}

export interface IssueSummary {
  id: string;
  title: string;
  severity: string;
  status: string;
  taskId?: string;
  phaseId?: string;
  milestoneId?: string;
}

/** Full issue record returned by the workspace-scoped issues endpoint. */
export interface WorkspaceIssueDto {
  id: string;
  title: string;
  status: string;
  severity?: string;
  scope?: string;
  repro?: string;
  expected?: string;
  actual?: string;
  impactedFiles: string[];
  phaseId?: string;
  taskId?: string;
  milestoneId?: string;
}

/** Request body for POST /v1/workspaces/{wsId}/issues */
export interface WorkspaceIssueCreateRequest {
  title: string;
  severity?: string;
  scope?: string;
  repro?: string;
  expected?: string;
  actual?: string;
  impactedFiles?: string[];
  phaseId?: string;
  taskId?: string;
  milestoneId?: string;
}

export interface WorkspacePosition {
  milestoneId: string;
  phaseId: string;
  taskId: string;
  status: string;
}

export interface StateSummary {
  position: WorkspacePosition;
  decisions: object[];
  blockers: object[];
}

export interface HandoffSummary {
  cursor?: string;
  inFlightTaskId?: string;
  nextCommand?: string;
  writtenAt?: string;
}

export interface EventSummary {
  type: string;
  timestamp: string;
  payload?: string;
}

export interface ContextPackSummary {
  packId: string;
  mode: string;
  artifactCount: number;
}

export interface ContinuitySnapshot {
  state?: StateSummary;
  handoff?: HandoffSummary;
  recentEvents: EventSummary[];
  contextPacks: ContextPackSummary[];
}

// Workspace-scoped state types (Phase 5 endpoints under /v1/workspaces/:id/state/*)

export interface WorkspaceContinuityState {
  position?: {
    milestoneId?: string;
    phaseId?: string;
    taskId?: string;
    stepIndex?: number;
    status?: string;
  };
  decisions: object[];
  blockers: object[];
  lastTransition?: {
    from?: string;
    to?: string;
    timestamp?: string;
    trigger?: string;
  };
}

export interface WorkspaceHandoff {
  cursor?: {
    milestoneId?: string;
    phaseId?: string;
    taskId?: string;
    stepIndex?: number;
    status?: string;
  };
  inFlightTask?: string;
  inFlightStep?: number;
  allowedScope: string[];
  pendingVerification: boolean;
  nextCommand?: string;
  timestamp?: string;
}

export interface WorkspaceEvent {
  type?: string;
  timestamp?: string;
  payload?: string;
  references: string[];
}

export interface WorkspaceContextPack {
  packId: string;
  mode?: string;
  budgetTokens?: number;
  artifactCount: number;
}

export interface FilesystemNode {
  name: string;
  path: string;
  /** "file" or "directory" — matches DirectoryEntryDto.Type on the backend. */
  type: string;
  sizeBytes?: number;
  lastModified?: string;
  children: FilesystemNode[];
}

export interface ApiHostLogLine {
  id: number;
  ts: string;
  level: string;
  msg: string;
}

export interface ApiSurfaceStatus {
  name: string;
  path: string;
  ok: boolean;
  latencyMs?: number;
  reason?: string;
}

export interface ApiDiagLogEntry {
  label: string;
  lines: number;
  warnings: number;
  errors: number;
  path: string;
}

export interface ApiDiagArtifactEntry {
  name: string;
  size: string;
  type: string;
  path: string;
}

export interface ApiDiagLockEntry {
  id: string;
  scope: string;
  owner: string;
  acquired: string;
  stale: boolean;
}

export interface ApiDiagCacheEntry {
  label: string;
  size: string;
  path: string;
  stale: boolean;
}

export interface DiagnosticsSnapshot {
  logs: ApiDiagLogEntry[];
  artifacts: ApiDiagArtifactEntry[];
  locks: ApiDiagLockEntry[];
  cacheEntries: ApiDiagCacheEntry[];
}

export interface ApiCodebaseArtifact {
  id: string;
  name: string;
  type: string;
  description: string;
  status: string;
  lastUpdated: string;
  size: string;
  path: string;
}

export interface ApiLanguageBreakdown {
  name: string;
  pct: number;
  color: string;
}

export interface ApiStackEntry {
  name: string;
  category: string;
  color: string;
}

export interface CodebaseIntel {
  artifacts: ApiCodebaseArtifact[];
  languages: ApiLanguageBreakdown[];
  stack: ApiStackEntry[];
}

// Backend-exact DTOs for GET /v1/workspaces/{id}/codebase and /codebase/{artifactId}

export interface CodebaseArtifactDto {
  id: string;
  type: string;
  /** "ready" | "stale" | "missing" | "error" */
  status: string;
  path: string;
  lastUpdated: string | null;
}

export interface CodebaseInventoryDto {
  artifacts: CodebaseArtifactDto[];
  /** Language names detected in the workspace (from stack.json). */
  languages: string[];
  /** Framework / runtime names detected in the workspace (from stack.json). */
  stack: string[];
}

export interface CodebaseArtifactDetailDto {
  id: string;
  type: string;
  status: string;
  path: string;
  lastUpdated: string | null;
  /** Parsed JSON payload; absent when the file is missing or unreadable. */
  payload?: unknown;
}

// Orchestrator state

export interface OrchestratorGateCheck {
  kind: string;
  label: string;
  detail: string;
  status: string;
}

export interface OrchestratorGate {
  taskId: string;
  taskTitle: string;
  runnable: boolean;
  recommendedAction: string;
  checks: OrchestratorGateCheck[];
}

export interface OrchestratorTimelineStep {
  id: string;
  label: string;
  status: string;
}

export interface OrchestratorStateResponse {
  mode: string;
  currentGate: OrchestratorGate | null;
  timeline: OrchestratorTimelineStep[];
}

// Backend-exact DTOs for GET /v1/workspaces/{id}/orchestrator/gate and /timeline

export interface OrchestratorGateCheckDto {
  id: string;
  kind: string;
  label: string;
  detail: string;
  status: string;
}

export interface OrchestratorGateDto {
  taskId: string | null;
  taskTitle: string | null;
  runnable: boolean;
  recommendedAction: string | null;
  checks: OrchestratorGateCheckDto[];
}

export interface OrchestratorTimelineStepDto {
  id: string;
  label: string;
  status: string;
}

export interface OrchestratorTimelineDto {
  steps: OrchestratorTimelineStepDto[];
}

// Workspace gate summary — GET /v1/workspaces/{id}/status
// Mirrors WorkspaceGateSummaryDto and CodebaseReadinessSummaryDto from nirmata.Data.Dto/Models/WorkspaceStatus/

/** Brownfield codebase readiness details embedded in a workspace gate summary. */
export interface CodebaseReadinessSummaryDto {
  /** Freshness status: "missing" | "stale" | "ready" */
  mapStatus: string;
  /** Human-readable explanation of the readiness state. */
  detail: string;
  /** ISO timestamp when map.json was last written; null when absent. */
  lastUpdated: string | null;
}

/**
 * Workspace-scoped gate summary derived server-side from canonical artifacts.
 * Returned by GET /v1/workspaces/{workspaceId}/status.
 * Gates: "interview" | "codebase-preflight" | "roadmap" | "planning" |
 *        "execution" | "verification" | "fix" | "ready"
 */
export interface WorkspaceGateSummaryDto {
  /** Identifier of the current blocking gate. */
  currentGate: string;
  /** Artifact-backed explanation of why the workspace is blocked; null when gate is "ready". */
  blockingReason: string | null;
  /** The next CLI command or action the operator should run; null when gate is "ready". */
  nextRequiredStep: string | null;
  /** Brownfield codebase readiness details; present when map.json is missing or stale. */
  codebaseReadiness: CodebaseReadinessSummaryDto | null;
}

// Chat — types aligned with backend DTOs in nirmata.Data.Dto/Models/Chat/

/** POST /v1/workspaces/{workspaceId}/chat — request body */
export interface ChatTurnRequestDto {
  /** Freeform text or an explicit `aos …` command string. */
  input: string;
}

/**
 * A single message in the workspace chat thread.
 * Mirrors OrchestratorMessageDto from nirmata.Data.Dto.
 * Returned inside ChatSnapshot and as the body of POST /v1/workspaces/{id}/chat.
 */
export interface ChatApiMessage {
  /** Message role: "user" | "assistant" | "system" | "result" */
  role: string;
  content: string;
  /** Gate evaluation snapshot at the time this message was produced. Null for user-role messages. */
  gate?: OrchestratorGateDto;
  /** Artifact references produced or referenced during this turn. */
  artifacts: string[];
  /** Ordered timeline snapshot captured at the end of this turn. */
  timeline?: OrchestratorTimelineDto;
  /** Next recommended `aos` command surfaced by the orchestrator. */
  nextCommand?: string;
  /** Identifier of the agent run that produced this message (e.g. "RUN-…"). */
  runId?: string;
  /** Log lines captured during the run. */
  logs: string[];
  /** UTC timestamp when this message was produced. */
  timestamp: string;
  /** Identifier of the agent that produced this message. */
  agentId?: string;
}

/** Mirrors CommandSuggestionDto from nirmata.Data.Dto. */
export interface ChatApiSuggestion {
  command: string;
  description: string;
}

/** Mirrors QuickActionDto from nirmata.Data.Dto. */
export interface ChatApiQuickAction {
  label: string;
  command: string;
  /** Optional icon hint for the frontend (e.g. "play", "check", "refresh"). */
  icon?: string;
}

/**
 * Full chat snapshot for a workspace.
 * Mirrors ChatSnapshotDto from nirmata.Data.Dto.
 * Returned by GET /v1/workspaces/{workspaceId}/chat.
 */
export interface ChatSnapshot {
  messages: ChatApiMessage[];
  commandSuggestions: ChatApiSuggestion[];
  quickActions: ChatApiQuickAction[];
}

// Missing-endpoint response types (hooks currently use mock fallback)

export interface MilestoneSummary {
  id: string;
  title: string;
  status: string;
  phaseIds: string[];
}

export interface PhaseSummary {
  id: string;
  milestoneId: string;
  title: string;
  status: string;
  order: number;
  taskIds: string[];
}

export interface ProjectSpecResponse {
  name: string;
  description: string;
  version: string;
  owner: string;
  repo: string;
  milestones: string[];
  constraints: string[];
  tags: string[];
  createdAt: string;
  updatedAt: string;
}

export interface CheckpointSummary {
  id: string;
  timestamp: string;
  description: string;
  milestoneId: string;
  phaseId: string;
  taskId: string | null;
  source: "manual" | "auto";
}

export interface WorkspaceCheckpointPosition {
  milestoneId: string | null;
  phaseId: string | null;
  taskId: string | null;
  stepIndex: number | null;
  status: string | null;
}

export interface WorkspaceCheckpointSummary {
  id: string;
  position: WorkspaceCheckpointPosition | null;
  timestamp: string | null;
}

export interface WorkspaceRunSummary {
  id: string;
  taskId: string | null;
  status: string | null;
  timestamp: string | null;
}

// ── Error ─────────────────────────────────────────────────────────────────────

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly body?: unknown,
    public readonly endpoint?: string,
    public readonly suggestedFix?: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export interface ApiFailureDiagnostic {
  endpoint: string;
  status: number;
  detail: string;
  suggestedFix: string;
}

function inferSuggestedFix(endpoint: string, status: number): string {
  if (endpoint.includes("/api/v1/health") || endpoint.includes("/api/v1/commands") || endpoint.includes("/api/v1/service/host-profile")) {
    return "Check that the daemon is running, the base URL matches your dev config, and CORS allows https://localhost:8443.";
  }

  if (endpoint.includes("/v1/github/bootstrap")) {
    if (status === 400) {
      return "Check the GitHub OAuth client configuration (GitHub:ClientId, GitHub:ClientSecret) and confirm the callback URL is registered in your GitHub OAuth app.";
    }
    return "GitHub authorization failed. Check the OAuth app configuration and retry the connection.";
  }

  if (endpoint.includes("/v1/workspaces/")) {
    if (status === 404) {
      return "Verify the workspace identifier or path, then reopen or register the workspace.";
    }
    return "Check the domain API URL and confirm the workspace exists and is reachable.";
  }

  return "Check the configured API URL and try again.";
}

export function getApiFailureDiagnostic(
  err: unknown,
  fallback: { endpoint: string; suggestedFix?: string },
): ApiFailureDiagnostic {
  if (err instanceof ApiError) {
    return {
      endpoint: err.endpoint ?? fallback.endpoint,
      status: err.status,
      detail: err.message,
      suggestedFix: err.suggestedFix ?? fallback.suggestedFix ?? inferSuggestedFix(err.endpoint ?? fallback.endpoint, err.status),
    };
  }

  return {
    endpoint: fallback.endpoint,
    status: 0,
    detail: err instanceof Error ? err.message : String(err),
    suggestedFix: fallback.suggestedFix ?? inferSuggestedFix(fallback.endpoint, 0),
  };
}

export function formatApiFailureDiagnostic(diagnostic: ApiFailureDiagnostic): string {
  const statusText = diagnostic.status === 0 ? "network error / no HTTP status" : `HTTP ${diagnostic.status}`;
  return [
    `Endpoint: ${diagnostic.endpoint}`,
    `Status: ${statusText}`,
    `Detail: ${diagnostic.detail}`,
    `Suggested fix: ${diagnostic.suggestedFix}`,
  ].join("\n");
}

/**
 * Discriminated union for AOS hook error state.
 *   - "network": connection failure, timeout, DNS, or other transport-level problem (no HTTP status)
 *   - "server":  the server responded with a non-success HTTP status (4xx, 5xx)
 */
export type AosApiError =
  | { kind: "network"; message: string }
  | { kind: "server"; status: number; message: string };

/**
 * Maps any caught error to a typed AosApiError.
 * Used by AOS data hooks to normalise their error state consistently.
 *
 *   ApiError(status=0)   → { kind: "network", ... }   (timeout / refused / DNS)
 *   ApiError(status>0)   → { kind: "server",  ... }   (4xx / 5xx)
 *   Any other thrown val → { kind: "network", ... }   (unexpected throw)
 */
export function mapApiError(err: unknown): AosApiError {
  if (err instanceof ApiError) {
    if (err.status === 0) {
      return { kind: "network", message: err.message };
    }
    return { kind: "server", status: err.status, message: err.message };
  }
  return { kind: "network", message: err instanceof Error ? err.message : String(err) };
}

// ── BaseApiClient ─────────────────────────────────────────────────────────────

export class BaseApiClient {
  protected readonly baseUrl: string;
  protected readonly timeoutMs: number;
  private static readonly inFlightRequests = new Map<string, Promise<unknown>>();

  constructor(baseUrl: string, timeoutMs: number = DEFAULT_TIMEOUT_MS) {
    this.baseUrl = baseUrl.replace(/\/$/, "");
    this.timeoutMs = timeoutMs;
  }

  protected async request<T>(
    path: string,
    init: RequestInit = {},
  ): Promise<T> {
    const url = `${this.baseUrl}${path}`;
    const method = (init.method ?? "GET").toUpperCase();
    const cacheKey = `${method} ${url} ${init.body ?? ""}`;
    const existing = BaseApiClient.inFlightRequests.get(cacheKey);

    if (existing) {
      return existing as Promise<T>;
    }

    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);

    const requestPromise = (async () => {
      try {
        const res = await fetch(url, {
          credentials: "same-origin",
          ...init,
          signal: controller.signal,
          headers: {
            "Content-Type": "application/json",
            Accept: "application/json",
            ...init.headers,
          },
        });

        if (!res.ok) {
          let body: unknown;
          try {
            body = await res.json();
          } catch {
            body = await res.text().catch(() => undefined);
          }
          const problemDetail =
            typeof body === "object" && body !== null
              ? ((body as Record<string, unknown>)["detail"] as string | undefined)
              : undefined;
          throw new ApiError(
            res.status,
            problemDetail ?? `HTTP ${res.status}: ${res.statusText}`,
            body,
            url,
            inferSuggestedFix(url, res.status),
          );
        }

        // 204 No Content — return undefined cast to T
        if (res.status === 204) {
          return undefined as T;
        }

        return (await res.json()) as T;
      } catch (err) {
        if (err instanceof ApiError) throw err;
        if (err instanceof DOMException && err.name === "AbortError") {
          throw new ApiError(0, `Request timed out after ${this.timeoutMs}ms`, undefined, url, inferSuggestedFix(url, 0));
        }
        throw new ApiError(0, err instanceof Error ? err.message : String(err), undefined, url, inferSuggestedFix(url, 0));
      } finally {
        clearTimeout(timer);
      }
    })();

    let sharedPromise: Promise<T>;
    sharedPromise = requestPromise.finally(() => {
      if (BaseApiClient.inFlightRequests.get(cacheKey) === sharedPromise) {
        BaseApiClient.inFlightRequests.delete(cacheKey);
      }
    });

    BaseApiClient.inFlightRequests.set(cacheKey, sharedPromise as Promise<unknown>);
    return sharedPromise;
  }
}

// ── DaemonClient ──────────────────────────────────────────────────────────────

/**
 * Agent Manager daemon endpoints (nirmata.Windows.Service.Api).
 *   GET  /api/v1/health                — Service health & version
 *   GET  /api/v1/service               — Current service lifecycle status
 *   POST /api/v1/service/start         — Start service
 *   POST /api/v1/service/stop          — Stop service
 *   POST /api/v1/service/restart       — Restart service
 *   PUT  /api/v1/service/host-profile  — Register / update host profile
 *   POST /api/v1/commands              — Dispatch a workflow command
 *   GET  /api/v1/host/logs?limit       — Host console log lines
 *   GET  /api/v1/host/surfaces         — API surface health statuses
 *   GET  /api/v1/diagnostics           — Diagnostics snapshot
 */
export class DaemonClient extends BaseApiClient {
  getHealth(): Promise<ServiceHealthResponse> {
    return this.request<ServiceHealthResponse>("/api/v1/health", {
      cache: "no-store",
    });
  }

  getServiceStatus(): Promise<ServiceStatusResponse> {
    return this.request<ServiceStatusResponse>("/api/v1/service");
  }

  startService(): Promise<ServiceLifecycleResponse> {
    return this.request<ServiceLifecycleResponse>("/api/v1/service/start", { method: "POST" });
  }

  stopService(): Promise<ServiceLifecycleResponse> {
    return this.request<ServiceLifecycleResponse>("/api/v1/service/stop", { method: "POST" });
  }

  restartService(): Promise<ServiceLifecycleResponse> {
    return this.request<ServiceLifecycleResponse>("/api/v1/service/restart", { method: "POST" });
  }

  submitCommand(req: CommandRequest): Promise<CommandAcceptedResponse> {
    return this.request<CommandAcceptedResponse>("/api/v1/commands", {
      method: "POST",
      body: JSON.stringify(req),
    });
  }

  setHostProfile(req: HostProfileRequest): Promise<void> {
    return this.request<void>("/api/v1/service/host-profile", {
      method: "PUT",
      body: JSON.stringify(req),
    });
  }

  getHostLogs(limit = 100): Promise<ApiHostLogLine[]> {
    return this.request<ApiHostLogLine[]>(`/api/v1/host/logs?limit=${limit}`);
  }

  getHostSurfaces(): Promise<ApiSurfaceStatus[]> {
    return this.request<ApiSurfaceStatus[]>("/api/v1/host/surfaces");
  }

  getDiagnostics(): Promise<DiagnosticsSnapshot> {
    return this.request<DiagnosticsSnapshot>("/api/v1/diagnostics");
  }
}

// ── DomainClient ──────────────────────────────────────────────────────────────

export interface TaskFilters {
  phaseId?: string;
  milestoneId?: string;
  status?: string;
}

export interface RunFilters {
  taskId?: string;
  status?: string;
}

export interface TaskPlanFilters {
  taskId?: string;
  phaseId?: string;
}

export interface IssueFilters {
  status?: string;
  severity?: string;
  taskId?: string;
  phaseId?: string;
  milestoneId?: string;
}

/**
 * Workspace data endpoints (nirmata.Api).
 *   GET /v1/workspaces                                      — All workspaces
 *   GET /v1/workspaces/:id                                  — Single workspace
 *   POST /v1/workspaces                                     — Register workspace
 *   PUT /v1/workspaces/:id                                  — Update workspace path
 *   DELETE /v1/workspaces/:id                               — Deregister workspace
 *   GET /v1/workspaces/:id/spec/milestones                  — Milestones from .aos/spec/
 *   GET /v1/workspaces/:id/spec/phases                      — Phases from .aos/spec/
 *   GET /v1/workspaces/:id/spec/tasks                       — Tasks from .aos/spec/
 *   GET /v1/workspaces/:id/spec/project                     — Project spec from .aos/spec/
 *   GET /v1/workspaces/:id/files/{*path}                    — Workspace file tree / file content
 *   GET /api/v1/task-plans?...         — Task plans (filterable)
 *   GET /api/v1/runs?...               — Runs (filterable)
 *   GET /api/v1/issues?...             — Issues (filterable)
 *   GET /api/v1/checkpoints            — Checkpoints
 *   GET /api/v1/continuity             — State + events + context packs
 *   GET /v1/workspaces/:id/codebase              — Codebase artifact inventory + lang/stack intel
 *   GET /v1/workspaces/:id/codebase/:artifactId — Single codebase artifact detail
 *   GET /v1/workspaces/:id/orchestrator/gate    — Orchestrator gate checks + runnable status
 *   GET /v1/workspaces/:id/orchestrator/timeline — Ordered workspace timeline steps
 *   GET  /v1/workspaces/:id/chat       — Chat snapshot (thread + suggestions + actions)
 *   POST /v1/workspaces/:id/chat       — Submit a chat turn; returns OrchestratorMessage
 */
export class DomainClient extends BaseApiClient {
  getWorkspaces(): Promise<WorkspaceSummary[]> {
    return this.request<WorkspaceSummary[]>("/v1/workspaces");
  }

  getWorkspace(id: string): Promise<WorkspaceSummary> {
    return this.request<WorkspaceSummary>(`/v1/workspaces/${encodeURIComponent(id)}`);
  }

  createWorkspace(req: WorkspaceCreateRequest): Promise<WorkspaceSummary> {
    return this.request<WorkspaceSummary>("/v1/workspaces", {
      method: "POST",
      body: JSON.stringify(req),
    });
  }

  bootstrapWorkspace(path: string): Promise<WorkspaceBootstrapResult> {
    return this.request<WorkspaceBootstrapResult>("/v1/workspaces/bootstrap", {
      method: "POST",
      body: JSON.stringify({ path }),
    });
  }

  startGitHubWorkspaceBootstrap(req: GitHubWorkspaceBootstrapStartRequest): Promise<GitHubWorkspaceBootstrapStartResponse> {
    return this.request<GitHubWorkspaceBootstrapStartResponse>("/v1/github/bootstrap/start", {
      method: "POST",
      body: JSON.stringify(req),
    });
  }

  updateWorkspace(workspaceId: string, req: WorkspaceUpdateRequest): Promise<WorkspaceSummary> {
    return this.request<WorkspaceSummary>(`/v1/workspaces/${encodeURIComponent(workspaceId)}`, {
      method: "PUT",
      body: JSON.stringify(req),
    });
  }

  getTasks(workspaceId: string, filters?: TaskFilters): Promise<TaskSummary[]> {
    return this.request<TaskSummary[]>(`/v1/workspaces/${encodeURIComponent(workspaceId)}/spec/tasks`);
  }

  getRuns(filters?: RunFilters): Promise<RunSummary[]> {
    const params = new URLSearchParams();
    if (filters?.taskId) params.set("taskId", filters.taskId);
    if (filters?.status) params.set("status", filters.status);
    const qs = params.size > 0 ? `?${params}` : "";
    return this.request<RunSummary[]>(`/api/v1/runs${qs}`);
  }

  getTaskPlans(filters?: TaskPlanFilters): Promise<TaskPlanSummary[]> {
    const params = new URLSearchParams();
    if (filters?.taskId) params.set("taskId", filters.taskId);
    if (filters?.phaseId) params.set("phaseId", filters.phaseId);
    const qs = params.size > 0 ? `?${params}` : "";
    return this.request<TaskPlanSummary[]>(`/api/v1/task-plans${qs}`);
  }

  getIssues(filters?: IssueFilters): Promise<IssueSummary[]> {
    const params = new URLSearchParams();
    if (filters?.status) params.set("status", filters.status);
    if (filters?.severity) params.set("severity", filters.severity);
    if (filters?.taskId) params.set("taskId", filters.taskId);
    if (filters?.phaseId) params.set("phaseId", filters.phaseId);
    if (filters?.milestoneId) params.set("milestoneId", filters.milestoneId);
    const qs = params.size > 0 ? `?${params}` : "";
    return this.request<IssueSummary[]>(`/api/v1/issues${qs}`);
  }

  getWorkspaceIssues(workspaceId: string, filters?: IssueFilters): Promise<WorkspaceIssueDto[]> {
    const params = new URLSearchParams();
    if (filters?.status) params.set("status", filters.status);
    if (filters?.severity) params.set("severity", filters.severity);
    if (filters?.taskId) params.set("taskId", filters.taskId);
    if (filters?.phaseId) params.set("phaseId", filters.phaseId);
    if (filters?.milestoneId) params.set("milestoneId", filters.milestoneId);
    const qs = params.size > 0 ? `?${params}` : "";
    return this.request<WorkspaceIssueDto[]>(`/v1/workspaces/${encodeURIComponent(workspaceId)}/issues${qs}`);
  }

  createWorkspaceIssue(workspaceId: string, req: WorkspaceIssueCreateRequest): Promise<WorkspaceIssueDto> {
    return this.request<WorkspaceIssueDto>(`/v1/workspaces/${encodeURIComponent(workspaceId)}/issues`, {
      method: "POST",
      body: JSON.stringify(req),
    });
  }

  getContinuity(): Promise<ContinuitySnapshot> {
    return this.request<ContinuitySnapshot>("/api/v1/continuity");
  }

  /**
   * Fetches a workspace-relative path from the backend.
   *
   * - Directory  → backend returns `application/json` (DirectoryListingDto);
   *   this method maps the flat entry list to a FilesystemNode with children.
   * - File       → backend returns raw bytes with the file's Content-Type;
   *   this method returns a FilesystemNode with type "file" and no children.
   * - 404 / 403  → returns null.
   *
   * NOTE: Accept header is set to "*\/*" so the browser does not reject
   * non-JSON responses.  Do NOT route through request<T>() which forces
   * Accept: application/json and always calls res.json().
   */
  async getWorkspaceFiles(workspaceId: string, path?: string): Promise<FilesystemNode | null> {
    const pathSegment = path ? `/${path}` : "";
    const url = `${this.baseUrl}/v1/workspaces/${encodeURIComponent(workspaceId)}/files${pathSegment}`;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);

    try {
      const res = await fetch(url, {
        credentials: "same-origin",
        signal: controller.signal,
        headers: { Accept: "*/*" },
      });

      if (res.status === 404 || res.status === 403) return null;

      if (!res.ok) {
        throw new ApiError(res.status, `HTTP ${res.status}: ${res.statusText}`, undefined, url, inferSuggestedFix(url, res.status));
      }

      const contentType = res.headers.get("Content-Type") ?? "";

      if (contentType.includes("application/json")) {
        // Directory listing — maps DirectoryListingDto
        const listing = (await res.json()) as {
          path: string;
          entries: Array<{ name: string; path: string; type: string; sizeBytes?: number }>;
        };
        const segments = listing.path.split("/").filter(Boolean);
        return {
          name: segments[segments.length - 1] ?? "",
          path: listing.path,
          type: "directory",
          children: listing.entries.map((e) => ({
            name: e.name,
            path: e.path,
            type: e.type,
            sizeBytes: e.sizeBytes,
            children: [],
          })),
        };
      }

      // File — return a lightweight node (raw bytes not needed at hook level)
      const segments = (path ?? "").split("/").filter(Boolean);
      return {
        name: segments[segments.length - 1] ?? "",
        path: path ?? "",
        type: "file",
        children: [],
      };
    } catch (err) {
      if (err instanceof ApiError) throw err;
      if (err instanceof DOMException && err.name === "AbortError") {
        throw new ApiError(0, `Request timed out after ${this.timeoutMs}ms`, undefined, url, inferSuggestedFix(url, 0));
      }
      throw new ApiError(0, err instanceof Error ? err.message : String(err), undefined, url, inferSuggestedFix(url, 0));
    } finally {
      clearTimeout(timer);
    }
  }

  getCodebaseIntel(): Promise<CodebaseIntel> {
    return this.request<CodebaseIntel>("/api/v1/codebase/intel");
  }

  getOrchestratorState(): Promise<OrchestratorStateResponse> {
    return this.request<OrchestratorStateResponse>("/api/v1/orchestrator/state");
  }

  // Workspace-scoped codebase endpoints (phase 7)

  getWorkspaceCodebase(workspaceId: string): Promise<CodebaseInventoryDto> {
    return this.request<CodebaseInventoryDto>(`/v1/workspaces/${encodeURIComponent(workspaceId)}/codebase`);
  }

  getWorkspaceCodebaseArtifact(workspaceId: string, artifactId: string): Promise<CodebaseArtifactDetailDto> {
    return this.request<CodebaseArtifactDetailDto>(
      `/v1/workspaces/${encodeURIComponent(workspaceId)}/codebase/${encodeURIComponent(artifactId)}`,
    );
  }

  // Workspace gate summary — derived server-side from canonical artifacts

  getWorkspaceGateSummary(workspaceId: string): Promise<WorkspaceGateSummaryDto> {
    return this.request<WorkspaceGateSummaryDto>(
      `/v1/workspaces/${encodeURIComponent(workspaceId)}/status`,
    );
  }

  // Workspace-scoped orchestrator endpoints (phase 7)

  getOrchestratorGate(workspaceId: string): Promise<OrchestratorGateDto> {
    return this.request<OrchestratorGateDto>(
      `/v1/workspaces/${encodeURIComponent(workspaceId)}/orchestrator/gate`,
    );
  }

  getOrchestratorTimeline(workspaceId: string): Promise<OrchestratorTimelineDto> {
    return this.request<OrchestratorTimelineDto>(
      `/v1/workspaces/${encodeURIComponent(workspaceId)}/orchestrator/timeline`,
    );
  }

  getChatSnapshot(workspaceId: string): Promise<ChatSnapshot> {
    return this.request<ChatSnapshot>(`/v1/workspaces/${encodeURIComponent(workspaceId)}/chat`);
  }

  postChatTurn(workspaceId: string, input: string): Promise<ChatApiMessage> {
    return this.request<ChatApiMessage>(`/v1/workspaces/${encodeURIComponent(workspaceId)}/chat`, {
      method: "POST",
      body: JSON.stringify({ input } satisfies ChatTurnRequestDto),
    });
  }

  getMilestones(workspaceId: string): Promise<MilestoneSummary[]> {
    return this.request<MilestoneSummary[]>(`/v1/workspaces/${encodeURIComponent(workspaceId)}/spec/milestones`);
  }

  getPhases(workspaceId: string): Promise<PhaseSummary[]> {
    return this.request<PhaseSummary[]>(`/v1/workspaces/${encodeURIComponent(workspaceId)}/spec/phases`);
  }

  getProjectSpec(workspaceId: string): Promise<ProjectSpecResponse> {
    return this.request<ProjectSpecResponse>(`/v1/workspaces/${encodeURIComponent(workspaceId)}/spec/project`);
  }

  getCheckpoints(): Promise<CheckpointSummary[]> {
    return this.request<CheckpointSummary[]>("/api/v1/checkpoints");
  }

  getWorkspaceState(wsId: string): Promise<WorkspaceContinuityState> {
    return this.request<WorkspaceContinuityState>(`/v1/workspaces/${encodeURIComponent(wsId)}/state`);
  }

  async getWorkspaceHandoff(wsId: string): Promise<WorkspaceHandoff | null> {
    try {
      return await this.request<WorkspaceHandoff>(`/v1/workspaces/${encodeURIComponent(wsId)}/state/handoff`);
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) return null;
      throw err;
    }
  }

  getWorkspaceEvents(wsId: string, limit?: number): Promise<WorkspaceEvent[]> {
    const qs = limit !== undefined ? `?limit=${limit}` : "";
    return this.request<WorkspaceEvent[]>(`/v1/workspaces/${encodeURIComponent(wsId)}/state/events${qs}`);
  }

  getWorkspaceStatePacks(wsId: string): Promise<WorkspaceContextPack[]> {
    return this.request<WorkspaceContextPack[]>(`/v1/workspaces/${encodeURIComponent(wsId)}/state/packs`);
  }

  getWorkspaceCheckpoints(wsId: string): Promise<WorkspaceCheckpointSummary[]> {
    return this.request<WorkspaceCheckpointSummary[]>(`/v1/workspaces/${encodeURIComponent(wsId)}/checkpoints`);
  }

  getWorkspaceRuns(wsId: string): Promise<WorkspaceRunSummary[]> {
    return this.request<WorkspaceRunSummary[]>(`/v1/workspaces/${encodeURIComponent(wsId)}/runs`);
  }

  getWorkspaceUat(wsId: string): Promise<WorkspaceUatSummaryDto> {
    return this.request<WorkspaceUatSummaryDto>(`/v1/workspaces/${encodeURIComponent(wsId)}/uat`);
  }
}

// ── UAT summary types ─────────────────────────────────────────────────────────

export interface WorkspaceUatCheck {
  criterionId: string;
  passed: boolean;
  message?: string;
  checkType?: string;
}

export interface WorkspaceUatRecord {
  id: string;
  taskId?: string;
  phaseId?: string;
  status: string;
  observations?: string;
  reproSteps?: string;
  checks: WorkspaceUatCheck[];
}

export interface WorkspaceUatTaskSummary {
  taskId: string;
  /** One of: "passed", "failed", "unknown" */
  status: string;
  recordCount: number;
}

export interface WorkspaceUatPhaseSummary {
  phaseId: string;
  /** One of: "passed", "failed", "unknown" */
  status: string;
  taskIds: string[];
}

export interface WorkspaceUatSummaryDto {
  records: WorkspaceUatRecord[];
  taskSummaries: WorkspaceUatTaskSummary[];
  phaseSummaries: WorkspaceUatPhaseSummary[];
}

// ── Singletons ────────────────────────────────────────────────────────────────

export const daemonClient = new DaemonClient(DAEMON_BASE_URL);
export const domainClient = new DomainClient(DOMAIN_BASE_URL);
