import { Link, Outlet, useLocation } from "react-router";
import {
  LayoutDashboard,
  Compass,
  Shield,
  History,
  Code,
  Settings,
  Copy,
  PauseCircle,
  GitBranch,
  MessageSquare,
  RefreshCw,
} from "lucide-react";
import { toast } from "sonner";
import { copyToClipboard } from "../utils/clipboard";
import { useState, useMemo, useEffect } from "react";

import { DiagnosticsDrawer } from "./layout/DiagnosticsDrawer";
import { GlobalCommandPalette } from "./layout/GlobalCommandPalette";
import { ArtifactsDrawer } from "./layout/ArtifactsDrawer";
import { TopRibbon } from "./layout/TopRibbon";
import { FileExplorer } from "./layout/FileExplorer";
import { cn } from "./ui/utils";

import { getAosLink } from "../utils/aosResolver";
import { useWorkspaceContext, type EngineStatus } from "../context/WorkspaceContext";
import { isGuidWorkspaceId, useWorkspace, useWorkspaces } from "../hooks/useAosData";

const engineStatusConfig: Record<EngineStatus, { label: string; color: string; dot: string }> = {
  idle:    { label: "Idle",              color: "text-muted-foreground", dot: "bg-muted-foreground" },
  running: { label: "Running",           color: "text-green-400",       dot: "bg-green-500" },
  paused:  { label: "Paused",            color: "text-yellow-400",      dot: "bg-yellow-500" },
  waiting: { label: "Waiting for input", color: "text-blue-400",        dot: "bg-blue-500" },
};

function getNavigation(workspaceId: string) {
  return [
    { name: "Workspace", href: `/ws/${workspaceId}`, icon: LayoutDashboard },
    { name: "Chat", href: `/ws/${workspaceId}/chat`, icon: MessageSquare },
    { name: "Plan", href: getAosLink(workspaceId, ".aos/spec"), icon: Compass },
    { name: "Verification", href: getAosLink(workspaceId, ".aos/spec/uat"), icon: Shield },
    { name: "Runs", href: getAosLink(workspaceId, ".aos/evidence/runs"), icon: History },
    { name: "Continuity", href: getAosLink(workspaceId, ".aos/state"), icon: PauseCircle },
    { name: "Codebase", href: getAosLink(workspaceId, ".aos/codebase"), icon: Code },
    { name: "Settings", href: `/ws/${workspaceId}/settings`, icon: Settings },
  ];
}

function getNoWorkspaceNavigation() {
  return [
    { name: "Workspace", href: "/", icon: LayoutDashboard },
    { name: "Chat", href: "/chat", icon: MessageSquare },
    { name: "Plan", href: "/plan", icon: Compass },
    { name: "Verification", href: "/verification", icon: Shield },
    { name: "Runs", href: "/runs", icon: History },
    { name: "Continuity", href: "/continuity", icon: PauseCircle },
    { name: "Codebase", href: "/codebase", icon: Code },
    { name: "Settings", href: "/settings", icon: Settings },
  ];
}

function WorkspaceScopeResolver({ workspaceToken }: { workspaceToken: string }) {
  const { setActiveWorkspaceId } = useWorkspaceContext();
  const { workspaces, isLoading } = useWorkspaces();

  useEffect(() => {
    if (isGuidWorkspaceId(workspaceToken)) {
      setActiveWorkspaceId(workspaceToken);
      return;
    }

    if (isLoading) {
      return;
    }

    const resolved = workspaces.find((ws) => ws.projectName === workspaceToken || ws.id === workspaceToken);
    if (resolved) {
      setActiveWorkspaceId(resolved.id);
    }
  }, [isLoading, setActiveWorkspaceId, workspaces, workspaceToken]);

  return null;
}

export function Layout() {
  const location = useLocation();
  const [diagnosticsOpen, setDiagnosticsOpen] = useState(false);
  const [artifactsOpen, setArtifactsOpen] = useState(false);

  // Global state from context
  const {
    activeWorkspaceId,
    engineStatus,
    daemonConnectionState,
    reconnect,
    gitState,
  } = useWorkspaceContext();
  const engineCfg = engineStatusConfig[engineStatus];

  // Detect whether we are under /ws/:workspaceId/...
  const pathParts = location.pathname.split('/');
  const isWorkspaceScoped = pathParts[1] === 'ws' && Boolean(pathParts[2]);
  const workspaceId = isWorkspaceScoped ? pathParts[2] : undefined;

  // Resolve the active workspace data via hook (undefined = no fetch)
  const { workspace: activeWs } = useWorkspace(activeWorkspaceId);

  const navigation = useMemo(
    () => isWorkspaceScoped ? getNavigation(workspaceId!) : getNoWorkspaceNavigation(),
    [isWorkspaceScoped, workspaceId]
  );

  // Files-based routes always show the explorer
  const showFileExplorer = location.pathname.includes('/files/');

  const daemonConnectionConfig = {
    connecting: { label: "Connecting", dot: "bg-amber-500 animate-pulse", color: "text-amber-400" },
    connected: { label: "Connected", dot: "bg-green-500", color: "text-green-400" },
    disconnected: { label: "Reconnect required", dot: "bg-red-500", color: "text-red-400" },
  } as const;
  const daemonCfg = daemonConnectionConfig[daemonConnectionState];

  const copyToClipboardHandler = async (text: string, label: string) => {
    const success = await copyToClipboard(text);
    if (success) {
      toast.success(`Copied ${label}`);
    } else {
      toast.error("Failed to copy to clipboard");
    }
  };

  return (
    <div className="flex h-screen bg-background text-foreground">
      <GlobalCommandPalette />
      <DiagnosticsDrawer open={diagnosticsOpen} onOpenChange={setDiagnosticsOpen} />
      <ArtifactsDrawer open={artifactsOpen} onOpenChange={setArtifactsOpen} />
      {isWorkspaceScoped && workspaceId ? <WorkspaceScopeResolver workspaceToken={workspaceId} /> : null}

      {/* Sidebar */}
      <div className="w-64 border-r border-border bg-card flex flex-col">
        <div className="p-4 border-b border-border">
          <h1 className="font-mono font-semibold text-sm">Nirmata</h1>
          <p className="text-xs text-muted-foreground mt-1">Developer Console</p>
        </div>

        <nav className="flex-1 p-3 space-y-1">
          {navigation.map((item) => {
            let isActive: boolean;

            if (!isWorkspaceScoped) {
              // No-workspace mode: simple pathname match
              if (item.name === "Workspace") {
                isActive = location.pathname === "/";
              } else {
                isActive = location.pathname === item.href ||
                  location.pathname.startsWith(item.href + '/');
              }
            } else {
              // Workspace-scoped mode: existing active-state logic
              isActive = location.pathname === item.href ||
                (item.href !== `/ws/${workspaceId}` && location.pathname.startsWith(item.href));

              if (item.name === "Workspace") {
                isActive = location.pathname === `/ws/${workspaceId}`;
              }

              if (item.name === "Plan") {
                isActive = location.pathname.includes("/.aos/spec") &&
                  !location.pathname.includes("/.aos/spec/uat") &&
                  !location.pathname.includes("/.aos/spec/issues");
              }

              if (item.name === "Verification") {
                isActive = location.pathname.includes("/.aos/spec/uat") ||
                  location.pathname.includes("/.aos/spec/issues");
              }

              if (item.name === "Runs") {
                isActive = location.pathname.includes("/.aos/evidence/runs");
              }

              if (item.name === "Codebase") {
                isActive = location.pathname.includes("/.aos/codebase");
              }

              if (item.name === "Continuity") {
                isActive = location.pathname.includes("/.aos/state");
              }
            }

            return (
              <Link
                key={item.name}
                to={item.href}
                className={cn(
                  "flex items-center gap-3 px-3 py-2 rounded text-sm transition-colors",
                  isActive
                    ? "bg-primary/10 text-primary"
                    : "text-muted-foreground hover:bg-accent/50 hover:text-foreground"
                )}
              >
                <item.icon className="h-4 w-4" />
                {item.name}
              </Link>
            );
          })}

        </nav>

        {/* Workspace Status Footer — only shown when a workspace is active */}
        {isWorkspaceScoped && <div className="p-2 border-t border-border space-y-1">
          <div className="text-[10px] leading-tight">

            <div className="flex items-center justify-between mb-0.5">
              <span className="text-muted-foreground">Project</span>
              <span className="font-mono text-foreground">
                {activeWs.projectName}
              </span>
            </div>

            <div className="flex items-center justify-between mb-0.5">
              <span className="text-muted-foreground">Cursor</span>
              <button
                onClick={() =>
                  copyToClipboardHandler(
                    `${activeWs.cursor.milestone}/${activeWs.cursor.phase}/${activeWs.cursor.task}`,
                    "cursor path"
                  )
                }
                className="flex items-center gap-1 font-mono text-foreground hover:text-primary"
              >
                <span>
                  {activeWs.cursor.phase ? `${activeWs.cursor.phase}/${activeWs.cursor.task || "—"}` : "—"}
                </span>
                <Copy className="h-2.5 w-2.5" />
              </button>
            </div>

            <div className="flex items-center justify-between mb-0.5">
              <span className="text-muted-foreground">Last Run</span>
              <button
                onClick={() =>
                  copyToClipboardHandler(activeWs.lastRun.id, "run ID")
                }
                className="flex items-center gap-1 font-mono text-foreground hover:text-primary"
              >
                <span
                  className={cn(
                    "inline-block w-1.5 h-1.5 rounded-full mr-1",
                    activeWs.lastRun.status === "success"
                      ? "bg-green-500"
                      : activeWs.lastRun.status === "failed"
                        ? "bg-red-500"
                        : "bg-yellow-500"
                  )}
                />
                <span className="text-[10px]">{activeWs.lastRun.id || "—"}</span>
                <Copy className="h-2.5 w-2.5" />
              </button>
            </div>

          </div>
        </div>}
      </div>

      {/* Main Content */}
      <div className="flex-1 flex flex-col overflow-hidden">
        <TopRibbon />
        <div className="flex-1 flex overflow-hidden">
           {showFileExplorer && <FileExplorer />}
           <div className="flex-1 flex flex-col overflow-hidden bg-background">
             {/* Page content */}
             <div className="flex-1 flex flex-col w-full overflow-hidden">
               <Outlet />
             </div>
           </div>
        </div>

        {/* Bottom Status Bar */}
        <div className="h-7 border-t border-border bg-card flex items-center px-4 gap-6 text-[11px] font-mono shrink-0">
          {/* Engine status */}
          <div className="flex items-center gap-2">
            <span className="text-muted-foreground">Engine</span>
            <span className="flex items-center gap-1.5">
              <span className={cn("inline-block w-1.5 h-1.5 rounded-full", engineCfg.dot, engineStatus === "running" && "animate-pulse")} />
              <span className={engineCfg.color}>{engineCfg.label}</span>
            </span>
          </div>

          {/* Daemon connection — click to open diagnostics */}
          <div className="flex items-center gap-2">
            <button
              className="flex items-center gap-2 hover:text-foreground transition-colors cursor-pointer"
              onClick={() => setDiagnosticsOpen(true)}
              title="Open diagnostics"
            >
              <span className="text-muted-foreground">Daemon</span>
              <span className="flex items-center gap-1.5">
                <span className={cn("inline-block w-1.5 h-1.5 rounded-full", daemonCfg.dot)} />
                <span className={daemonCfg.color}>
                  {daemonCfg.label}
                </span>
              </span>
            </button>

            {daemonConnectionState === "disconnected" && (
              <button
                type="button"
                onClick={reconnect}
                className="inline-flex items-center gap-1 rounded border border-border bg-background px-2 py-1 text-[10px] font-medium text-muted-foreground transition-colors hover:text-foreground"
                title="Retry daemon health check"
              >
                <RefreshCw className="h-3 w-3" />
                Reconnect
              </button>
            )}
          </div>

          <div className="h-3.5 w-px bg-border" />

          {/* Repo safety: branch + clean/dirty + ahead/behind */}
          <div className="flex items-center gap-2">
            <GitBranch className="h-3 w-3 text-muted-foreground" />
            <span className="text-foreground">{gitState.branch}</span>
            <span
              className={cn(
                "px-1.5 py-px rounded text-[10px]",
                gitState.dirty === 0
                  ? "bg-green-500/10 text-green-400 border border-green-500/20"
                  : "bg-yellow-500/10 text-yellow-400 border border-yellow-500/20"
              )}
            >
              {gitState.dirty === 0 ? "Clean" : `${gitState.dirty} dirty`}
            </span>
            {(gitState.ahead > 0 || gitState.behind > 0) && (
              <span className="text-muted-foreground">
                {gitState.ahead > 0 && <span className="text-green-400">↑{gitState.ahead}</span>}
                {gitState.ahead > 0 && gitState.behind > 0 && " "}
                {gitState.behind > 0 && <span className="text-red-400">↓{gitState.behind}</span>}
              </span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}