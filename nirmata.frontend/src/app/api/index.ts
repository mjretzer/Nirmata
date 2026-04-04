/**
 * Public API surface — import from here, not from utils/apiClient directly.
 *
 * Routing map (base URLs):
 *   import { API_ROUTING, DAEMON_BASE_URL, DOMAIN_BASE_URL } from "@/app/api";
 *
 * Classes / singletons:
 *   import { domainClient, daemonClient, DaemonClient, ApiError } from "@/app/api";
 *
 * Response types:
 *   import type { WorkspaceSummary, TaskSummary, MilestoneSummary } from "@/app/api";
 */

// Routing map — single source of truth for base URLs
export { API_ROUTING, DAEMON_BASE_URL, DOMAIN_BASE_URL } from "./routing";

// Classes & singletons
export {
  ApiError,
  BaseApiClient,
  DaemonClient,
  DomainClient,
  daemonClient,
  domainClient,
} from "../utils/apiClient";

// Daemon response types
export type {
  ServiceHealthResponse,
  ServiceStatusResponse,
  ServiceLifecycleResponse,
  CommandRequest,
  CommandAcceptedResponse,
  HostProfileRequest,
} from "../utils/apiClient";

// Domain response types — wired
export type {
  WorkspaceSummary,
  TaskSummary,
  TaskFilters,
  RunSummary,
  RunFilters,
  TaskPlanSummary,
  TaskPlanFilters,
  IssueSummary,
  IssueFilters,
  ContinuitySnapshot,
  StateSummary,
  HandoffSummary,
  EventSummary,
  ContextPackSummary,
  WorkspacePosition,
  FilesystemNode,
  ApiHostLogLine,
  ApiSurfaceStatus,
  ApiDiagLogEntry,
  ApiDiagArtifactEntry,
  ApiDiagLockEntry,
  ApiDiagCacheEntry,
  DiagnosticsSnapshot,
  ApiCodebaseArtifact,
  ApiLanguageBreakdown,
  ApiStackEntry,
  CodebaseIntel,
  OrchestratorGateCheck,
  OrchestratorGate,
  OrchestratorTimelineStep,
  OrchestratorStateResponse,
  WorkspaceGateSummaryDto,
  CodebaseReadinessSummaryDto,
  ChatTurnRequestDto,
  ChatApiMessage,
  ChatApiSuggestion,
  ChatApiQuickAction,
  ChatSnapshot,
} from "../utils/apiClient";

// Domain response types — missing endpoints (hooks use mock fallback until backends exist)
export type {
  MilestoneSummary,
  PhaseSummary,
  ProjectSpecResponse,
  CheckpointSummary,
} from "../utils/apiClient";
