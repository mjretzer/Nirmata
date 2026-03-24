import { useState, useCallback } from "react";
import { useNavigate } from "react-router";
import {
  FolderOpen,
  Plus,
  History,
  Pin,
  CheckCircle,
  AlertCircle,
  Activity,
  AlertTriangle,
  ArrowRight,
  ChevronUp,
  ChevronDown,
  Zap,
} from "lucide-react";
import { Button } from "../components/ui/button";
import { Input } from "../components/ui/input";
import { Label } from "../components/ui/label";
import { Checkbox } from "../components/ui/checkbox";
import { cn } from "../components/ui/utils";
import { toast } from "sonner";
import { useWorkspaces, useRegisterWorkspace } from "../hooks/useAosData";
import type { WorkspaceSummary } from "../hooks/useAosData";
import { WorkspaceStatusBadge } from "../components/WorkspaceStatusBadge";
import { relativeTime } from "../utils/format";

// ── Page ─────────────────────────────────────────────────────────

export function WorkspaceLauncherPage() {
  const navigate = useNavigate();
  const [recentOpen, setRecentOpen] = useState(true);

  // Inline form mode
  const [openMode, setOpenMode] = useState<"idle" | "init" | "confirm-init">("idle");
  const [pendingFolderName, setPendingFolderName] = useState<string | null>(null);
  const [confirmInitPath, setConfirmInitPath] = useState("");
  const [confirmInitPathError, setConfirmInitPathError] = useState("");

  // "Init New Project" form state
  const [initName, setInitName] = useState("");
  const [initNameError, setInitNameError] = useState("");
  const [initPath, setInitPath] = useState("");
  const [initPathError, setInitPathError] = useState("");
  const [initRunAosInit, setInitRunAosInit] = useState(true);

  const PATH_RE = /^(\/|[A-Za-z]:[/\\])/;
  const NAME_RE = /^[a-z0-9][a-z0-9-]*$/;

  const validatePath = (val: string) => {
    if (!val.trim()) return "Path is required";
    if (!PATH_RE.test(val.trim())) return "Must be an absolute path (e.g. /home/user/repo)";
    return "";
  };

  const handleOpenFolder = useCallback(async () => {
    type FullHandle = { name?: string; getDirectoryHandle: (n: string) => Promise<unknown> };
    const picker = (window as unknown as { showDirectoryPicker?: () => Promise<FullHandle> })
      .showDirectoryPicker;

    if (!picker) {
      toast.error("Folder picker is not supported in this browser");
      return;
    }

    try {
      const dirHandle = await picker();
      const name = dirHandle?.name?.trim() ?? "";
      if (!name) {
        toast.error("No folder selected");
        return;
      }

      // Check whether an .aos workspace exists in the selected folder.
      try {
        await dirHandle.getDirectoryHandle(".aos");
        navigate(`/ws/${name}`);
        toast.success(`Opened ${name}`);
      } catch (err) {
        const e = err as { name?: string } | undefined;
        if (e?.name === "NotFoundError") {
          setPendingFolderName(name);
          setOpenMode("confirm-init");
        } else {
          toast.error("Could not read folder contents");
        }
      }
    } catch (err) {
      const e = err as { name?: string } | undefined;
      if (e?.name === "AbortError") return;
      toast.error("Failed to open folder picker");
    }
  }, [navigate]);

  const handleInitNew = useCallback(() => {
    setOpenMode("init");
  }, []);

  const handleOpenWorkspace = useCallback(
    (ws: WorkspaceSummary) => {
      navigate(`/ws/${ws.projectName}`);
      toast.success(`Opened ${ws.alias ?? ws.projectName}`);
    },
    [navigate]
  );

  const { workspaces, refresh: refreshWorkspaces } = useWorkspaces();
  const { register: registerWorkspace, isRegistering } = useRegisterWorkspace();

  return (
    <div className="flex h-full items-start justify-center overflow-y-auto py-12 px-4">
      <div className="w-full max-w-md space-y-8">
        {/* ── Hero ──────────────────────────────────────────── */}
        <div
          className="border-2 border-dashed border-border/60 rounded-xl p-8 space-y-5 text-center"
          style={{
            background:
              "radial-gradient(ellipse at center, hsl(var(--muted)/0.3) 0%, transparent 70%)",
          }}
        >
          <div className="mx-auto w-16 h-16 relative">
            <div className="absolute inset-0 rounded-2xl bg-primary/10 animate-pulse" />
            <div className="relative h-full flex items-center justify-center">
              <Zap className="h-8 w-8 text-primary/80" aria-hidden="true" />
            </div>
          </div>

          <div className="space-y-1.5">
            <h1 className="text-xl">AOS Engine</h1>
            <p className="text-sm text-muted-foreground max-w-xs mx-auto">
              Open a folder to continue where you left off, or start a fresh
              project with a new workspace.
            </p>
          </div>

          <div className="space-y-2.5">
            <Button
              className="w-full gap-2 h-11 focus-visible:ring-2 focus-visible:ring-primary"
              onClick={handleOpenFolder}
              aria-label="Open an existing workspace folder"
            >
              <FolderOpen className="h-4 w-4" aria-hidden="true" />
              Open Folder
            </Button>
            <Button
              variant="outline"
              className="w-full gap-2 h-11 focus-visible:ring-2"
              onClick={handleInitNew}
              aria-label="Initialize a new project with .aos setup"
            >
              <Plus className="h-4 w-4" aria-hidden="true" />
              Init New Project
            </Button>

            {/* ── Initialize workspace prompt (empty folder selected) ── */}
            {openMode === "confirm-init" && pendingFolderName && (
              <div className="border border-dashed border-primary/40 rounded-lg p-4 space-y-3 mt-2">
                <div className="space-y-1">
                  <p className="text-sm">Initialize workspace</p>
                  <p className="text-xs text-muted-foreground">
                    <code className="font-mono">{pendingFolderName}</code> doesn't have an AOS
                    workspace yet. Enter its absolute path to continue.
                  </p>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="confirm-init-path" className="text-xs">Root path</Label>
                  <Input
                    id="confirm-init-path"
                    value={confirmInitPath}
                    placeholder="/absolute/path/to/your/repo"
                    className="h-8 text-xs font-mono"
                    onChange={(e) => {
                      setConfirmInitPath(e.target.value);
                      setConfirmInitPathError(validatePath(e.target.value));
                    }}
                  />
                  {confirmInitPathError && (
                    <p className="text-[10px] text-red-400">{confirmInitPathError}</p>
                  )}
                </div>
                <Button
                  className="w-full h-9 text-sm gap-2"
                  disabled={isRegistering}
                  onClick={async () => {
                    const pathErr = validatePath(confirmInitPath);
                    setConfirmInitPathError(pathErr);
                    if (pathErr) return;
                    const name = pendingFolderName!;
                    const created = await registerWorkspace(name, confirmInitPath.trim());
                    if (!created) return;
                    refreshWorkspaces();
                    navigate(`/ws/${created.name}/settings/workspace`, {
                      state: { rootPath: confirmInitPath.trim() },
                    });
                    toast.info("Save the root path and run aos init to finish setup");
                  }}
                >
                  <Zap className="h-4 w-4" aria-hidden="true" />
                  Initialize Workspace
                </Button>
                <Button
                  variant="ghost"
                  className="w-full h-8 text-xs"
                  onClick={() => {
                    setOpenMode("idle");
                    setPendingFolderName(null);
                    setConfirmInitPath("");
                    setConfirmInitPathError("");
                  }}
                >
                  Cancel
                </Button>
              </div>
            )}

            {/* ── Init New Project inline form ── */}
            {openMode === "init" && (
              <div className="border border-dashed border-border/40 rounded-lg p-4 space-y-3 mt-2">
                <div className="space-y-1.5">
                  <Label htmlFor="init-name" className="text-xs">Project name</Label>
                  <Input
                    id="init-name"
                    value={initName}
                    placeholder="my-app"
                    className="h-8 text-xs font-mono"
                    onChange={(e) => {
                      setInitName(e.target.value);
                      setInitNameError(NAME_RE.test(e.target.value) || !e.target.value ? "" : "Lowercase letters, numbers, and hyphens only");
                    }}
                  />
                  {initNameError && (
                    <p className="text-[10px] text-red-400">{initNameError}</p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="init-path" className="text-xs">Root path</Label>
                  <Input
                    id="init-path"
                    value={initPath}
                    placeholder="/absolute/path/to/your/repo"
                    className="h-8 text-xs font-mono"
                    onChange={(e) => {
                      setInitPath(e.target.value);
                      setInitPathError(validatePath(e.target.value));
                    }}
                  />
                  {initPathError && (
                    <p className="text-[10px] text-red-400">{initPathError}</p>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  <Checkbox
                    id="init-run-aos"
                    checked={initRunAosInit}
                    onCheckedChange={(v) => setInitRunAosInit(!!v)}
                  />
                  <Label htmlFor="init-run-aos" className="text-xs cursor-pointer">
                    Initialize .aos/ workspace
                  </Label>
                </div>
                <Button
                  className="w-full h-9 text-sm"
                  disabled={isRegistering}
                  onClick={async () => {
                    const nameErr = !NAME_RE.test(initName) ? "Invalid project name" : "";
                    const pathErr = validatePath(initPath);
                    setInitNameError(nameErr);
                    setInitPathError(pathErr);
                    if (nameErr || pathErr) return;
                    const created = await registerWorkspace(initName, initPath.trim());
                    if (!created) return;
                    refreshWorkspaces();
                    navigate(`/ws/${created.name}/settings/workspace`, {
                      state: { rootPath: initPath.trim() },
                    });
                    toast.info("Save the root path and run aos init to finish setup");
                  }}
                >
                  Create Workspace
                </Button>
                <Button
                  variant="ghost"
                  className="w-full h-8 text-xs"
                  onClick={() => {
                    setOpenMode("idle");
                    setInitName(""); setInitNameError("");
                    setInitPath(""); setInitPathError("");
                  }}
                >
                  Cancel
                </Button>
              </div>
            )}
          </div>
        </div>

        {/* ── Recent workspaces ─────────────────────────────── */}
        <div className="space-y-3">
          <p className="text-[11px] text-muted-foreground/40">
            {workspaces.length} workspace{workspaces.length !== 1 ? "s" : ""} ·{" "}
            {workspaces.filter((ws) => ws.status === "healthy").length} healthy
          </p>
          <button
            className="w-full flex items-center justify-between text-xs text-muted-foreground uppercase tracking-wider hover:text-foreground transition-colors focus-visible:outline-none focus-visible:text-foreground"
            onClick={() => setRecentOpen((o) => !o)}
            aria-expanded={recentOpen}
            aria-controls="recent-workspaces-list"
          >
            <span className="flex items-center gap-1.5">
              <History className="h-3.5 w-3.5" aria-hidden="true" />
              Recent Workspaces
            </span>
            {recentOpen ? (
              <ChevronUp className="h-3.5 w-3.5" aria-hidden="true" />
            ) : (
              <ChevronDown className="h-3.5 w-3.5" aria-hidden="true" />
            )}
          </button>

          {recentOpen && (
            <ul
              id="recent-workspaces-list"
              className="space-y-2"
              role="list"
              aria-label="Recent workspaces"
            >
              {workspaces.length === 0 ? (
                <li className="text-center py-8 text-sm text-muted-foreground/50 border border-dashed border-border/40 rounded-lg">
                  No recent workspaces.{" "}
                  <button
                    className="underline hover:no-underline focus-visible:outline-none"
                    onClick={handleOpenFolder}
                  >
                    Open a folder
                  </button>{" "}
                  to get started.
                </li>
              ) : (
                workspaces.map((ws) => (
                  <li key={ws.id}>
                    <div
                      className={cn(
                        "group rounded-lg border bg-card/50 p-3 space-y-2 transition-colors hover:bg-card focus-within:ring-2 focus-within:ring-primary/30",
                        ws.status === "invalid" || ws.status === "repair-needed"
                          ? "border-border/50"
                          : "border-border/40"
                      )}
                    >
                      <div className="flex items-start justify-between gap-2">
                        <div className="min-w-0 flex-1">
                          <div className="flex items-center gap-2">
                            <span className="text-sm truncate">
                              {ws.alias ?? ws.projectName}
                            </span>
                            {ws.pinned && (
                              <Pin
                                className="h-2.5 w-2.5 text-muted-foreground/40 shrink-0"
                                aria-label="Pinned workspace"
                              />
                            )}
                          </div>
                          <code className="text-[10px] text-muted-foreground/50 font-mono truncate block mt-0.5">
                            {ws.repoRoot}
                          </code>
                        </div>
                        <WorkspaceStatusBadge status={ws.status} />
                      </div>
                      {ws.status === "needs-init" && (
                        <button
                          type="button"
                          onClick={() => navigate(`/ws/${ws.projectName}/settings/workspace`)}
                          className="text-[10px] text-primary/60 hover:text-primary transition-colors flex items-center gap-1"
                        >
                          <Zap className="h-2.5 w-2.5" />
                          Run aos init →
                        </button>
                      )}

                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-3 text-[10px] text-muted-foreground/50">
                          {ws.lastRun?.status && ws.lastRun.id && (
                            <span className="flex items-center gap-1">
                              {ws.lastRun.status === "success" ? (
                                <CheckCircle
                                  className="h-3 w-3 text-green-500/60"
                                  aria-label="Last run succeeded"
                                />
                              ) : ws.lastRun.status === "failed" ? (
                                <AlertCircle
                                  className="h-3 w-3 text-red-500/60"
                                  aria-label="Last run failed"
                                />
                              ) : (
                                <Activity
                                  className="h-3 w-3 text-yellow-500/60"
                                  aria-label="Run in progress"
                                />
                              )}
                              {relativeTime(ws.lastOpened)}
                            </span>
                          )}
                          {ws.openIssuesCount > 0 && (
                            <span className="flex items-center gap-1 text-yellow-400/70">
                              <AlertTriangle className="h-3 w-3" aria-hidden="true" />
                              {ws.openIssuesCount} issue
                              {ws.openIssuesCount !== 1 ? "s" : ""}
                            </span>
                          )}
                        </div>
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-7 gap-1 text-xs opacity-0 group-hover:opacity-100 focus-visible:opacity-100 transition-opacity"
                          onClick={() => handleOpenWorkspace(ws)}
                          aria-label={`Open workspace ${ws.alias ?? ws.projectName}`}
                        >
                          Open
                          <ArrowRight className="h-3 w-3" aria-hidden="true" />
                        </Button>
                      </div>
                    </div>
                  </li>
                ))
              )}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}

export default WorkspaceLauncherPage;