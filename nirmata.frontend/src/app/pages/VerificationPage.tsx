import { useState, useEffect } from "react";
import { useParams, useNavigate, useLocation } from "react-router";
import { Shield, Wrench } from "lucide-react";
import { cn } from "../components/ui/utils";
import { FixKanbanView } from "../components/verification/FixKanbanView";
import { PhaseUATViewer } from "../components/verification/PhaseUATViewer";
import type { CreateIssueFromUAT } from "../components/verification/PhaseUATViewer";
import { DefaultFileViewer } from "../components/viewers/DefaultFileViewer";
import { useTasks, useTaskPlans, useIssues, useWorkspace, useFileSystem, type Task, type TaskPlan, type Issue } from "../hooks/useAosData";

// ── Synthetic file content generators ──────────────────────────

function getUATFileContent(relativePath: string, allTasks: Task[], allTaskPlans: TaskPlan[], allIssues: Issue[]): string | null {
  const fileName = relativePath.replace(/\/$/, "");
  if (fileName === "index.json") {
    return JSON.stringify({
      "$schema": "../../schemas/uat.schema.json",
      entries: allTasks.map((t) => t.id.replace("TSK", "UAT")),
      count: allTasks.length,
    }, null, 2);
  }
  const uatMatch = fileName.match(/^(UAT-\d+)\.json$/);
  if (uatMatch) {
    const taskId = uatMatch[1].replace("UAT", "TSK");
    const task = allTasks.find((t) => t.id === taskId);
    const plan = allTaskPlans.find((p) => p.taskId === taskId);
    if (task) {
      return JSON.stringify({
        "$schema": "../../schemas/uat.schema.json",
        id: uatMatch[1],
        taskId: task.id,
        taskName: task.name,
        phaseId: task.phaseId,
        status: task.status,
        verification: plan?.verification || [],
        definitionOfDone: plan?.definitionOfDone || [],
        linkedIssues: allIssues.filter((i) => i.linkedTasks.includes(task.id)).map((i) => i.id),
      }, null, 2);
    }
  }
  return null;
}

function getIssueFileContent(relativePath: string, allIssues: Issue[]): string | null {
  const fileName = relativePath.replace(/\/$/, "");
  if (fileName === "index.json") {
    return JSON.stringify({
      "$schema": "../../schemas/issue.schema.json",
      issues: allIssues.map((i) => i.id),
      count: allIssues.length,
    }, null, 2);
  }
  const issueMatch = fileName.match(/^(ISS-\d+)\.json$/);
  if (issueMatch) {
    const issue = allIssues.find((i) => i.id === issueMatch[1]);
    if (issue) return JSON.stringify(issue, null, 2);
  }
  return null;
}

// ── View Mode Toggle ───────────────────────────────────────────

type ViewMode = "uat" | "fix";

function ModeToggle({ view, onChange }: { view: ViewMode; onChange: (v: ViewMode) => void }) {
  return (
    <div className="relative flex items-center w-[260px] h-9 bg-muted/50 rounded-full p-1 border border-border">
      <div
        className={cn(
          "absolute top-1 bottom-1 rounded-full bg-foreground transition-all duration-200 ease-out",
          view === "uat" ? "left-1 w-[calc(50%-4px)]" : "left-[calc(50%+2px)] w-[calc(50%-4px)]"
        )}
      />
      <button
        onClick={() => onChange("uat")}
        className={cn(
          "relative z-10 flex-1 flex items-center justify-center gap-1.5 h-full rounded-full text-[11px] font-mono font-medium tracking-wide transition-colors cursor-pointer select-none",
          view === "uat" ? "text-background" : "text-muted-foreground hover:text-foreground"
        )}
      >
        <Shield className="h-3 w-3" />
        UAT
      </button>
      <button
        onClick={() => onChange("fix")}
        className={cn(
          "relative z-10 flex-1 flex items-center justify-center gap-1.5 h-full rounded-full text-[11px] font-mono font-medium tracking-wide transition-colors cursor-pointer select-none",
          view === "fix" ? "text-background" : "text-muted-foreground hover:text-foreground"
        )}
      >
        <Wrench className="h-3 w-3" />
        Fix Loop
      </button>
    </div>
  );
}

// ── Page Component ─────────────────────────────────────────────

export function VerificationPage() {
  const params = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const { workspace } = useWorkspace();
  const { tasks: allTasks } = useTasks();
  const { plans: allTaskPlans } = useTaskPlans();
  const { issues: allIssues } = useIssues();
  const { findNode } = useFileSystem();
  const wsId = workspace.projectName;
  const [view, setView] = useState<ViewMode>("uat");
  const [issueSeed, setIssueSeed] = useState<CreateIssueFromUAT | null>(null);
  const relativePath = params["*"] || "";

  // Sync view state when the URL changes (e.g. explorer folder click)
  useEffect(() => {
    const newView = location.pathname.includes("/.aos/spec/issues") ? "fix" : "uat";
    setView(newView);
  }, [location.pathname]);

  // Navigate to the matching folder URL so the file explorer stays in sync
  const handleViewChange = (newView: ViewMode) => {
    setView(newView);
    const targetPath = newView === "fix" ? ".aos/spec/issues" : ".aos/spec/uat";
    navigate(`/ws/${wsId}/files/${targetPath}`, { replace: true });
  };

  // ── File view routing (preserves file explorer navigation) ──
  if (relativePath !== "") {
    const pathname = window.location.pathname;
    const isIssuePath = pathname.includes("/.aos/spec/issues/");

    let fullPath: string;
    let content: string | null;

    if (isIssuePath) {
      fullPath = `.aos/spec/issues/${relativePath}`;
      content = getIssueFileContent(relativePath, allIssues);
    } else {
      fullPath = `.aos/spec/uat/${relativePath}`;
      content = getUATFileContent(relativePath, allTasks, allTaskPlans, allIssues);
    }

    const fileNode = findNode(fullPath.split("/").filter(Boolean));

    if (fileNode && fileNode.type === "file") {
      return (
        <div className="flex-1 flex flex-col overflow-hidden bg-background">
          <DefaultFileViewer node={fileNode} path={fullPath} content={content || JSON.stringify({ path: fullPath }, null, 2)} />
        </div>
      );
    }

    if (content) {
      const fileName = relativePath.split("/").pop() || relativePath;
      return (
        <div className="flex-1 flex flex-col overflow-hidden bg-background">
          <DefaultFileViewer node={{ id: `vf-${fileName}`, name: fileName, type: "file" }} path={fullPath} content={content} />
        </div>
      );
    }

    return (
      <div className="flex-1 flex flex-col items-center justify-center text-muted-foreground p-8">
        <Shield className="h-12 w-12 mb-4 opacity-20" />
        <p className="text-sm font-mono">Path not found: {relativePath}</p>
      </div>
    );
  }

  // ── Dashboard view ──
  return (
    <div className="flex-1 flex flex-col overflow-hidden bg-background">
      {/* ── Sub-toolbar: view mode toggle ── */}
      <div className="shrink-0 border-b border-border/50 px-6 py-2.5 flex items-center gap-4">
        <ModeToggle view={view} onChange={handleViewChange} />
      </div>

      {/* ── Content area ────────────────────────── */}
      {view === "uat" ? (
        <PhaseUATViewer
          onCreateIssue={(seed) => {
            setIssueSeed(seed);
            handleViewChange("fix");
          }}
        />
      ) : (
        <FixKanbanView
          issueSeed={issueSeed}
          onSeedConsumed={() => setIssueSeed(null)}
        />
      )}
    </div>
  );
}

export default VerificationPage;