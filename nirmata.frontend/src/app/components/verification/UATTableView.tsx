import { useState } from "react";
import { 
  Shield, 
  AlertCircle, 
  Clock, 
  CheckCircle2, 
  CircleDashed, 
  ChevronRight, 
  Play, 
  ExternalLink, 
  Bug,
  MoreHorizontal,
  ChevronDown,
  Link as LinkIcon,
  X
} from "lucide-react";
import { Badge } from "../ui/badge";
import { Button } from "../ui/button";
import { Table, TableHeader, TableHead, TableBody, TableRow, TableCell } from "../ui/table";
import { Checkbox } from "../ui/checkbox";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
  DropdownMenuSeparator,
  DropdownMenuLabel
} from "../ui/dropdown-menu";
import { cn } from "../ui/utils";
import { type UATItem, type UATStatus } from "./verificationState";
import { useVerificationState } from "../../hooks/useAosData";
import { toast } from "sonner";

const statusConfig: Record<UATStatus, { label: string; color: string; icon: React.ReactNode }> = {
  pass:       { label: "Pass",       color: "text-green-400 bg-green-400/10 border-green-400/20", icon: <CheckCircle2 className="h-3 w-3" /> },
  fail:       { label: "Fail",       color: "text-red-400 bg-red-400/10 border-red-400/20",     icon: <AlertCircle className="h-3 w-3" /> },
  partial:    { label: "Partial",    color: "text-yellow-400 bg-yellow-400/10 border-yellow-400/20", icon: <Clock className="h-3 w-3" /> },
  unverified: { label: "Unverified", color: "text-muted-foreground bg-muted/50 border-border",  icon: <CircleDashed className="h-3 w-3" /> },
};

export function UATTableView() {
  const { verification: ctx } = useVerificationState();
  const [selectedId, setSelectedId] = useState<string | null>(ctx.uatItems[0]?.id ?? null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  
  const selected = ctx.uatItems.find((u) => u.id === selectedId) ?? null;

  // Selection handlers
  const toggleRow = (
    id: string,
    e: React.MouseEvent | boolean | "indeterminate",
  ) => {
    if (typeof e !== "boolean" && e !== "indeterminate") e.stopPropagation();

    const shouldSelect = e === true ? true : e === false ? false : !selectedIds.has(id);
    const newSelected = new Set(selectedIds);
    if (shouldSelect) newSelected.add(id);
    else newSelected.delete(id);
    setSelectedIds(newSelected);
  };

  const toggleAll = (checked: boolean) => {
    if (checked) {
      setSelectedIds(new Set(ctx.uatItems.map(u => u.id)));
    } else {
      setSelectedIds(new Set());
    }
  };

  // Bulk Actions
  const handleBulkRun = () => {
    toast.success(`Running verification for ${selectedIds.size} items...`);
  };

  const handleBulkStatus = (status: UATStatus) => {
    toast.success(`Marked ${selectedIds.size} items as ${statusConfig[status].label}`);
  };

  const handleBulkIssue = () => {
    toast.success(`Created issues for ${selectedIds.size} items`);
  };

  const handleRunFailing = () => {
    const failingCount = ctx.uatItems.filter(u => u.status === "fail").length;
    toast.success(`Running ${failingCount} failing checks...`);
  };

  const handleRunUnverified = () => {
    const unverifiedCount = ctx.uatItems.filter(u => u.status === "unverified").length;
    toast.success(`Running ${unverifiedCount} unverified checks...`);
  };

  return (
    <div className="flex-1 flex overflow-hidden">
      {/* ── Table Panel ─────────────────────────── */}
      <div className="flex-1 flex flex-col overflow-hidden border-r border-border">
        
        {/* Toolbar */}
        <div className="flex items-center justify-between px-4 py-2 border-b border-border bg-muted/10 min-h-[44px]">
          {selectedIds.size > 0 ? (
            <div className="flex items-center gap-2 w-full animate-in fade-in duration-200">
              <span className="text-xs font-medium mr-2">{selectedIds.size} selected</span>
              
              <div className="h-4 w-px bg-border mx-1" />
              
              <Button size="sm" variant="secondary" className="h-7 text-xs gap-1.5" onClick={handleBulkRun}>
                <Play className="h-3 w-3" /> Run
              </Button>
              
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button size="sm" variant="secondary" className="h-7 text-xs gap-1.5">
                    <CheckCircle2 className="h-3 w-3" /> Mark <ChevronDown className="h-3 w-3 opacity-50" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="start">
                  <DropdownMenuItem onClick={() => handleBulkStatus("pass")}>
                    <CheckCircle2 className="h-3.5 w-3.5 mr-2 text-green-400" /> Pass
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => handleBulkStatus("fail")}>
                    <AlertCircle className="h-3.5 w-3.5 mr-2 text-red-400" /> Fail
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => handleBulkStatus("unverified")}>
                    <CircleDashed className="h-3.5 w-3.5 mr-2 text-muted-foreground" /> Unverified
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>

              <Button size="sm" variant="secondary" className="h-7 text-xs gap-1.5" onClick={handleBulkIssue}>
                <Bug className="h-3 w-3" /> Create Issue
              </Button>

              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button size="sm" variant="ghost" className="h-7 w-7 p-0">
                    <MoreHorizontal className="h-3.5 w-3.5 text-muted-foreground" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="start">
                  <DropdownMenuItem onClick={() => toast.success(`Linking tasks for ${selectedIds.size} items...`)}>
                    <LinkIcon className="h-3.5 w-3.5 mr-2" /> Link to Task
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>

              <Button size="sm" variant="ghost" className="h-7 w-7 p-0 ml-auto" onClick={() => setSelectedIds(new Set())}>
                <X className="h-3.5 w-3.5 text-muted-foreground" />
              </Button>
            </div>
          ) : (
            <div className="flex items-center gap-2 w-full animate-in fade-in duration-200">
               <div className="flex items-center gap-2">
                 <Button size="sm" variant="outline" className="h-7 text-xs gap-1.5" onClick={handleRunFailing}>
                   <Play className="h-3 w-3" /> Run Failing
                 </Button>
                 <Button size="sm" variant="outline" className="h-7 text-xs gap-1.5" onClick={handleRunUnverified}>
                   <Play className="h-3 w-3" /> Run Unverified
                 </Button>
               </div>
               <div className="ml-auto flex items-center gap-2">
                  <span className="text-[10px] text-muted-foreground uppercase tracking-wider font-medium">Batch Actions</span>
               </div>
            </div>
          )}
        </div>

        <div className="flex-1 overflow-auto">
          <Table>
            <TableHeader>
              <TableRow className="border-b border-border bg-muted/30">
                <TableHead className="w-[40px] pl-4">
                  <Checkbox 
                    checked={selectedIds.size === ctx.uatItems.length && ctx.uatItems.length > 0}
                    onCheckedChange={(checked) => toggleAll(!!checked)}
                  />
                </TableHead>
                <TableHead className="w-[72px] font-mono text-[10px] uppercase tracking-wider text-muted-foreground">Status</TableHead>
                <TableHead className="w-[100px] font-mono text-[10px] uppercase tracking-wider text-muted-foreground">ID</TableHead>
                <TableHead className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">Title</TableHead>
                <TableHead className="w-[100px] font-mono text-[10px] uppercase tracking-wider text-muted-foreground">Task</TableHead>
                <TableHead className="w-[100px] font-mono text-[10px] uppercase tracking-wider text-muted-foreground">Last Run</TableHead>
                <TableHead className="w-[72px] text-right pr-4 font-mono text-[10px] uppercase tracking-wider text-muted-foreground">Issues</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {ctx.uatItems.map((item) => {
                const sc = statusConfig[item.status];
                const isSelected = item.id === selectedId;
                const isChecked = selectedIds.has(item.id);

                return (
                  <TableRow
                    key={item.id}
                    className={cn(
                      "cursor-pointer transition-colors group",
                      isSelected ? "bg-accent/50" : "hover:bg-muted/30",
                      isChecked && "bg-accent/30"
                    )}
                    onClick={() => setSelectedId(item.id)}
                  >
                    <TableCell className="pl-4 w-[40px]" onClick={(e) => e.stopPropagation()}>
                      <Checkbox 
                        checked={isChecked}
                        onCheckedChange={(checked) => toggleRow(item.id, checked)}
                      />
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline" className={cn("gap-1 text-[10px] py-0 px-1.5", sc.color)}>
                        {sc.icon}
                        {sc.label}
                      </Badge>
                    </TableCell>
                    <TableCell className="font-mono text-xs text-muted-foreground">{item.id}</TableCell>
                    <TableCell className="text-xs truncate max-w-[260px]">{item.taskName}</TableCell>
                    <TableCell className="font-mono text-xs text-muted-foreground">{item.taskId}</TableCell>
                    <TableCell className="font-mono text-[11px] text-muted-foreground">
                      {item.lastRun ? (
                        <span className={cn(item.lastRun.status === "pass" ? "text-green-400" : "text-red-400")}>
                          {item.lastRun.timeAgo}
                        </span>
                      ) : (
                        <span className="text-muted-foreground/40">—</span>
                      )}
                    </TableCell>
                    <TableCell className="text-right pr-4">
                      {item.linkedIssueIds.length > 0 ? (
                        <span className="font-mono text-xs text-red-400">{item.linkedIssueIds.length}</span>
                      ) : (
                        <span className="font-mono text-xs text-muted-foreground/40">0</span>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      </div>

      {/* ── Inspector Panel ─────────────────────── */}
      <div className="w-[340px] shrink-0 flex flex-col overflow-hidden bg-card/30 border-l border-border">
        {selected ? (
          <div className="flex-1 overflow-auto p-4 space-y-5">
            {/* Header */}
            <div>
              <div className="flex items-center gap-2 mb-1">
                <Badge variant="outline" className={cn("gap-1 text-[10px] py-0 px-1.5", statusConfig[selected.status].color)}>
                  {statusConfig[selected.status].icon}
                  {statusConfig[selected.status].label}
                </Badge>
                <span className="font-mono text-[10px] text-muted-foreground">{selected.id}</span>
              </div>
              <h3 className="text-sm font-medium mt-2 leading-snug">{selected.taskName}</h3>
              <p className="font-mono text-[10px] text-muted-foreground mt-1">{selected.taskId} · {selected.phaseTitle}</p>
            </div>

            {/* Acceptance Criteria */}
            <section>
              <h4 className="text-[10px] uppercase tracking-wider text-muted-foreground font-medium mb-2">Acceptance Criteria</h4>
              {selected.acceptanceCriteria.length > 0 ? (
                <ul className="space-y-1.5">
                  {selected.acceptanceCriteria.map((ac, i) => (
                    <li key={i} className="text-xs text-foreground/80 flex items-start gap-2">
                      <CheckCircle2 className="h-3 w-3 mt-0.5 text-muted-foreground/50 shrink-0" />
                      {ac}
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-xs text-muted-foreground/50 italic">None defined</p>
              )}
            </section>

            {/* Verification Checks */}
            <section>
              <h4 className="text-[10px] uppercase tracking-wider text-muted-foreground font-medium mb-2">
                Checks <span className="text-muted-foreground/50">({selected.checksPassed}/{selected.checksTotal})</span>
              </h4>
              {selected.checks.length > 0 ? (
                <div className="space-y-1">
                  {selected.checks.map((chk) => (
                    <div key={chk.id} className="flex items-center gap-2 text-xs py-1 px-2 rounded bg-muted/30">
                      <div className={cn(
                        "h-1.5 w-1.5 rounded-full shrink-0",
                        chk.result === "pass" ? "bg-green-400" : chk.result === "fail" ? "bg-red-400" : "bg-yellow-400"
                      )} />
                      <span className="font-mono text-[11px] truncate text-foreground/70">{chk.command}</span>
                      <Badge variant="outline" className="ml-auto text-[9px] py-0 px-1">{chk.type}</Badge>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-xs text-muted-foreground/50 italic">No checks configured</p>
              )}
            </section>

            {/* File Scope */}
            <section>
              <h4 className="text-[10px] uppercase tracking-wider text-muted-foreground font-medium mb-2">File Scope</h4>
              {selected.fileScope.length > 0 ? (
                <div className="space-y-0.5">
                  {selected.fileScope.map((f, i) => (
                    <p key={i} className="font-mono text-[11px] text-muted-foreground truncate">{f}</p>
                  ))}
                </div>
              ) : (
                <p className="text-xs text-muted-foreground/50 italic">No files scoped</p>
              )}
            </section>

            {/* Linked Issues */}
            <section>
              <h4 className="text-[10px] uppercase tracking-wider text-muted-foreground font-medium mb-2 flex items-center justify-between">
                <span>Linked Issues <span className="text-red-400">({selected.linkedIssues.length})</span></span>
              </h4>
              <div className="space-y-1">
                {selected.linkedIssues.length > 0 ? (
                  selected.linkedIssues.map((iss) => (
                    <div key={iss.id} className="flex items-center gap-2 text-xs py-1 px-2 rounded bg-muted/30">
                      <Bug className="h-3 w-3 text-red-400 shrink-0" />
                      <span className="font-mono text-[11px] text-muted-foreground">{iss.id}</span>
                      <span className="truncate text-foreground/70">{iss.description}</span>
                    </div>
                  ))
                ) : (
                  <div className="text-center p-2 border border-dashed border-border rounded opacity-50">
                    <p className="text-[10px] text-muted-foreground">No linked issues</p>
                  </div>
                )}
              </div>
            </section>

            {/* Actions */}
            <div className="flex gap-2 pt-2">
              <Button size="sm" variant="outline" className="flex-1 text-xs font-mono gap-1.5">
                <Play className="h-3 w-3" /> Run
              </Button>
              <Button size="sm" variant="outline" className="flex-1 text-xs font-mono gap-1.5">
                <ExternalLink className="h-3 w-3" /> Open
              </Button>
            </div>
            
             {/* Secondary Actions */}
             <div className="flex gap-2">
               <Button size="sm" variant="ghost" className="flex-1 text-xs font-mono gap-1.5 text-muted-foreground">
                 <LinkIcon className="h-3 w-3" /> Link Task
               </Button>
               <Button size="sm" variant="ghost" className="flex-1 text-xs font-mono gap-1.5 text-muted-foreground">
                 <Bug className="h-3 w-3" /> Create Issue
               </Button>
             </div>
          </div>
        ) : (
          <div className="flex-1 flex items-center justify-center text-muted-foreground/40">
            <div className="text-center">
              <Shield className="h-8 w-8 mx-auto mb-2 opacity-40" />
              <p className="text-xs font-mono">Select a UAT item</p>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}