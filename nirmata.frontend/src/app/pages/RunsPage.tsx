import {
  Activity,
} from "lucide-react";
import { useParams } from "react-router";
import { RunsDashboard } from "../components/dashboards/RunsDashboard";
import { DefaultFileViewer } from "../components/viewers/DefaultFileViewer";
import { useRuns, useFileSystem, type Run } from "../hooks/useAosData";

// ── Helpers ──────────────────────────────────────────────────────

function getRunFileContent(relativePath: string, allRuns: Run[]): string | null {
  const parts = relativePath.split("/");
  const runId = parts[0];
  const fileName = parts.slice(1).join("/");
  const run = allRuns.find((r) => r.id === runId);

  if (!run) return null;

  if (fileName === "run.json" || fileName === "") {
    return JSON.stringify(run, null, 2);
  }

  if (fileName === "commands.ndjson") {
    return run.logs
      .map((l) =>
        JSON.stringify({ ts: new Date().toISOString(), cmd: l })
      )
      .join("\n");
  }

  if (fileName.endsWith(".log")) {
    return `[${run.startTime}] Execution started\n[${run.startTime}] Command: ${run.command}\n[${run.startTime}] Status: ${run.status}\n${run.logs.map((l) => `[log] ${l}`).join("\n")}\n[${run.endTime || "..."}] Execution ${run.status}`;
  }

  return JSON.stringify(
    { file: fileName, runId: run.id, type: "artifact" },
    null,
    2
  );
}

// ── Run Detail View ──────────────────────────────────────────────

function RunDetailView({ run }: { run: Run }) {
  return (
    <div className="flex-1 flex flex-col overflow-hidden bg-background">
      <div className="flex-1 overflow-hidden">
        <DefaultFileViewer
          node={{
            id: `run-${run.id}`,
            name: "run.json",
            type: "file" as const,
            status: run.status === "success" ? "valid" : run.status === "failed" ? "error" : "warning",
            size: `${(JSON.stringify(run).length / 1024).toFixed(2)} KB`
          } as any}
          path={`.aos/evidence/runs/${run.id}/run.json`}
          content={JSON.stringify(run, null, 2)}
        />
      </div>
    </div>
  );
}

// ── Page Component ───────────────────────────────────────────────

export function RunsPage() {
  const params = useParams();
  const relativePath = params["*"] || "";

  // All hooks at the top — unconditional
  const { runs: allRuns } = useRuns();
  const { findNode } = useFileSystem();

  // Dashboard view
  if (relativePath === "") {
    return <RunsDashboard />;
  }

  // Run detail: check if this is a run directory or specific file
  const runId = relativePath.split("/")[0];
  const run = allRuns.find((r) => r.id === runId);
  const fileName = relativePath.split("/").slice(1).join("/");

  // If navigating to a specific non-standard file, use DefaultFileViewer
  if (
    run &&
    fileName &&
    fileName !== "run.json" &&
    fileName !== ""
  ) {
    const fullPath = `.aos/evidence/runs/${relativePath}`;
    const fileNode = findNode(
      fullPath.split("/").filter(Boolean)
    );
    const content = getRunFileContent(relativePath, allRuns);

    if (fileNode && fileNode.type === "file") {
      return (
        <div className="flex-1 flex flex-col overflow-hidden bg-background">
          <DefaultFileViewer
            node={fileNode}
            path={fullPath}
            content={
              content ||
              JSON.stringify({ path: fullPath }, null, 2)
            }
          />
        </div>
      );
    }
  }

  // Run detail view (run directory or run.json)
  if (run) {
    return <RunDetailView run={run} />;
  }

  // Fallback
  return (
    <div className="flex-1 flex flex-col items-center justify-center text-muted-foreground p-8">
      <Activity className="h-12 w-12 mb-4 opacity-20" />
      <p className="text-sm font-mono">
        Path not found: {relativePath}
      </p>
    </div>
  );
}

export default RunsPage;