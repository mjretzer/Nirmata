import { 
  CheckCircle, 
  ArrowRight, 
  GitCommit, 
  FileCode, 
  Shield, 
  ChevronDown, 
  Plus, 
  GitBranch, 
  GitMerge, 
  Search,
  Layout,
  Check,
  Loader2,
  AlertCircle,
  Layers,
  Flag,
  Calendar,
  Trash2,
  PanelRightOpen,
  PanelRightClose,
  X,
  Terminal,
  Tag,
  Clock,
  ChevronRight,
  ListTodo,
} from "lucide-react";
import { Button } from "../ui/button";
import { Badge } from "../ui/badge";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "../ui/card";
import { usePhases, useTasks, useWorkspace, useMilestones, type Task } from "../../hooks/useAosData";
import { useNavigate } from "react-router";
import { cn } from "../ui/utils";
import { useState, useRef, useEffect, useCallback } from "react";
import { GitGraphManager } from "./GitGraphManager";
import { TaskHighlightProvider, useTaskHighlight } from "./TaskHighlightContext";
import { CreateTaskPanel, type CreateTaskFormData } from "./CreateTaskPanel";
import { toast } from "sonner";

// --- Mock Git Data Helpers ---

type GitStatus = "merged" | "open" | "review" | "draft";

interface GitMeta {
  branchName: string;
  baseBranch: string;
  commitHash?: string;
  status: GitStatus;
  ahead: number;
  behind: number;
  lastUpdate: string;
  shortHash: string;
}

// Deterministic mock data generator
const getGitMeta = (id: string, type: "milestone" | "phase" | "task", cursorMilestone: string, firstPhaseId: string): GitMeta => {
  const hash = id.split("-")[1] || "0000";
  const num = parseInt(hash, 10);
  
  if (type === "milestone") {
    return {
      branchName: `ms/${id}`,
      baseBranch: "main",
      status: "open",
      ahead: num * 12 + 4,
      behind: 2,
      lastUpdate: "2h ago",
      shortHash: "a1b2c3d"
    };
  }
  
  if (type === "phase") {
    return {
      branchName: `ph/${id}`,
      baseBranch: `ms/${cursorMilestone}`,
      status: num % 2 === 0 ? "merged" : "open",
      ahead: num * 5,
      behind: num,
      lastUpdate: "1d ago",
      shortHash: (num * 12345).toString(16).substring(0, 7)
    };
  }

  // task
  const fullHash = (num * 999999 + 12345).toString(16);
  return {
    branchName: `tsk/${id}`,
    baseBranch: `ph/${firstPhaseId}`,
    commitHash: fullHash,
    shortHash: fullHash.substring(0, 7),
    status: num % 3 === 0 ? "merged" : num % 3 === 1 ? "review" : "open",
    ahead: 1,
    behind: 0,
    lastUpdate: "4h ago"
  };
};

// --- Components ---

const GitBranchPill = ({ name, type = "subtle" }: { name: string, type?: "subtle" | "primary" | "accent" }) => {
  return (
    <div className={cn(
      "flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[10px] font-mono border transition-all cursor-pointer hover:bg-opacity-80",
      type === "primary" 
        ? "bg-indigo-500/10 text-indigo-400 border-indigo-500/20" 
        : type === "accent"
          ? "bg-emerald-500/10 text-emerald-400 border-emerald-500/20"
          : "bg-muted/30 text-muted-foreground/70 border-border/30 hover:border-border/60"
    )}>
      <GitBranch className="h-3 w-3 shrink-0 opacity-70" />
      <span className="truncate max-w-[120px]">{name}</span>
    </div>
  );
};

/** Task row that participates in bidirectional git-graph ↔ roadmap highlighting */
function HighlightableTaskRow({ task, taskGit, isTaskCompleted, isTaskActive, isTaskFailed, onSelect, onRemove }: {
  task: Task;
  taskGit: GitMeta;
  isTaskCompleted: boolean;
  isTaskActive: boolean;
  isTaskFailed: boolean;
  onSelect: () => void;
  onRemove: () => void;
}) {
  const { highlightedTaskId, highlightSource, highlightFromRoadmap } = useTaskHighlight();
  const isHighlighted = highlightedTaskId === task.id && highlightSource === "graph";
  const taskRowRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (isHighlighted && taskRowRef.current) {
      requestAnimationFrame(() => {
        taskRowRef.current?.scrollIntoView({ behavior: "smooth", block: "center" });
      });
    }
  }, [isHighlighted]);

  return (
    <div ref={taskRowRef} className={cn(
      "group/task relative flex items-center gap-2 text-xs py-1.5 pl-4 pr-2 rounded transition-all cursor-pointer",
      "hover:bg-accent/40",
      isTaskCompleted && "opacity-80 hover:opacity-100",
      isHighlighted && "bg-emerald-500/10 ring-1 ring-emerald-500/30 opacity-100"
    )} onClick={() => { onSelect(); highlightFromRoadmap(task.id); }}>
      {/* Task node */}
      <div className={cn(
        "absolute left-[-1px] top-1/2 -translate-y-1/2 h-[7px] w-[7px] rounded-full shrink-0 border border-background transition-colors",
        isHighlighted ? "bg-emerald-400 ring-2 ring-emerald-400/40" :
        isTaskCompleted ? "bg-emerald-400" : isTaskFailed ? "bg-red-400" : isTaskActive ? "bg-primary animate-pulse" : "bg-muted-foreground/25"
      )} />
      {/* Status icon */}
      <span className="shrink-0 w-4 flex items-center justify-center">
        {isTaskCompleted && <Check className="h-3 w-3 text-emerald-400" />}
        {isTaskActive && <Loader2 className="h-3 w-3 text-primary animate-spin" />}
        {isTaskFailed && <AlertCircle className="h-3 w-3 text-red-400" />}
        {task.status === "planned" && <span className="h-2 w-2 rounded-full bg-muted-foreground/15" />}
      </span>
      <span className={cn(
        "font-mono w-20 shrink-0 transition-colors",
        isHighlighted ? "text-emerald-400" :
        isTaskCompleted ? "text-emerald-500/50" : isTaskActive ? "text-primary/60" : "text-muted-foreground/60",
        "group-hover/task:text-muted-foreground"
      )}>{task.id}</span>
      <span className={cn(
        "truncate transition-colors",
        isHighlighted ? "text-emerald-300" :
        isTaskCompleted ? "text-muted-foreground/70 line-through decoration-muted-foreground/30" : isTaskActive ? "text-foreground" : "text-foreground/80",
        "group-hover/task:text-foreground"
      )}>{task.name}</span>
      <span className="ml-auto shrink-0 flex items-center gap-2">
        {task.commitHash && <span className="font-mono text-[10px] text-muted-foreground/50 flex items-center gap-0.5 group-hover/task:text-muted-foreground/70"><GitCommit className="h-2.5 w-2.5" />{task.commitHash}</span>}
        <span className="font-mono text-[10px] text-muted-foreground/50 flex items-center gap-0.5 opacity-0 group-hover/task:opacity-100 transition-opacity">
          <GitBranchPill name={taskGit.branchName} />
        </span>
        <button
          className="opacity-0 group-hover/task:opacity-100 transition-opacity p-0.5 rounded hover:bg-red-500/10 text-muted-foreground/40 hover:text-red-400"
          onClick={(e) => { e.stopPropagation(); onRemove(); }}
          title={`Remove ${task.id}`}
        >
          <Trash2 className="h-3 w-3" />
        </button>
        <ArrowRight className="h-3 w-3 text-muted-foreground/30 group-hover/task:text-muted-foreground/60 transition-colors" />
      </span>
    </div>
  );
}

// --- Main Component ---

export function RoadmapTimeline() {
  const navigate = useNavigate();
  const { phases: allPhases } = usePhases();
  const { tasks: hookTasks } = useTasks();
  const { workspace } = useWorkspace();
  const { milestones: allMilestones } = useMilestones();
  const [selectedTask, setSelectedTask] = useState<string | null>(null);
  
  // State for expansions
  const [expandedMilestones, setExpandedMilestones] = useState<Set<string>>(new Set([workspace.cursor.milestone]));
  const [expandedPhases, setExpandedPhases] = useState<Set<string>>(new Set(allPhases.filter(p => p.status === "in-progress" || p.id === "PH-0001").map(p => p.id)));

  // Task State
  const [tasks, setTasks] = useState<Task[]>(hookTasks);

  // Phase removal state
  const [removedPhaseIds, setRemovedPhaseIds] = useState<Set<string>>(new Set());

  const removePhase = (phaseId: string, phaseName: string) => {
    setRemovedPhaseIds(prev => new Set([...prev, phaseId]));
    toast(`Removed phase ${phaseId}`, {
      action: {
        label: "Undo",
        onClick: () => setRemovedPhaseIds(prev => {
          const next = new Set(prev);
          next.delete(phaseId);
          return next;
        }),
      },
      duration: 5000,
    });
  };

  // Git panel sidebar state
  const [gitPanelOpen, setGitPanelOpen] = useState(false);

  // Create Task panel state
  const [createPanelOpen, setCreatePanelOpen] = useState(false);
  const [createPanelPhaseId, setCreatePanelPhaseId] = useState<string | undefined>();

  // Close panels on Escape
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape" && gitPanelOpen) setGitPanelOpen(false);
      if (e.key === "g" && (e.metaKey || e.ctrlKey)) { e.preventDefault(); setGitPanelOpen(prev => !prev); }
      if (e.key === "n" && (e.metaKey || e.ctrlKey)) { e.preventDefault(); openCreatePanel(); }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [gitPanelOpen]);

  const openCreatePanel = useCallback((phaseId?: string) => {
    setCreatePanelPhaseId(phaseId);
    setCreatePanelOpen(true);
  }, []);

  const toggleMilestone = (id: string) => {
    const next = new Set(expandedMilestones);
    if (next.has(id)) next.delete(id); else next.add(id);
    setExpandedMilestones(next);
  };

  const togglePhase = (id: string) => {
    const next = new Set(expandedPhases);
    if (next.has(id)) next.delete(id); else next.add(id);
    setExpandedPhases(next);
  };

  const handleCreateSaveDraft = useCallback((data: CreateTaskFormData) => {
    const nextNum = String(tasks.length + 1).padStart(6, "0");
    const newTask: Task = {
      id: `TSK-${nextNum}`,
      phaseId: data.phaseId,
      milestone: allPhases.find(p => p.id === data.phaseId)?.milestoneId ?? "MS-0001",
      name: data.name || `Draft task ${tasks.length + 1}`,
      status: "planned",
      assignedTo: data.assignee || "Unassigned",
    };
    setTasks(prev => [...prev, newTask]);
  }, [tasks]);

  const handleCreatePublish = useCallback((data: CreateTaskFormData) => {
    const nextNum = String(tasks.length + 1).padStart(6, "0");
    const newTask: Task = {
      id: `TSK-${nextNum}`,
      phaseId: data.phaseId,
      milestone: allPhases.find(p => p.id === data.phaseId)?.milestoneId ?? "MS-0001",
      name: data.name,
      status: "planned",
      assignedTo: data.assignee || "Unassigned",
      plan: {
        fileScope: data.fileScope,
        steps: data.steps.filter(s => s.trim()),
        verification: data.verifications.filter(v => v.trim()),
      },
    };
    setTasks(prev => [...prev, newTask]);
    // Auto-expand the target phase
    if (data.phaseId) {
      setExpandedPhases(prev => new Set([...prev, data.phaseId]));
      setExpandedMilestones(prev => {
        const ms = allPhases.find(p => p.id === data.phaseId)?.milestoneId;
        return ms ? new Set([...prev, ms]) : prev;
      });
    }
  }, [tasks]);

  const removeTask = (taskId: string) => {
    const task = tasks.find(t => t.id === taskId);
    setTasks(prev => prev.filter(t => t.id !== taskId));
    if (task) {
      toast(`Removed task ${task.id}`, {
        action: {
          label: "Undo",
          onClick: () => setTasks(prev => {
            const idx = hookTasks.findIndex(t => t.id === taskId);
            const next = [...prev];
            next.splice(idx >= 0 ? idx : prev.length, 0, task);
            return next;
          }),
        },
        duration: 5000,
      });
    }
  };

  const activeMilestone = allMilestones.find(m => m.id === workspace.cursor.milestone) || allMilestones[0];

  return (
    <TaskHighlightProvider>
    <div className="flex-1 flex flex-col h-full overflow-hidden bg-[#0A0A0A] relative text-foreground selection:bg-emerald-500/20">

      {/* Full-width content area */}
      <div className="flex-1 overflow-y-auto px-[0px] py-[24px]">
        <div className="max-w-5xl mx-auto space-y-5">

            {/* Header */}
            <div className="flex items-start justify-between">
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <Layout className="h-4 w-4 text-muted-foreground" />
                  <span className="font-mono text-xs text-muted-foreground">.aos/spec/plan</span>
                </div>
                <h1 className="text-2xl font-bold tracking-tight mb-1">Plan</h1>
                <p className="text-muted-foreground text-sm max-w-2xl font-mono">
                  {allMilestones.length} milestones — {allPhases.length} phases — {tasks.length} tasks
                </p>
              </div>
              <div className="flex items-center gap-2">
                {/* Create Task CTA */}
                <Button
                  size="sm"
                  className="h-8 gap-2 text-xs"
                  onClick={() => navigate(`/ws/${workspace.projectName}/orchestrator`)}
                  aria-label="Create — go to Orchestrator (Ctrl+N)"
                >
                  <Plus className="h-3.5 w-3.5" aria-hidden="true" />
                  Create
                  
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  className={cn(
                    "h-8 gap-2 text-xs border-border/30 text-muted-foreground/70 hover:text-emerald-400 hover:border-emerald-500/30 hover:bg-emerald-500/5 transition-all",
                    gitPanelOpen && "bg-emerald-500/10 text-emerald-400 border-emerald-500/30"
                  )}
                  onClick={() => setGitPanelOpen(prev => !prev)}
                >
                  <Terminal className="h-3.5 w-3.5" />
                  <GitBranch className="h-3 w-3 text-emerald-500" />
                  <span className="font-mono">git graph</span>
                  <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
                  <kbd className="hidden sm:inline-flex h-5 items-center gap-0.5 rounded border border-border/40 bg-muted/30 px-1.5 font-mono text-[9px] text-muted-foreground/50">⌘G</kbd>
                </Button>
              </div>
            </div>

            {/* Milestone List */}
            <div className="space-y-3">
              {allMilestones.map((milestone, milestoneIndex) => {
                const isMilestoneActive = milestone.id === workspace.cursor.milestone;
                const isMilestoneCompleted = milestone.status === "completed";
                const isMilestonePlanned = milestone.status === "planned";
                const milestonePhases = allPhases.filter(p => milestone.phases.includes(p.id));
                const milestoneTasks = tasks.filter(t => t.milestone === milestone.id);
                const msCompletedTasks = milestoneTasks.filter(t => t.status === "completed").length;
                const msProgress = milestoneTasks.length > 0 ? Math.round((msCompletedTasks / milestoneTasks.length) * 100) : 0;
                const msGit = getGitMeta(milestone.id, "milestone", workspace.cursor.milestone, allPhases[0].id);

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
                      onClick={() => toggleMilestone(milestone.id)}
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
                      {milestonePhases.filter(p => !removedPhaseIds.has(p.id)).map((phase, phaseIndex) => {
                        const isActive = phase.id === workspace.cursor.phase;
                        const isCompleted = phase.status === "completed";
                        const isPlanned = phase.status === "planned";
                        const phaseTasks = tasks.filter(t => t.phaseId === phase.id);
                        const completedCount = phaseTasks.filter(t => t.status === "completed").length;
                        const failedCount = phaseTasks.filter(t => t.status === "failed").length;
                        const phaseProgress = phaseTasks.length > 0 ? Math.round((completedCount / phaseTasks.length) * 100) : 0;
                        const phaseFiles = phase.links.artifacts.map(a => a.path);
                        const taskGitMetas = phaseTasks.map(t => getGitMeta(t.id, "task", workspace.cursor.milestone, allPhases[0].id));

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
                              )} onClick={() => togglePhase(phase.id)}>
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
                                      onClick={(e) => { e.stopPropagation(); removePhase(phase.id, phase.title); }}
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
                                        const taskGit = getGitMeta(task.id, "task", workspace.cursor.milestone, allPhases[0].id);
                                        const isTaskCompleted = task.status === "completed";
                                        const isTaskActive = task.status === "in-progress";
                                        const isTaskFailed = task.status === "failed";
                                        return (
                                        <HighlightableTaskRow
                                          key={task.id}
                                          task={task}
                                          taskGit={taskGit}
                                          isTaskCompleted={isTaskCompleted}
                                          isTaskActive={isTaskActive}
                                          isTaskFailed={isTaskFailed}
                                          onSelect={() => setSelectedTask(task.id)}
                                          onRemove={() => removeTask(task.id)}
                                        />
                                        );
                                      })}
                                      <div className="flex items-center gap-2 text-xs py-1.5 pl-4 pr-2 rounded hover:bg-accent/40 transition-colors cursor-pointer group/add"
                                        onClick={() => openCreatePanel(phase.id)}
                                        role="button"
                                        aria-label={`Add a new task to phase ${phase.id}`}
                                      >
                                        <Plus className="h-3.5 w-3.5 text-muted-foreground/50 group-hover/add:text-muted-foreground" />
                                        <span className="font-mono text-muted-foreground/50 group-hover/add:text-muted-foreground transition-colors">Add Task</span>
                                      </div>
                                    </div>
                                  </div>

                                  {phaseFiles.length > 0 && (
                                    <div className="bg-muted/20 rounded-md p-2.5 border border-border/30">
                                      <div className="flex items-center gap-4 text-[10px] text-muted-foreground/70 font-mono">
                                        <span className="flex items-center gap-1"><FileCode className="h-3 w-3" /> {phaseFiles.length} files in scope</span>
                                        <span className="flex items-center gap-1"><Shield className="h-3 w-3" /> {phase.acceptance.criteria.length} verifications</span>
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
      </div>

      {/* Git Graph side panel */}
      {gitPanelOpen && (
        <div className="absolute inset-y-0 right-0 w-max border-l border-border bg-background shadow-xl z-30 flex flex-col">
          <div className="flex items-center justify-between px-4 py-3 border-b border-border">
            <span className="text-sm flex items-center gap-2">
              <GitBranch className="h-4 w-4 text-emerald-500" />
              Git Graph
            </span>
            <button
              onClick={() => setGitPanelOpen(false)}
              className="p-1 rounded text-muted-foreground hover:text-foreground transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
              aria-label="Close git graph panel"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
          <div className="flex-1 overflow-auto">
            <GitGraphManager />
          </div>
        </div>
      )}

      {/* Create Task panel */}
      {createPanelOpen && (
        <CreateTaskPanel
          defaultPhaseId={createPanelPhaseId}
          onClose={() => setCreatePanelOpen(false)}
          onSaveDraft={handleCreateSaveDraft}
          onPublish={handleCreatePublish}
        />
      )}

    </div>
    </TaskHighlightProvider>
  );
}