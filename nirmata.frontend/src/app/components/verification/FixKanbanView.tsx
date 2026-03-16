import { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate, useParams } from "react-router";
import {
  AlertCircle,
  AlertTriangle,
  ArrowDown,
  ArrowRight,
  Bug,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Clock,
  FileCode,
  FileText,
  Hammer,
  Layers,
  Loader2,
  Play,
  Plus,
  RotateCcw,
  Search,
  Shield,
  Trash2,
  X,
  XCircle,
  Zap,
} from "lucide-react";
import { Badge } from "../ui/badge";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { Textarea } from "../ui/textarea";
import { Card, CardContent, CardHeader } from "../ui/card";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "../ui/select";
import { cn } from "../ui/utils";
import {
  type FixItem,
  type FixStatus,
} from "./verificationState";
import { toast } from "sonner";
import type { CreateIssueFromUAT } from "./PhaseUATViewer";
import { useVerificationState } from "../../hooks/useAosData";

/* ── Config maps ────────────────────────────────────────────────── */

const severityConfig: Record<
  string,
  { label: string; color: string; icon: React.ReactNode; iconClass: typeof AlertCircle }
> = {
  critical: { label: "Critical", color: "text-red-400 bg-red-400/10 border-red-400/20", icon: <AlertCircle className="h-3 w-3" />, iconClass: AlertCircle },
  high:     { label: "High",     color: "text-orange-400 bg-orange-400/10 border-orange-400/20", icon: <AlertTriangle className="h-3 w-3" />, iconClass: AlertTriangle },
  medium:   { label: "Medium",   color: "text-yellow-400 bg-yellow-400/10 border-yellow-400/20", icon: <AlertTriangle className="h-3 w-3" />, iconClass: AlertTriangle },
  low:      { label: "Low",      color: "text-blue-400 bg-blue-400/10 border-blue-400/20", icon: <Clock className="h-3 w-3" />, iconClass: Clock },
};

const statusConfig: Record<FixStatus, { label: string; color: string }> = {
  open:      { label: "Open",      color: "text-red-400" },
  triaging:  { label: "Triaging",  color: "text-orange-400" },
  planned:   { label: "Planned",   color: "text-yellow-400" },
  executing: { label: "Executing", color: "text-blue-400" },
  resolved:  { label: "Resolved",  color: "text-green-400" },
};

const STATUS_PIPELINE: FixStatus[] = ["open", "triaging", "planned", "executing", "resolved"];

/* ── Component ──────────────────────────────────────────────────── */

export interface FixKanbanViewProps {
  issueSeed?: CreateIssueFromUAT | null;
  onSeedConsumed?: () => void;
}

export function FixKanbanView({ issueSeed, onSeedConsumed }: FixKanbanViewProps) {
  const { verification: ctx } = useVerificationState();
  const navigate = useNavigate();
  const { workspaceId } = useParams();
  
  // State
  const [issues, setIssues] = useState<FixItem[]>(ctx.fixItems);
  const [selectedUatId, setSelectedUatId] = useState<string | null>(null);
  const [selectedIssueId, setSelectedIssueId] = useState<string | null>(null);
  
  // We keep this for the "Create Issue" form logic
  const [newIssueForm, setNewIssueForm] = useState({
    title: "",
    severity: "medium",
    description: "",
    reproStep: "",
    linkedUATId: "",
  });

  const failingUATs = ctx.uatItems.filter((u) => u.status === "fail");

  // Handle incoming UAT issue seed from props
  useEffect(() => {
    if (issueSeed) {
      setSelectedUatId(issueSeed.uatId);
      onSeedConsumed?.();
    }
  }, [issueSeed]);

  // Sync Form with Selection
  useEffect(() => {
    if (selectedUatId) {
      const uat = failingUATs.find(u => u.id === selectedUatId);
      if (uat) {
        setNewIssueForm({
          title: `Fix: ${uat.taskName}`,
          severity: "high",
          description: `UAT ${uat.id} FAILED.\n\nFiles: ${uat.fileScope.join(", ")}\nChecks: ${uat.checksFailed}/${uat.checksTotal}`,
          reproStep: `Run verification for ${uat.taskId}`,
          linkedUATId: uat.id,
        });
      }
    }
  }, [selectedUatId]);

  const handleCreateIssue = useCallback(() => {
    const newId = `ISS-${String(issues.length + 1).padStart(4, "0")}`;
    const seedTaskId = newIssueForm.linkedUATId ? newIssueForm.linkedUATId.replace("UAT", "TSK") : "";
    const seedUat = ctx.uatItems.find((u) => u.id === newIssueForm.linkedUATId);
    
    const newIssue: FixItem = {
      id: newId,
      severity: newIssueForm.severity as any,
      description: newIssueForm.title || "Untitled Issue",
      issueStatus: "open",
      fixStatus: "open",
      linkedUATIds: newIssueForm.linkedUATId ? [newIssueForm.linkedUATId] : [],
      linkedTaskIds: seedTaskId ? [seedTaskId] : [],
      linkedRunIds: [],
      linkedRuns: [],
      impactedFiles: seedUat?.fileScope ?? [],
      impactedArea: seedUat?.phaseTitle ?? "Unknown",
      repro: newIssueForm.reproStep ? [newIssueForm.reproStep] : [],
      history: [{ date: new Date().toISOString(), action: `Issue created from ${newIssueForm.linkedUATId || "manual entry"}` }],
      tags: newIssueForm.linkedUATId ? ["from-uat"] : [],
      lastRun: null,
    };
    
    setIssues(prev => [newIssue, ...prev]);
    toast.success(`Issue ${newId} created`);
  }, [issues.length, newIssueForm]);

  // Fix Loop simulation state
  const FIX_LOOP_STAGES = [
    { key: "ingesting", label: "Loading issue, UAT, run, and task specs", pct: 10 },
    { key: "analyzing", label: "Parsing failure context from RUN-00034", pct: 25 },
    { key: "diffing", label: "Comparing file scope against passing checks", pct: 40 },
    { key: "generating", label: "Drafting patch instructions for TSK-00013", pct: 60 },
    { key: "validating", label: "Checking patch against UAT-00012 acceptance criteria", pct: 80 },
    { key: "writing", label: "Writing TSK-00013.json to /specs/tasks", pct: 95 },
    { key: "done", label: "Patch task ready for execution", pct: 100 },
  ] as const;

  const [fixLoopStatus, setFixLoopStatus] = useState<"idle" | "running" | "done">("idle");
  const [fixLoopStageIdx, setFixLoopStageIdx] = useState(0);
  const [fixLoopProgress, setFixLoopProgress] = useState(0);
  const fixLoopTimer = useRef<ReturnType<typeof setInterval> | null>(null);

  const startFixLoop = useCallback(() => {
    setFixLoopStatus("running");
    setFixLoopStageIdx(0);
    setFixLoopProgress(0);

    let idx = 0;
    fixLoopTimer.current = setInterval(() => {
      idx++;
      if (idx >= FIX_LOOP_STAGES.length) {
        clearInterval(fixLoopTimer.current!);
        setFixLoopStatus("done");
        setFixLoopStageIdx(FIX_LOOP_STAGES.length - 1);
        setFixLoopProgress(100);
        handleCreateIssue();
        toast.success("TSK-00013.json generated successfully");
        return;
      }
      setFixLoopStageIdx(idx);
      setFixLoopProgress(FIX_LOOP_STAGES[idx].pct);
    }, 1400);
  }, [handleCreateIssue]);

  const resetFixLoop = useCallback(() => {
    if (fixLoopTimer.current) clearInterval(fixLoopTimer.current);
    setFixLoopStatus("idle");
    setFixLoopStageIdx(0);
    setFixLoopProgress(0);
  }, []);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (fixLoopTimer.current) clearInterval(fixLoopTimer.current);
    };
  }, []);

  /* ── Render ────────────────────────────────────────────────────── */

  return (
    <div className="flex-1 flex overflow-hidden">
      {/* ── Main Content Area ── */}
      <div className="flex-1 overflow-hidden p-6 bg-zinc-950/50">
        <div className="grid grid-cols-12 gap-6 h-full min-h-0">
          
          {/* ── BOX 1: INBOX (Failed UATs) ── */}
          <div className="col-span-4 flex flex-col gap-0 min-h-0 bg-black/40 border border-zinc-800 rounded-xl overflow-hidden shadow-sm">
            <div className="p-3 border-b border-zinc-800 bg-zinc-900/30 flex items-center justify-between">
              <div className="flex items-center gap-2">
                 <div className="h-2 w-2 rounded-full bg-orange-500" />
                 <h3 className="text-xs font-mono font-bold text-zinc-300 tracking-wider">ISSUE QUEUE</h3>
              </div>
              <Badge variant="outline" className="font-mono text-[10px] border-zinc-700 text-zinc-500">
                {issues.length} ACTIVE
              </Badge>
            </div>
            
            <div className="flex-1 overflow-y-auto p-2 space-y-2">
              {issues.length === 0 ? (
                <div className="h-full flex flex-col items-center justify-center text-zinc-700 gap-2">
                   <Bug className="w-8 h-8 opacity-20" />
                   <span className="text-xs font-mono">NO ISSUES IN QUEUE</span>
                   <span className="text-[10px] text-zinc-700 max-w-[180px] text-center">Failed UATs will appear here as open issues</span>
                </div>
              ) : (
                issues.map((issue) => {
                  const resolvedUatId =
                    issue.linkedUATIds.find((id) =>
                      failingUATs.some((u) => u.id === id)
                    ) ?? failingUATs[0]?.id ?? null;
                  const isSelected = issue.id === selectedIssueId;
                  
                  return (
                    <div 
                      key={issue.id}
                      onClick={() => {
                        setSelectedIssueId(issue.id);
                        if (resolvedUatId) setSelectedUatId(resolvedUatId);
                      }}
                      className={cn(
                        "p-3 rounded-lg border cursor-pointer transition-all flex flex-col gap-2 relative overflow-hidden",
                        isSelected 
                          ? "bg-orange-500/5 border-orange-500/50 ring-1 ring-orange-500/20" 
                          : "bg-zinc-900/40 border-zinc-800/60 hover:border-zinc-700 hover:bg-zinc-800/40"
                      )}
                    >
                       {isSelected && <div className="absolute left-0 top-0 bottom-0 w-1 bg-orange-500" />}
                       
                       <div className="flex justify-between items-start">
                          <div className="flex items-center gap-2">
                              <span className={cn("text-xs font-mono font-bold", isSelected ? "text-orange-400" : "text-zinc-400")}>
                                 {issue.id}
                              </span>
                               {severityConfig[issue.severity]?.icon}
                          </div>
                          <Badge variant="outline" className={cn("text-[9px] h-4 px-1 rounded-sm border-0 bg-opacity-10", statusConfig[issue.fixStatus].color.replace('text-', 'bg-'))}>
                              {statusConfig[issue.fixStatus].label.toUpperCase()}
                          </Badge>
                       </div>
                       
                       <div className="text-[11px] text-zinc-300 font-medium line-clamp-1">
                          {issue.description}
                       </div>

                       {issue.linkedUATIds.length > 0 && (
                           <div className="flex items-center gap-1.5 mt-1 pt-2 border-t border-zinc-800/50">
                              <span className="text-[9px] font-mono text-zinc-500">LINKED UAT:</span>
                              <Badge variant="secondary" className="text-[9px] h-3.5 px-1 bg-zinc-800 text-zinc-400 border-zinc-700">
                                 {issue.linkedUATIds[0]}
                              </Badge>
                           </div>
                       )}
                    </div>
                  );
                })
              )}
            </div>
          </div>

          {/* ── BOX 2 & 3: DETAIL & ACTION ── */}
          <div className="col-span-8 flex flex-col gap-6 min-h-0">
             {selectedUatId ? (
               <>
                 {/* ── BOX 2: EVIDENCE VIEWER ── */}
                 <div className="flex-1 flex flex-col gap-3 min-h-[300px]">
                    {(() => {
                       const uat = failingUATs.find(u => u.id === selectedUatId);
                       if (!uat) return null;
                       
                       return (
                          <>
                            <div className="flex items-center justify-between px-1 pb-1">
                                <div className="flex items-center gap-2">
                                    <h3 className="text-xs font-mono font-bold text-zinc-400">EVIDENCE FILES</h3>
                                    <div className="h-px w-12 bg-zinc-800" />
                                    <span className="text-[10px] font-mono text-zinc-600 uppercase tracking-wider">
                                        {uat.taskName}
                                    </span>
                                </div>
                                <div className="flex items-center gap-1.5 text-green-400 text-[10px] font-mono bg-green-500/10 px-2 py-1 rounded border border-green-500/20 shadow-[0_0_10px_rgba(34,197,94,0.1)]">
                                    <CheckCircle2 className="w-3 h-3" />
                                    <span>4 FILES LOADED</span>
                                </div>
                            </div>

                            <div className="flex-1 grid grid-cols-2 gap-4">
                                {/* 1. Issue Spec */}
                                <Card className="bg-black/40 border-zinc-800 flex flex-col overflow-hidden group hover:border-zinc-700 transition-colors">
                                    <div className="p-3 border-b border-zinc-800 bg-zinc-900/20 flex justify-between items-center">
                                         <div className="flex items-center gap-2">
                                            <Bug className="w-3.5 h-3.5 text-orange-400" />
                                            <span className="text-[10px] font-mono font-bold text-zinc-300">ISS-0001.json</span>
                                        </div>
                                        <CheckCircle2 className="w-3.5 h-3.5 text-green-500 opacity-80" />
                                    </div>
                                    <div className="flex-1 p-3 bg-zinc-950/30 font-mono text-[10px] overflow-hidden relative">
                                        <div className="absolute inset-0 p-3 overflow-auto scrollbar-thin scrollbar-thumb-zinc-800">
                                             <div className="opacity-80 text-zinc-400">
                                                <span className="text-purple-400">{"{"}</span>
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"id"</span>: <span className="text-green-400">"ISS-0001"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"title"</span>: <span className="text-green-400">"OAuth callback fails on mobile Safari"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"status"</span>: <span className="text-orange-400">"open"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"severity"</span>: <span className="text-red-400">"high"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"linked_uat"</span>: [<span className="text-green-400">"UAT-00012"</span>],
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"linked_task"</span>: <span className="text-green-400">"TSK-00012"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"evidence"</span>: <span className="text-purple-400">{"{"}</span>
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-blue-400">"latest_run"</span>: <span className="text-green-400">"RUN-00034"</span>
                                                <br />&nbsp;&nbsp;<span className="text-purple-400">{"}"}</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"file_scope"</span>: [
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-green-400">"src/auth/OAuthProvider.ts"</span>,
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-green-400">"src/components/LoginButton.tsx"</span>
                                                <br />&nbsp;&nbsp;],
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"notes"</span>: <span className="text-green-400">"Element not interactive on iOS Safari; likely timing/redirect issue."</span>
                                                <br /><span className="text-purple-400">{"}"}</span>
                                             </div>
                                        </div>
                                    </div>
                                </Card>

                                {/* 2. UAT Definition */}
                                <Card className="bg-black/40 border-zinc-800 flex flex-col overflow-hidden group hover:border-zinc-700 transition-colors">
                                    <div className="p-3 border-b border-zinc-800 bg-zinc-900/20 flex justify-between items-center">
                                        <div className="flex items-center gap-2">
                                            <FileText className="w-3.5 h-3.5 text-zinc-400" />
                                            <span className="text-[10px] font-mono font-bold text-zinc-300">UAT-00012.json</span>
                                        </div>
                                        <CheckCircle2 className="w-3.5 h-3.5 text-green-500 opacity-80" />
                                    </div>
                                    <div className="flex-1 p-3 bg-zinc-950/30 font-mono text-[10px] text-zinc-500 overflow-hidden relative">
                                         <div className="absolute inset-0 p-3 overflow-auto">
                                            <div className="opacity-80">
                                                <span className="text-purple-400">{"{"}</span>
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"id"</span>: <span className="text-green-400">"UAT-00012"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"task_id"</span>: <span className="text-green-400">"TSK-00012"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"status"</span>: <span className="text-red-400">"failed"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"checks_total"</span>: <span className="text-yellow-400">3</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"checks_passed"</span>: <span className="text-yellow-400">2</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"failure_context"</span>: [
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-green-400">"Element not interactive"</span>,
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-green-400">"Timeout 5000ms"</span>
                                                <br />&nbsp;&nbsp;],
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"repro"</span>: <span className="text-purple-400">{"{"}</span>
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-blue-400">"command"</span>: <span className="text-green-400">"npm run test:uat -- --id UAT-00012"</span>,
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-blue-400">"env"</span>: <span className="text-green-400">"ios-safari"</span>
                                                <br />&nbsp;&nbsp;<span className="text-purple-400">{"}"}</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"evidence"</span>: <span className="text-purple-400">{"{"}</span>
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-blue-400">"run_id"</span>: <span className="text-green-400">"RUN-00034"</span>
                                                <br />&nbsp;&nbsp;<span className="text-purple-400">{"}"}</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"file_scope"</span>: [
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-green-400">"src/auth/OAuthProvider.ts"</span>,
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-green-400">"src/components/LoginButton.tsx"</span>
                                                <br />&nbsp;&nbsp;]
                                                <br /><span className="text-purple-400">{"}"}</span>
                                            </div>
                                         </div>
                                    </div>
                                </Card>

                                {/* 3. Latest Run */}
                                <Card className="bg-black/40 border-zinc-800 flex flex-col overflow-hidden group hover:border-zinc-700 transition-colors">
                                    <div className="p-3 border-b border-zinc-800 bg-zinc-900/20 flex justify-between items-center">
                                         <div className="flex items-center gap-2">
                                            <Clock className="w-3.5 h-3.5 text-blue-400" />
                                            <span className="text-[10px] font-mono font-bold text-zinc-300">latest.json</span>
                                        </div>
                                        <CheckCircle2 className="w-3.5 h-3.5 text-green-500 opacity-80" />
                                    </div>
                                    <div className="flex-1 p-3 bg-zinc-950/30 font-mono text-[10px] overflow-hidden relative">
                                         <div className="absolute inset-0 p-3 overflow-auto">
                                            <div className="opacity-80 text-zinc-400">
                                                <span className="text-purple-400">{"{"}</span>
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"run_id"</span>: <span className="text-green-400">"RUN-00034"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"uat_id"</span>: <span className="text-green-400">"UAT-00012"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"timestamp"</span>: <span className="text-green-400">"2026-02-26T09:14:22Z"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"outcome"</span>: <span className="text-red-400">"failed"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"checks_total"</span>: <span className="text-yellow-400">3</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"checks_passed"</span>: <span className="text-yellow-400">2</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"duration_ms"</span>: <span className="text-yellow-400">5340</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"failure_detail"</span>: <span className="text-purple-400">{"{"}</span>
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-blue-400">"check"</span>: <span className="text-green-400">"oauth_redirect_completes"</span>,
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-blue-400">"error"</span>: <span className="text-green-400">"Element not interactive after 5000ms"</span>,
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-blue-400">"env"</span>: <span className="text-green-400">"ios-safari"</span>
                                                <br />&nbsp;&nbsp;<span className="text-purple-400">{"}"}</span>
                                                <br /><span className="text-purple-400">{"}"}</span>
                                            </div>
                                         </div>
                                    </div>
                                </Card>
                                
                                {/* 4. Task Definition */}
                                <Card className="bg-black/40 border-zinc-800 flex flex-col overflow-hidden group hover:border-zinc-700 transition-colors">
                                    <div className="p-3 border-b border-zinc-800 bg-zinc-900/20 flex justify-between items-center">
                                         <div className="flex items-center gap-2">
                                            <FileCode className="w-3.5 h-3.5 text-purple-400" />
                                            <span className="text-[10px] font-mono font-bold text-zinc-300">TSK-00008.json</span>
                                        </div>
                                        <CheckCircle2 className="w-3.5 h-3.5 text-green-500 opacity-80" />
                                    </div>
                                    <div className="flex-1 p-3 bg-zinc-950/30 font-mono text-[10px] overflow-hidden relative">
                                         <div className="absolute inset-0 p-3 overflow-auto">
                                            <div className="opacity-80 text-zinc-400">
                                                <span className="text-purple-400">{"{"}</span>
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"id"</span>: <span className="text-green-400">"TSK-00008"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"title"</span>: <span className="text-green-400">"Implement OAuth callback handler"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"status"</span>: <span className="text-orange-400">"patching"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"phase"</span>: <span className="text-green-400">"auth-integration"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"linked_uat"</span>: <span className="text-green-400">"UAT-00012"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"linked_issue"</span>: <span className="text-green-400">"ISS-0001"</span>,
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"file_scope"</span>: [
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-green-400">"src/auth/OAuthProvider.ts"</span>,
                                                <br />&nbsp;&nbsp;&nbsp;&nbsp;<span className="text-green-400">"src/components/LoginButton.tsx"</span>
                                                <br />&nbsp;&nbsp;],
                                                <br />&nbsp;&nbsp;<span className="text-blue-400">"patch_notes"</span>: <span className="text-green-400">"Add iOS Safari workaround for redirect timing"</span>
                                                <br /><span className="text-purple-400">{"}"}</span>
                                            </div>
                                         </div>
                                    </div>
                                </Card>
                            </div>
                          </>
                       );
                    })()}
                 </div>

                 {/* ── BOX 3: ACTION / FIX LOOP ── */}
                 <div className="h-auto bg-zinc-900/20 border border-zinc-800 rounded-xl overflow-hidden shadow-sm flex flex-col">
                    {(() => {
                       const uat = failingUATs.find(u => u.id === selectedUatId);
                       const linkedIssue = issues.find(i => i.linkedUATIds.includes(uat?.id || ""));
                       
                       if (!uat) return null;

                       return (
                          <div className="p-5 flex flex-col gap-4">
                             {/* ── Top row: Info + Start Button ── */}
                             <div className="flex items-center gap-5">
                                <div className="flex-1 space-y-3">
                                   <div className="flex items-center gap-2">
                                      <div className={cn(
                                         "p-2 rounded-md",
                                         fixLoopStatus === "running" ? "bg-blue-500/20 text-blue-400" :
                                         fixLoopStatus === "done" ? "bg-green-500/20 text-green-400" :
                                         "bg-blue-500/10 text-blue-400"
                                      )}>
                                         {fixLoopStatus === "running" ? (
                                            <Loader2 className="w-5 h-5 animate-spin" />
                                         ) : fixLoopStatus === "done" ? (
                                            <CheckCircle2 className="w-5 h-5" />
                                         ) : (
                                            <Zap className="w-5 h-5" />
                                         )}
                                      </div>
                                      <div>
                                         <h3 className="text-sm font-medium text-zinc-200">
                                            {fixLoopStatus === "done" ? "Patch Task Generated" : "Fix Loop Agent"}
                                         </h3>
                                         <p className="text-xs text-zinc-500">
                                            {fixLoopStatus === "idle" && "Reads the 4 evidence files above to generate a new patch task"}
                                            {fixLoopStatus === "running" && FIX_LOOP_STAGES[fixLoopStageIdx].label}
                                            {fixLoopStatus === "done" && "TSK-00013.json written — ready for execution pipeline"}
                                         </p>
                                      </div>
                                   </div>

                                   {/* Context badges */}
                                   <div className="flex flex-wrap gap-1.5">
                                      <Badge variant="secondary" className="bg-zinc-800 text-zinc-400 border-zinc-700 font-mono text-[10px]">
                                         ISS-0001
                                      </Badge>
                                      <Badge variant="secondary" className="bg-zinc-800 text-zinc-400 border-zinc-700 font-mono text-[10px]">
                                         {uat.id}
                                      </Badge>
                                      <Badge variant="secondary" className="bg-zinc-800 text-zinc-400 border-zinc-700 font-mono text-[10px]">
                                         RUN-00034
                                      </Badge>
                                      <Badge variant="secondary" className="bg-zinc-800 text-zinc-400 border-zinc-700 font-mono text-[10px]">
                                         TSK-00008
                                      </Badge>
                                      {uat.fileScope.map(f => (
                                         <Badge key={f} variant="outline" className="text-zinc-600 border-zinc-700/50 border-dashed font-mono text-[10px]">
                                            {f}
                                         </Badge>
                                      ))}
                                   </div>
                                </div>

                                <div className="w-[1px] self-stretch bg-zinc-800" />

                                {/* Action column */}
                                <div className="min-w-[200px] flex flex-col gap-2 justify-center">
                                   {fixLoopStatus === "idle" && (
                                      <>
                                         <span className="text-[10px] text-zinc-500 font-mono text-center tracking-wider">READY TO GENERATE</span>
                                         <Button
                                            className="w-full h-12 bg-blue-600 hover:bg-blue-500 text-white shadow-lg shadow-blue-900/30 transition-all hover:shadow-blue-800/40"
                                            onClick={startFixLoop}
                                         >
                                            <Zap className="w-4 h-4 mr-2" />
                                            START FIX LOOP
                                         </Button>
                                      </>
                                   )}
                                   {fixLoopStatus === "running" && (
                                      <div className="flex flex-col items-center gap-1.5">
                                         <span className="text-[10px] text-blue-400 font-mono animate-pulse">AGENT RUNNING</span>
                                         <span className="text-2xl font-mono font-bold text-blue-400 tabular-nums">{fixLoopProgress}%</span>
                                         <span className="text-[9px] font-mono text-zinc-600">
                                            STAGE {fixLoopStageIdx + 1}/{FIX_LOOP_STAGES.length}
                                         </span>
                                      </div>
                                   )}
                                   {fixLoopStatus === "done" && (
                                      <div className="flex flex-col gap-2">
                                         <div className="flex flex-col items-center gap-1">
                                            <span className="text-[10px] text-green-400 font-mono">COMPLETE</span>
                                            <Badge className="bg-green-500/10 text-green-400 border-green-500/20 font-mono text-[10px] px-3 py-1">
                                               TSK-00013.json
                                            </Badge>
                                         </div>
                                         <Button
                                            variant="outline"
                                            className="w-full h-8 border-zinc-700 hover:bg-zinc-800 text-zinc-400"
                                            onClick={resetFixLoop}
                                         >
                                            <RotateCcw className="w-3 h-3 mr-2" />
                                            RESET
                                         </Button>
                                      </div>
                                   )}
                                </div>
                             </div>

                             {/* ── Progress bar + stage pipeline ── */}
                             {fixLoopStatus !== "idle" && (
                                <div className="space-y-2">
                                   {/* Progress track */}
                                   <div className="relative h-1.5 bg-zinc-800 rounded-full overflow-hidden">
                                      <div
                                         className={cn(
                                            "absolute inset-y-0 left-0 rounded-full transition-all duration-700 ease-out",
                                            fixLoopStatus === "done" ? "bg-green-500" : "bg-blue-500"
                                         )}
                                         style={{ width: `${fixLoopProgress}%` }}
                                      />
                                      {fixLoopStatus === "running" && (
                                         <div
                                            className="absolute inset-y-0 left-0 rounded-full bg-blue-400/30 animate-pulse transition-all duration-700 ease-out"
                                            style={{ width: `${Math.min(fixLoopProgress + 8, 100)}%` }}
                                         />
                                      )}
                                   </div>

                                   {/* Stage steps */}
                                   <div className="flex items-center gap-1 overflow-x-auto pb-1">
                                      {FIX_LOOP_STAGES.map((stage, i) => {
                                         const isActive = i === fixLoopStageIdx && fixLoopStatus === "running";
                                         const isPast = i < fixLoopStageIdx || fixLoopStatus === "done";
                                         return (
                                            <div key={stage.key} className="flex items-center gap-1 shrink-0">
                                               {i > 0 && <div className={cn("w-2 h-px", isPast ? "bg-green-500/40" : "bg-zinc-800")} />}
                                               <div className={cn(
                                                  "flex items-center gap-1 px-1.5 py-0.5 rounded text-[9px] font-mono transition-all",
                                                  isActive && "bg-blue-500/10 text-blue-400 ring-1 ring-blue-500/20",
                                                  isPast && "text-green-500/70",
                                                  !isActive && !isPast && "text-zinc-700"
                                               )}>
                                                  {isPast ? (
                                                     <CheckCircle2 className="w-2.5 h-2.5 shrink-0" />
                                                  ) : isActive ? (
                                                     <Loader2 className="w-2.5 h-2.5 animate-spin shrink-0" />
                                                  ) : (
                                                     <div className="w-2.5 h-2.5 rounded-full border border-zinc-700 shrink-0" />
                                                  )}
                                                  <span className="whitespace-nowrap">{stage.label}</span>
                                               </div>
                                            </div>
                                         );
                                      })}
                                   </div>
                                </div>
                             )}
                          </div>
                       );
                    })()}
                 </div>
               </>
             ) : (
                <div className="col-span-8 flex flex-col items-center justify-center h-full border border-dashed border-zinc-800 rounded-xl bg-zinc-950/30 text-zinc-600 gap-4">
                   <div className="p-4 rounded-full bg-zinc-900/50">
                      <Bug className="w-8 h-8 opacity-50" />
                   </div>
                   <div className="text-center">
                      <h3 className="text-sm font-medium text-zinc-400">No Issue Selected</h3>
                      <p className="text-xs text-zinc-600 mt-1 max-w-xs mx-auto">
                         Select an issue from the queue to load its evidence files and run the fix loop agent.
                      </p>
                   </div>
                </div>
             )}
          </div>
          
        </div>
      </div>
    </div>
  );
}