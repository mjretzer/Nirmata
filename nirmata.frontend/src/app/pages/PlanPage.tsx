import { FileQuestion, Loader2 } from "lucide-react";
import { RoadmapTimeline } from "../components/plan/RoadmapTimeline";
import { PhaseTaskList } from "../components/plan/PhaseTaskList";
import { DefaultFileViewer } from "../components/viewers/DefaultFileViewer";
import { useParams } from "react-router";

import { usePhases, useFileSystem } from "../hooks/useAosData";

export function PlanPage() {
  const params = useParams();
  const relativePath = params["*"] || "";
  const specPath = `.aos/spec/${relativePath}`;

  // All hooks at the top — unconditional
  const { phases: allPhases, isLoading: phasesLoading } = usePhases();
  const { node: fileNode, isLoading: fileLoading } = useFileSystem(
    relativePath === "" ? undefined : specPath
  );

  // ─── Root: roadmap lens ───
  if (relativePath === "") {
    return <RoadmapTimeline />;
  }

  // ─── Phase directory (e.g. phases/PH-0001 or phases/PH-0001/) ───
  const phaseDirMatch = relativePath.match(/^phases\/(PH-\d+)\/?$/);
  if (phaseDirMatch) {
    const phaseId = phaseDirMatch[1];
    if (phasesLoading) {
      return (
        <div className="flex-1 flex flex-col items-center justify-center text-muted-foreground p-8">
          <Loader2 className="h-8 w-8 mb-4 opacity-30 animate-spin" />
          <p className="text-sm font-mono">Loading {relativePath}…</p>
        </div>
      );
    }
    const phase = allPhases.find(p => p.id === phaseId);
    if (phase) {
      return <PhaseTaskList phase={phase} />;
    }
    // Unknown phase → missing-artifact
    return (
      <div className="flex-1 flex flex-col items-center justify-center text-muted-foreground p-8">
        <FileQuestion className="h-12 w-12 mb-4 opacity-20" />
        <h2 className="text-lg font-medium mb-2">Phase Not Found</h2>
        <p className="text-sm font-mono">No phase artifact at .aos/spec/{relativePath}</p>
        <p className="text-xs text-muted-foreground/60 mt-1 font-mono">path: {relativePath}</p>
      </div>
    );
  }

  // ─── Loading state for file artifact reads ───
  if (fileLoading) {
    return (
      <div className="flex-1 flex flex-col items-center justify-center text-muted-foreground p-8">
        <Loader2 className="h-8 w-8 mb-4 opacity-30 animate-spin" />
        <p className="text-sm font-mono">Loading {relativePath}…</p>
      </div>
    );
  }

  // ─── Workspace-backed file artifact viewer ───
  if (fileNode && fileNode.type === "file") {
    return (
      <div className="flex-1 flex flex-col overflow-hidden bg-background">
        <DefaultFileViewer node={fileNode} path={specPath} />
      </div>
    );
  }

  // ─── Missing artifact (path not found or fetch failed) ───
  return (
    <div className="flex-1 flex flex-col items-center justify-center text-muted-foreground p-8">
      <FileQuestion className="h-12 w-12 mb-4 opacity-20" />
      <h2 className="text-lg font-medium mb-2">Artifact Not Found</h2>
      <p className="text-sm font-mono">No artifact at .aos/spec/{relativePath}</p>
      <p className="text-xs text-muted-foreground/60 mt-1 font-mono">path: {relativePath}</p>
    </div>
  );
}

export default PlanPage;