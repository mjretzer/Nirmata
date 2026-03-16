import { useState, useMemo, useCallback, useRef, useEffect } from "react";
import {
  Shield,
  ChevronDown,
  ChevronRight,
  CheckCircle,
  XCircle,
  HelpCircle,
  AlertTriangle,
  Layers,
  FileCode,
  GitCommit,
  Terminal,
  ArrowRight,
  Bug,
  Plus,
  X,
  Trash2,
  Play,
  Square,
  Loader2,
} from "lucide-react";
import { Badge } from "../ui/badge";
import { Card, CardContent, CardHeader } from "../ui/card";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { cn } from "../ui/utils";
import { useNavigate } from "react-router";
import { usePhases, useTasks, useWorkspace, useVerificationState } from "../../hooks/useAosData";
import {
  type UATItem,
  type UATStatus,
} from "./verificationState";
import { toast } from "sonner";
import { JsonPreviewCard } from "../JsonPreviewCard";

// ── Status helpers ──────────────────────────────────────────────

const statusConfig: Record<
  UATStatus,
  { icon: typeof CheckCircle; color: string; bg: string; label: string }
> = {
  pass: {
    icon: CheckCircle,
    color: "text-green-500",
    bg: "bg-green-500",
    label: "Pass",
  },
  fail: {
    icon: XCircle,
    color: "text-red-500",
    bg: "bg-red-500",
    label: "Fail",
  },
  partial: {
    icon: AlertTriangle,
    color: "text-yellow-500",
    bg: "bg-yellow-500",
    label: "Partial",
  },
  unverified: {
    icon: HelpCircle,
    color: "text-muted-foreground",
    bg: "bg-muted-foreground/40",
    label: "Unverified",
  },
};

// ── Phase UAT Viewer ────────────────────────────────────────────

export interface CreateIssueFromUAT {
  uatId: string;
  taskId: string;
  taskName: string;
  status: UATStatus;
  fileScope: string[];
  checksFailed: number;
  checksTotal: number;
  acceptanceCriteria: string[];
}

interface PhaseUATViewerProps {
  onCreateIssue?: (seed: CreateIssueFromUAT) => void;
}

export function PhaseUATViewer({ onCreateIssue }: PhaseUATViewerProps) {
  const navigate = useNavigate();
  const { phases: allPhases } = usePhases();
  const { tasks: allTasks } = useTasks();
  const { workspace } = useWorkspace();
  const { verification: verificationCtx } = useVerificationState();
  const [selectedPhaseId, setSelectedPhaseId] = useState<string | null>(null);
  const [expandedUATs, setExpandedUATs] = useState<Set<string>>(new Set());
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [localUATs, setLocalUATs] = useState<UATItem[]>([]);
  const [nextUATSeq, setNextUATSeq] = useState(100); // start high to avoid collisions

  // UAT Run State
  const [uatRunning, setUatRunning] = useState(false);
  const [uatProgress, setUatProgress] = useState(0);
  const [uatCurrentCheck, setUatCurrentCheck] = useState("");
  const [uatElapsed, setUatElapsed] = useState(0);
  const uatTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const uatProgressRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Cleanup timers on unmount
  useEffect(() => {
    return () => {
      if (uatTimerRef.current) clearInterval(uatTimerRef.current);
      if (uatProgressRef.current) clearInterval(uatProgressRef.current);
    };
  }, []);

  // Group UAT items by phase (merge derived + local)
  const uatByPhase = useMemo(() => {
    const allItems = [...verificationCtx.uatItems, ...localUATs];
    const map = new Map<string, UATItem[]>();
    for (const item of allItems) {
      const arr = map.get(item.phaseId) || [];
      arr.push(item);
      map.set(item.phaseId, arr);
    }
    return map;
  }, [verificationCtx, localUATs]);

  const selectedPhase = allPhases.find((p) => p.id === selectedPhaseId);
  const selectedUATs = selectedPhaseId
    ? uatByPhase.get(selectedPhaseId) || []
    : [];

  const startUATRun = useCallback(() => {
    if (!selectedPhaseId) return;
    const uats = uatByPhase.get(selectedPhaseId) || [];
    if (uats.length === 0) {
      toast.error("No UAT checks to run");
      return;
    }

    setUatRunning(true);
    setUatProgress(0);
    setUatElapsed(0);
    setUatCurrentCheck(uats[0]?.taskName || "Initializing...");

    // Elapsed time counter
    uatTimerRef.current = setInterval(() => {
      setUatElapsed((prev) => prev + 1);
    }, 1000);

    // Simulated progress
    let step = 0;
    const totalSteps = uats.length;
    uatProgressRef.current = setInterval(() => {
      step++;
      const pct = Math.min(Math.round((step / totalSteps) * 100), 100);
      setUatProgress(pct);
      if (step < totalSteps) {
        setUatCurrentCheck(uats[step]?.taskName || "Verifying...");
      }
      if (step >= totalSteps) {
        // Complete
        if (uatProgressRef.current) clearInterval(uatProgressRef.current);
        if (uatTimerRef.current) clearInterval(uatTimerRef.current);
        setUatCurrentCheck("Complete");
        toast.success(`UAT suite finished — ${totalSteps} checks evaluated`);
        setTimeout(() => {
          setUatRunning(false);
          setUatProgress(0);
          setUatCurrentCheck("");
          setUatElapsed(0);
        }, 1500);
      }
    }, 1200);

    toast.info(`Running ${totalSteps} UAT checks for ${selectedPhaseId}...`);
  }, [selectedPhaseId, uatByPhase]);

  const stopUATRun = useCallback(() => {
    if (uatTimerRef.current) clearInterval(uatTimerRef.current);
    if (uatProgressRef.current) clearInterval(uatProgressRef.current);
    setUatRunning(false);
    setUatProgress(0);
    setUatCurrentCheck("");
    setUatElapsed(0);
    toast.warning("UAT run stopped");
  }, []);

  const handleCreateUAT = useCallback((data: {
    name: string;
    linkedTaskId: string;
    acceptanceCriteria: string[];
    fileScope: string[];
  }) => {
    if (!selectedPhaseId) return;
    const seq = nextUATSeq;
    const uatId = `UAT-${String(seq).padStart(6, "0")}`;
    const newUAT: UATItem = {
      id: uatId,
      taskId: data.linkedTaskId || `TSK-${String(seq).padStart(6, "0")}`,
      taskName: data.name,
      phaseId: selectedPhaseId,
      phaseTitle: selectedPhase?.title ?? selectedPhaseId,
      status: "unverified",
      checks: [],
      checksTotal: 0,
      checksPassed: 0,
      checksFailed: 0,
      checksPending: 0,
      acceptanceCriteria: data.acceptanceCriteria,
      fileScope: data.fileScope,
      linkedIssueIds: [],
      linkedIssues: [],
      linkedRunIds: [],
      linkedRuns: [],
      lastRun: null,
      taskStatus: "planned",
      planSteps: [],
    };
    setLocalUATs((prev) => [...prev, newUAT]);
    setNextUATSeq((s) => s + 1);
    setShowCreateForm(false);
    toast.success(`Created ${uatId} — "${data.name}"`);
  }, [selectedPhaseId, selectedPhase, nextUATSeq]);

  const handleDeleteLocalUAT = useCallback((uatId: string) => {
    setLocalUATs((prev) => prev.filter((u) => u.id !== uatId));
    toast.info(`Removed ${uatId}`);
  }, []);

  const isLocalUAT = useCallback((uatId: string) => {
    return localUATs.some((u) => u.id === uatId);
  }, [localUATs]);

  const toggleUAT = (uatId: string) => {
    setExpandedUATs((prev) => {
      const next = new Set(prev);
      if (next.has(uatId)) next.delete(uatId);
      else next.add(uatId);
      return next;
    });
  };

  // Phase-level UAT summary
  function phaseUATSummary(phaseId: string) {
    const items = uatByPhase.get(phaseId) || [];
    const pass = items.filter((i) => i.status === "pass").length;
    const fail = items.filter((i) => i.status === "fail").length;
    const partial = items.filter((i) => i.status === "partial").length;
    const unverified = items.filter((i) => i.status === "unverified").length;
    return { total: items.length, pass, fail, partial, unverified };
  }

  return (
    <div className="flex-1 flex overflow-hidden">
      {/* ── Phase Sidebar ──────────────────────── */}
      <div className="w-[300px] shrink-0 border-r border-border flex flex-col overflow-hidden bg-card/30">
        <div className="px-4 py-3 border-b border-border">
          <div className="flex items-center gap-2">
            <Layers className="h-3.5 w-3.5 text-muted-foreground" />
            <span className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium font-mono">
              Phases
            </span>
          </div>
        </div>
        <div className="flex-1 overflow-auto">
          {allPhases.map((phase) => {
            const isActive = phase.id === workspace.cursor.phase;
            const isSelected = phase.id === selectedPhaseId;
            const summary = phaseUATSummary(phase.id);

            return (
              <button
                key={phase.id}
                onClick={() => setSelectedPhaseId(phase.id)}
                className={cn(
                  "w-full text-left px-4 py-3 border-b border-border/50 transition-colors cursor-pointer",
                  isSelected
                    ? "bg-accent/60"
                    : "hover:bg-accent/30"
                )}
              >
                <div className="flex items-center gap-2 mb-1">
                  <div
                    className={cn(
                      "h-2 w-2 rounded-full shrink-0",
                      phase.status === "completed"
                        ? "bg-green-500"
                        : phase.status === "in-progress"
                        ? "bg-blue-500 animate-pulse"
                        : "bg-muted-foreground/30"
                    )}
                  />
                  <Badge
                    variant="outline"
                    className="font-mono text-[10px] h-5"
                  >
                    {phase.id}
                  </Badge>
                  {isActive && (
                    <Badge className="bg-primary/10 text-primary border-primary/20 text-[10px] h-4 px-1">
                      HEAD
                    </Badge>
                  )}
                </div>
                <div className="text-sm truncate mb-1.5 pl-4">
                  {phase.title}
                </div>

                {/* Mini UAT bar */}
                {summary.total > 0 ? (
                  <div className="pl-4 space-y-1">
                    <div className="flex h-1.5 rounded-full overflow-hidden bg-muted">
                      {summary.pass > 0 && (
                        <div
                          className="bg-green-500"
                          style={{
                            width: `${(summary.pass / summary.total) * 100}%`,
                          }}
                        />
                      )}
                      {summary.partial > 0 && (
                        <div
                          className="bg-yellow-500"
                          style={{
                            width: `${
                              (summary.partial / summary.total) * 100
                            }%`,
                          }}
                        />
                      )}
                      {summary.fail > 0 && (
                        <div
                          className="bg-red-500"
                          style={{
                            width: `${(summary.fail / summary.total) * 100}%`,
                          }}
                        />
                      )}
                    </div>
                    <div className="flex items-center gap-2 text-[10px] text-muted-foreground font-mono">
                      {summary.pass > 0 && (
                        <span className="text-green-500">{summary.pass}P</span>
                      )}
                      {summary.fail > 0 && (
                        <span className="text-red-500">{summary.fail}F</span>
                      )}
                      {summary.partial > 0 && (
                        <span className="text-yellow-500">
                          {summary.partial}~
                        </span>
                      )}
                      {summary.unverified > 0 && (
                        <span>{summary.unverified}?</span>
                      )}
                      <span className="ml-auto">{summary.total} UATs</span>
                    </div>
                  </div>
                ) : (
                  <div className="pl-4 text-[10px] text-muted-foreground/60 font-mono">
                    No UATs
                  </div>
                )}
              </button>
            );
          })}
        </div>
      </div>

      {/* ── UAT Detail Panel ──────────────────── */}
      <div className="flex-1 flex flex-col overflow-hidden">
        {!selectedPhase ? (
          <div className="flex-1 flex flex-col items-center justify-center text-muted-foreground p-8">
            <Shield className="h-12 w-12 mb-4 opacity-20" />
            <p className="text-sm font-mono">Select a phase to view its UAT suite</p>
            <p className="text-xs text-muted-foreground/60 mt-1 font-mono">
              {allPhases.length} phases · {verificationCtx.totalUAT} total UATs
            </p>
          </div>
        ) : (
          <div className="flex-1 overflow-auto p-6">
            <div className="max-w-4xl mx-auto space-y-5">
              {/* Phase Header */}
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <Terminal className="h-3.5 w-3.5 text-muted-foreground" />
                  <span className="font-mono text-xs text-muted-foreground">
                    .aos/spec/phases/{selectedPhase.id}/uat/
                  </span>
                </div>
                <div className="flex items-center gap-3 mb-1">
                  <h2 className="text-xl tracking-tight">
                    {selectedPhase.title}
                  </h2>
                  <Badge variant="outline" className="font-mono text-xs">
                    {selectedPhase.id}
                  </Badge>
                  <Badge
                    variant={
                      selectedPhase.status === "completed"
                        ? "secondary"
                        : selectedPhase.status === "in-progress"
                        ? "default"
                        : "outline"
                    }
                  >
                    {selectedPhase.status}
                  </Badge>
                </div>
                <p className="text-muted-foreground text-xs font-mono">
                  {selectedPhase.summary}
                </p>
              </div>

              {/* ── UAT Run Controls ── */}
              <Card className={cn(
                "transition-all",
                uatRunning ? "border-blue-500/40 bg-blue-500/5" : "bg-card/50"
              )}>
                <CardContent className="p-4">
                  {uatRunning ? (
                    <div className="space-y-3">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2">
                          <Loader2 className="h-4 w-4 text-blue-500 animate-spin" />
                          <span className="text-sm font-mono font-medium text-blue-400">
                            Running UAT Suite
                          </span>
                          <Badge variant="outline" className="font-mono text-[10px] h-5 text-blue-400 border-blue-500/30">
                            {uatProgress}%
                          </Badge>
                        </div>
                        <div className="flex items-center gap-3">
                          <span className="text-[10px] text-muted-foreground font-mono tabular-nums">
                            {uatElapsed}s elapsed
                          </span>
                          <Button
                            variant="outline"
                            size="sm"
                            className="h-7 gap-1.5 text-[11px] font-mono text-red-400 border-red-500/30 hover:bg-red-500/10 hover:border-red-500/50"
                            onClick={stopUATRun}
                          >
                            <Square className="h-3 w-3" /> Stop
                          </Button>
                        </div>
                      </div>
                      <div className="space-y-1.5">
                        <div className="flex h-2 rounded-full overflow-hidden bg-muted">
                          <div
                            className="bg-blue-500 transition-all duration-300 ease-out"
                            style={{ width: `${uatProgress}%` }}
                          />
                        </div>
                        <div className="flex items-center justify-between text-[10px] font-mono text-muted-foreground">
                          <span className="truncate max-w-[300px]">
                            {uatCurrentCheck === "Complete" ? (
                              <span className="text-green-500">All checks complete</span>
                            ) : (
                              <>Verifying: {uatCurrentCheck}</>
                            )}
                          </span>
                          <span>{selectedUATs.length} checks total</span>
                        </div>
                      </div>
                    </div>
                  ) : (
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-3">
                        <Shield className="h-4 w-4 text-muted-foreground" />
                        <div>
                          <div className="text-sm font-medium">UAT Verification</div>
                          <div className="text-[10px] text-muted-foreground font-mono">
                            Run all {selectedUATs.length} checks for {selectedPhase.id}
                          </div>
                        </div>
                      </div>
                      <Button
                        size="sm"
                        className="h-8 gap-1.5 text-[11px] font-mono"
                        onClick={startUATRun}
                        disabled={selectedUATs.length === 0}
                      >
                        <Play className="h-3.5 w-3.5" /> Run UATs
                      </Button>
                    </div>
                  )}
                </CardContent>
              </Card>

              {/* UAT Metrics */}
              {(() => {
                const s = phaseUATSummary(selectedPhase.id);
                return (
                  <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
                    <Card className="bg-card/50">
                      <CardContent className="p-3">
                        <div className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium mb-1">
                          Total
                        </div>
                        <div className="font-mono text-lg font-bold">
                          {s.total}
                        </div>
                      </CardContent>
                    </Card>
                    <Card className="bg-card/50">
                      <CardContent className="p-3">
                        <div className="text-[10px] text-green-500 uppercase tracking-wider font-medium mb-1">
                          Pass
                        </div>
                        <div className="font-mono text-lg font-bold text-green-500">
                          {s.pass}
                        </div>
                      </CardContent>
                    </Card>
                    <Card className="bg-card/50">
                      <CardContent className="p-3">
                        <div className="text-[10px] text-red-500 uppercase tracking-wider font-medium mb-1">
                          Fail
                        </div>
                        <div className="font-mono text-lg font-bold text-red-500">
                          {s.fail}
                        </div>
                      </CardContent>
                    </Card>
                    <Card className="bg-card/50">
                      <CardContent className="p-3">
                        <div className="text-[10px] text-yellow-500 uppercase tracking-wider font-medium mb-1">
                          Partial
                        </div>
                        <div className="font-mono text-lg font-bold text-yellow-500">
                          {s.partial}
                        </div>
                      </CardContent>
                    </Card>
                    <Card className="bg-card/50">
                      <CardContent className="p-3">
                        <div className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium mb-1">
                          Unverified
                        </div>
                        <div className="font-mono text-lg font-bold">
                          {s.unverified}
                        </div>
                      </CardContent>
                    </Card>
                  </div>
                );
              })()}

              {/* Status bar */}
              {selectedUATs.length > 0 && (() => {
                const s = phaseUATSummary(selectedPhase.id);
                return (
                  <div className="flex items-center gap-4 text-xs text-muted-foreground font-mono">
                    <div className="flex-1 flex h-2 rounded-full overflow-hidden bg-muted">
                      {s.pass > 0 && (
                        <div
                          className="bg-green-500"
                          style={{ width: `${(s.pass / s.total) * 100}%` }}
                        />
                      )}
                      {s.partial > 0 && (
                        <div
                          className="bg-yellow-500"
                          style={{ width: `${(s.partial / s.total) * 100}%` }}
                        />
                      )}
                      {s.fail > 0 && (
                        <div
                          className="bg-red-500"
                          style={{ width: `${(s.fail / s.total) * 100}%` }}
                        />
                      )}
                    </div>
                    <div className="flex items-center gap-3 shrink-0">
                      {s.pass > 0 && (
                        <span className="flex items-center gap-1">
                          <div className="h-2 w-2 rounded-full bg-green-500" />
                          {s.pass} pass
                        </span>
                      )}
                      {s.partial > 0 && (
                        <span className="flex items-center gap-1">
                          <div className="h-2 w-2 rounded-full bg-yellow-500" />
                          {s.partial} partial
                        </span>
                      )}
                      {s.fail > 0 && (
                        <span className="flex items-center gap-1">
                          <div className="h-2 w-2 rounded-full bg-red-500" />
                          {s.fail} fail
                        </span>
                      )}
                      {s.unverified > 0 && (
                        <span className="flex items-center gap-1">
                          <div className="h-2 w-2 rounded-full bg-muted-foreground/40" />
                          {s.unverified} unverified
                        </span>
                      )}
                    </div>
                  </div>
                );
              })()}

              {/* UAT Items */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <div className="text-xs text-muted-foreground uppercase tracking-wider font-medium font-mono">
                    UAT Suite — {selectedPhase.id}
                  </div>
                  <div className="flex items-center gap-3">
                    <div className="text-xs text-muted-foreground font-mono">
                      {selectedUATs.length}{" "}
                      {selectedUATs.length === 1 ? "check" : "checks"}
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      className="h-7 gap-1.5 text-[11px] font-mono text-violet-400 border-violet-500/30 hover:bg-violet-500/10 hover:border-violet-500/50"
                      onClick={() => setShowCreateForm(true)}
                      disabled={showCreateForm}
                    >
                      <Plus className="h-3 w-3" /> New UAT
                    </Button>
                  </div>
                </div>

                {/* ── Inline Create UAT Form ── */}
                {showCreateForm && (
                  <CreateUATForm
                    phaseId={selectedPhaseId!}
                    existingTasks={allTasks.filter((t) => t.phaseId === selectedPhaseId)}
                    onSubmit={handleCreateUAT}
                    onCancel={() => setShowCreateForm(false)}
                  />
                )}

                {selectedUATs.length === 0 && (
                  <Card className="bg-card/50">
                    <CardContent className="p-8 text-center text-muted-foreground">
                      <p className="text-sm font-mono">
                        No UAT checks defined for this phase yet.
                      </p>
                    </CardContent>
                  </Card>
                )}

                {selectedUATs.map((uat) => {
                  const isExpanded = expandedUATs.has(uat.id);
                  const cfg = statusConfig[uat.status];
                  const StatusIcon = cfg.icon;
                  const task = allTasks.find((t) => t.id === uat.taskId);

                  return (
                    <div
                      key={uat.id}
                      className={cn(
                        "border rounded-lg transition-all",
                        uat.status === "pass"
                          ? "border-green-500/20 bg-green-500/[0.02]"
                          : uat.status === "fail"
                            ? "border-red-500/20 bg-red-500/[0.01]"
                            : "border-border/60 bg-card/30",
                        isExpanded && "shadow-sm"
                      )}
                    >
                      {/* Row Header */}
                      <div
                        className="flex items-center gap-3 px-4 py-3 cursor-pointer hover:bg-accent/30 transition-colors rounded-lg"
                        onClick={() => toggleUAT(uat.id)}
                      >
                        {/* Status dot */}
                        <div className="relative shrink-0">
                          <div
                            className={cn(
                              "h-2.5 w-2.5 rounded-full",
                              uat.status === "pass" ? "bg-green-500" :
                              uat.status === "fail" ? "bg-red-500" :
                              uat.status === "partial" ? "bg-yellow-500" :
                              "bg-muted-foreground"
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

                        {/* UAT ID */}
                        <span className="font-mono text-xs text-foreground/80 shrink-0">
                          {uat.id}
                        </span>

                        {/* Status badge */}
                        <Badge
                          variant="outline"
                          className={cn(
                            "text-[9px] h-4.5 px-1.5 gap-0.5 shrink-0",
                            uat.status === "pass" &&
                              "text-green-500 bg-green-500/10 border-green-500/20",
                            uat.status === "fail" &&
                              "text-red-500 bg-red-500/10 border-red-500/20",
                            uat.status === "partial" &&
                              "text-yellow-500 bg-yellow-500/10 border-yellow-500/20",
                            uat.status === "unverified" &&
                              "text-muted-foreground bg-muted border-border"
                          )}
                        >
                          <StatusIcon className="h-2.5 w-2.5" />
                          {cfg.label}
                        </Badge>

                        {/* Task Name */}
                        

                        {/* Spacer */}
                        <div className="flex-1" />

                        {/* Checks count */}
                        <div className="flex items-center gap-1 text-[10px] font-mono text-muted-foreground/50 shrink-0">
                          <Shield className="h-3 w-3" />
                          {uat.checksPassed}/{uat.checksTotal}
                        </div>

                        {/* Delete Action (Local only) */}
                        {isLocalUAT(uat.id) && (
                          <button
                            className="p-1 rounded hover:bg-red-500/10 text-muted-foreground hover:text-red-400 transition-colors ml-2"
                            title="Remove UAT"
                            onClick={(e) => { e.stopPropagation(); handleDeleteLocalUAT(uat.id); }}
                          >
                            <Trash2 className="h-3 w-3" />
                          </button>
                        )}
                      </div>

                      {/* Expanded Detail */}
                      {isExpanded && (
                        <div className="px-4 pb-4 pt-3 border-t border-border/30 space-y-3">
                          {/* UAT File Preview */}
                          <div>
                            
                            {(() => {
                              const uatJson = {
                                id: uat.id,
                                taskId: uat.taskId,
                                status: uat.status,
                                checks: { total: uat.checksTotal, passed: uat.checksPassed, failed: uat.checksFailed, pending: uat.checksPending },
                                fileScope: uat.fileScope,
                                acceptanceCriteria: uat.acceptanceCriteria,
                              };
                              return (
                                <JsonPreviewCard
                                  label={uat.id}
                                  filename={`${uat.id}.json`}
                                  icon={Shield}
                                  data={uatJson}
                                  maxHeight="max-h-48"
                                  onClick={() => navigate(`/ws/${workspace.projectName}/files/.aos/spec/uat/${uat.id}.json`)}
                                />
                              );
                            })()}
                          </div>

                          {/* Contributing Task */}
                          {task && (
                            <div>
                              <div className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium mb-2 font-mono">
                                Contributing Task
                              </div>
                              <JsonPreviewCard
                                label={task.name}
                                filename={`${task.id}.json`}
                                icon={FileCode}
                                data={{
                                  id: task.id,
                                  name: task.name,
                                  status: task.status,
                                  assignedTo: task.assignedTo,
                                  ...(task.commitHash && { commitHash: task.commitHash }),
                                  ...(task.plan?.fileScope && { fileScope: task.plan.fileScope }),
                                }}
                                onClick={() => navigate(`/ws/${workspace.projectName}/files/.aos/spec/tasks/${task.id}/task.json`)}
                              />
                            </div>
                          )}

                          {/* File Scope */}
                          {task?.plan?.fileScope && task.plan.fileScope.length > 0 && (
                            <div>
                              <div className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium mb-2 font-mono">
                                Files Under Verification
                              </div>
                              <div className="space-y-1">
                                {task.plan.fileScope.map((f, i) => (
                                  <div
                                    key={i}
                                    className="flex items-center gap-2 text-xs py-1.5 px-2 rounded bg-muted/30"
                                  >
                                    <FileCode className="h-3 w-3 text-muted-foreground shrink-0" />
                                    <code className="text-[11px] font-mono text-foreground/80">
                                      {f}
                                    </code>
                                  </div>
                                ))}
                              </div>
                            </div>
                          )}

                          {/* Create Issue action for failing / partial UATs */}
                          {(uat.status === "fail" || uat.status === "partial") && onCreateIssue && (
                            <div className="pt-2 border-t border-border/50">
                              <Button
                                variant="outline"
                                size="sm"
                                className="w-full gap-2 text-xs font-mono text-red-400 border-red-500/30 hover:bg-red-500/10 hover:border-red-500/50 hover:text-red-300 transition-colors"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  onCreateIssue({
                                    uatId: uat.id,
                                    taskId: uat.taskId,
                                    taskName: uat.taskName,
                                    status: uat.status,
                                    fileScope: uat.fileScope,
                                    checksFailed: uat.checksFailed,
                                    checksTotal: uat.checksTotal,
                                    acceptanceCriteria: uat.acceptanceCriteria,
                                  });
                                }}
                              >
                                <Bug className="h-3.5 w-3.5" />
                                Create Issue → Fix Loop
                                <span className="ml-auto text-[10px] text-muted-foreground">
                                  {uat.checksFailed}/{uat.checksTotal} failed
                                </span>
                              </Button>
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

// ── Inline Create UAT Form ──────────────────────────────────────

interface CreateUATFormProps {
  phaseId: string;
  existingTasks: { id: string; name: string }[];
  onSubmit: (data: { name: string; linkedTaskId: string; acceptanceCriteria: string[]; fileScope: string[] }) => void;
  onCancel: () => void;
}

function CreateUATForm({ phaseId, existingTasks, onSubmit, onCancel }: CreateUATFormProps) {
  const [name, setName] = useState("");
  const [linkedTaskId, setLinkedTaskId] = useState("");
  const [criteriaInput, setCriteriaInput] = useState("");
  const [acceptanceCriteria, setAcceptanceCriteria] = useState<string[]>([]);
  const [fileScopeInput, setFileScopeInput] = useState("");
  const [fileScope, setFileScope] = useState<string[]>([]);
  const [showTaskPicker, setShowTaskPicker] = useState(false);

  const handleAddCriteria = () => {
    const trimmed = criteriaInput.trim();
    if (trimmed) {
      setAcceptanceCriteria((prev) => [...prev, trimmed]);
      setCriteriaInput("");
    }
  };

  const handleAddFileScope = () => {
    const trimmed = fileScopeInput.trim();
    if (trimmed) {
      setFileScope((prev) => [...prev, trimmed]);
      setFileScopeInput("");
    }
  };

  const handleCriteriaKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter") { e.preventDefault(); handleAddCriteria(); }
  };

  const handleFileScopeKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter") { e.preventDefault(); handleAddFileScope(); }
  };

  const handleSubmit = () => {
    if (!name.trim()) { toast.error("UAT name is required"); return; }
    onSubmit({
      name: name.trim(),
      linkedTaskId,
      acceptanceCriteria: acceptanceCriteria.filter(Boolean),
      fileScope: fileScope.filter(Boolean),
    });
  };

  const selectedTask = existingTasks.find((t) => t.id === linkedTaskId);

  return (
    <Card className="border-violet-500/30 bg-violet-500/5">
      <CardContent className="p-4 space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="h-2 w-2 rounded-full bg-violet-400" />
            <span className="text-xs font-mono font-medium text-violet-400 uppercase tracking-wider">
              New UAT Check
            </span>
            <Badge variant="outline" className="font-mono text-[10px] h-4 text-muted-foreground">
              {phaseId}
            </Badge>
          </div>
          <button
            className="p-1 rounded hover:bg-muted/80 text-muted-foreground hover:text-foreground transition-colors"
            onClick={onCancel}
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>

        {/* Name */}
        <div className="space-y-1.5">
          <label className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium font-mono">
            UAT Name *
          </label>
          <Input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="e.g. User can complete checkout flow"
            className="font-mono text-sm h-9 bg-background/60"
            autoFocus
          />
        </div>

        {/* Linked Task */}
        <div className="space-y-1.5">
          <label className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium font-mono">
            Linked Task
          </label>
          {linkedTaskId && selectedTask ? (
            <div className="flex items-center gap-2 p-2 rounded border border-border/50 bg-background/60">
              <Badge variant="outline" className="font-mono text-[10px] h-5">{selectedTask.id}</Badge>
              <span className="text-xs truncate">{selectedTask.name}</span>
              <button
                className="ml-auto p-0.5 rounded hover:bg-muted/80 text-muted-foreground hover:text-foreground"
                onClick={() => { setLinkedTaskId(""); setShowTaskPicker(false); }}
              >
                <X className="h-3 w-3" />
              </button>
            </div>
          ) : (
            <div className="space-y-1">
              <div className="flex gap-2">
                <Input
                  value={linkedTaskId}
                  onChange={(e) => setLinkedTaskId(e.target.value)}
                  placeholder="TSK-000001 or pick from phase tasks"
                  className="font-mono text-sm h-9 bg-background/60 flex-1"
                />
                {existingTasks.length > 0 && (
                  <Button
                    variant="outline"
                    size="sm"
                    className="h-9 text-[10px] font-mono shrink-0"
                    onClick={() => setShowTaskPicker(!showTaskPicker)}
                  >
                    {showTaskPicker ? "Hide" : "Pick"}
                  </Button>
                )}
              </div>
              {showTaskPicker && (
                <div className="border border-border/50 rounded bg-background/80 max-h-32 overflow-y-auto">
                  {existingTasks.map((t) => (
                    <button
                      key={t.id}
                      className="w-full text-left px-3 py-1.5 text-xs font-mono hover:bg-accent/40 transition-colors flex items-center gap-2"
                      onClick={() => { setLinkedTaskId(t.id); setShowTaskPicker(false); }}
                    >
                      <Badge variant="outline" className="text-[9px] h-4">{t.id}</Badge>
                      <span className="truncate">{t.name}</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Acceptance Criteria */}
        <div className="space-y-1.5">
          <label className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium font-mono">
            Acceptance Criteria
          </label>
          {acceptanceCriteria.length > 0 && (
            <div className="space-y-1">
              {acceptanceCriteria.map((c, i) => (
                <div key={i} className="flex items-center gap-2 text-xs py-1 px-2 rounded bg-background/60 border border-border/30 group">
                  <CheckCircle className="h-3 w-3 text-muted-foreground shrink-0" />
                  <span className="font-mono flex-1">{c}</span>
                  <button
                    className="p-0.5 rounded opacity-0 group-hover:opacity-100 hover:bg-red-500/10 text-muted-foreground hover:text-red-400 transition-all"
                    onClick={() => setAcceptanceCriteria((prev) => prev.filter((_, idx) => idx !== i))}
                  >
                    <X className="h-2.5 w-2.5" />
                  </button>
                </div>
              ))}
            </div>
          )}
          <div className="flex gap-2">
            <Input
              value={criteriaInput}
              onChange={(e) => setCriteriaInput(e.target.value)}
              onKeyDown={handleCriteriaKeyDown}
              placeholder="Type criteria, press Enter to add"
              className="font-mono text-sm h-8 bg-background/60 flex-1"
            />
            <Button
              variant="outline"
              size="sm"
              className="h-8 px-2 text-[10px] font-mono shrink-0"
              onClick={handleAddCriteria}
              disabled={!criteriaInput.trim()}
            >
              <Plus className="h-3 w-3" />
            </Button>
          </div>
        </div>

        {/* File Scope */}
        <div className="space-y-1.5">
          <label className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium font-mono">
            File Scope
          </label>
          {fileScope.length > 0 && (
            <div className="space-y-1">
              {fileScope.map((f, i) => (
                <div key={i} className="flex items-center gap-2 text-xs py-1 px-2 rounded bg-background/60 border border-border/30 group">
                  <FileCode className="h-3 w-3 text-muted-foreground shrink-0" />
                  <code className="font-mono flex-1 text-[11px]">{f}</code>
                  <button
                    className="p-0.5 rounded opacity-0 group-hover:opacity-100 hover:bg-red-500/10 text-muted-foreground hover:text-red-400 transition-all"
                    onClick={() => setFileScope((prev) => prev.filter((_, idx) => idx !== i))}
                  >
                    <X className="h-2.5 w-2.5" />
                  </button>
                </div>
              ))}
            </div>
          )}
          <div className="flex gap-2">
            <Input
              value={fileScopeInput}
              onChange={(e) => setFileScopeInput(e.target.value)}
              onKeyDown={handleFileScopeKeyDown}
              placeholder="src/components/Checkout.tsx"
              className="font-mono text-sm h-8 bg-background/60 flex-1"
            />
            <Button
              variant="outline"
              size="sm"
              className="h-8 px-2 text-[10px] font-mono shrink-0"
              onClick={handleAddFileScope}
              disabled={!fileScopeInput.trim()}
            >
              <Plus className="h-3 w-3" />
            </Button>
          </div>
        </div>

        {/* Preview */}
        <div className="space-y-1.5">
          <label className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium font-mono">
            Preview
          </label>
          <pre className="text-[10px] font-mono text-muted-foreground bg-background/60 rounded p-2.5 border border-border/30 leading-relaxed max-h-28 overflow-y-auto">
            {JSON.stringify({
              id: "UAT-XXXXXX",
              taskId: linkedTaskId || "TSK-XXXXXX",
              status: "unverified",
              name: name || "(untitled)",
              acceptanceCriteria,
              fileScope,
            }, null, 2)}
          </pre>
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2 pt-1 border-t border-border/30">
          <Button
            size="sm"
            className="h-8 gap-1.5 text-[11px] font-mono"
            onClick={handleSubmit}
            disabled={!name.trim()}
          >
            <Shield className="h-3 w-3" /> Create UAT
          </Button>
          <Button
            variant="ghost"
            size="sm"
            className="h-8 text-[11px] font-mono text-muted-foreground"
            onClick={onCancel}
          >
            Cancel
          </Button>
          <div className="ml-auto text-[10px] text-muted-foreground/50 font-mono">
            {acceptanceCriteria.length} criteria · {fileScope.length} files
          </div>
        </div>
      </CardContent>
    </Card>
  );
}