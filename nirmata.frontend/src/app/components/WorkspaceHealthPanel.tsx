import { useState } from "react";
import { CheckCircle, AlertCircle, AlertTriangle, RefreshCw, Shield } from "lucide-react";
import { Button } from "./ui/button";
import { Badge } from "./ui/badge";
import { cn } from "./ui/utils";
import { toast } from "sonner";
import { useWorkspace, useWorkspaceInit } from "../hooks/useAosData";
import type { ValidationResult } from "../hooks/useAosData";

interface WorkspaceHealthPanelProps {
  workspaceId: string | undefined;
  /** When true, renders as a compact inline card instead of a full section */
  compact?: boolean;
}

const CHECKS: { key: keyof ValidationResult; label: string; description: string }[] = [
  { key: "schemas",  label: "Schemas",  description: "JSON schema contracts under .aos/schemas/" },
  { key: "spec",     label: "Spec",     description: "Project, roadmap, tasks and phases" },
  { key: "state",    label: "State",    description: "Operational cursor and event log" },
  { key: "evidence", label: "Evidence", description: "Run records and task evidence" },
  { key: "codebase", label: "Codebase", description: "Codebase intelligence pack" },
];

function StatusChip({ status }: { status: "valid" | "invalid" | "warning" }) {
  if (status === "valid")
    return (
      <Badge className="text-[10px] bg-green-500/10 text-green-400 border border-green-500/20 hover:bg-green-500/10">
        valid
      </Badge>
    );
  if (status === "invalid")
    return (
      <Badge className="text-[10px] bg-red-500/10 text-red-400 border border-red-500/20 hover:bg-red-500/10">
        invalid
      </Badge>
    );
  return (
    <Badge className="text-[10px] bg-amber-500/10 text-amber-400 border border-amber-500/20 hover:bg-amber-500/10">
      warning
    </Badge>
  );
}

export function WorkspaceHealthPanel({ workspaceId, compact = false }: WorkspaceHealthPanelProps) {
  const { workspace } = useWorkspace(workspaceId);
  const { validate, isValidating, validationResult } = useWorkspaceInit(workspaceId);

  // Use live result if available, fall back to workspace.validation
  const current: ValidationResult = validationResult ?? workspace.validation;

  const overallOk  = CHECKS.every((c)  => current[c.key] === "valid");
  const hasError   = CHECKS.some((c)   => current[c.key] === "invalid");
  const hasWarning = CHECKS.some((c)   => current[c.key] === "warning");

  const [_lastRan, _setLastRan] = useState<Date | null>(null);

  const handleValidate = async () => {
    const result = await validate();
    _setLastRan(new Date());
    if (result.schemas === "invalid" || result.spec === "invalid") {
      toast.error("Validation failed — see highlighted layers");
    } else {
      toast.success("Workspace validated successfully");
    }
  };

  const SummaryRow = (
    <div className="flex items-center gap-2">
      {isValidating ? (
        <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
          <RefreshCw className="h-3.5 w-3.5 animate-spin" />
          Validating…
        </span>
      ) : overallOk ? (
        <span className="flex items-center gap-1.5 text-xs text-green-400">
          <CheckCircle className="h-3.5 w-3.5" />
          All layers valid
        </span>
      ) : hasError ? (
        <span className="flex items-center gap-1.5 text-xs text-red-400">
          <AlertCircle className="h-3.5 w-3.5" />
          Validation errors detected
        </span>
      ) : hasWarning ? (
        <span className="flex items-center gap-1.5 text-xs text-amber-400">
          <AlertTriangle className="h-3.5 w-3.5" />
          Warnings present
        </span>
      ) : null}
    </div>
  );

  const RunButton = (
    <Button
      variant="outline"
      size="sm"
      className="gap-2"
      disabled={isValidating}
      onClick={handleValidate}
    >
      <RefreshCw className={cn("h-3 w-3", isValidating && "animate-spin")} />
      {isValidating ? "Validating…" : "Run aos validate"}
    </Button>
  );

  if (compact) {
    return (
      <div className="border border-border/40 rounded-lg p-3 flex items-center justify-between gap-3">
        <div className="flex items-center gap-2 min-w-0">
          <Shield className="h-3.5 w-3.5 text-primary/60 shrink-0" />
          <span className="text-xs text-muted-foreground shrink-0">Health</span>
          {SummaryRow}
        </div>
        {RunButton}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Section header */}
      <div className="flex items-start gap-3 pb-3 border-b border-border/40">
        <div className="h-8 w-8 rounded-md bg-primary/10 flex items-center justify-center shrink-0 mt-0.5">
          <Shield className="h-4 w-4 text-primary" />
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-sm">Workspace Health</p>
          <p className="text-xs text-muted-foreground mt-0.5">
            Runs <code className="font-mono text-[11px]">aos validate</code> across all workspace layers. Fix issues before starting a run.
          </p>
        </div>
      </div>

      {/* Summary row */}
      <div className="px-1">{SummaryRow}</div>

      {/* Check rows */}
      <div className="space-y-1">
        {CHECKS.map((c) => (
          <div
            key={c.key}
            className="flex items-center justify-between gap-3 px-3 py-2.5 rounded-md border border-border/30 bg-muted/5"
          >
            <div className="min-w-0 flex-1">
              <p className="text-sm text-foreground/80">{c.label}</p>
              <p className="text-xs text-muted-foreground/50 mt-0.5">{c.description}</p>
            </div>
            <StatusChip status={current[c.key] as "valid" | "invalid" | "warning"} />
          </div>
        ))}
      </div>

      {/* Footer action */}
      <div className="flex items-center gap-3 pt-1">
        {RunButton}
      </div>
    </div>
  );
}
