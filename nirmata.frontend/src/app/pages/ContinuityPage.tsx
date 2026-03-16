import {
  AlertCircle,
  ArrowRight,
  Calendar,
  Check,
  CheckCircle,
  ChevronDown,
  FileCode,
  Flag,
  GitCommit,
  Layers,
  Loader2,
  Plus,
  Shield,
  Trash2,
  Zap,
  Cpu,
  AlertTriangle,
} from "lucide-react";
import { useState, useMemo } from "react";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "../components/ui/alert-dialog";
import { Badge } from "../components/ui/badge";
import { Button } from "../components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "../components/ui/card";
import { cn } from "../components/ui/utils";
import {
  useWorkspace,
  useTasks,
  usePhases,
  useMilestones,
  useContinuityState,
  type Task,
  type HandoffSnapshot,
} from "../hooks/useAosData";
import { InspectorPanel } from "../components/continuity/InspectorPanel";

// --- Components ---

function ModeToggle({ view, onChange }: { view: "atomic" | "manual"; onChange: (v: "atomic" | "manual") => void }) {
  return (
    <div className="relative flex items-center w-[260px] h-9 bg-muted/50 rounded-full p-1 border border-border">
      <div
        className={cn(
          "absolute top-1 bottom-1 rounded-full bg-foreground transition-all duration-200 ease-out",
          view === "atomic" ? "left-1 w-[calc(50%-4px)]" : "left-[calc(50%+2px)] w-[calc(50%-4px)]"
        )}
      />
      <button
        onClick={() => onChange("atomic")}
        className={cn(
          "relative z-10 flex-1 flex items-center justify-center gap-1.5 h-full rounded-full text-[11px] font-mono font-medium tracking-wide transition-colors cursor-pointer select-none",
          view === "atomic" ? "text-background" : "text-muted-foreground hover:text-foreground"
        )}
      >
        <Zap className="h-3 w-3" />
        Atomic
      </button>
      <button
        onClick={() => onChange("manual")}
        className={cn(
          "relative z-10 flex-1 flex items-center justify-center gap-1.5 h-full rounded-full text-[11px] font-mono font-medium tracking-wide transition-colors cursor-pointer select-none",
          view === "manual" ? "text-background" : "text-muted-foreground hover:text-foreground"
        )}
      >
        <Cpu className="h-3 w-3" />
        Manual
      </button>
    </div>
  );
}

// --- Main Page ---

export function ContinuityPage() {
  const navigate = useNavigate();
  const { workspace } = useWorkspace();
  const { tasks: hookTasks } = useTasks();
  const { phases: allPhases } = usePhases();
  const { milestones: allMilestones } = useMilestones();
  const { handoff: mockHandoff } = useContinuityState();
  const [executionMode, setExecutionMode] = useState<"atomic" | "manual">("atomic");
  const [pendingMode, setPendingMode] = useState<"atomic" | "manual" | null>(null);
  const [systemStatus, setSystemStatus] = useState<"idle" | "executing" | "paused">("paused");
  const [expandedMilestones, setExpandedMilestones] = useState<Set<string>>(new Set(["MS-0001"]));
  const [expandedPhases, setExpandedPhases] = useState<Set<string>>(new Set(["PH-0002"]));
  const [selectedId, setSelectedId] = useState<string | null>("TSK-000003");
  const [tasks, setTasks] = useState<Task[]>([...hookTasks]);

  // Settings/Toggles — wired in a future iteration
  const [_runUntil, _setRunUntil] = useState<"task" | "phase" | "milestone">("task");
  const [_alwaysVerify, _setAlwaysVerify] = useState(true);
  const [_autoFix, _setAutoFix] = useState(false);

  // UAT check states: track pass/fail/untested per "phaseId::index" — future feature
  const [_uatStates, _setUatStates] = useState<Record<string, "pass" | "fail" | "untested">>({});

  // Handoff state
  const [handoff, setHandoff] = useState<HandoffSnapshot | null>(null);
  const [_showHandoff, _setShowHandoff] = useState(false);

  const _toggleUat = (key: string) => {
    _setUatStates(prev => {
      const current = prev[key] || "untested";
      const next = current === "untested" ? "pass" : current === "pass" ? "fail" : "untested";
      return { ...prev, [key]: next };
    });
  };

  const toggleMilestone = (milestoneId: string) => {
    setExpandedMilestones(prev => {
      const next = new Set(prev);
      if (next.has(milestoneId)) next.delete(milestoneId);
      else next.add(milestoneId);
      return next;
    });
  };

  const togglePhase = (phaseId: string) => {
    setExpandedPhases(prev => {
      const next = new Set(prev);
      if (next.has(phaseId)) next.delete(phaseId);
      else next.add(phaseId);
      return next;
    });
  };

  const addTask = (phaseId: string) => {
    const phaseTasks = tasks.filter(t => t.phaseId === phaseId);
    const nextNum = String(tasks.length + 1).padStart(6, "0");
    const newTask: Task = {
      id: `TSK-${nextNum}`,
      phaseId,
      milestone: allPhases.find(p => p.id === phaseId)?.milestoneId ?? "MS-01",
      name: `New task ${phaseTasks.length + 1}`,
      status: "planned",
      assignedTo: "unassigned",
    };
    setTasks(prev => [...prev, newTask]);
    toast.success(`Created ${newTask.id} in ${phaseId}`);
  };

  const removeTask = (taskId: string) => {
    setTasks(prev => prev.filter(t => t.id !== taskId));
    toast.success(`Removed ${taskId}`);
  };

  const handleAction = (action: string) => {
    if (action === "Pause" || action === "pause") {
      setSystemStatus("paused");
      setHandoff(mockHandoff);
      toast.success("Work paused. Handoff artifact written to .aos/state/handoff.json", {
        description: "Execution state captured and sealed.",
      });
    }
    if (action === "Resume" || action === "Start" || action === "resume") {
      setSystemStatus("executing");
      setHandoff(null);
      toast.success("Work resumed.", {
        description: "Validated handoff.json matches current cursor.",
      });
    }
    if (action === "Stop" || action === "stop") {
      setSystemStatus("idle");
      setHandoff(null);
    }
    if (action === "Resume Task") {
      const runId = "RUN-2026-02-22T143022Z";
      setSelectedId("TSK-000012");
      setSystemStatus("executing");
      setHandoff(null);
      toast.info(`Jumping to run ${runId}...`, {
        description: "Continuing execution from last checkpoint.",
      });
    }
    if (action === "checkpoint") {
      const ts = new Date().toISOString().replace(/[:.]/g, "-");
      toast.success(`aos checkpoint create`, {
        description: `Checkpoint written to .aos/state/checkpoints/CHK-${ts.slice(0, 19)}.json`,
      });
    }
    if (action.startsWith("selectTask:")) {
      const taskId = action.split(":")[1];
      setSelectedId(taskId);

      // Auto-expand the phase and milestone containing this task
      const task = tasks.find(t => t.id === taskId);
      if (task) {
        const phase = allPhases.find(p => p.id === task.phaseId);

        // Expand the phase
        setExpandedPhases(prev => {
          const next = new Set(prev);
          next.add(task.phaseId);
          return next;
        });

        // Expand the parent milestone
        if (phase) {
          setExpandedMilestones(prev => {
            const next = new Set(prev);
            next.add(phase.milestoneId);
            return next;
          });
        }
      }
    }
  };

  return (
    <div className="flex-1 flex flex-col h-full bg-background overflow-hidden">
      {/* ── Sub-toolbar: execution mode toggle ── */}
      <div className="shrink-0 border-b border-border/50 px-6 py-2.5 flex items-center gap-4">
        <ModeToggle
          view={executionMode}
          onChange={(m) => {
            if (m !== executionMode) setPendingMode(m);
          }}
        />
      </div>

      {/* Main Content Area */}
      <div className="flex-1 flex overflow-hidden p-6 gap-8">

        {/* LEFT: Master Queue (Floating List) */}
        <div className="flex-1 flex flex-col min-w-0">
          <div className="flex-1 overflow-y-auto pr-4 -mr-4 space-y-3 custom-scrollbar">
            {allMilestones.map((milestone, milestoneIndex) => {
              const isMilestoneActive = milestone.id === workspace.cursor.milestone;
              const isMilestoneCompleted = milestone.status === "completed";
              const isMilestonePlanned = milestone.status === "planned";
              const milestonePhases = allPhases.filter(p => milestone.phases.includes(p.id));
              const milestoneTasks = tasks.filter(t => t.milestone === milestone.id);
              const msCompletedTasks = milestoneTasks.filter(t => t.status === "completed").length;
              const msProgress = milestoneTasks.length > 0 ? Math.round((msCompletedTasks / milestoneTasks.length) * 100) : 0;

              return (
                <div key={milestone.id} className="relative">
                  {/* Vertical connector line */}
                  {milestonePhases.length > 0 && expandedMilestones.has(milestone.id) && (
                    <div className="absolute left-[15px] top-8 bottom-0 w-0.5 bg-border/50" />
                  )}

                  {/* Inter-milestone connector */}
                  {milestoneIndex < allMilestones.length - 1 && !expandedMilestones.has(milestone.id) && (
                    <div className="absolute left-[15px] top-8 h-4 w-0.5 bg-border/50" />
                  )}

                  {/* Milestone Header */}
                  <div
                    className={cn(
                      "flex items-start gap-3 cursor-pointer group/ms rounded-lg p-2 -ml-2 transition-all",
                      expandedMilestones.has(milestone.id) && "mb-4",
                      "hover:bg-accent/50"
                    )}
                    onClick={() => { setSelectedId(milestone.id); toggleMilestone(milestone.id); }}
                  >
                    <div className={cn(
                      "relative z-10 flex items-center justify-center h-8 w-8 rounded-lg border-2 shrink-0 transition-all",
                      isMilestoneCompleted
                        ? "border-emerald-400/60 bg-emerald-400/5 group-hover/ms:border-emerald-400 group-hover/ms:bg-emerald-400/10"
                        : isMilestoneActive
                          ? "border-primary/70 ring-4 ring-primary/10 bg-background group-hover/ms:ring-primary/20 group-hover/ms:border-primary"
                          : "border-muted-foreground/15 bg-muted/20 group-hover/ms:border-muted-foreground/30 group-hover/ms:bg-muted/40"
                    )}>
                      {isMilestoneCompleted ? (
                        <Check className="h-4 w-4 text-emerald-400" />
                      ) : isMilestoneActive ? (
                        <Flag className="h-4 w-4 text-primary" />
                      ) : (
                        <Flag className="h-4 w-4 text-muted-foreground/40 group-hover/ms:text-muted-foreground/60" />
                      )}
                    </div>
                    <div className="flex-1 min-w-0 pt-0.5">
                      <div className="flex items-center gap-2 flex-wrap">
                        <ChevronDown className={cn(
                          "h-4 w-4 transition-transform shrink-0 text-muted-foreground/50 group-hover/ms:text-muted-foreground/80",
                          !expandedMilestones.has(milestone.id) && "-rotate-90"
                        )} />
                        <Badge variant="outline" className={cn(
                          "font-mono text-xs transition-colors",
                          isMilestoneCompleted && "border-emerald-400/30 text-emerald-500",
                          isMilestoneActive && "border-primary/30 text-primary",
                          isMilestonePlanned && "text-muted-foreground/70"
                        )}>{milestone.id}</Badge>
                        <span className={cn(
                          "text-lg font-semibold tracking-tight",
                          isMilestonePlanned && "text-foreground/70"
                        )}>{milestone.name}</span>
                        {isMilestoneActive && (
                          <Badge className="bg-primary/10 text-primary border-primary/20 text-[10px] h-5 gap-1">
                            <Loader2 className="h-2.5 w-2.5 animate-spin" />ACTIVE
                          </Badge>
                        )}
                        {isMilestoneCompleted && (
                          <Badge className="bg-emerald-400/10 text-emerald-500 border-emerald-400/20 text-[10px] h-5 gap-1">
                            <Check className="h-2.5 w-2.5" />DONE
                          </Badge>
                        )}
                        <Badge
                          variant="outline"
                          className={cn(
                            "ml-auto transition-colors font-mono text-xs",
                            isMilestoneCompleted && "bg-emerald-400/5 text-emerald-500 border-emerald-400/25",
                            isMilestoneActive && "bg-primary/5 text-primary border-primary/25",
                            isMilestonePlanned && "bg-muted/30 text-muted-foreground/50 border-muted-foreground/15"
                          )}
                        >{milestonePhases.filter(p => p.status === "completed").length}/{milestonePhases.length} phases</Badge>
                      </div>
                      <p className={cn(
                        "text-xs font-mono mt-1 line-clamp-1",
                        isMilestonePlanned ? "text-muted-foreground/60" : "text-muted-foreground"
                      )}>{milestone.description}</p>
                      <div className="flex items-center gap-3 mt-1.5 text-[10px] text-muted-foreground/80 font-mono">
                        <span className="flex items-center gap-1"><Calendar className="h-3 w-3" /> Target: {milestone.targetDate}</span>
                        <span className={cn("flex items-center gap-1", isMilestoneCompleted && "text-emerald-500/70")}>
                          <CheckCircle className="h-3 w-3" /> {msCompletedTasks}/{milestoneTasks.length} tasks
                        </span>
                        <span className="flex items-center gap-1"><Layers className="h-3 w-3" /> {milestonePhases.length} phases</span>
                        {milestoneTasks.length > 0 && (
                          <span className={cn(
                            "flex items-center gap-1.5 ml-1",
                            msProgress === 100 ? "text-emerald-500/70" : isMilestoneActive ? "text-primary/70" : ""
                          )}>
                            <span className="h-1 w-12 bg-muted/60 rounded-full overflow-hidden inline-block">
                              <span
                                className={cn(
                                  "block h-full rounded-full transition-all",
                                  msProgress === 100 ? "bg-emerald-400" : isMilestoneActive ? "bg-primary" : "bg-muted-foreground/25"
                                )}
                                style={{ width: `${msProgress}%` }}
                              />
                            </span>
                            {msProgress}%
                          </span>
                        )}
                      </div>
                    </div>
                  </div>

                  {/* Phases */}
                  {expandedMilestones.has(milestone.id) && (
                  <div className="relative ml-[15px] pl-[2px] space-y-5">
                    {milestonePhases.map((phase, phaseIndex) => {
                      const isActive = phase.id === workspace.cursor.phase;
                      const isCompleted = phase.status === "completed";
                      const isPlanned = phase.status === "planned";
                      const phaseTasks = tasks.filter(t => t.phaseId === phase.id);
                      const completedCount = phaseTasks.filter(t => t.status === "completed").length;
                      const failedCount = phaseTasks.filter(t => t.status === "failed").length;
                      const phaseProgress = phaseTasks.length > 0 ? Math.round((completedCount / phaseTasks.length) * 100) : 0;
                      const phaseFiles = phase.links.artifacts.map(a => a.path);
                      const phaseVerifications = phase.acceptance.criteria.length;

                      return (
                        <div key={phase.id} className="relative ml-6">
                          {/* Phase node */}
                          <div className={cn(
                            "absolute -left-[33px] top-3 h-4 w-4 rounded-full border-2 bg-[#0A0A0A] flex items-center justify-center transition-colors z-10",
                            isActive ? "border-primary ring-4 ring-primary/15" : isCompleted ? "border-emerald-400/60" : "border-muted-foreground/20"
                          )}>
                            {isCompleted && <Check className="h-2.5 w-2.5 text-emerald-400" />}
                            {isActive && <div className="h-2 w-2 bg-primary rounded-full animate-pulse" />}
                          </div>

                          {/* Terminate the line at last phase */}
                          {phaseIndex === milestonePhases.length - 1 && (
                            <div className="absolute -left-[26px] top-[19px] bottom-0 w-0.5 bg-border/50 z-[5]" />
                          )}

                          <Card className={cn(
                            "transition-all group/phase",
                            isActive ? "border-primary/40 shadow-sm shadow-primary/5" : isCompleted ? "border-emerald-400/15" : "border-border/40",
                            "hover:border-accent-foreground/20 hover:shadow-md hover:shadow-black/5"
                          )}>
                            <CardHeader className={cn(
                              "pb-3 cursor-pointer transition-colors rounded-t-lg",
                              "hover:bg-accent/40"
                            )} onClick={() => { setSelectedId(phase.id); togglePhase(phase.id); }}>
                              <div className="flex items-start justify-between">
                                <div className="space-y-1.5 min-w-0">
                                  <div className="flex items-center gap-2 flex-wrap">
                                    <ChevronDown className={cn(
                                      "h-4 w-4 transition-transform shrink-0 text-muted-foreground/50",
                                      !expandedPhases.has(phase.id) && "-rotate-90"
                                    )} />
                                    <Badge variant="outline" className={cn(
                                      "font-mono text-xs",
                                      isCompleted && "border-emerald-400/30 text-emerald-500",
                                      isActive && "border-primary/30 text-primary",
                                      isPlanned && "text-muted-foreground/70"
                                    )}>{phase.id}</Badge>
                                    <CardTitle className={cn(
                                      "text-base",
                                      isPlanned && "text-foreground/70"
                                    )}>{phase.title}</CardTitle>
                                    {isActive && <Badge className="bg-primary/10 text-primary border-primary/20 text-[10px] h-5">HEAD</Badge>}
                                    {isCompleted && <Badge className="bg-emerald-400/10 text-emerald-500 border-emerald-400/20 text-[10px] h-5 gap-0.5"><Check className="h-2.5 w-2.5" />DONE</Badge>}
                                  </div>
                                  <CardDescription className={cn("line-clamp-1 font-mono text-xs ml-6", isPlanned && "opacity-70")}>{phase.summary}</CardDescription>
                                </div>
                                <div className="flex items-center gap-2 shrink-0 ml-3">
                                  <span className={cn(
                                    "text-xs font-mono",
                                    isCompleted ? "text-emerald-500/70" : "text-muted-foreground/70"
                                  )}>{completedCount}/{phaseTasks.length}</span>
                                  <Badge
                                    variant="outline"
                                    className={cn(
                                      "transition-colors",
                                      isCompleted && "bg-emerald-400/5 text-emerald-500 border-emerald-400/25",
                                      isActive && "bg-primary/5 text-primary border-primary/25",
                                      isPlanned && "bg-muted/30 text-muted-foreground/50 border-muted-foreground/15"
                                    )}
                                  >{phase.status}</Badge>
                                  <button
                                    className="opacity-0 group-hover/phase:opacity-100 transition-opacity p-1 rounded hover:bg-red-500/10 text-muted-foreground/30 hover:text-red-400"
                                    onClick={(e) => { e.stopPropagation(); toast(`Removed ${phase.id}`, { action: { label: "Undo", onClick: () => {} }, duration: 5000 }); }}
                                    title={`Remove ${phase.id} from milestone`}
                                    aria-label={`Remove phase ${phase.id}`}
                                  >
                                    <Trash2 className="h-3.5 w-3.5" />
                                  </button>
                                </div>
                              </div>
                              {/* Phase progress bar */}
                              {phaseTasks.length > 0 && (
                                <div className="mt-2 ml-6">
                                  <div className="h-1 w-full bg-muted/50 rounded-full overflow-hidden">
                                    <div
                                      className={cn(
                                        "h-full rounded-full transition-all",
                                        isCompleted ? "bg-emerald-400/70" : isActive ? "bg-primary/70" : "bg-muted-foreground/15"
                                      )}
                                      style={{ width: `${phaseProgress}%` }}
                                    />
                                  </div>
                                </div>
                              )}
                            </CardHeader>
                            {expandedPhases.has(phase.id) && (
                              <CardContent className="space-y-3 pt-0">
                                {/* Tasks */}
                                <div>
                                  <div className="text-[10px] text-muted-foreground/70 uppercase tracking-wider font-medium mb-2">Tasks</div>
                                  <div className={cn(
                                    "relative ml-2 border-l space-y-0",
                                    isCompleted ? "border-emerald-400/20" : isActive ? "border-primary/20" : "border-border/50"
                                  )}>
                                    {phaseTasks.map(task => {
                                      const isTaskCompleted = task.status === "completed";
                                      const isTaskRunning = task.status === "in-progress" || (task.id === workspace.cursor.task && systemStatus === "executing");
                                      const isTaskFailed = task.status === "failed";
                                      const isSelectedTask = task.id === selectedId;

                                      return (
                                      <div key={task.id} className={cn(
                                        "group/task relative flex items-center gap-2 text-xs py-1.5 pl-4 pr-2 rounded transition-all cursor-pointer",
                                        isSelectedTask
                                          ? "bg-blue-500/[0.12] ring-1 ring-blue-500/25"
                                          : isTaskRunning
                                            ? "bg-emerald-500/[0.12] ring-1 ring-emerald-500/25"
                                            : "hover:bg-accent/40",
                                        isTaskCompleted && !isSelectedTask && !isTaskRunning && "opacity-80 hover:opacity-100"
                                      )} onClick={() => setSelectedId(task.id)}>
                                        {/* Task node */}
                                        <div className={cn(
                                          "absolute left-[-1px] top-1/2 -translate-y-1/2 h-[7px] w-[7px] rounded-full shrink-0 border border-background transition-colors",
                                          isSelectedTask ? "bg-blue-500 ring-2 ring-blue-500/30" : isTaskRunning ? "bg-emerald-500 ring-2 ring-emerald-500/30" : isTaskCompleted ? "bg-emerald-400" : isTaskFailed ? "bg-red-400" : "bg-muted-foreground/25"
                                        )} />
                                        {/* Status icon */}
                                        <span className="shrink-0 w-4 flex items-center justify-center">
                                          {isTaskCompleted && <Check className="h-3 w-3 text-emerald-400" />}
                                          {isTaskRunning && <Loader2 className="h-3 w-3 text-emerald-500 animate-spin" />}
                                          {isTaskFailed && <AlertCircle className="h-3 w-3 text-red-400" />}
                                          {task.status === "planned" && !isTaskRunning && <span className="h-2 w-2 rounded-full bg-muted-foreground/15" />}
                                        </span>
                                        <span className={cn(
                                          "font-mono w-20 shrink-0 transition-colors",
                                          isSelectedTask ? "text-blue-400/80" : isTaskRunning ? "text-emerald-500/80" : isTaskCompleted ? "text-emerald-500/50" : "text-muted-foreground/60",
                                          "group-hover/task:text-muted-foreground"
                                        )}>{task.id}</span>
                                        <span className={cn(
                                          "truncate transition-colors",
                                          isSelectedTask ? "text-blue-100" : isTaskRunning ? "text-emerald-100" : isTaskCompleted ? "text-muted-foreground/70 line-through decoration-muted-foreground/30" : "text-foreground/80",
                                          "group-hover/task:text-foreground"
                                        )}>{task.name}</span>
                                        <span className="ml-auto shrink-0 flex items-center gap-2">
                                          {task.commitHash && <span className="font-mono text-[10px] text-muted-foreground/50 flex items-center gap-0.5 group-hover/task:text-muted-foreground/70"><GitCommit className="h-2.5 w-2.5" />{task.commitHash}</span>}
                                          <button
                                            className="opacity-0 group-hover/task:opacity-100 transition-opacity p-0.5 rounded hover:bg-red-500/10 text-muted-foreground/40 hover:text-red-400"
                                            onClick={(e) => { e.stopPropagation(); removeTask(task.id); }}
                                            title={`Remove ${task.id}`}
                                          >
                                            <Trash2 className="h-3 w-3" />
                                          </button>
                                          <ArrowRight className="h-3 w-3 text-muted-foreground/30 group-hover/task:text-muted-foreground/60 transition-colors" />
                                        </span>
                                      </div>
                                      );
                                    })}
                                    <div className="flex items-center gap-2 text-xs py-1.5 pl-4 pr-2 rounded hover:bg-accent/40 transition-colors cursor-pointer group/add" onClick={() => addTask(phase.id)}>
                                      <Plus className="h-3.5 w-3.5 text-muted-foreground/50 group-hover/add:text-muted-foreground" />
                                      <span className="font-mono text-muted-foreground/50 group-hover/add:text-muted-foreground transition-colors">Add Task</span>
                                    </div>
                                  </div>
                                </div>

                                {phaseFiles.length > 0 && (
                                  <div className="bg-muted/20 rounded-md p-2.5 border border-border/30">
                                    <div className="flex items-center gap-4 text-[10px] text-muted-foreground/70 font-mono">
                                      <span className="flex items-center gap-1"><FileCode className="h-3 w-3" /> {phaseFiles.length} files in scope</span>
                                      <span className="flex items-center gap-1"><Shield className="h-3 w-3" /> {phaseVerifications} verifications</span>
                                    </div>
                                    <div className="flex flex-wrap gap-1 mt-1.5">
                                      {phaseFiles.slice(0, 4).map((f, i) => (
                                        <code key={i} className="text-[10px] bg-muted/50 px-1.5 py-0.5 rounded text-muted-foreground/70">{f.split('/').pop()}</code>
                                      ))}
                                      {phaseFiles.length > 4 && <span className="text-[10px] text-muted-foreground/50 px-1.5 py-0.5">+{phaseFiles.length - 4} more</span>}
                                    </div>
                                  </div>
                                )}

                                <div className="flex items-center justify-between pt-2 border-t border-border/40">
                                  <div className="flex items-center gap-3 text-xs text-muted-foreground/70 font-mono">
                                    <span className={cn("flex items-center gap-1", isCompleted && "text-emerald-500/60")}><CheckCircle className="h-3 w-3" />{completedCount}/{phaseTasks.length}</span>
                                    {failedCount > 0 && <span className="flex items-center gap-1 text-red-400/80"><AlertCircle className="h-3 w-3" />{failedCount} failed</span>}
                                    <span className="flex items-center gap-1"><Layers className="h-3 w-3" />{phase.deliverables.length} deliverables</span>
                                  </div>
                                  <Button variant="ghost" size="sm" className="h-7 text-xs gap-1 font-mono text-muted-foreground/60 hover:text-foreground" onClick={(e) => { e.stopPropagation(); navigate(`/ws/${workspace.projectName}/files/.aos/spec/phases/${phase.id}/phase.json`); }}>
                                    phase.json <ArrowRight className="h-3 w-3" />
                                  </Button>
                                </div>
                              </CardContent>
                            )}
                          </Card>
                        </div>
                      );
                    })}

                    {/* Add Phase */}
                    <div className="flex justify-center mt-4 ml-6">
                      <Button variant="outline" size="sm" className="gap-2 text-muted-foreground border-dashed border-border/40 hover:border-emerald-500/50 hover:text-emerald-500 hover:bg-emerald-500/5">
                        <Plus className="h-3.5 w-3.5" /> Add Phase
                      </Button>
                    </div>
                  </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* RIGHT: Detail Pane (Inspector Panel) */}
        <InspectorPanel
          systemStatus={systemStatus}
          selectedId={selectedId}
          tasks={tasks}
          handoff={handoff}
          executionMode={executionMode}
          onAction={handleAction}
        />
      </div>

      {/* ── Mode-switch confirmation dialog ── */}
      <AlertDialog open={pendingMode !== null} onOpenChange={(open) => { if (!open) setPendingMode(null); }}>
        <AlertDialogContent className="max-w-sm">
          <AlertDialogHeader>
            <AlertDialogTitle className="flex items-center gap-2">
              <AlertTriangle className="h-4 w-4 text-yellow-500 shrink-0" />
              Switch to {pendingMode === "manual" ? "Manual" : "Atomic"} mode?
            </AlertDialogTitle>
            <AlertDialogDescription className="font-mono text-xs leading-relaxed">
              {pendingMode === "manual" ? (
                <>
                  <span className="text-foreground/80">Manual mode</span> disables the autonomous
                  step sequencer. The orchestrator will pause after each task and wait for your
                  explicit &quot;Resume&quot; before proceeding.
                  {systemStatus === "executing" && (
                    <span className="block mt-2 text-yellow-500/90">
                      A run is currently active — switching now will take effect after the current
                      task completes.
                    </span>
                  )}
                </>
              ) : (
                <>
                  <span className="text-foreground/80">Atomic mode</span> re-enables autonomous
                  sequencing. The orchestrator will advance through tasks without pausing for
                  confirmation.
                  {systemStatus === "executing" && (
                    <span className="block mt-2 text-yellow-500/90">
                      A run is currently active — switching now will take effect after the current
                      task completes.
                    </span>
                  )}
                </>
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => setPendingMode(null)}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (pendingMode) {
                  setExecutionMode(pendingMode);
                  toast.success(`Execution mode: ${pendingMode}`, {
                    description:
                      pendingMode === "manual"
                        ? "Orchestrator will pause after each task."
                        : "Orchestrator will sequence tasks autonomously.",
                  });
                }
                setPendingMode(null);
              }}
            >
              Switch to {pendingMode === "manual" ? "Manual" : "Atomic"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

export default ContinuityPage;
