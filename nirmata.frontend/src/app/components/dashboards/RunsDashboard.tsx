import { useState, useMemo } from "react";
import {
  Terminal,
  Play,
  CheckCircle,
  XCircle,
  Loader2,
  Clock,
  FileCode,
  ChevronRight,
  Search,
  RotateCcw,
  ArrowRight,
  Shield,
  Copy,
  Activity,
} from "lucide-react";
import { Badge } from "../ui/badge";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { cn } from "../ui/utils";
import { toast } from "sonner";
import { useNavigate } from "react-router";
import { copyToClipboard } from "../../utils/clipboard";
import { CommandChip } from "../CommandChip";
import { PathChip } from "../PathChip";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "../ui/tabs";
import { JsonPreviewCard } from "../JsonPreviewCard";
import { useRuns, useWorkspace, useTasks, usePhases } from "../../hooks/useAosData";
import { type Run, type Task, type Phase } from "../../hooks/useAosData";

// ── Helpers ──────────────────────────────────────────────────────

function formatDuration(start: string, end?: string): string {
  if (!end) return "running…";
  const ms = new Date(end).getTime() - new Date(start).getTime();
  const secs = Math.floor(ms / 1000);
  if (secs < 60) return `${secs}s`;
  const mins = Math.floor(secs / 60);
  const remSecs = secs % 60;
  return `${mins}m ${remSecs}s`;
}

function formatTimestamp(iso: string): string {
  return new Date(iso).toLocaleString("en-US", {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

function getRunScope(run: Run, allTasks: Task[], allPhases: Phase[]): { label: string; taskName?: string; phaseName?: string } {
  if (run.taskId) {
    const task = allTasks.find((t) => t.id === run.taskId);
    const phase = task ? allPhases.find((p) => p.id === task.phaseId) : null;
    return {
      label: `${task?.phaseId ?? "?"}/${run.taskId}`,
      taskName: task?.name,
      phaseName: phase?.title,
    };
  }
  if (run.command.includes("validate")) return { label: "validation" };
  return { label: "global" };
}

function getRunType(run: Run): { label: string; icon: typeof Play; color: string } {
  if (run.command.includes("execute-plan")) {
    return { label: "Execute", icon: Play, color: "text-blue-400 bg-blue-400/10 border-blue-400/20" };
  }
  if (run.command.includes("validate")) {
    return { label: "Validate", icon: CheckCircle, color: "text-cyan-400 bg-cyan-400/10 border-cyan-400/20" };
  }
  return { label: "Command", icon: Terminal, color: "text-muted-foreground bg-muted border-border" };
}

const statusConfig: Record<string, { icon: typeof CheckCircle; color: string; dotColor: string; label: string }> = {
  success: {
    icon: CheckCircle,
    color: "text-green-500",
    dotColor: "bg-green-500",
    label: "Success",
  },
  failed: {
    icon: XCircle,
    color: "text-red-500",
    dotColor: "bg-red-500",
    label: "Failed",
  },
  running: {
    icon: Loader2,
    color: "text-yellow-500",
    dotColor: "bg-yellow-500",
    label: "Running",
  },
};

const FALLBACK_STATUS = statusConfig.failed;

// ── Main Component ───────────────────────────────────────────────

export function RunsDashboard() {
  const navigate = useNavigate();
  const { workspace: currentWs } = useWorkspace();
  const { runs: allRuns } = useRuns();
  const { tasks: allTasks } = useTasks();
  const { phases: allPhases } = usePhases();
  const ws = currentWs.projectName;

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [typeFilter, setTypeFilter] = useState<string>("");
  const [expandedRun, setExpandedRun] = useState<string | null>(null);

  const sortedRuns = useMemo(
    () =>
      [...allRuns].sort(
        (a, b) =>
          new Date(b.startTime).getTime() - new Date(a.startTime).getTime()
      ),
    [allRuns]
  );

  const filteredRuns = useMemo(() => {
    return sortedRuns.filter((run) => {
      if (statusFilter && run.status !== statusFilter) return false;
      if (typeFilter) {
        const type = getRunType(run);
        if (type.label.toLowerCase() !== typeFilter) return false;
      }
      if (search) {
        const q = search.toLowerCase();
        const scope = getRunScope(run, allTasks, allPhases);
        return (
          run.id.toLowerCase().includes(q) ||
          run.command.toLowerCase().includes(q) ||
          run.status.toLowerCase().includes(q) ||
          scope.label.toLowerCase().includes(q) ||
          (scope.taskName && scope.taskName.toLowerCase().includes(q))
        );
      }
      return true;
    });
  }, [sortedRuns, statusFilter, typeFilter, search, allTasks, allPhases]);

  // Stats
  const successCount = allRuns.filter((r) => r.status === "success").length;
  const failedCount = allRuns.filter((r) => r.status === "failed").length;
  const totalArtifacts = [...new Set(allRuns.flatMap((r) => r.changedFiles))].length;

  const copy = async (text: string, label: string) => {
    const ok = await copyToClipboard(text);
    if (ok) toast.success(`Copied ${label}: ${text}`);
    else toast.error(`Failed to copy ${label}`);
  };

  const handleReRun = (run: Run) => {
    toast.info(`Re-running: ${run.command}`, {
      description: `Would create a new run based on ${run.id}`,
    });
  };

  const activeRun = sortedRuns.find((r) => r.status === "running");

  return (
    <div className="flex-1 flex flex-col overflow-hidden bg-background">
      {/* ── Content ── */}
      <div className="flex-1 overflow-auto">
        <div className="max-w-5xl mx-auto p-6 space-y-5">

          {/* Header */}
          <div className="flex items-start justify-between">
            <div>
              <div className="flex items-center gap-2 mb-1">
                <Terminal className="h-4 w-4 text-muted-foreground" />
                <span className="font-mono text-xs text-muted-foreground">.aos/evidence/runs</span>
              </div>
              <h1 className="text-2xl font-bold tracking-tight mb-1">Run History</h1>
              <p className="text-muted-foreground text-sm max-w-2xl font-mono">
                {allRuns.length} runs recorded — {successCount} passed, {failedCount} failed
              </p>
            </div>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                className="font-mono text-xs"
                onClick={() => copy(".aos/evidence/runs", "path")}
              >
                <Copy className="h-3 w-3 mr-1.5" /> Copy Path
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="font-mono text-xs"
                onClick={() => toast.info("Exporting run history...")}
              >
                Export JSON
              </Button>
            </div>
          </div>

          {/* Active Run Banner */}
          {activeRun && (
            <div className="flex items-center gap-3 px-4 py-3 rounded-lg border-2 border-emerald-500/40 bg-emerald-500/5">
              <div className="relative">
                <Loader2 className="h-5 w-5 text-emerald-500 animate-spin" />
                <div className="absolute inset-0 rounded-full bg-emerald-500/20 animate-ping" />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-mono text-emerald-400">
                    RUNNING
                  </span>
                  <span className="text-xs font-mono text-foreground">
                    {activeRun.id}
                  </span>
                </div>
                <p className="text-[11px] font-mono text-muted-foreground truncate">
                  {activeRun.command}
                </p>
              </div>
              <Button
                variant="outline"
                size="sm"
                className="text-xs font-mono gap-1.5 border-emerald-500/30 text-emerald-400 hover:bg-emerald-500/10"
                onClick={() =>
                  navigate(
                    `/ws/${ws}/files/.aos/evidence/runs/${activeRun.id}/run.json`
                  )
                }
              >
                <ArrowRight className="h-3 w-3" /> View
              </Button>
            </div>
          )}

          {/* Filter Row */}
          <div className="flex items-center gap-3">
            <div className="relative flex-1 max-w-sm">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
              <Input
                placeholder="Search runs by ID, command, scope..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="pl-8 h-8 text-xs font-mono"
              />
            </div>
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="h-8 px-2 text-xs font-mono bg-background border border-border rounded-md text-foreground"
            >
              <option value="">All Statuses</option>
              <option value="success">Success</option>
              <option value="failed">Failed</option>
              <option value="running">Running</option>
            </select>
            <select
              value={typeFilter}
              onChange={(e) => setTypeFilter(e.target.value)}
              className="h-8 px-2 text-xs font-mono bg-background border border-border rounded-md text-foreground"
            >
              <option value="">All Types</option>
              <option value="execute">Execute</option>
              <option value="validate">Validate</option>
              <option value="command">Command</option>
            </select>
            <span className="text-xs text-muted-foreground font-mono ml-auto">
              {filteredRuns.length} run{filteredRuns.length !== 1 ? "s" : ""}
            </span>
          </div>

          {/* Run List */}
          <div className="space-y-2">
            {filteredRuns.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-16 text-center">
                <Activity className="h-10 w-10 text-muted-foreground/20 mb-3" />
                <p className="text-sm font-mono text-muted-foreground">
                  {search || statusFilter || typeFilter
                    ? "No runs match your filters."
                    : "No runs recorded yet."}
                </p>
              </div>
            ) : (
              filteredRuns.map((run) => {
                const status = statusConfig[run.status] ?? FALLBACK_STATUS;
                const scope = getRunScope(run, allTasks, allPhases);
                const runType = getRunType(run);
                const StatusIcon = status.icon;
                const isExpanded = expandedRun === run.id;
                const duration = formatDuration(run.startTime, run.endTime);

                return (
                  <div
                    key={run.id}
                    className={cn(
                      "border rounded-lg transition-all",
                      run.status === "running"
                        ? "border-emerald-500/30 bg-emerald-500/[0.02]"
                        : run.status === "failed"
                          ? "border-red-500/20 bg-red-500/[0.01]"
                          : "border-border/60 bg-card/30",
                      isExpanded && "shadow-sm"
                    )}
                  >
                    {/* Row Header */}
                    <div
                      className="flex items-center gap-3 px-4 py-3 cursor-pointer hover:bg-accent/30 transition-colors rounded-lg"
                      onClick={() =>
                        setExpandedRun(isExpanded ? null : run.id)
                      }
                    >
                      {/* Status dot */}
                      <div className="relative shrink-0">
                        <div
                          className={cn(
                            "h-2.5 w-2.5 rounded-full",
                            status.dotColor,
                            run.status === "running" && "animate-pulse"
                          )}
                        />
                      </div>

                      {/* Expand chevron */}
                      <ChevronRight
                        className={cn(
                          "h-3.5 w-3.5 text-muted-foreground/40 shrink-0 transition-transform",
                          isExpanded && "rotate-90"
                        )}
                      />

                      {/* Run ID */}
                      <span className="font-mono text-xs text-foreground/80 shrink-0">
                        {run.id}
                      </span>

                      {/* Status badge */}
                      <Badge
                        variant="outline"
                        className={cn(
                          "text-[9px] h-4.5 px-1.5 gap-0.5 shrink-0",
                          run.status === "success" &&
                            "text-green-500 bg-green-500/10 border-green-500/20",
                          run.status === "failed" &&
                            "text-red-500 bg-red-500/10 border-red-500/20",
                          run.status === "running" &&
                            "text-yellow-500 bg-yellow-500/10 border-yellow-500/20"
                        )}
                      >
                        <StatusIcon
                          className={cn(
                            "h-2.5 w-2.5",
                            run.status === "running" && "animate-spin"
                          )}
                        />
                        {status.label}
                      </Badge>

                      {/* Scope */}
                      {scope.label !== "global" && (
                        <span className="text-[10px] font-mono text-muted-foreground/60 truncate">
                          {scope.label}
                        </span>
                      )}

                      {/* Spacer */}
                      <div className="flex-1" />

                      {/* Duration */}
                      <div className="flex items-center gap-1 text-[10px] font-mono text-muted-foreground/50 shrink-0">
                        <Clock className="h-3 w-3" />
                        {duration}
                      </div>

                      {/* Changed files count */}
                      {run.changedFiles.length > 0 && (
                        <div className="flex items-center gap-1 text-[10px] font-mono text-muted-foreground/50 shrink-0">
                          <FileCode className="h-3 w-3" />
                          {run.changedFiles.length}
                        </div>
                      )}

                      {/* Timestamp */}
                      <span className="text-[10px] font-mono text-muted-foreground/40 shrink-0">
                        {formatTimestamp(run.startTime)}
                      </span>
                    </div>

                    {/* Expanded Detail */}
                    {isExpanded && (
                      <div className="px-4 pb-4 pt-2 border-t border-border/30">
                        <div className="bg-muted/10 border border-border/40 rounded-lg overflow-hidden">
                           <JsonPreviewCard
                             label="Run Evidence"
                             filename="run.json"
                             data={run}
                             onClick={() => navigate(`/ws/${ws}/files/.aos/evidence/runs/${run.id}/run.json`)}
                           />
                        </div>

                        {/* Footer Actions */}
                        <div className="flex items-center gap-2 mt-3">
                          <Button
                            variant="outline"
                            size="sm"
                            className="text-xs font-mono gap-1.5 h-7"
                            onClick={() => handleReRun(run)}
                          >
                            <RotateCcw className="h-3 w-3" /> Re-run
                          </Button>
                          {run.taskId && (
                            <Button
                              variant="outline"
                              size="sm"
                              className="text-xs font-mono gap-1.5 h-7 border-cyan-500/30 text-cyan-400 hover:bg-cyan-500/10"
                              onClick={() =>
                                navigate(`/ws/${ws}/files/.aos/spec/uat`)
                              }
                            >
                              <Shield className="h-3 w-3" /> View UAT
                            </Button>
                          )}
                        </div>
                      </div>
                    )}
                  </div>
                );
              })
            )}
          </div>

          {/* Hint */}
          {filteredRuns.length > 0 && (
            <p className="text-[10px] text-muted-foreground/60 font-mono text-center">
              Click a run to expand details · View Raw to open in editor
            </p>
          )}
        </div>
      </div>
    </div>
  );
}