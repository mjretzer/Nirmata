import { CheckCircle, ArrowLeft, ArrowRight, AlertCircle, Target, Layers, GitCommit, FileCode, Shield, Copy, Terminal, Clock, ChevronRight, Plus, ListTodo, Sparkles } from "lucide-react";
import { Button } from "../ui/button";
import { Badge } from "../ui/badge";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "../ui/card";
import { useWorkspace, useTasks, useTaskPlans, type Phase } from "../../hooks/useAosData";
import { toast } from "sonner";
import { useNavigate } from "react-router";
import { cn } from "../ui/utils";
import { copyToClipboard } from "../../utils/clipboard";
import { CreateTaskPanel } from "./CreateTaskPanel";
import { useState } from "react";

interface PhaseTaskListProps {
  phase: Phase;
}

export function PhaseTaskList({ phase }: PhaseTaskListProps) {
  const navigate = useNavigate();
  const { workspace } = useWorkspace();
  const { tasks: allTasks } = useTasks();
  const { plans: allTaskPlans } = useTaskPlans();
  const ws = workspace.projectName;
  const [createPanelOpen, setCreatePanelOpen] = useState(false);

  const phaseTasks = allTasks.filter(t => t.phaseId === phase.id);
  const completedCount = phaseTasks.filter(t => t.status === "completed").length;
  const failedCount = phaseTasks.filter(t => t.status === "failed").length;
  const inProgressCount = phaseTasks.filter(t => t.status === "in-progress").length;
  const plannedCount = phaseTasks.filter(t => t.status === "planned").length;
  const isActive = phase.id === workspace.cursor.phase;
  const allFiles = [...new Set(phaseTasks.flatMap(t => t.plan?.fileScope || []))];
  const allVerifications = [...new Set(phaseTasks.flatMap(t => t.plan?.verification || []))];
  const allSteps = phaseTasks.reduce((sum, t) => sum + (t.plan?.steps?.length || 0), 0);

  const copy = async (text: string, label: string) => {
    const ok = await copyToClipboard(text);
    if (ok) toast.success(`Copied ${label}: ${text}`);
    else toast.error(`Failed to copy ${label}`);
  };

  return (
    <div className="flex-1 flex flex-col overflow-hidden bg-background">
      <div className="flex-1 overflow-auto p-6">
        <div className="max-w-5xl mx-auto space-y-6">

          {/* Header */}
          <div className="flex items-start justify-between">
            <div>
              <div className="flex items-center gap-2 mb-1">
                <Terminal className="h-4 w-4 text-muted-foreground" />
                <span className="font-mono text-xs text-muted-foreground">.aos/spec/phases/{phase.id}/</span>
              </div>
              <div className="flex items-center gap-3 mb-1">
                <h1 className="text-2xl font-bold tracking-tight">{phase.title}</h1>
                <Badge variant="outline" className="font-mono text-xs">{phase.id}</Badge>
                {isActive && <Badge className="bg-primary/10 text-primary border-primary/20 text-[10px] h-5">HEAD</Badge>}
                <Badge variant={phase.status === "completed" ? "secondary" : phase.status === "in-progress" ? "default" : "outline"}>{phase.status}</Badge>
              </div>
              <p className="text-muted-foreground text-sm max-w-2xl font-mono">
                {phase.summary}
              </p>
            </div>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" className="font-mono text-xs" onClick={() => navigate(`/ws/${ws}/files/.aos/spec`)}>
                <ArrowLeft className="h-3 w-3 mr-1.5" /> Roadmap
              </Button>
              <Button variant="outline" size="sm" className="font-mono text-xs" onClick={() => copy(`.aos/spec/phases/${phase.id}/`, "path")}>
                <Copy className="h-3 w-3 mr-1.5" /> Copy Path
              </Button>
              <Button
                size="sm"
                className="gap-2 text-xs"
                onClick={() => setCreatePanelOpen(true)}
                aria-label="Create a new task in this phase"
              >
                <Plus className="h-3.5 w-3.5" aria-hidden="true" />
                New Task
              </Button>
            </div>
          </div>

          {/* Metrics */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
            <Card className="bg-card/50"><CardContent className="p-3">
              <div className="flex items-center justify-between mb-1"><span className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium">Tasks</span><CheckCircle className="h-3.5 w-3.5 text-muted-foreground/50" /></div>
              <div className="font-mono text-lg font-bold">{completedCount}<span className="text-muted-foreground text-sm">/{phaseTasks.length}</span></div>
              <div className="h-1 w-full bg-muted mt-1.5 rounded-full overflow-hidden"><div className="h-full bg-green-500 rounded-full" style={{ width: `${phaseTasks.length > 0 ? (completedCount / phaseTasks.length) * 100 : 0}%` }} /></div>
            </CardContent></Card>
            <Card className="bg-card/50"><CardContent className="p-3">
              <div className="flex items-center justify-between mb-1"><span className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium">File Scope</span><FileCode className="h-3.5 w-3.5 text-muted-foreground/50" /></div>
              <div className="font-mono text-lg font-bold">{allFiles.length}</div>
              <div className="text-[10px] text-muted-foreground mt-1 font-mono">across {phaseTasks.length} tasks</div>
            </CardContent></Card>
            <Card className="bg-card/50"><CardContent className="p-3">
              <div className="flex items-center justify-between mb-1"><span className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium">Verifications</span><Shield className="h-3.5 w-3.5 text-muted-foreground/50" /></div>
              <div className="font-mono text-lg font-bold">{allVerifications.length}</div>
              <div className="text-[10px] text-muted-foreground mt-1 font-mono">{allSteps} total steps</div>
            </CardContent></Card>
            <Card className="bg-card/50"><CardContent className="p-3">
              <div className="flex items-center justify-between mb-1"><span className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium">Commits</span><GitCommit className="h-3.5 w-3.5 text-muted-foreground/50" /></div>
              <div className="font-mono text-lg font-bold">{phaseTasks.filter(t => t.commitHash).length}</div>
              <div className="text-[10px] text-muted-foreground mt-1 font-mono">tasks with commits</div>
            </CardContent></Card>
          </div>

          {/* Status breakdown bar */}
          {phaseTasks.length > 0 && (
            <div className="flex items-center gap-4 text-xs text-muted-foreground font-mono">
              <div className="flex-1 flex h-2 rounded-full overflow-hidden bg-muted">
                {completedCount > 0 && <div className="bg-green-500" style={{ width: `${(completedCount / phaseTasks.length) * 100}%` }} />}
                {inProgressCount > 0 && <div className="bg-blue-500" style={{ width: `${(inProgressCount / phaseTasks.length) * 100}%` }} />}
                {failedCount > 0 && <div className="bg-red-500" style={{ width: `${(failedCount / phaseTasks.length) * 100}%` }} />}
                {plannedCount > 0 && <div className="bg-muted-foreground/30" style={{ width: `${(plannedCount / phaseTasks.length) * 100}%` }} />}
              </div>
              <div className="flex items-center gap-3 shrink-0">
                {completedCount > 0 && <span className="flex items-center gap-1"><div className="h-2 w-2 rounded-full bg-green-500" />{completedCount} done</span>}
                {inProgressCount > 0 && <span className="flex items-center gap-1"><div className="h-2 w-2 rounded-full bg-blue-500" />{inProgressCount} active</span>}
                {failedCount > 0 && <span className="flex items-center gap-1"><div className="h-2 w-2 rounded-full bg-red-500" />{failedCount} failed</span>}
                {plannedCount > 0 && <span className="flex items-center gap-1"><div className="h-2 w-2 rounded-full bg-muted-foreground/30" />{plannedCount} planned</span>}
              </div>
            </div>
          )}

          {/* Task Cards */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <div className="text-xs text-muted-foreground uppercase tracking-wider font-medium font-mono">
                Tasks in {phase.id}
              </div>
              <div className="text-xs text-muted-foreground font-mono">
                {phaseTasks.length} {phaseTasks.length === 1 ? "task" : "tasks"}
              </div>
            </div>

            {phaseTasks.map(task => {
              const taskPlan = allTaskPlans.find(p => p.taskId === task.id);
              const isCurrentTask = workspace.cursor.task === task.id;

              return (
                <Card
                  key={task.id}
                  className={cn(
                    "transition-all hover:shadow-md cursor-pointer group",
                    isCurrentTask ? "border-primary/50 shadow-sm" : ""
                  )}
                  onClick={() => navigate(`/ws/${ws}/files/.aos/spec/tasks/${task.id}/task.json`)}
                >
                  <CardHeader className="pb-3">
                    <div className="flex items-start justify-between">
                      <div className="space-y-1.5 min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <div className={cn(
                            "h-2 w-2 rounded-full shrink-0",
                            task.status === "completed" ? "bg-green-500" :
                            task.status === "failed" ? "bg-red-500" :
                            task.status === "in-progress" ? "bg-blue-500 animate-pulse" :
                            "bg-muted-foreground/30"
                          )} />
                          <Badge variant="outline" className="font-mono text-xs">{task.id}</Badge>
                          <CardTitle className="text-base">{task.name}</CardTitle>
                          {isCurrentTask && <Badge className="bg-primary/10 text-primary border-primary/20 text-[10px] h-5">CURSOR</Badge>}
                        </div>
                        <CardDescription className="font-mono text-xs flex items-center gap-2">
                          <span>assigned: {task.assignedTo}</span>
                          {task.commitHash && (
                            <>
                              <span className="text-muted-foreground/30">|</span>
                              <span className="flex items-center gap-1"><GitCommit className="h-3 w-3" />{task.commitHash}</span>
                            </>
                          )}
                        </CardDescription>
                      </div>
                      <Badge
                        variant={task.status === "completed" ? "secondary" : task.status === "in-progress" ? "default" : task.status === "failed" ? "destructive" : "outline"}
                        className="shrink-0 ml-3"
                      >
                        {task.status}
                      </Badge>
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    {/* Plan steps */}
                    {task.plan?.steps && task.plan.steps.length > 0 && (
                      <div>
                        <div className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium mb-2">Plan Steps</div>
                        <div className="space-y-1">
                          {task.plan.steps.map((step, i) => (
                            <div key={i} className="flex items-start gap-2 text-xs py-1 px-2 rounded">
                              <span className="font-mono text-muted-foreground/60 shrink-0 w-5 text-right">{i + 1}.</span>
                              <span className="text-foreground/80">{step}</span>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}

                    {/* File scope */}
                    {task.plan?.fileScope && task.plan.fileScope.length > 0 && (
                      <div className="bg-muted/30 rounded-md p-2.5 border border-border/50">
                        <div className="flex items-center gap-4 text-[10px] text-muted-foreground font-mono">
                          <span className="flex items-center gap-1"><FileCode className="h-3 w-3" /> {task.plan.fileScope.length} files in scope</span>
                          {task.plan?.verification && (
                            <span className="flex items-center gap-1"><Shield className="h-3 w-3" /> {task.plan.verification.length} verifications</span>
                          )}
                        </div>
                        <div className="flex flex-wrap gap-1 mt-1.5">
                          {task.plan.fileScope.map((f, i) => (
                            <code key={i} className="text-[10px] bg-muted px-1.5 py-0.5 rounded text-muted-foreground">{f}</code>
                          ))}
                        </div>
                      </div>
                    )}

                    {/* Footer */}
                    <div className="flex items-center justify-between pt-2 border-t border-border">
                      <div className="flex items-center gap-3 text-xs text-muted-foreground font-mono">
                        <span className="flex items-center gap-1">
                          <Target className="h-3 w-3" />
                          {task.plan?.steps?.length || 0} steps
                        </span>
                        {task.plan?.verification && (
                          <span className="flex items-center gap-1">
                            <Shield className="h-3 w-3" />
                            {task.plan.verification.length} checks
                          </span>
                        )}
                        {task.plan?.fileScope && (
                          <span className="flex items-center gap-1">
                            <FileCode className="h-3 w-3" />
                            {task.plan.fileScope.length} files
                          </span>
                        )}
                      </div>
                      <Button variant="ghost" size="sm" className="h-7 text-xs gap-1 font-mono opacity-0 group-hover:opacity-100 transition-opacity">
                        task.json <ArrowRight className="h-3 w-3" />
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              );
            })}

            {phaseTasks.length === 0 && (
              /* ── Illustrated empty state ── */
              <div
                className="rounded-xl border-2 border-dashed border-border/50 bg-muted/5 p-10 text-center space-y-5"
                role="status"
                aria-label="No tasks in this phase yet"
              >
                {/* Icon */}
                <div className="mx-auto h-14 w-14 rounded-2xl bg-primary/5 border border-primary/10 flex items-center justify-center">
                  <ListTodo className="h-7 w-7 text-primary/40" aria-hidden="true" />
                </div>

                <div className="space-y-1.5">
                  <h3 className="text-sm">No tasks in this phase yet</h3>
                  <p className="text-xs text-muted-foreground max-w-xs mx-auto leading-relaxed">
                    Break this phase into concrete tasks. Each task becomes a unit of work the engine can plan, execute, and verify.
                  </p>
                </div>

                {/* Example tasks */}
                <div className="inline-block text-left space-y-1.5 bg-muted/20 rounded-lg border border-border/40 p-3 mx-auto">
                  <p className="text-[10px] uppercase tracking-wider text-muted-foreground/50 mb-2 flex items-center gap-1.5">
                    <Sparkles className="h-3 w-3" aria-hidden="true" /> Example tasks for this phase
                  </p>
                  {[
                    "Implement core feature logic",
                    "Write unit and integration tests",
                    "Update documentation",
                  ].map((ex, i) => (
                    <div key={i} className="flex items-center gap-2 text-xs text-muted-foreground/60">
                      <span className="h-1.5 w-1.5 rounded-full bg-muted-foreground/25 shrink-0" aria-hidden="true" />
                      {ex}
                    </div>
                  ))}
                </div>

                <Button
                  className="gap-2"
                  onClick={() => setCreatePanelOpen(true)}
                  aria-label="Create the first task in this phase"
                >
                  <Plus className="h-4 w-4" aria-hidden="true" />
                  Create your first task
                </Button>
              </div>
            )}
          </div>

          {/* Phase metadata footer */}
          <div className="border-t border-border pt-4 flex items-center justify-between text-xs text-muted-foreground font-mono">
            <div className="flex items-center gap-4">
              <span className="flex items-center gap-1"><Target className="h-3 w-3" /> {phase.brief.priorities.length} priorities</span>
              <span className="flex items-center gap-1"><Layers className="h-3 w-3" /> {phase.deliverables.length} deliverables</span>
              <span className="flex items-center gap-1"><Clock className="h-3 w-3" /> {phase.metadata.updatedAt}</span>
            </div>
            <span>owner: {phase.metadata.owner}</span>
          </div>

        </div>
      </div>

      {/* Create Task panel */}
      {createPanelOpen && (
        <CreateTaskPanel
          defaultPhaseId={phase.id}
          onClose={() => setCreatePanelOpen(false)}
          onSaveDraft={(_data) => {}}
          onPublish={(_data) => {}}
        />
      )}
    </div>
  );
}