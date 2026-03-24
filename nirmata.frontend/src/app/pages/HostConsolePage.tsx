import { useState, useEffect, useRef } from "react";
import { useParams, Link } from "react-router";
import {
  Server,
  Play,
  StopCircle,
  RefreshCw,
  CheckCircle,
  AlertCircle,
  Clock,
  Activity,
  ArrowLeft,
  Settings,
  Wifi,
  WifiOff,
  Terminal,
  ChevronDown,
  Circle,
} from "lucide-react";
import { Button } from "../components/ui/button";
import { Badge } from "../components/ui/badge";
import { ScrollArea } from "../components/ui/scroll-area";
import { Separator } from "../components/ui/separator";
import { cn } from "../components/ui/utils";
import { toast } from "sonner";
import {
  useHostConsole,
  type ServiceStatus,
  type ApiSurface,
  type HostLogLine,
} from "../hooks/useAosData";
import { useLiveClock, useUptime } from "../hooks/useTimers";
import { DAEMON_BASE_URL } from "../api/routing";

// ── Helpers ───────────────────────────────────────────────────────────

function fmtTime(d: Date) {
  return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

// ── HostConsolePage ───────────────────────────────────────────────────

export function HostConsolePage() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const now = useLiveClock();
  const { logs: seedLogs, surfaces: seedSurfaces } = useHostConsole();

  const [status, setStatus] = useState<ServiceStatus>("running");
  const [surfaces, setSurfaces] = useState<ApiSurface[]>(seedSurfaces);
  const [logs, setLogs] = useState<HostLogLine[]>(seedLogs);
  const [logFilter, setLogFilter] = useState<"all" | "warn" | "error">("all");
  const [pingMs, setPingMs] = useState(2);
  const [showAllLogs, setShowAllLogs] = useState(false);
  const logsEndRef = useRef<HTMLDivElement>(null);

  // Simulate a live ping ticker
  useEffect(() => {
    const id = setInterval(() => {
      setPingMs(Math.floor(Math.random() * 8) + 1);
    }, 3000);
    return () => clearInterval(id);
  }, []);

  const uptime = useUptime(status === "running");

  const handleStart = () => {
    setStatus("running");
    const newLog: HostLogLine = {
      id: logs.length + 1,
      ts: fmtTime(new Date()),
      level: "info",
      msg: "Service started by user",
    };
    setLogs((prev) => [...prev, newLog]);
    toast.success("Engine service started");
  };

  const handleStop = () => {
    setStatus("stopped");
    setSurfaces((prev) => prev.map((s) => ({ ...s, ok: false, reason: "Service stopped" })));
    const newLog: HostLogLine = {
      id: logs.length + 1,
      ts: fmtTime(new Date()),
      level: "warn",
      msg: "Service stopped by user",
    };
    setLogs((prev) => [...prev, newLog]);
    toast.error("Engine service stopped");
  };

  const handleRestart = () => {
    setStatus("restarting");
    const restartLog: HostLogLine = {
      id: logs.length + 1,
      ts: fmtTime(new Date()),
      level: "info",
      msg: "Restart initiated by user…",
    };
    setLogs((prev) => [...prev, restartLog]);
    toast.info("Restarting engine…");
    setTimeout(() => {
      setStatus("running");
      setSurfaces(seedSurfaces);
      const doneLog: HostLogLine = {
        id: logs.length + 2,
        ts: fmtTime(new Date()),
        level: "info",
        msg: "Service restarted — all surfaces operational",
      };
      setLogs((prev) => [...prev, doneLog]);
      toast.success("Engine restarted");
    }, 2000);
  };

  const handlePing = () => {
    const ms = Math.floor(Math.random() * 8) + 1;
    setPingMs(ms);
    toast.success(`Ping OK — ${ms}ms`);
  };

  const visibleLogs = logs.filter((l) =>
    logFilter === "all" ? true : l.level === logFilter
  );
  const displayedLogs = showAllLogs ? visibleLogs : visibleLogs.slice(-8);

  const statusColor: Record<ServiceStatus, string> = {
    running:    "bg-green-500",
    stopped:    "bg-red-500",
    restarting: "bg-yellow-500",
  };
  const statusLabel: Record<ServiceStatus, string> = {
    running:    "Running",
    stopped:    "Stopped",
    restarting: "Restarting…",
  };

  return (
    <div className="flex flex-col h-full bg-background">
      {/* ── Top bar ───────────────────────────────────────────────── */}
      <div className="flex items-center justify-between px-6 py-3 border-b border-border bg-muted/10 shrink-0">
        <div className="flex items-center gap-3">
          <Link
            to={`/ws/${workspaceId}/settings`}
            className="flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground transition-colors"
          >
            <ArrowLeft className="h-3.5 w-3.5" />
            Settings
          </Link>
          <span className="text-muted-foreground/30">/</span>
          <div className="flex items-center gap-2">
            <Server className="h-4 w-4 text-primary" />
            <span className="text-sm">Host Console</span>
            <Badge
              className={cn(
                "text-[10px] h-5 px-2 text-white gap-1",
                statusColor[status]
              )}
            >
              <Circle className="h-1.5 w-1.5 fill-current" />
              {statusLabel[status]}
            </Badge>
          </div>
        </div>

        <div className="flex items-center gap-3">
          {/* Live clock */}
          <span className="text-[11px] text-muted-foreground/50 font-mono tabular-nums">
            {fmtTime(now)}
          </span>

          {/* Ping */}
          <div className="flex items-center gap-1.5 text-[11px] text-muted-foreground">
            {status === "running" ? (
              <Wifi className="h-3.5 w-3.5 text-green-400" />
            ) : (
              <WifiOff className="h-3.5 w-3.5 text-red-400" />
            )}
            <span className="font-mono">{status === "running" ? `${pingMs}ms` : "—"}</span>
          </div>

          <Link
            to={`/ws/${workspaceId}/settings`}
            className="flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground transition-colors"
          >
            <Settings className="h-3.5 w-3.5" />
            Host Settings
          </Link>
        </div>
      </div>

      <ScrollArea className="flex-1">
        <div className="max-w-4xl mx-auto p-6 space-y-6">

          {/* ── Stat strip ──────────────────────────────────────── */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
            {[
              {
                label: "Service Status",
                value: statusLabel[status],
                color: status === "running" ? "text-green-400" : status === "restarting" ? "text-yellow-400" : "text-red-400",
                bg: status === "running" ? "bg-green-500/10 border-green-500/20" : status === "restarting" ? "bg-yellow-500/10 border-yellow-500/20" : "bg-red-500/10 border-red-500/20",
              },
              {
                label: "Uptime",
                value: status === "running" ? uptime : "—",
                color: "text-foreground/70",
                bg: "bg-muted/20 border-border",
              },
              {
                label: "Version",
                value: "v2.4.0-alpha",
                color: "text-foreground/70",
                bg: "bg-muted/20 border-border",
              },
              {
                label: "Last Ping",
                value: status === "running" ? `${pingMs}ms` : "—",
                color: "text-foreground/70",
                bg: "bg-muted/20 border-border",
              },
            ].map((stat) => (
              <div key={stat.label} className={cn("p-3 rounded-lg border", stat.bg)}>
                <p className="text-[10px] text-muted-foreground uppercase tracking-wide mb-1">
                  {stat.label}
                </p>
                <p className={cn("text-sm font-mono", stat.color)}>{stat.value}</p>
              </div>
            ))}
          </div>

          {/* ── Service controls ──────────────────────────────── */}
          <div className="border border-border rounded-lg bg-card">
            <div className="flex items-center gap-2 px-4 py-3 border-b border-border">
              <Activity className="h-4 w-4 text-primary" />
              <h2 className="text-sm">Service Control</h2>
              <span className="ml-auto text-[11px] text-muted-foreground">
                Last restart: Today 08:14
              </span>
            </div>
            <div className="p-4 flex items-center gap-2 flex-wrap">
              <Button
                variant="outline"
                size="sm"
                className="gap-2 text-green-400 border-green-500/20 hover:bg-green-500/10"
                disabled={status === "running" || status === "restarting"}
                onClick={handleStart}
              >
                <Play className="h-3 w-3" />
                Start
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="gap-2 text-destructive border-destructive/20 hover:bg-destructive/10"
                disabled={status === "stopped" || status === "restarting"}
                onClick={handleStop}
              >
                <StopCircle className="h-3 w-3" />
                Stop
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="gap-2"
                disabled={status === "restarting"}
                onClick={handleRestart}
              >
                <RefreshCw className={cn("h-3 w-3", status === "restarting" && "animate-spin")} />
                Restart
              </Button>
              <Separator orientation="vertical" className="h-5 mx-1" />
              <Button
                variant="outline"
                size="sm"
                className="gap-2"
                onClick={handlePing}
                disabled={status !== "running"}
              >
                <Clock className="h-3 w-3" />
                Ping
              </Button>
            </div>
          </div>

          {/* ── API surface health ──────────────────────────────── */}
          <div className="border border-border rounded-lg bg-card">
            <div className="flex items-center gap-2 px-4 py-3 border-b border-border">
              <Wifi className="h-4 w-4 text-primary" />
              <h2 className="text-sm">API Surface Reachability</h2>
              <span className="ml-auto text-[11px] text-muted-foreground">
                All endpoints must be green before running tasks
              </span>
            </div>
            <div className="p-4 grid grid-cols-2 md:grid-cols-4 gap-3">
              {surfaces.map((surface) => (
                <div
                  key={surface.name}
                  className={cn(
                    "flex flex-col gap-2 p-3 rounded-lg border",
                    surface.ok
                      ? "border-green-500/20 bg-green-500/5"
                      : "border-red-500/20 bg-red-500/5"
                  )}
                >
                  <div className="flex items-center gap-2">
                    {surface.ok ? (
                      <CheckCircle className="h-4 w-4 text-green-400 shrink-0" />
                    ) : (
                      <AlertCircle className="h-4 w-4 text-red-400 shrink-0" />
                    )}
                    <span className="text-sm">{surface.name}</span>
                  </div>
                  <code className="text-[10px] text-muted-foreground/50 font-mono">
                    {surface.path}
                  </code>
                  {surface.ok && surface.latencyMs !== undefined && (
                    <span className="text-[10px] text-green-400/70 font-mono">
                      {surface.latencyMs}ms
                    </span>
                  )}
                  {!surface.ok && surface.reason && (
                    <span className="text-[10px] text-red-400/70">{surface.reason}</span>
                  )}
                </div>
              ))}
            </div>
          </div>

          {/* ── Service log ─────────────────────────────────────── */}
          <div className="border border-border rounded-lg bg-card">
            <div className="flex items-center gap-2 px-4 py-3 border-b border-border">
              <Terminal className="h-4 w-4 text-primary" />
              <h2 className="text-sm">Service Log</h2>
              <div className="ml-auto flex items-center gap-1">
                {(["all", "warn", "error"] as const).map((f) => (
                  <button
                    key={f}
                    type="button"
                    onClick={() => setLogFilter(f)}
                    className={cn(
                      "text-[10px] px-2 py-0.5 rounded transition-colors",
                      logFilter === f
                        ? "bg-muted text-foreground"
                        : "text-muted-foreground hover:text-foreground"
                    )}
                  >
                    {f}
                  </button>
                ))}
              </div>
            </div>

            <div className="font-mono text-[11px] divide-y divide-border/50">
              {displayedLogs.map((line) => (
                <div
                  key={line.id}
                  className={cn(
                    "flex items-start gap-3 px-4 py-2",
                    line.level === "error" && "bg-red-500/5",
                    line.level === "warn" && "bg-yellow-500/5"
                  )}
                >
                  <span className="text-muted-foreground/40 shrink-0 tabular-nums w-14">
                    {line.ts}
                  </span>
                  <span
                    className={cn(
                      "uppercase shrink-0 w-8",
                      line.level === "info"  && "text-muted-foreground/50",
                      line.level === "warn"  && "text-yellow-400",
                      line.level === "error" && "text-red-400"
                    )}
                  >
                    {line.level}
                  </span>
                  <span
                    className={cn(
                      "flex-1",
                      line.level === "error" ? "text-red-300" :
                      line.level === "warn"  ? "text-yellow-300/80" :
                      "text-foreground/70"
                    )}
                  >
                    {line.msg}
                  </span>
                </div>
              ))}
            </div>

            {visibleLogs.length > 8 && (
              <button
                type="button"
                onClick={() => setShowAllLogs((v) => !v)}
                className="w-full flex items-center justify-center gap-1.5 py-2.5 text-[11px] text-muted-foreground hover:text-foreground transition-colors border-t border-border"
              >
                <ChevronDown
                  className={cn("h-3 w-3 transition-transform", showAllLogs && "rotate-180")}
                />
                {showAllLogs
                  ? "Show fewer"
                  : `Show all ${visibleLogs.length} lines`}
              </button>
            )}
          </div>

          {/* ── Host info strip ─────────────────────────────────── */}
          <div className="flex items-center gap-4 px-4 py-3 bg-muted/10 border border-border rounded-lg text-[11px] text-muted-foreground">
            <span className="flex items-center gap-1.5">
              <Server className="h-3 w-3" />
              Local Dev Host
            </span>
            <span className="text-muted-foreground/30">·</span>
            <span className="font-mono">{DAEMON_BASE_URL}</span>
            <span className="text-muted-foreground/30">·</span>
            <span>Windows Service</span>
            <span className="text-muted-foreground/30">·</span>
            <span>local env</span>
            <Link
              to={`/ws/${workspaceId}/settings`}
              className="ml-auto flex items-center gap-1 hover:text-foreground transition-colors"
            >
              <Settings className="h-3 w-3" />
              Edit profile
            </Link>
          </div>

        </div>
      </ScrollArea>
    </div>
  );
}