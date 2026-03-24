import { useState, useCallback, useEffect } from "react";
import { useNavigate, useParams } from "react-router";
import {
  FolderOpen,
  Loader2,
  WifiOff,
  RefreshCw,
  ArrowRight,
  Folder,
  GitBranch,
  ExternalLink,
  Clock,
  CheckCircle,
  AlertCircle,
  Circle,
  Settings,
  Plus,
  AlertTriangle,
  ChevronRight,
  MessageSquare,
  History,
  FileJson,
  Shield,
  BookOpen,
  Map,
  ListTodo,
  Play,
  Database,
} from "lucide-react";
import { Badge } from "../components/ui/badge";
import { Button } from "../components/ui/button";
import { Label } from "../components/ui/label";
import { Switch } from "../components/ui/switch";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
  DialogTrigger,
  DialogClose,
} from "../components/ui/dialog";
import { Input } from "../components/ui/input";
import { cn } from "../components/ui/utils";
import { toast } from "sonner";
import { WorkspaceConfigPanel } from "../components/workspace-config-panel";
import {
  useWorkspaces,
  useCodebaseIntel,
  useOrchestratorState,
} from "../hooks/useAosData";
import type { WorkspaceSummary } from "../hooks/useAosData";
import { WorkspaceStatusBadge } from "../components/WorkspaceStatusBadge";
import { relativeTime } from "../utils/format";
import { useWorkspaceContext } from "../context/WorkspaceContext";

// ── Gating types ────────────────────────────────────────────────────
type GatingStep =
  | "no-workspace"
  | "missing-spec"
  | "missing-roadmap"
  | "needs-plan"
  | "ready-to-execute"
  | "needs-verify"
  | "needs-fix";

interface GatingMeta {
  label: string;
  cta: string;
  description: string;
  icon: React.ElementType;
  color: string;
  bgColor: string;
  borderColor: string;
  navPath: (wsName: string) => string;
  secondaryPath?: (wsName: string) => string;
  secondaryLabel?: string;
}

const gatingMap: Record<GatingStep, GatingMeta> = {
  "no-workspace": {
    label: "Open a Folder",
    cta: "Open Folder",
    description: "Select a repo or project directory to get started.",
    icon: FolderOpen,
    color: "text-muted-foreground",
    bgColor: "bg-muted/20",
    borderColor: "border-border",
    navPath: () => "/",
  },
  "missing-spec": {
    label: "Create Project Spec",
    cta: "Start Spec →",
    description: "Define your project's goals, constraints, and boundaries before generating a roadmap.",
    icon: FileJson,
    color: "text-blue-400",
    bgColor: "bg-blue-500/5",
    borderColor: "border-blue-500/20",
    navPath: (ws) => `/ws/${ws}/files/.aos/spec`,
    secondaryLabel: "Open Spec Folder",
    secondaryPath: (ws) => `/ws/${ws}/files/.aos/spec`,
  },
  "missing-roadmap": {
    label: "Generate Roadmap",
    cta: "Generate Roadmap →",
    description: "Your spec is ready. Break it into milestones and phases so the engine can plan work.",
    icon: Map,
    color: "text-purple-400",
    bgColor: "bg-purple-500/5",
    borderColor: "border-purple-500/20",
    navPath: (ws) => `/ws/${ws}/files/.aos/spec`,
    secondaryLabel: "View Spec",
    secondaryPath: (ws) => `/ws/${ws}/files/.aos/spec`,
  },
  "needs-plan": {
    label: "Create Task Plan",
    cta: "Create Plan →",
    description: "The current phase needs a task plan before execution can begin.",
    icon: ListTodo,
    color: "text-orange-400",
    bgColor: "bg-orange-500/5",
    borderColor: "border-orange-500/20",
    navPath: (ws) => `/ws/${ws}/orchestrator`,
    secondaryLabel: "View Roadmap",
    secondaryPath: (ws) => `/ws/${ws}/files/.aos/spec`,
  },
  "ready-to-execute": {
    label: "Execute Plan",
    cta: "Execute Plan →",
    description: "All prerequisites are met. The engine is ready to run the current task plan.",
    icon: Play,
    color: "text-green-400",
    bgColor: "bg-green-500/5",
    borderColor: "border-green-500/25",
    navPath: (ws) => `/ws/${ws}/orchestrator`,
    secondaryLabel: "View Task Plan",
    secondaryPath: (ws) => `/ws/${ws}/files/.aos/spec`,
  },
  "needs-verify": {
    label: "Verify Work",
    cta: "Run Verification →",
    description: "Execution is complete. Confirm correctness before moving to the next task.",
    icon: Shield,
    color: "text-cyan-400",
    bgColor: "bg-cyan-500/5",
    borderColor: "border-cyan-500/20",
    navPath: (ws) => `/ws/${ws}/files/.aos/spec/uat`,
    secondaryLabel: "View Evidence",
    secondaryPath: (ws) => `/ws/${ws}/files/.aos/evidence/runs`,
  },
  "needs-fix": {
    label: "Plan a Fix",
    cta: "Plan Fix →",
    description: "The last run failed. Review the issues and plan a fix before re-executing.",
    icon: AlertTriangle,
    color: "text-red-400",
    bgColor: "bg-red-500/5",
    borderColor: "border-red-500/20",
    navPath: (ws) => `/ws/${ws}/orchestrator`,
    secondaryLabel: "View Run Logs",
    secondaryPath: (ws) => `/ws/${ws}/files/.aos/evidence/runs`,
  },
};

// ── Orchestrator gate → gating step ──────────────────────────────────

function recommendedActionToGatingStep(action: string): GatingStep {
  switch (action) {
    case "new-project":    return "missing-spec";
    case "create-roadmap": return "missing-roadmap";
    case "plan-phase":     return "needs-plan";
    case "execute-plan":   return "ready-to-execute";
    case "verify-work":    return "needs-verify";
    case "plan-fix":       return "needs-fix";
    default:               return "ready-to-execute";
  }
}

// ── Skeleton loader ──────────────────────────────────────────────────

function Skeleton({ className }: { className?: string }) {
  return (
    <div className={cn("animate-pulse rounded bg-muted/40", className)} />
  );
}

function WorkspaceSkeleton() {
  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <Skeleton className="h-6 w-40" />
        <Skeleton className="h-4 w-64" />
      </div>
      <div className="h-px bg-border/40" />
      <Skeleton className="h-36 w-full rounded-xl" />
      <div className="grid grid-cols-2 gap-3">
        <Skeleton className="h-20 rounded-lg" />
        <Skeleton className="h-20 rounded-lg" />
      </div>
      <div className="flex gap-2">
        {[1, 2, 3, 4, 5].map((i) => (
          <Skeleton key={i} className="h-9 w-16 rounded-lg" />
        ))}
      </div>
    </div>
  );
}

// ── SwitchFolderDialog ────────────────────────────────────────────────
function SwitchFolderDialog({ workspaces }: { workspaces: WorkspaceSummary[] }) {
  const navigate = useNavigate();

  return (
    <Button
      variant="ghost"
      size="sm"
      className="text-xs gap-1.5 text-muted-foreground hover:text-foreground focus-visible:ring-2"
      aria-label="Switch to a different workspace folder"
      onClick={() => {
        const ws = workspaces[0];
        if (ws) {
          navigate(`/ws/${ws.projectName}`);
          toast.success(`Opened ${ws.alias ?? ws.projectName}`);
        }
      }}
    >
      <FolderOpen className="h-3.5 w-3.5" aria-hidden="true" />
      Switch Folder
    </Button>
  );
}

// ── InitNewDialog ─────────────────────────────────────────────────────
function InitNewDialog() {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [path, setPath] = useState("");
  const [createAos, setCreateAos] = useState(true);
  const [initGit, setInitGit] = useState(true);

  const nameError =
    name.trim() === ""
      ? ""
      : /[^a-z0-9_\-]/.test(name.trim())
      ? "Use lowercase letters, numbers, hyphens, or underscores only"
      : "";
  const pathError =
    path.trim() !== "" && !/^(\/|[A-Za-z]:[\\\/])/.test(path.trim())
      ? "Must be an absolute path (e.g. /home/user/my-app)"
      : "";
  const canSubmit = name.trim() !== "" && !nameError && !pathError;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    const _resolvedPath = path.trim() || `/Users/dev/projects/${name.trim()}`;
    const slug = name.trim();
    navigate(`/ws/${slug}`);
    toast.success(
      `${createAos ? ".aos/ created · " : ""}${initGit ? "git init · " : ""}Workspace ready → ${slug}`
    );
    setOpen(false);
    setName("");
    setPath("");
    setCreateAos(true);
    setInitGit(true);
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button
          variant="ghost"
          size="sm"
          className="text-xs gap-1.5 text-muted-foreground hover:text-foreground focus-visible:ring-2"
          aria-label="Initialize a new project workspace"
        >
          <Plus className="h-3.5 w-3.5" aria-hidden="true" />
          Initialize New
        </Button>
      </DialogTrigger>

      <DialogContent className="max-w-md gap-0 p-0 overflow-hidden">
        <form onSubmit={handleSubmit}>
          <DialogHeader className="px-5 pt-5 pb-4">
            <DialogTitle className="flex items-center gap-2 text-base">
              <Plus className="h-4 w-4 text-primary shrink-0" />
              Initialize New Workspace
            </DialogTitle>
            <DialogDescription className="text-xs">
              Creates a new project folder and bootstraps the AOS directory structure.
            </DialogDescription>
          </DialogHeader>

          <div className="px-5 pb-5 space-y-4 border-t border-border/40 pt-4">
            {/* Project name */}
            <div className="space-y-1.5">
              <Label htmlFor="init-name" className="text-xs">
                Project name <span className="text-red-400" aria-hidden="true">*</span>
              </Label>
              <Input
                id="init-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="my-new-app"
                className={cn(
                  "h-8 text-xs font-mono",
                  nameError && "border-red-500/50 focus-visible:ring-red-500/30"
                )}
                aria-invalid={!!nameError}
                aria-describedby={nameError ? "init-name-error" : undefined}
                autoComplete="off"
                spellCheck={false}
              />
              {nameError ? (
                <p id="init-name-error" role="alert" className="text-[10px] text-red-400 flex items-center gap-1">
                  <AlertCircle className="h-3 w-3 shrink-0" aria-hidden="true" />
                  {nameError}
                </p>
              ) : name.trim() && (
                <p className="text-[10px] text-muted-foreground/40 font-mono">
                  slug: {name.trim()}
                </p>
              )}
            </div>

            {/* Root path */}
            <div className="space-y-1.5">
              <Label htmlFor="init-path" className="text-xs">
                Root path
                <span className="ml-1.5 text-muted-foreground/40 font-normal">(optional — inferred from name if blank)</span>
              </Label>
              <div className="relative">
                <FolderOpen className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground/30 pointer-events-none" aria-hidden="true" />
                <Input
                  id="init-path"
                  value={path}
                  onChange={(e) => setPath(e.target.value)}
                  placeholder="/Users/dev/projects/my-new-app"
                  className={cn(
                    "h-8 pl-8 text-xs font-mono",
                    pathError && "border-red-500/50 focus-visible:ring-red-500/30"
                  )}
                  aria-invalid={!!pathError}
                  aria-describedby={pathError ? "init-path-error" : undefined}
                  autoComplete="off"
                  spellCheck={false}
                />
              </div>
              {pathError && (
                <p id="init-path-error" role="alert" className="text-[10px] text-red-400 flex items-center gap-1">
                  <AlertCircle className="h-3 w-3 shrink-0" aria-hidden="true" />
                  {pathError}
                </p>
              )}
            </div>

            {/* Options */}
            <div className="rounded-lg border border-border/40 bg-muted/10 divide-y divide-border/30">
              <div className="flex items-center justify-between px-4 py-3">
                <div>
                  <Label htmlFor="init-aos" className="text-xs cursor-pointer">
                    Create .aos/ structure
                  </Label>
                  <p className="text-[10px] text-muted-foreground/40 mt-0.5">
                    Bootstraps spec, roadmap, and config scaffolding
                  </p>
                </div>
                <Switch
                  id="init-aos"
                  checked={createAos}
                  onCheckedChange={setCreateAos}
                  aria-label="Create .aos directory structure"
                />
              </div>
              <div className="flex items-center justify-between px-4 py-3">
                <div>
                  <Label htmlFor="init-git" className="text-xs cursor-pointer">
                    Initialize git repo
                  </Label>
                  <p className="text-[10px] text-muted-foreground/40 mt-0.5">
                    Runs git init and creates an initial commit
                  </p>
                </div>
                <Switch
                  id="init-git"
                  checked={initGit}
                  onCheckedChange={setInitGit}
                  aria-label="Initialize git repository"
                />
              </div>
            </div>
          </div>

          <DialogFooter className="px-5 py-3 border-t border-border/40 gap-2">
            <DialogClose asChild>
              <Button variant="ghost" size="sm" type="button" className="text-xs">
                Cancel
              </Button>
            </DialogClose>
            <Button
              size="sm"
              type="submit"
              disabled={!canSubmit}
              className="text-xs gap-1.5"
              aria-label="Initialize workspace"
            >
              <Plus className="h-3.5 w-3.5" aria-hidden="true" />
              Initialize
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// ── Main component ────────────────────────────────────────────────────

export function WorkspaceDashboard() {
  const navigate = useNavigate();
  const { workspaceId } = useParams<{ workspaceId: string }>();

  const [loadError, setLoadError] = useState(false);
  const [configOpen, setConfigOpen] = useState(false);
  const [_recentOpen, _setRecentOpen] = useState(true);

  const { setActiveWorkspaceId } = useWorkspaceContext();
  const { workspaces: allWorkspaces, isLoading: wsListLoading, errorDiagnostic, refresh: refreshWorkspaces } = useWorkspaces();
  const { artifacts: codebaseArtifacts } = useCodebaseIntel();

  const { runnableGate, blockedGate, isLoading: gateLoading } = useOrchestratorState();
  const activeGate = runnableGate.runnable ? runnableGate : blockedGate;
  const gatingStep: GatingStep = recommendedActionToGatingStep(activeGate.recommendedAction);

  const isLoading = wsListLoading;

  // Resolve workspace from list using URL slug or UUID
  const workspace = workspaceId
    ? allWorkspaces.find((ws) => ws.projectName === workspaceId || ws.id === workspaceId) ?? null
    : null;

  // Sync activeWorkspaceId so gate and codebase hooks target the right workspace
  const resolvedId = workspace?.id;
  useEffect(() => {
    if (resolvedId) {
      setActiveWorkspaceId(resolvedId);
    }
  }, [resolvedId, setActiveWorkspaceId]);

  const handleRetry = useCallback(() => {
    setLoadError(false);
    refreshWorkspaces();
  }, [refreshWorkspaces]);

  const _handleOpenFolder = useCallback(() => {
    navigate(`/ws/my-app`);
    toast.success("Opened my-app workspace");
  }, [navigate]);

  const _handleInitNew = useCallback(() => {
    navigate(`/ws/new-project`);
    toast.success("Initialized new-project — .aos/ created");
  }, [navigate]);

  const _handleOpenWorkspace = useCallback(
    (ws: WorkspaceSummary) => {
      navigate(`/ws/${ws.projectName}`);
      toast.success(`Opened ${ws.alias ?? ws.projectName}`);
    },
    [navigate]
  );

  // ── Render: loading ──────────────────────────────────────────────
  const renderLoading = () => (
    <div className="w-full max-w-md px-6 py-12">
      <div className="flex items-center gap-2 mb-8 text-muted-foreground/60">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
        <span className="text-xs">Loading workspace…</span>
      </div>
      <WorkspaceSkeleton />
    </div>
  );

  // ── Render: error ────────────────────────────────────────────────
  const renderError = () => (
    <div className="w-full max-w-md px-6 py-12 text-center space-y-4">
      <div className="mx-auto h-12 w-12 rounded-full bg-destructive/10 flex items-center justify-center">
        <WifiOff className="h-5 w-5 text-destructive/70" aria-hidden="true" />
      </div>
      <div>
        <h2 className="text-base">Could not load workspace</h2>
        <p className="text-sm text-muted-foreground mt-1">
          {errorDiagnostic
            ? "The workspace request failed. Review the diagnostic details below and retry."
            : "The workspace data failed to load. Check your connection and try again."}
        </p>
      </div>
      {errorDiagnostic && (
        <div className="rounded-lg border border-border/60 bg-muted/20 p-3 text-left text-[11px] font-mono whitespace-pre-wrap text-muted-foreground/80">
          {errorDiagnostic}
        </div>
      )}
      <div className="flex gap-2 justify-center">
        <Button variant="outline" className="gap-2" onClick={handleRetry}>
          <RefreshCw className="h-4 w-4" aria-hidden="true" />
          Retry
        </Button>

        <Button variant="ghost" className="gap-2" onClick={() => navigate("/")}>
          Go Home
        </Button>
      </div>
    </div>
  );

  // ── Render: no workspace / onboarding ────────────────────────────
  const renderNotFound = () => (
    <div className="w-full max-w-md px-6 py-12 text-center space-y-5">
      <div className="mx-auto h-12 w-12 rounded-full bg-muted/20 border border-border/50 flex items-center justify-center">
        <FolderOpen className="h-5 w-5 text-muted-foreground/40" aria-hidden="true" />
      </div>
      <div className="space-y-1.5">
        <h2 className="text-base">Workspace not found</h2>
        <p className="text-sm text-muted-foreground">
          No workspace matching{" "}
          <code className="font-mono text-xs bg-muted/40 px-1.5 py-0.5 rounded border border-border/40">
            {workspaceId}
          </code>{" "}
          exists in this session.
        </p>
      </div>
      <div className="flex gap-2 justify-center">
        <Button
          variant="outline"
          className="gap-2"
          onClick={() => navigate("/")}
          aria-label="Go to workspace launcher"
        >
          <ArrowRight className="h-4 w-4 rotate-180" aria-hidden="true" />
          Go Home
        </Button>
      </div>
    </div>
  );

  // ── Render: workspace loaded ─────────────────────────────────────
  const renderWorkspace = (ws: WorkspaceSummary) => {
    const gating = gatingStep;
    const step = gatingMap[gating];
    const StepIcon = step.icon;

    return (
      <div className="space-y-6">
        {/* ── Workspace identity ─────────────────────────── */}
        <div className="space-y-2">
          <div className="flex items-start justify-between gap-3">
            <h1 className="text-xl leading-tight">{ws.alias ?? ws.projectName}</h1>
            <WorkspaceStatusBadge status={ws.status} />
          </div>
          <div className="flex flex-wrap items-center gap-3 text-xs text-muted-foreground/60 font-mono">
            <span className="flex items-center gap-1">
              <Folder className="h-3 w-3 shrink-0" aria-hidden="true" />
              <span className="truncate max-w-[18ch]">{ws.repoRoot}</span>
            </span>
            {ws.isGitRepo && (
              <span className="flex items-center gap-1">
                <GitBranch className="h-3 w-3 shrink-0" aria-hidden="true" />
                main
              </span>
            )}
          </div>
        </div>

        {/* Gradient rule */}
        <div
          className="h-px"
          style={{
            background:
              "linear-gradient(to right, transparent, hsl(var(--border)) 20%, hsl(var(--border)) 80%, transparent)",
          }}
          role="separator"
        />

        {/* ── Next Step card ─────── */}
        {gateLoading ? (
          <Skeleton className="h-36 w-full rounded-xl" />
        ) : (
        <div
          className={cn(
            "rounded-xl border p-5 space-y-4 relative overflow-hidden",
            step.bgColor,
            step.borderColor
          )}
          role="region"
          aria-label="Recommended next step"
        >
          <div
            className={cn(
              "absolute top-0 left-0 right-0 h-px opacity-60",
              gating === "ready-to-execute"
                ? "bg-green-400"
                : gating === "needs-fix"
                ? "bg-red-400"
                : gating === "needs-verify"
                ? "bg-cyan-400"
                : "bg-primary/40"
            )}
          />

          <div className="flex items-start gap-3">
            <div
              className={cn(
                "h-10 w-10 rounded-lg flex items-center justify-center shrink-0 border",
                step.bgColor,
                step.borderColor
              )}
            >
              <StepIcon className={cn("h-5 w-5", step.color)} aria-hidden="true" />
            </div>
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-[10px] uppercase tracking-widest text-muted-foreground/50">
                  Next Step
                </span>
              </div>
              <h2 className="text-base leading-tight mt-0.5">{step.label}</h2>
              <p className="text-xs text-muted-foreground mt-1 leading-relaxed">
                {step.description}
              </p>
            </div>
          </div>

          {(activeGate.phaseId || activeGate.taskId) && (
            <div className="flex items-center gap-1.5 flex-wrap">
              <span className="text-[10px] text-muted-foreground/40 uppercase tracking-wider">
                Position
              </span>
              {[activeGate.phaseId, activeGate.taskId]
                .filter(Boolean)
                .map((seg, i, arr) => (
                  <span key={seg} className="flex items-center gap-1">
                    <span
                      className={cn(
                        "inline-flex items-center px-2 py-0.5 rounded text-[10px] font-mono border",
                        i === arr.length - 1
                          ? cn("border", step.borderColor, step.color, step.bgColor)
                          : "bg-muted/40 border-border/50 text-muted-foreground/70"
                      )}
                    >
                      {seg}
                    </span>
                    {i < arr.length - 1 && (
                      <ChevronRight
                        className="h-3 w-3 text-muted-foreground/25"
                        aria-hidden="true"
                      />
                    )}
                  </span>
                ))}
            </div>
          )}

          <div className="flex items-center gap-2 flex-wrap">
            <Button
              className={cn(
                "gap-2 h-9 text-sm focus-visible:ring-2",
                gating === "ready-to-execute"
                  ? "bg-green-500/20 hover:bg-green-500/30 text-green-300 border border-green-500/30 shadow-none"
                  : gating === "needs-fix"
                  ? "bg-red-500/20 hover:bg-red-500/30 text-red-300 border border-red-500/30 shadow-none"
                  : ""
              )}
              onClick={() => navigate(step.navPath(ws.projectName))}
              aria-label={step.cta}
            >
              <StepIcon className="h-4 w-4" aria-hidden="true" />
              {step.cta}
            </Button>

            {step.secondaryPath && step.secondaryLabel && (
              <Button
                variant="ghost"
                size="sm"
                className="gap-1.5 text-xs text-muted-foreground hover:text-foreground focus-visible:ring-2"
                onClick={() =>
                  navigate(step.secondaryPath!(ws.projectName))
                }
                aria-label={step.secondaryLabel}
              >
                <ExternalLink className="h-3 w-3" aria-hidden="true" />
                {step.secondaryLabel}
              </Button>
            )}
          </div>

          {ws.lastRun?.id && (
            <div
              className="flex items-center gap-1.5 text-[10px] text-muted-foreground/40 border-t border-border/20 pt-3 mt-1"
              aria-label={`Last run: ${ws.lastRun.id}, status: ${ws.lastRun.status}`}
            >
              <Clock className="h-3 w-3" aria-hidden="true" />
              Last run:
              <span
                className={cn(
                  "font-mono",
                  ws.lastRun.status === "success"
                    ? "text-green-400/60"
                    : ws.lastRun.status === "failed"
                    ? "text-red-400/60"
                    : "text-yellow-400/60"
                )}
              >
                {ws.lastRun.id}
              </span>
              <span>· {relativeTime(ws.lastRun.timestamp)}</span>
              {ws.lastRun.status === "success" ? (
                <CheckCircle className="h-3 w-3 text-green-500/60" aria-label="Succeeded" />
              ) : ws.lastRun.status === "failed" ? (
                <AlertCircle className="h-3 w-3 text-red-500/60" aria-label="Failed" />
              ) : (
                <Loader2 className="h-3 w-3 text-yellow-500/60 animate-spin" aria-label="Running" />
              )}
            </div>
          )}
        </div>
        )}

        {/* ── Stats row ─────────────────────── */}
        <div className="grid grid-cols-2 gap-3" role="list" aria-label="Workspace status">
          <div
            className="rounded-lg border border-border/50 bg-card/40 p-3 space-y-1.5"
            role="listitem"
            aria-label="Last run status"
          >
            <span className="text-[10px] uppercase tracking-wider text-muted-foreground/50">
              Last Run
            </span>
            {ws.lastRun?.id ? (
              <div className="flex items-center gap-1.5 flex-wrap">
                {ws.lastRun.status === "success" ? (
                  <CheckCircle className="h-3.5 w-3.5 text-green-500 shrink-0" aria-label="Success" />
                ) : ws.lastRun.status === "failed" ? (
                  <AlertCircle className="h-3.5 w-3.5 text-red-500 shrink-0" aria-label="Failed" />
                ) : (
                  <Loader2 className="h-3.5 w-3.5 text-yellow-500 animate-spin shrink-0" aria-label="Running" />
                )}
                <span
                  className={cn(
                    "text-xs font-mono truncate",
                    ws.lastRun.status === "success"
                      ? "text-green-400"
                      : ws.lastRun.status === "failed"
                      ? "text-red-400"
                      : "text-yellow-400"
                  )}
                >
                  {ws.lastRun.id.slice(-8)}
                </span>
                <span className="text-[10px] text-muted-foreground/40">
                  {relativeTime(ws.lastRun.timestamp)}
                </span>
              </div>
            ) : (
              <span className="text-xs text-muted-foreground/40 italic">No runs yet</span>
            )}
          </div>

          <div
            className="rounded-lg border border-border/50 bg-card/40 p-3 space-y-1.5"
            role="listitem"
            aria-label="Open issues"
          >
            <span className="text-[10px] uppercase tracking-wider text-muted-foreground/50">
              Issues
            </span>
            {ws.openIssuesCount > 0 ? (
              <div className="flex items-center gap-1.5">
                <AlertTriangle
                  className="h-3.5 w-3.5 text-yellow-500 shrink-0"
                  aria-hidden="true"
                />
                <span className="text-xs text-yellow-400">
                  {ws.openIssuesCount} open
                </span>
              </div>
            ) : (
              <div className="flex items-center gap-1.5">
                <CheckCircle
                  className="h-3.5 w-3.5 text-green-500/50 shrink-0"
                  aria-hidden="true"
                />
                <span className="text-xs text-muted-foreground/50">None open</span>
              </div>
            )}
          </div>
        </div>

        {/* ── Git Repository ─────────────────── */}
        <div
          className={cn(
            "rounded-lg border p-3 space-y-2.5",
            ws.isGitRepo
              ? "border-border/50 bg-card/40"
              : "border-dashed border-border/40 bg-muted/10"
          )}
          role="region"
          aria-label="Git repository connection"
        >
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-1.5">
              <GitBranch className="h-3.5 w-3.5 text-primary/60 shrink-0" aria-hidden="true" />
              <span className="text-[10px] uppercase tracking-wider text-muted-foreground/50">
                Git Repository
              </span>
            </div>
            {ws.isGitRepo ? (
              <span className="flex items-center gap-1 text-[10px] text-green-400">
                <CheckCircle className="h-3 w-3" aria-hidden="true" />
                Connected
              </span>
            ) : (
              <span className="flex items-center gap-1 text-[10px] text-muted-foreground/40">
                <WifiOff className="h-3 w-3" aria-hidden="true" />
                Not initialised
              </span>
            )}
          </div>

          {ws.isGitRepo ? (
            <div className="space-y-2">
              {ws.gitRemote ? (
                <div className="flex items-center gap-1.5">
                  <ExternalLink className="h-3 w-3 text-muted-foreground/30 shrink-0" aria-hidden="true" />
                  <code className="text-[11px] font-mono text-primary/60 truncate">
                    {ws.gitRemote}
                  </code>
                </div>
              ) : (
                <div className="flex items-center gap-1.5 text-[11px] text-yellow-400/70">
                  <AlertCircle className="h-3 w-3 shrink-0" aria-hidden="true" />
                  <span>No remote configured</span>
                </div>
              )}

              <div className="flex items-center gap-3 text-[10px] font-mono text-muted-foreground/50 flex-wrap">
                {ws.gitBranch && (
                  <span className="flex items-center gap-1">
                    <GitBranch className="h-3 w-3 shrink-0" aria-hidden="true" />
                    {ws.gitBranch}
                  </span>
                )}
                <span className="flex items-center gap-1">
                  <Circle className="h-2 w-2 fill-muted-foreground/30 shrink-0" aria-hidden="true" />
                  a4f2c91
                </span>
                {ws.gitLastSync && (
                  <span className="flex items-center gap-1 text-muted-foreground/30">
                    <Clock className="h-3 w-3 shrink-0" aria-hidden="true" />
                    synced {relativeTime(ws.gitLastSync)}
                  </span>
                )}
              </div>

              {(ws.aheadCount !== undefined || ws.behindCount !== undefined) && (
                <div className="flex items-center gap-2 text-[10px] font-mono">
                  <span className={cn(
                    "flex items-center gap-1",
                    (ws.aheadCount ?? 0) > 0 ? "text-primary/60" : "text-muted-foreground/40"
                  )}>
                    <ArrowRight className="h-3 w-3 rotate-[-90deg] shrink-0" aria-hidden="true" />
                    {ws.aheadCount ?? 0} ahead
                  </span>
                  <span className="text-muted-foreground/20">·</span>
                  <span className={cn(
                    "flex items-center gap-1",
                    (ws.behindCount ?? 0) > 0 ? "text-yellow-400/70" : "text-muted-foreground/40"
                  )}>
                    <ArrowRight className="h-3 w-3 rotate-90 shrink-0" aria-hidden="true" />
                    {ws.behindCount ?? 0} behind origin
                  </span>
                </div>
              )}

              <div className="flex items-center gap-1 pt-0.5 border-t border-border/20">
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-7 gap-1.5 text-xs text-muted-foreground hover:text-foreground px-2 focus-visible:ring-2"
                  onClick={() => toast.success("Fetching from origin…")}
                  aria-label="Fetch from remote"
                >
                  <RefreshCw className="h-3 w-3" aria-hidden="true" />
                  Fetch
                </Button>
                {ws.gitRemote && (
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 gap-1.5 text-xs text-muted-foreground hover:text-foreground px-2 focus-visible:ring-2"
                    onClick={() => toast.info("Opening remote in browser…")}
                    aria-label="Open remote repository"
                  >
                    <ExternalLink className="h-3 w-3" aria-hidden="true" />
                    Open Remote
                  </Button>
                )}
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-7 gap-1.5 text-xs text-muted-foreground hover:text-foreground ml-auto px-2 focus-visible:ring-2"
                  onClick={() => navigate(`/ws/${ws.projectName}/settings/git`)}
                  aria-label="Configure git settings"
                >
                  <Settings className="h-3 w-3" aria-hidden="true" />
                  Configure
                </Button>
              </div>
            </div>
          ) : (
            <div className="space-y-2">
              <p className="text-[11px] text-muted-foreground/40 leading-relaxed">
                This folder is not a git repository. Initialise one to enable commit
                tracking, branching, and remote sync.
              </p>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  className="flex-1 gap-1.5 h-8 text-xs border-dashed focus-visible:ring-2"
                  onClick={() => toast.info("Run: git init in your project root")}
                  aria-label="Initialize git repository"
                >
                  <Plus className="h-3.5 w-3.5" aria-hidden="true" />
                  git init
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  className="flex-1 gap-1.5 h-8 text-xs border-dashed focus-visible:ring-2"
                  onClick={() => toast.info("Paste a remote URL to clone")}
                  aria-label="Clone from remote"
                >
                  <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
                  Clone Remote
                </Button>
              </div>
            </div>
          )}
        </div>

        {/* ── Quick actions ─────────────────────────────────── */}
        <nav aria-label="Quick workspace actions">
          <p className="text-[10px] uppercase tracking-wider text-muted-foreground/40 mb-2">
            Jump to
          </p>
          <div className="flex flex-wrap gap-2">
            {[
              {
                label: "Chat",
                icon: MessageSquare,
                path: `/ws/${ws.projectName}/chat`,
                aria: "Open chat with AOS engine",
              },
              {
                label: "Runs",
                icon: History,
                path: `/ws/${ws.projectName}/files/.aos/evidence/runs`,
                aria: "View run history",
              },
              {
                label: "Spec",
                icon: FileJson,
                path: `/ws/${ws.projectName}/files/.aos/spec`,
                aria: "View project spec",
              },
              {
                label: "Verify",
                icon: Shield,
                path: `/ws/${ws.projectName}/files/.aos/spec/uat`,
                aria: "View verification",
              },
              {
                label: "State",
                icon: BookOpen,
                path: `/ws/${ws.projectName}/files/.aos/state`,
                aria: "View continuity state",
              },
              {
                label: "Settings",
                icon: Settings,
                path: `/ws/${ws.projectName}/settings`,
                aria: "Workspace settings",
              },
            ].map((item) => (
              <Button
                key={item.label}
                variant="outline"
                size="sm"
                className="gap-1.5 h-8 text-xs focus-visible:ring-2 focus-visible:ring-primary"
                onClick={() => navigate(item.path)}
                aria-label={item.aria}
              >
                <item.icon className="h-3.5 w-3.5" aria-hidden="true" />
                {item.label}
              </Button>
            ))}
          </div>
        </nav>

        {/* ── Readiness ─────────────────────── */}
        <div className="space-y-2" role="region" aria-label="Context readiness">
          {(() => {
            const intelArtifacts = codebaseArtifacts.filter(a => a.type === "intel");
            const readyCount = intelArtifacts.filter(a => a.status === "ready").length;
            const staleCount = intelArtifacts.filter(a => a.status === "stale").length;
            const missingCount = intelArtifacts.filter(a => a.status === "missing" || a.status === "error").length;
            const totalCount = intelArtifacts.length;
            const allContextReady = staleCount === 0 && missingCount === 0;
            const hasWarnings = staleCount > 0 && missingCount === 0;

            return (
              <div className={cn(
                "rounded-lg border p-3 space-y-2.5",
                allContextReady
                  ? "border-green-500/20 bg-green-500/5"
                  : hasWarnings
                  ? "border-yellow-500/20 bg-yellow-500/5"
                  : "border-red-500/20 bg-red-500/5"
              )}>
                {/* Header row */}
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5">
                    <Database className="h-3.5 w-3.5 text-primary/60 shrink-0" aria-hidden="true" />
                    <span className="text-[10px] uppercase tracking-wider text-muted-foreground/50">
                      Context Ready
                    </span>
                  </div>
                  <div className="flex items-center gap-1.5">
                    {allContextReady ? (
                      <span className="flex items-center gap-1 text-[10px] text-green-400">
                        <CheckCircle className="h-3 w-3" aria-hidden="true" />
                        All ready
                      </span>
                    ) : hasWarnings ? (
                      <span className="flex items-center gap-1 text-[10px] text-yellow-400">
                        <AlertTriangle className="h-3 w-3" aria-hidden="true" />
                        {staleCount} stale
                      </span>
                    ) : (
                      <span className="flex items-center gap-1 text-[10px] text-red-400">
                        <AlertCircle className="h-3 w-3" aria-hidden="true" />
                        {missingCount} missing
                      </span>
                    )}
                  </div>
                </div>

                {/* Count pills */}
                <div className="flex items-center gap-2 text-[10px] font-mono flex-wrap">
                  <span className="flex items-center gap-1 text-green-400/80">
                    <span className="tabular-nums">{readyCount}</span>
                    <span className="text-muted-foreground/40">/ {totalCount} ready</span>
                  </span>
                  {staleCount > 0 && (
                    <Badge variant="outline" className="h-4 px-1.5 text-[9px] border-yellow-500/20 text-yellow-400/70 bg-yellow-500/5 normal-case tracking-normal">
                      {staleCount} stale
                    </Badge>
                  )}
                  {missingCount > 0 && (
                    <Badge variant="outline" className="h-4 px-1.5 text-[9px] border-red-500/20 text-red-400/70 bg-red-500/5 normal-case tracking-normal">
                      {missingCount} missing
                    </Badge>
                  )}
                </div>

                {/* Artifact status bar */}
                <div className="flex gap-px h-1 rounded-full overflow-hidden" aria-label="Intel file status breakdown" role="img">
                  {intelArtifacts.map((a) => (
                    <div
                      key={a.id}
                      className={cn(
                        "flex-1",
                        a.status === "ready" ? "bg-green-500/60" :
                        a.status === "stale" ? "bg-yellow-500/60" :
                        "bg-red-500/60"
                      )}
                      title={`${a.name}: ${a.status}`}
                    />
                  ))}
                </div>

                {/* Footer link */}
                <div className="flex items-center justify-between pt-0.5 border-t border-border/20">
                  <span className="text-[10px] text-muted-foreground/40">
                    .aos/intel/ · {intelArtifacts.length} files tracked
                  </span>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-6 gap-1 text-[10px] text-muted-foreground/50 hover:text-foreground px-1.5 focus-visible:ring-2"
                    onClick={() => navigate(`/ws/${ws.projectName}/files/.aos/codebase`)}
                    aria-label="Open codebase context page"
                  >
                    View Codebase
                    <ArrowRight className="h-3 w-3" aria-hidden="true" />
                  </Button>
                </div>
              </div>
            );
          })()}
        </div>

        {/* ── Config panel ──────── */}
        <WorkspaceConfigPanel
          wsName={ws.projectName}
          isOpen={configOpen}
          onToggle={() => setConfigOpen((o) => !o)}
          inline
        />

        {/* Gradient rule */}
        <div
          className="h-px"
          style={{
            background:
              "linear-gradient(to right, transparent, hsl(var(--border)) 20%, hsl(var(--border)) 80%, transparent)",
          }}
          role="separator"
        />

        {/* ── Switch workspace ─────────────────────────────── */}
        <div
          className="flex items-center justify-center gap-2 pt-1"
          role="group"
          aria-label="Change workspace"
        >
          <SwitchFolderDialog workspaces={allWorkspaces} />
          <span className="text-muted-foreground/20 text-xs" aria-hidden="true">
            ·
          </span>
          <InitNewDialog />
        </div>
      </div>
    );
  };

  // ── Root layout ───────────────────────────────────────────────────
  return (
    <div className="flex h-full items-start justify-center overflow-y-auto py-12 px-4">
      <div className="w-full max-w-md">
        {isLoading
          ? renderLoading()
          : loadError || !!errorDiagnostic
          ? renderError()
          : workspace
          ? renderWorkspace(workspace)
          : renderNotFound()}
      </div>
    </div>
  );
}

export default WorkspaceDashboard;