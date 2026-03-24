import { Map } from "lucide-react";
import { RoadmapTimeline } from "../components/plan/RoadmapTimeline";
import { PhaseTaskList } from "../components/plan/PhaseTaskList";
import { DefaultFileViewer } from "../components/viewers/DefaultFileViewer";
import { useNavigate, useParams } from "react-router";

import { usePhases, useWorkspace, useTasks, useMilestones, useProjectSpec, useTaskPlans, useFileSystem } from "../hooks/useAosData";
import type { Phase, Task, Milestone, ProjectSpec, TaskPlan } from "../hooks/useAosData";

// Generate content for any spec file based on path — pure function, no hooks
function getSpecFileContent(
  relativePath: string,
  spec: ProjectSpec,
  milestones: Milestone[],
  phases: Phase[],
  tasks: Task[],
  taskPlans: TaskPlan[],
): string | null {
  if (relativePath === "project.json") {
    return JSON.stringify(spec, null, 2);
  }

  if (relativePath === "roadmap.json") {
    return JSON.stringify({
      "$schema": "../schemas/roadmap.schema.json",
      projectName: spec.name,
      milestones: milestones.map(m => ({
        id: m.id, name: m.name, status: m.status, targetDate: m.targetDate, phases: m.phases
      })),
      phases: phases.map(p => ({
        id: p.id, title: p.title, status: p.status, order: p.order, milestoneId: p.milestoneId
      }))
    }, null, 2);
  }

  if (relativePath === "milestones/index.json") {
    return JSON.stringify({
      "$schema": "../../schemas/milestone.schema.json",
      milestones: milestones.map(m => m.id),
      count: milestones.length
    }, null, 2);
  }

  const msMatch = relativePath.match(/^milestones\/(MS-\d+)\/milestone\.json$/);
  if (msMatch) {
    const ms = milestones.find(m => m.id === msMatch[1]);
    if (ms) return JSON.stringify(ms, null, 2);
  }

  if (relativePath === "phases/index.json") {
    return JSON.stringify({
      "$schema": "../../schemas/phase.schema.json",
      phases: phases.map(p => p.id),
      count: phases.length
    }, null, 2);
  }

  const phPhaseMatch = relativePath.match(/^phases\/(PH-\d+)\/phase\.json$/);
  if (phPhaseMatch) {
    const phase = phases.find(p => p.id === phPhaseMatch[1]);
    if (phase) return JSON.stringify(phase, null, 2);
  }

  const phAssumptionsMatch = relativePath.match(/^phases\/(PH-\d+)\/assumptions\.json$/);
  if (phAssumptionsMatch) {
    const phase = phases.find(p => p.id === phAssumptionsMatch[1]);
    if (phase) {
      return JSON.stringify({
        "$schema": "../../../schemas/phase.schema.json",
        phaseId: phase.id,
        assumptions: phase.brief?.constraints || ["No external API dependencies", "Mock data is sufficient for demo"],
        openQuestions: ["Production deployment timeline?", "Auth provider decision pending"],
        capturedAt: phase.metadata?.updatedAt || new Date().toISOString()
      }, null, 2);
    }
  }

  const phResearchMatch = relativePath.match(/^phases\/(PH-\d+)\/research\.json$/);
  if (phResearchMatch) {
    const phase = phases.find(p => p.id === phResearchMatch[1]);
    if (phase) {
      return JSON.stringify({
        "$schema": "../../../schemas/phase.schema.json",
        phaseId: phase.id,
        notes: ["Evaluated Tailwind v4 migration path", "Confirmed Radix UI compatibility with React 19"],
        capturedAt: phase.metadata?.updatedAt || new Date().toISOString()
      }, null, 2);
    }
  }

  if (relativePath === "tasks/index.json") {
    return JSON.stringify({
      "$schema": "../../schemas/task.schema.json",
      tasks: tasks.map(t => t.id),
      count: tasks.length
    }, null, 2);
  }

  const taskMatch = relativePath.match(/^tasks\/(TSK-\d+)\/task\.json$/);
  if (taskMatch) {
    const task = tasks.find(t => t.id === taskMatch[1]);
    if (task) return JSON.stringify(task, null, 2);
  }

  const planMatch = relativePath.match(/^tasks\/(TSK-\d+)\/plan\.json$/);
  if (planMatch) {
    const plan = taskPlans.find(p => p.taskId === planMatch[1]);
    if (plan) return JSON.stringify(plan, null, 2);
  }

  const uatMatch = relativePath.match(/^tasks\/(TSK-\d+)\/uat\.json$/);
  if (uatMatch) {
    const plan = taskPlans.find(p => p.taskId === uatMatch[1]);
    return JSON.stringify({
      "$schema": "../../../schemas/uat.schema.json",
      taskId: uatMatch[1],
      verifications: plan?.verification || [],
      definitionOfDone: plan?.definitionOfDone || []
    }, null, 2);
  }

  const linksMatch = relativePath.match(/^tasks\/(TSK-\d+)\/links\.json$/);
  if (linksMatch) {
    const task = tasks.find(t => t.id === linksMatch[1]);
    return JSON.stringify({
      "$schema": "../../../schemas/task.schema.json",
      taskId: linksMatch[1],
      phase: task?.phaseId || "unknown",
      milestone: task?.milestone || "unknown",
      issues: [],
      runs: []
    }, null, 2);
  }

  if (relativePath === "uat/index.json") {
    return JSON.stringify({
      "$schema": "../../schemas/uat.schema.json",
      entries: tasks.map(t => t.id.replace("TSK", "UAT")),
      count: tasks.length
    }, null, 2);
  }

  return null;
}

export function PlanPage() {
  const navigate = useNavigate();
  const params = useParams();
  const relativePath = params["*"] || "";
  const specPath = `.aos/spec/${relativePath}`;

  // All hooks at the top — unconditional
  const { workspace } = useWorkspace();
  const { phases: allPhases } = usePhases();
  const { tasks: allTasks } = useTasks();
  const { milestones: allMilestones } = useMilestones();
  const { spec } = useProjectSpec();
  const { plans: allTaskPlans } = useTaskPlans();
  const { node: fileNode } = useFileSystem(specPath);

  const ws = workspace.projectName;

  // ─── Dashboard view (empty path = directory listing) ───
  if (relativePath === "") {
    return <RoadmapTimeline />;
  }

  // ─── Phase directory → PhaseTaskList dashboard ───
  const phaseDirMatch = relativePath.match(/^phases\/(PH-\d+)\/?$/);
  if (phaseDirMatch) {
    const phase = allPhases.find(p => p.id === phaseDirMatch[1]);
    if (phase) {
      return <PhaseTaskList phase={phase} />;
    }
  }

  // ─── All files → DefaultFileViewer ───
  const content = getSpecFileContent(relativePath, spec, allMilestones, allPhases, allTasks, allTaskPlans);

  if (fileNode && fileNode.type === "file") {
    return (
      <div className="flex-1 flex flex-col overflow-hidden bg-background">
        <DefaultFileViewer
          node={fileNode}
          path={specPath}
          content={content || JSON.stringify({ path: specPath, type: "spec" }, null, 2)}
        />
      </div>
    );
  }

  // Fallback for paths that don't resolve to a known node
  if (content) {
    const fileName = relativePath.split("/").pop() || relativePath;
    const fallbackNode = { id: `spec-${fileName}`, name: fileName, type: "file" as const };
    return (
      <div className="flex-1 flex flex-col overflow-hidden bg-background">
        <DefaultFileViewer node={fallbackNode} path={specPath} content={content} />
      </div>
    );
  }

  return (
    <div className="flex-1 flex flex-col items-center justify-center text-muted-foreground p-8">
      <Map className="h-12 w-12 mb-4 opacity-20" />
      <h2 className="text-lg font-medium mb-2">Plan Console</h2>
      <p className="text-sm font-mono">Select an artifact in .aos/spec/ to open its lens.</p>
      <p className="text-xs text-muted-foreground/60 mt-1 font-mono">path: {relativePath || "(empty)"}</p>
    </div>
  );
}

export default PlanPage;