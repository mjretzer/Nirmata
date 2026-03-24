import { useState, useEffect } from "react";
import {
  Activity,
  AlertCircle,
  ArrowRight,
  Check,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Clipboard,
  Database,
  Edit3,
  FileJson,
  Flag,
  Loader2,
  Pause,
  PauseCircle,
  Play,
  Plus,
  RefreshCw,
  RotateCcw,
  Save,
  ShieldCheck,
  SkipForward,
  StopCircle,
  Zap,
} from "lucide-react";
import { Badge } from "../ui/badge";
import { Button } from "../ui/button";
import { cn } from "../ui/utils";
import {
  type Task,
  type Checkpoint,
  type HandoffSnapshot,
} from "../../hooks/useAosData";
import { useWorkspace, useCheckpoints, useMilestones, usePhases, useContinuityState } from "../../hooks/useAosData";
import { useWorkspaceContext } from "../../context/WorkspaceContext";
import { toast } from "sonner";
import { useNavigate } from "react-router";
import { motion, AnimatePresence } from "motion/react";

interface InspectorPanelProps {
  systemStatus: "idle" | "executing" | "paused";
  selectedId: string | null;
  tasks: Task[];
  handoff: HandoffSnapshot | null;
  executionMode: "atomic" | "manual";
  onAction: (action: string) => void;
}

function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "just now";
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  return `${Math.floor(hrs / 24)}d ago`;
}

// ════════════════════════════════════════
// MAIN EXPORT
// ════════════════════════════════════════

export function InspectorPanel({
  systemStatus,
  selectedId,
  tasks,
  handoff,
  executionMode,
  onAction,
}: InspectorPanelProps) {
  const navigate = useNavigate();
  const { activeWorkspaceId } = useWorkspaceContext();
  const { workspace } = useWorkspace(activeWorkspaceId);
  const { checkpoints: initialCheckpoints } = useCheckpoints();
  const { milestones } = useMilestones();
  const { phases } = usePhases();
  const { cursor: aosPosition, events: liveEvents, packs: livePacks } = useContinuityState();
  const ws = workspace.projectName;

  const liveCursor = {
    milestone: aosPosition.milestoneId,
    phase: aosPosition.phaseId,
    task: aosPosition.taskId,
  };

  const [checkpoints, setCheckpoints] = useState<Checkpoint[]>([
    ...initialCheckpoints,
  ]);

  // Keep checkpoints in sync with live data
  useEffect(() => {
    if (initialCheckpoints.length > 0) {
      setCheckpoints(initialCheckpoints);
    }
  }, [initialCheckpoints]);

  // Keep a "sticky" handoff so it can animate out after status changes
  const [stickyHandoff, setStickyHandoff] = useState<HandoffSnapshot | null>(handoff);
  const showHandoff = systemStatus === "paused" && handoff !== null;

  useEffect(() => {
    if (handoff) {
      setStickyHandoff(handoff);
    }
    // Don't clear stickyHandoff when handoff becomes null — AnimatePresence handles exit
  }, [handoff]);

  const _statusColor = {
    idle: "bg-muted-foreground/30",
    executing: "bg-emerald-500",
    paused: "bg-yellow-500",
  }[systemStatus];

  const statusLabel = {
    idle: "Idle",
    executing: "Running",
    paused: "Paused",
  }[systemStatus];

  const handleSave = () => {
    const now = new Date();
    const num = String(checkpoints.length + 1).padStart(3, "0");
    const newCp: Checkpoint = {
      id: `CHK-${num}`,
      timestamp: now.toISOString(),
      cursor: { ...liveCursor },
      description: `Saved at ${liveCursor.phase}/${liveCursor.task ?? "—"}`,
      source: "manual",
    };
    setCheckpoints((prev) => [newCp, ...prev]);
    toast.success(`Checkpoint ${newCp.id} saved`, {
      description: `.aos/state/checkpoints/${newCp.id}.json`,
    });
  };

  const handleRestore = (cp: Checkpoint) => {
    toast.success(`Restored ${cp.id} → state.json`, {
      description: `${cp.cursor.milestone} / ${cp.cursor.phase} / ${cp.cursor.task ?? "—"}`,
    });
  };

  // Resolve cursor to names
  const cursor = liveCursor;
  const _cursorMilestone = milestones.find((m) => m.id === cursor.milestone);
  const cursorPhase = phases.find((p) => p.id === cursor.phase);
  const cursorTask = cursor.task
    ? tasks.find((t) => t.id === cursor.task)
    : null;

  // Build ordered task list across all phases, find prev/next globally
  const allOrderedTasks = phases
    .slice()
    .sort((a, b) => a.order - b.order)
    .flatMap((ph) => tasks.filter((t) => t.phaseId === ph.id));
  const globalIndex = cursorTask
    ? allOrderedTasks.findIndex((t) => t.id === cursorTask.id)
    : -1;
  const _prevTask = globalIndex > 0 ? allOrderedTasks[globalIndex - 1] : null;
  const _nextTask =
    globalIndex >= 0 && globalIndex < allOrderedTasks.length - 1
      ? allOrderedTasks[globalIndex + 1]
      : null;
  const _prevTaskPhase = _prevTask
    ? phases.find((p) => p.id === _prevTask.phaseId)
    : null;
  const _nextTaskPhase = _nextTask
    ? phases.find((p) => p.id === _nextTask.phaseId)
    : null;

  // Phase-scoped progress
  const _phaseTasks = cursorPhase
    ? tasks.filter((t) => t.phaseId === cursorPhase.id)
    : [];
  const _currentTaskIndex = cursorTask
    ? _phaseTasks.findIndex((t) => t.id === cursorTask.id)
    : -1;

  // ── Manual mode: state for handoff creation ──
  const [handoffNotes, setHandoffNotes] = useState("");
  const [manualTaskStatuses, setManualTaskStatuses] = useState<Record<string, "queued" | "running" | "done" | "skipped">>({});
  const [selectedManualTaskId, setSelectedManualTaskId] = useState<string | null>(null);

  const getManualStatus = (taskId: string) => manualTaskStatuses[taskId] ?? "queued";

  const handleManualExecute = (task: Task) => {
    setManualTaskStatuses(prev => ({ ...prev, [task.id]: "running" }));
    toast.info(`Executing ${task.id}: ${task.name}`, {
      description: "aos task execute --manual --id " + task.id,
    });
    // Simulate completion after delay
    setTimeout(() => {
      setManualTaskStatuses(prev => ({ ...prev, [task.id]: "done" }));
      toast.success(`${task.id} completed`, {
        description: `Artifacts persisted to .aos/evidence/runs/`,
      });
    }, 2000);
  };

  const handleManualSkip = (task: Task) => {
    setManualTaskStatuses(prev => ({ ...prev, [task.id]: "skipped" }));
    toast.info(`Skipped ${task.id}: ${task.name}`, {
      description: "Task marked as skipped — cursor advanced.",
    });
  };

  const handleManualRetry = (task: Task) => {
    setManualTaskStatuses(prev => ({ ...prev, [task.id]: "queued" }));
    toast.info(`${task.id} reset to queue`);
  };

  const handleCreateHandoff = () => {
    const ts = new Date().toISOString();
    toast.success("Handoff artifact created", {
      description: `.aos/state/handoff.json written at ${ts.slice(0, 19)}`,
    });
    setHandoffNotes("");
  };

  // ── MANUAL MODE RENDER ──
  if (executionMode === "manual") {
    // Visible queue (retained for future use)
    const _queueTasks = allOrderedTasks.slice(
      Math.max(0, globalIndex - 1),
      Math.min(allOrderedTasks.length, globalIndex + 6)
    );

    return (
      <div className="w-[480px] flex flex-col shrink-0 h-full overflow-y-auto gap-4 custom-scrollbar">

        {/* ════════════════════════════════════════
            MANUAL MODE — UNIFIED CONTROL PANEL
            ════════════════════════════════════════ */}
        <div className="shrink-0 rounded-2xl border-2 border-amber-500/30 overflow-hidden bg-amber-500/[0.02]">

          {/* ── Mode banner ── */}
          <div className="px-4 py-2.5 flex items-center justify-between border-b border-amber-500/15 bg-amber-500/[0.04]">
            <div className="flex items-center gap-2">
              <div className="h-2 w-2 rounded-full bg-amber-500 ring-4 ring-amber-500/15" />
              <span className="text-[11px] font-mono font-bold uppercase tracking-widest text-amber-500">
                Manual Mode
              </span>
            </div>
            
            
          </div>

          {/* ── Active task context ── */}
          {(() => {
              const activeId = selectedManualTaskId ?? cursorTask?.id;
              const activeTask = activeId ? allOrderedTasks.find(t => t.id === activeId) : null;
              const activeStatus = activeTask ? getManualStatus(activeTask.id) : "queued";
              const activeIsDone = activeStatus === "done" || (activeTask?.status === "completed");
              const activeIsSkipped = activeStatus === "skipped";
              const activeIsRunning = activeStatus === "running";
              const activePhase = activeTask ? phases.find(p => p.id === activeTask.phaseId) : null;

              // Derive prev/next relative to the *selected* task, not just cursor
              const activeIndex = activeTask ? allOrderedTasks.findIndex(t => t.id === activeTask.id) : -1;
              const hasPrev = activeIndex > 0;
              const hasNext = activeIndex >= 0 && activeIndex < allOrderedTasks.length - 1;
              const prevTaskForNav = hasPrev ? allOrderedTasks[activeIndex - 1] : null;
              const nextTaskForNav = hasNext ? allOrderedTasks[activeIndex + 1] : null;

              return (
                <div className="flex flex-col">
                  {/* ── TASK INFO HEADER ── */}
                  <div className="px-4 py-3 border-b border-amber-500/10 bg-background/50">
                    <div className="flex items-start justify-between gap-3 mb-1.5">
                      <span className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground/50 truncate">
                        {activeTask?.milestone ?? "—"}/{activeTask?.phaseId ?? "—"}/{activeTask?.id ?? "—"}
                      </span>
                      <div>
                        {activeIsRunning && (
                          <Badge variant="outline" className="text-[8px] h-4 px-1.5 bg-amber-500/10 text-amber-500 border-amber-500/20 uppercase gap-1 font-mono animate-pulse">
                            <Loader2 className="h-2 w-2 animate-spin" />
                            Running
                          </Badge>
                        )}
                        {activeIsDone && (
                          <Badge variant="outline" className="text-[8px] h-4 px-1.5 bg-emerald-500/10 text-emerald-500 border-emerald-500/20 uppercase gap-1 font-mono">
                            <Check className="h-2 w-2" />
                            Done
                          </Badge>
                        )}
                        {activeIsSkipped && (
                          <Badge variant="outline" className="text-[8px] h-4 px-1.5 bg-muted/30 text-muted-foreground/50 border-muted-foreground/15 uppercase gap-1 font-mono">
                            <SkipForward className="h-2 w-2" />
                            Skipped
                          </Badge>
                        )}
                      </div>
                    </div>
                    <p className="text-sm font-mono text-foreground/90 truncate leading-snug font-medium">
                      {activeTask?.name ?? "No task selected"}
                    </p>
                    {activeTask && (
                      <p className="text-[10px] font-mono text-muted-foreground/50 truncate mt-0.5">
                        {activePhase?.title ?? activeTask.phaseId} · {activeTask.id}
                      </p>
                    )}
                  </div>

                  {/* ── CONTROLS AREA ── */}
                  <div className="p-3 space-y-3">
                    {/* Task Navigation & Actions (Media Player Style) */}
                    <div className="flex items-center justify-between gap-2">
                      <Button
                        size="sm"
                        variant="outline"
                        className={cn(
                          "h-8 px-3 text-[11px] font-mono gap-1.5 transition-all border-border/30",
                          hasPrev
                            ? "text-foreground/70 hover:bg-muted/40 hover:text-foreground"
                            : "text-muted-foreground/20 border-border/15"
                        )}
                        disabled={!hasPrev}
                        onClick={() => { 
                          if (prevTaskForNav) {
                            setSelectedManualTaskId(prevTaskForNav.id);
                            onAction(`selectTask:${prevTaskForNav.id}`);
                          }
                        }}
                      >
                        <ChevronLeft className="h-3.5 w-3.5" />
                        Prev
                      </Button>

                      <div className="flex items-center justify-center gap-1.5 flex-1">
                        {/* Run / Executing / Done */}
                        {activeIsRunning ? (
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-8 px-4 text-[11px] font-mono gap-1.5 bg-amber-500/10 text-amber-500 border-amber-500/25 pointer-events-none w-full max-w-[140px]"
                            disabled
                          >
                            <Loader2 className="h-3.5 w-3.5 animate-spin" />
                            Executing
                          </Button>
                        ) : (
                          <Button
                            size="sm"
                            variant={activeIsDone ? "outline" : "default"}
                            className={cn(
                              "h-8 px-4 text-[11px] font-mono gap-1.5 w-full max-w-[140px]",
                              activeIsDone
                                ? "bg-emerald-500/10 text-emerald-500 border-emerald-500/25 pointer-events-none"
                                : "bg-primary text-primary-foreground hover:bg-primary/90 shadow-sm shadow-primary/20"
                            )}
                            disabled={activeIsDone || !activeTask}
                            onClick={() => !activeIsDone && activeTask && handleManualExecute(activeTask)}
                          >
                            {activeIsDone ? (
                              <><Check className="h-3.5 w-3.5" />Done</>
                            ) : (
                              <><Play className="h-3.5 w-3.5" />Run</>
                            )}
                          </Button>
                        )}
                      </div>

                      <Button
                        size="sm"
                        variant="outline"
                        className={cn(
                          "h-8 px-3 text-[11px] font-mono gap-1.5 transition-all border-border/30",
                          hasNext
                            ? "text-foreground/70 hover:bg-muted/40 hover:text-foreground"
                            : "text-muted-foreground/20 border-border/15"
                        )}
                        disabled={!hasNext}
                        onClick={() => { 
                          if (nextTaskForNav) {
                            setSelectedManualTaskId(nextTaskForNav.id);
                            onAction(`selectTask:${nextTaskForNav.id}`);
                          }
                        }}
                      >
                        Next
                        <ChevronRight className="h-3.5 w-3.5" />
                      </Button>
                    </div>

                    {/* Pause & Handoff */}
                    <div className="flex items-center gap-2 pt-1 border-t border-border/20">
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-8 flex-1 text-[11px] font-mono gap-1.5 border-yellow-500/25 text-yellow-500 bg-yellow-500/10 hover:bg-yellow-500/20 hover:text-yellow-400 hover:border-yellow-500/40 transition-all"
                        onClick={handleCreateHandoff}
                      >
                        <Pause className="h-3.5 w-3.5" />
                        Pause &amp; Create Handoff
                      </Button>
                    </div>

                  </div>
                </div>
              );
            })()}
        </div>

        {/* ════════════════════════════════════════
            HANDOFF CREATION
            ════════════════════════════════════════ */}
        <AnimatePresence
          onExitComplete={() => {
            if (!handoff) setStickyHandoff(null);
          }}
        >
          {showHandoff && stickyHandoff && (
            <motion.div
              key="handoff-card"
              initial={{ opacity: 0, height: 0, marginTop: 0 }}
              animate={{ opacity: 1, height: "auto", marginTop: 0 }}
              exit={{ opacity: 0, height: 0, marginTop: 0 }}
              transition={{ duration: 0.3, ease: [0.4, 0, 0.2, 1] }}
              className="shrink-0 overflow-hidden"
            >
              <div className="bg-muted/10 border border-border/50 rounded-2xl shadow-inner overflow-hidden">
                <div
                  className="p-4 space-y-2 cursor-pointer hover:bg-muted/20 transition-colors"
                  onClick={() =>
                    navigate(`/ws/${ws}/files/.aos/state/handoff.json`)
                  }
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <FileJson className="h-4 w-4 text-primary/60" />
                      <span className="text-[11px] font-mono font-bold text-foreground/70 uppercase tracking-widest">
                        handoff.json
                      </span>
                      <Badge
                        className={cn(
                          "text-[10px] h-4.5 px-1.5",
                          stickyHandoff.integrity.matchesCursor
                            ? "bg-emerald-500/10 text-emerald-500 border-emerald-500/20"
                            : "bg-red-500/10 text-red-500 border-red-500/20"
                        )}
                      >
                        {stickyHandoff.integrity.matchesCursor ? "VALID" : "DRIFT"}
                      </Badge>
                    </div>
                    <ArrowRight className="h-4 w-4 text-muted-foreground/40" />
                  </div>
                  <pre className="text-[11px] font-mono text-muted-foreground/60 bg-muted/20 rounded-lg p-2.5 overflow-hidden max-h-24 leading-relaxed">
{`{
  "cursor": "${stickyHandoff.cursor.milestone}/${stickyHandoff.cursor.phase}/${stickyHandoff.cursor.task}@${stickyHandoff.cursor.step}",
  "inFlight": "${stickyHandoff.inFlight.step}",
  "nextCommand": "${stickyHandoff.nextCommand}"
}`}
                  </pre>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>

        {/* ════════════════════════════════════════
            EVENT FEED
            ════════════════════════════════════════ */}
        <div className="shrink-0 rounded-2xl border border-border/40 overflow-hidden bg-background">
          <div className="px-3 py-2 border-b border-border/50 bg-muted/10 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Activity className="h-3.5 w-3.5 text-amber-500" />
              <span className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">Event Feed</span>
            </div>
            <div className="flex items-center gap-1.5">
              <div className="h-1.5 w-1.5 rounded-full bg-amber-500 animate-pulse" />
              <span className="text-[9px] font-mono text-amber-500 uppercase tracking-wider">Manual</span>
            </div>
          </div>
          <div className="p-1 max-h-[180px] overflow-y-auto custom-scrollbar">
            {liveEvents.length === 0 ? (
              <div className="flex items-center justify-center py-6 text-[10px] font-mono text-muted-foreground/40">
                No events recorded
              </div>
            ) : liveEvents.map((evt) => {
              const evtType = (evt.type as string) ?? "";
              let Icon = Zap;
              let colorClass = "text-yellow-400";
              let bgClass = "bg-yellow-400/10";
              if (evtType.includes("uat") || evtType.includes("validated"))      { Icon = ShieldCheck;  colorClass = "text-cyan-400";    bgClass = "bg-cyan-400/10";    }
              if (evtType.includes("persisted") || evtType.includes("history"))  { Icon = Save;         colorClass = "text-blue-400";    bgClass = "bg-blue-400/10";    }
              if (evtType.includes("checkpoint") || evtType.includes("commit"))  { Icon = CheckCircle2; colorClass = "text-emerald-400"; bgClass = "bg-emerald-400/10"; }
              if (evtType.includes("fail") || evtType.includes("issue"))         { Icon = AlertCircle;  colorClass = "text-red-400";     bgClass = "bg-red-400/10";     }
              const ref = evt.references?.[0] ?? "";
              return (
                <div key={evt.id} className="flex items-start gap-3 p-2 rounded-xl hover:bg-muted/30 transition-colors group">
                  <div className={cn("mt-0.5 shrink-0 h-6 w-6 rounded-md flex items-center justify-center", bgClass)}>
                    <Icon className={cn("h-3.5 w-3.5", colorClass)} />
                  </div>
                  <div className="flex-1 min-w-0 pt-0.5">
                    <div className="flex items-center justify-between gap-2">
                      <span className="text-[10px] font-mono text-foreground/70">{ref || evtType}</span>
                      <span className="text-[9px] text-muted-foreground/40 whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity">{evt.timestamp ? timeAgo(evt.timestamp) : ""}</span>
                    </div>
                    <p className="text-[10px] text-muted-foreground/60 truncate mt-0.5">{String(evt.summary ?? "")}</p>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* ════════════════════════════════════════
            CHECKPOINTS
            ════════════════════════════════════════ */}
        <div className="shrink-0 flex flex-col bg-muted/10 border border-border/50 rounded-2xl shadow-inner overflow-hidden">
          <div className="shrink-0 px-5 pt-4 pb-3 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Database className="h-4 w-4 text-muted-foreground/40" />
              <span className="text-[11px] font-mono font-bold text-foreground/60 uppercase tracking-widest">
                Checkpoints
              </span>
              {checkpoints.length > 0 && (
                <span className="text-[10px] font-mono text-muted-foreground/40 ml-0.5">
                  {checkpoints.length}
                </span>
              )}
            </div>
            <button
              onClick={handleSave}
              className="flex items-center gap-1.5 text-[11px] font-mono text-muted-foreground/50 hover:text-primary transition-colors cursor-pointer"
            >
              <Plus className="h-3.5 w-3.5" />
              Save
            </button>
          </div>
          <div className="max-h-[400px] overflow-y-auto px-5 pb-5 custom-scrollbar">
            {checkpoints.length === 0 ? (
              <div className="flex flex-col items-center justify-center text-center py-12 opacity-50">
                <Database className="h-5 w-5 text-muted-foreground/30 mb-2.5" />
                <p className="text-[11px] font-mono text-muted-foreground/50">
                  No safe points yet
                </p>
              </div>
            ) : (
              <div className="relative">
                <div className="absolute left-[6px] top-2 bottom-2 w-px bg-border/40" />
                {checkpoints.map((cp, i) => (
                  <div
                    key={cp.id}
                    className="group/cp relative flex gap-3.5 py-2.5 cursor-pointer"
                    onClick={() =>
                      navigate(`/ws/${ws}/files/.aos/state/checkpoints/${cp.id}.json`)
                    }
                  >
                    <div className="relative z-10 mt-1 shrink-0">
                      {i === 0 ? (
                        <div className="h-[13px] w-[13px] rounded-full bg-primary/80 group-hover/cp:bg-primary transition-colors ring-2 ring-primary/20" />
                      ) : (
                        <div className="h-[13px] w-[13px] rounded-full border-2 bg-background transition-colors border-muted-foreground/25 group-hover/cp:border-muted-foreground/45" />
                      )}
                    </div>
                    <div className="flex-1 min-w-0 -mt-0.5">
                      <div className="flex items-center justify-between gap-2">
                        <div className="flex items-center gap-2 min-w-0">
                          <span className={cn(
                            "text-xs font-mono transition-colors truncate",
                            i === 0 ? "text-foreground/80 group-hover/cp:text-foreground" : "text-foreground/60 group-hover/cp:text-foreground/80"
                          )}>
                            {cp.id}
                          </span>
                          {i === 0 && (
                            <Badge className="text-[9px] h-4 px-1.5 bg-primary/10 text-primary border-primary/20 uppercase tracking-wider">
                              Latest
                            </Badge>
                          )}
                        </div>
                        <span className="text-[10px] font-mono text-muted-foreground/40 shrink-0">
                          {timeAgo(cp.timestamp)}
                        </span>
                      </div>
                      <p className="text-[11px] font-mono text-muted-foreground/50 truncate mt-0.5">
                        {cp.description}
                      </p>
                      <div className="flex items-center gap-1.5 text-[10px] font-mono text-muted-foreground/35 mt-1">
                        <Flag className="h-2.5 w-2.5 shrink-0" />
                        <span>{cp.cursor.milestone}/{cp.cursor.phase}/{cp.cursor.task ?? "—"}</span>
                      </div>
                      <button
                        className="flex items-center gap-1 mt-1.5 text-[10px] font-mono text-muted-foreground/35 hover:text-primary opacity-0 group-hover/cp:opacity-100 transition-all cursor-pointer"
                        onClick={(e) => { e.stopPropagation(); handleRestore(cp); }}
                      >
                        <RotateCcw className="h-3 w-3" />
                        Restore
                      </button>
                    </div>
                    <ArrowRight className="h-4 w-4 text-muted-foreground/20 group-hover/cp:text-muted-foreground/50 transition-colors mt-0.5 shrink-0" />
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    );
  }

  // ── ATOMIC MODE RENDER (existing) ──
  return (
    <div className="w-[480px] flex flex-col shrink-0 h-full overflow-y-auto gap-3 custom-scrollbar">
      {/* ─── TOP: Status + Controls ─── */}
      <div className={cn(
        "shrink-0 bg-muted/10 rounded-2xl shadow-inner p-4 border-2 transition-colors",
        systemStatus === "executing"
          ? "border-emerald-500/40 bg-emerald-500/[0.03]"
          : systemStatus === "paused"
            ? "border-yellow-500/40 bg-yellow-500/[0.03]"
            : "border-border/50"
      )}>
        <div className="flex items-center gap-2">
          {/* Status dot + label */}
          <div className="flex items-center gap-2 mr-auto">
            <div
              className={cn(
                "h-2.5 w-2.5 rounded-full ring-4 transition-all",
                systemStatus === "executing"
                  ? "bg-emerald-500 ring-emerald-500/20 animate-pulse"
                  : systemStatus === "paused"
                    ? "bg-yellow-500 ring-yellow-500/20"
                    : "bg-muted-foreground/30 ring-transparent"
              )}
            />
            <span className={cn(
              "text-xs font-mono font-bold uppercase tracking-widest transition-colors",
              systemStatus === "executing"
                ? "text-emerald-500"
                : systemStatus === "paused"
                  ? "text-yellow-600"
                  : "text-foreground/60"
            )}>
              {statusLabel}
            </span>
          </div>

          {/* All three buttons always visible, contextual styling */}
          <Button
            size="sm"
            variant="ghost"
            className={cn(
              "h-8 px-3 text-xs font-mono gap-1.5 transition-all",
              systemStatus === "executing"
                ? "bg-yellow-500/90 hover:bg-yellow-500 text-yellow-950 shadow-sm shadow-yellow-500/20"
                : "text-muted-foreground/40 hover:text-muted-foreground/60 hover:bg-muted/30 cursor-default"
            )}
            disabled={systemStatus !== "executing"}
            onClick={() => onAction("pause")}
          >
            <PauseCircle className="h-3.5 w-3.5" />
            Pause
          </Button>

          <Button
            size="sm"
            variant="ghost"
            className={cn(
              "h-8 px-3 text-xs font-mono gap-1.5 transition-all",
              systemStatus === "paused"
                ? "bg-emerald-500/90 hover:bg-emerald-500 text-emerald-950 shadow-sm shadow-emerald-500/20"
                : systemStatus === "idle"
                  ? "bg-primary hover:bg-primary/90 text-primary-foreground shadow-sm"
                  : "text-muted-foreground/40 hover:text-muted-foreground/60 hover:bg-muted/30 cursor-default"
            )}
            disabled={systemStatus === "executing"}
            onClick={() => onAction("resume")}
          >
            <Play className="h-3.5 w-3.5" />
            {systemStatus === "idle" ? "Start" : "Resume"}
          </Button>

          <Button
            variant="ghost"
            size="sm"
            className={cn(
              "h-8 px-2.5 text-xs font-mono gap-1 transition-all",
              systemStatus !== "idle"
                ? "text-destructive/60 hover:text-destructive hover:bg-destructive/10"
                : "text-muted-foreground/20 cursor-default"
            )}
            disabled={systemStatus === "idle"}
            onClick={() => onAction("stop")}
          >
            <StopCircle className="h-3.5 w-3.5" />
          </Button>
        </div>
      </div>

      {/* ─── Handoff Viewer (animated) ─── */}
      <AnimatePresence
        onExitComplete={() => {
          if (!handoff) setStickyHandoff(null);
        }}
      >
        {showHandoff && stickyHandoff && (
          <motion.div
            key="handoff-card"
            initial={{ opacity: 0, height: 0, marginTop: 0 }}
            animate={{ opacity: 1, height: "auto", marginTop: 0 }}
            exit={{ opacity: 0, height: 0, marginTop: 0 }}
            transition={{ duration: 0.3, ease: [0.4, 0, 0.2, 1] }}
            className="shrink-0 overflow-hidden"
          >
            <div className="bg-muted/10 border border-border/50 rounded-2xl shadow-inner overflow-hidden">
              <div
                className="p-4 space-y-2 cursor-pointer hover:bg-muted/20 transition-colors"
                onClick={() =>
                  navigate(`/ws/${ws}/files/.aos/state/handoff.json`)
                }
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <FileJson className="h-4 w-4 text-primary/60" />
                    <span className="text-[11px] font-mono font-bold text-foreground/70 uppercase tracking-widest">
                      handoff.json
                    </span>
                    <Badge
                      className={cn(
                        "text-[10px] h-4.5 px-1.5",
                        stickyHandoff.integrity.matchesCursor
                          ? "bg-emerald-500/10 text-emerald-500 border-emerald-500/20"
                          : "bg-red-500/10 text-red-500 border-red-500/20"
                      )}
                    >
                      {stickyHandoff.integrity.matchesCursor ? "VALID" : "DRIFT"}
                    </Badge>
                  </div>
                  <ArrowRight className="h-4 w-4 text-muted-foreground/40" />
                </div>
                <pre className="text-[11px] font-mono text-muted-foreground/60 bg-muted/20 rounded-lg p-2.5 overflow-hidden max-h-24 leading-relaxed">
{`{
  "cursor": "${stickyHandoff.cursor.milestone}/${stickyHandoff.cursor.phase}/${stickyHandoff.cursor.task}@${stickyHandoff.cursor.step}",
  "inFlight": "${stickyHandoff.inFlight.step}",
  "nextCommand": "${stickyHandoff.nextCommand}"
}`}
                </pre>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* ─── LIVE EVENT FEED ─── */}
      <div className="shrink-0 rounded-2xl border border-border/40 overflow-hidden bg-background">
        <div className="px-3 py-2 border-b border-border/50 bg-muted/10 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Activity className="h-3.5 w-3.5 text-primary" />
            <span className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">Event Feed</span>
          </div>
          {systemStatus === "executing" && (
            <div className="flex items-center gap-1.5">
              <div className="h-1.5 w-1.5 rounded-full bg-primary animate-pulse" />
              <span className="text-[9px] font-mono text-primary uppercase tracking-wider">Live</span>
            </div>
          )}
        </div>
        <div className="p-1 max-h-[180px] overflow-y-auto custom-scrollbar">
          {liveEvents.length === 0 ? (
            <div className="flex items-center justify-center py-6 text-[10px] font-mono text-muted-foreground/40">
              No events recorded
            </div>
          ) : liveEvents.map((evt) => {
            const evtType = (evt.type as string) ?? "";
            let Icon = Zap;
            let colorClass = "text-yellow-400";
            let bgClass = "bg-yellow-400/10";
            if (evtType.includes("uat") || evtType.includes("validated"))      { Icon = ShieldCheck;  colorClass = "text-cyan-400";    bgClass = "bg-cyan-400/10";    }
            if (evtType.includes("persisted") || evtType.includes("history"))  { Icon = Save;         colorClass = "text-blue-400";    bgClass = "bg-blue-400/10";    }
            if (evtType.includes("checkpoint") || evtType.includes("commit"))  { Icon = CheckCircle2; colorClass = "text-emerald-400"; bgClass = "bg-emerald-400/10"; }
            if (evtType.includes("fail") || evtType.includes("issue"))         { Icon = AlertCircle;  colorClass = "text-red-400";     bgClass = "bg-red-400/10";     }
            const ref = evt.references?.[0] ?? "";
            return (
              <div key={evt.id} className="flex items-start gap-3 p-2 rounded-xl hover:bg-muted/30 transition-colors group">
                <div className={cn("mt-0.5 shrink-0 h-6 w-6 rounded-md flex items-center justify-center", bgClass)}>
                  <Icon className={cn("h-3.5 w-3.5", colorClass)} />
                </div>
                <div className="flex-1 min-w-0 pt-0.5">
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-[10px] font-mono text-foreground/70">{ref || evtType}</span>
                    <span className="text-[9px] text-muted-foreground/40 whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity">{evt.timestamp ? timeAgo(evt.timestamp) : ""}</span>
                  </div>
                  <p className="text-[10px] text-muted-foreground/60 truncate mt-0.5">{String(evt.summary ?? "")}</p>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* ─── CONTEXT PACKS ─── */}
      {livePacks.length > 0 && (
        <div className="shrink-0 rounded-2xl border border-border/40 overflow-hidden bg-background">
          <div className="px-3 py-2 border-b border-border/50 bg-muted/10 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Clipboard className="h-3.5 w-3.5 text-blue-400" />
              <span className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">Context Packs</span>
            </div>
            <span className="text-[9px] font-mono text-muted-foreground/40">{livePacks.length}</span>
          </div>
          <div className="p-1 max-h-[120px] overflow-y-auto custom-scrollbar">
            {livePacks.map((pack) => (
              <div key={pack.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-muted/30 transition-colors">
                <FileJson className="h-3 w-3 text-blue-400/70 shrink-0" />
                <span className="text-[10px] font-mono text-foreground/70 truncate">{pack.id}</span>
                {pack.size && <span className="ml-auto text-[9px] font-mono text-muted-foreground/50 shrink-0">{pack.size}</span>}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ─── BOTTOM: Checkpoints ─── */}
      <div className="flex-1 flex flex-col bg-muted/10 border border-border/50 rounded-2xl shadow-inner overflow-hidden">
        {/* Section header */}
        <div className="shrink-0 px-4 pt-4 pb-2.5 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Database className="h-4 w-4 text-muted-foreground/40" />
            <span className="text-[11px] font-mono font-bold text-foreground/60 uppercase tracking-widest">
              Checkpoints
            </span>
            {checkpoints.length > 0 && (
              <span className="text-[10px] font-mono text-muted-foreground/40 ml-0.5">
                {checkpoints.length}
              </span>
            )}
          </div>
          <button
            onClick={handleSave}
            className="flex items-center gap-1.5 text-[11px] font-mono text-muted-foreground/50 hover:text-primary transition-colors cursor-pointer"
          >
            <Plus className="h-3.5 w-3.5" />
            Save
          </button>
        </div>

        {/* Checkpoint timeline */}
        <div className="flex-1 overflow-y-auto px-4 pb-4 custom-scrollbar">
          {checkpoints.length === 0 ? (
            <div className="flex flex-col items-center justify-center text-center py-12 opacity-50">
              <Database className="h-5 w-5 text-muted-foreground/30 mb-2.5" />
              <p className="text-[11px] font-mono text-muted-foreground/50">
                No safe points yet
              </p>
            </div>
          ) : (
            <div className="relative">
              {/* Timeline spine */}
              <div className="absolute left-[6px] top-2 bottom-2 w-px bg-border/40" />

              {checkpoints.map((cp, i) => (
                <div
                  key={cp.id}
                  className="group/cp relative flex gap-3.5 py-2.5 cursor-pointer"
                  onClick={() =>
                    navigate(
                      `/ws/${ws}/files/.aos/state/checkpoints/${cp.id}.json`
                    )
                  }
                >
                  {/* Timeline node */}
                  <div className="relative z-10 mt-1 shrink-0">
                    {i === 0 ? (
                      <div className="h-[13px] w-[13px] rounded-full bg-primary/80 group-hover/cp:bg-primary transition-colors ring-2 ring-primary/20" />
                    ) : (
                      <div
                        className="h-[13px] w-[13px] rounded-full border-2 bg-background transition-colors border-muted-foreground/25 group-hover/cp:border-muted-foreground/45"
                      />
                    )}
                  </div>

                  {/* Content */}
                  <div className="flex-1 min-w-0 -mt-0.5">
                    <div className="flex items-center justify-between gap-2">
                      <div className="flex items-center gap-2 min-w-0">
                        <span
                          className={cn(
                            "text-xs font-mono transition-colors truncate",
                            i === 0
                              ? "text-foreground/80 group-hover/cp:text-foreground"
                              : "text-foreground/60 group-hover/cp:text-foreground/80"
                          )}
                        >
                          {cp.id}
                        </span>
                        {i === 0 && (
                          <Badge className="text-[9px] h-4 px-1.5 bg-primary/10 text-primary border-primary/20 uppercase tracking-wider">
                            Latest
                          </Badge>
                        )}
                        {cp.source && (
                          <span
                            className={cn(
                              "text-[9px] font-mono uppercase tracking-wider flex items-center gap-0.5",
                              cp.source === "auto"
                                ? "text-yellow-500/50"
                                : "text-muted-foreground/40"
                            )}
                          >
                            {cp.source === "auto" ? (
                              <Zap className="h-2.5 w-2.5" />
                            ) : (
                              <Plus className="h-2.5 w-2.5" />
                            )}
                            {cp.source}
                          </span>
                        )}
                      </div>
                      <span className="text-[10px] font-mono text-muted-foreground/40 shrink-0">
                        {timeAgo(cp.timestamp)}
                      </span>
                    </div>

                    <p className="text-[11px] font-mono text-muted-foreground/50 truncate mt-0.5">
                      {cp.description}
                    </p>

                    <div className="flex items-center gap-1.5 text-[10px] font-mono text-muted-foreground/35 mt-1">
                      <Flag className="h-2.5 w-2.5 shrink-0" />
                      <span>
                        {cp.cursor.milestone}/{cp.cursor.phase}/
                        {cp.cursor.task ?? "—"}
                      </span>
                    </div>

                    {/* Restore — slides in on hover */}
                    <button
                      className="flex items-center gap-1 mt-1.5 text-[10px] font-mono text-muted-foreground/35 hover:text-primary opacity-0 group-hover/cp:opacity-100 transition-all cursor-pointer"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleRestore(cp);
                      }}
                    >
                      <RotateCcw className="h-3 w-3" />
                      Restore
                    </button>
                  </div>

                  {/* Nav arrow */}
                  <ArrowRight className="h-4 w-4 text-muted-foreground/20 group-hover/cp:text-muted-foreground/50 transition-colors mt-0.5 shrink-0" />
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}