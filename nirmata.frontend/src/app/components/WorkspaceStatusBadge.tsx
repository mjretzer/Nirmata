import { Badge } from "./ui/badge";
import { cn } from "./ui/utils";
import type { WorkspaceSummary } from "../hooks/useAosData";

const statusMap: Record<WorkspaceSummary["status"], { label: string; className: string }> = {
  healthy: {
    label: "Healthy",
    className: "border-green-500/20 text-green-400 bg-green-500/10",
  },
  "needs-init": {
    label: "Needs Init",
    className: "border-orange-500/20 text-orange-400 bg-orange-500/10",
  },
  invalid: {
    label: "Invalid",
    className: "border-red-500/20 text-red-400 bg-red-500/10",
  },
  "repair-needed": {
    label: "Repair Needed",
    className: "border-yellow-500/20 text-yellow-400 bg-yellow-500/10",
  },
  "missing-path": {
    label: "Missing Path",
    className: "border-border text-muted-foreground bg-muted/30",
  },
};

export function WorkspaceStatusBadge({ status }: { status: WorkspaceSummary["status"] }) {
  const m = statusMap[status] ?? statusMap["healthy"];
  return (
    <Badge variant="outline" className={cn("text-[10px] gap-1 shrink-0", m.className)}>
      {m.label}
    </Badge>
  );
}
