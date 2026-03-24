// Shared type definitions for AOS workspace state.
// Runtime mock values have been removed — all data is now sourced from the backend via useAosData.ts hooks.

export interface Workspace {
  repoRoot: string;
  projectName: string;
  hasAosDir: boolean;
  hasProjectSpec: boolean;
  hasRoadmap: boolean;
  hasTaskPlans: boolean;
  hasHandoff: boolean;
  cursor: {
    milestone: string;
    phase: string;
    task: string | null;
  };
  lastRun: {
    id: string;
    status: "success" | "failed" | "running";
    timestamp: string;
  };
  validation: {
    schemas: "valid" | "invalid";
    spec: "valid" | "invalid";
    state: "valid" | "invalid" | "warning";
    evidence: "valid" | "invalid";
    codebase: "valid" | "invalid";
  };
  lastValidationAt: string;
  hasStarterRoadmap?: boolean;
  openIssuesCount: number;
  openTodosCount: number;
}

export interface Phase {
  id: string;
  milestoneId: string;
  title: string;
  summary: string;
  order: number;
  status: "planned" | "in-progress" | "completed";

  brief: {
    goal: string;
    priorities: string[];
    nonGoals: string[];
    constraints: string[];
    dependencies: { type: string; ref: string; reason: string }[];
  };

  deliverables: {
    id: string;
    title: string;
    description: string;
  }[];

  acceptance: {
    criteria: string[];
    uatChecklist: string[];
  };

  links: {
    roadmapPhaseRef: string;
    tasks: string[];
    artifacts: { kind: "phase-file" | "phase-assumptions" | "phase-research" | "documentation" | "pipeline", path: string }[];
  };

  metadata: {
    tags: string[];
    owner: string;
    createdAt: string;
    updatedAt: string;
  };
}

export interface Task {
  id: string;
  phaseId: string;
  milestone: string;
  name: string;
  status: "planned" | "in-progress" | "completed" | "failed";
  assignedTo: string;
  plan?: {
    fileScope: string[];
    steps: string[];
    verification: string[];
  };
  commitHash?: string;
}

export interface Run {
  id: string;
  taskId: string | null;
  command: string;
  status: "success" | "failed" | "running";
  startTime: string;
  endTime?: string;
  artifacts: string[];
  logs: string[];
  changedFiles: string[];
}

export interface Issue {
  id: string;
  severity: "critical" | "high" | "medium" | "low";
  scope: string;
  description: string;
  repro: string[];
  linkedTasks: string[];
  status: "open" | "in-progress" | "resolved";
  impactedArea?: string;
  impactedFiles?: string[];
  expectedVsActual?: {
    expected: string;
    actual: string;
  };
  links?: {
    runId?: string;
    checklistItemId?: string;
    taskId?: string;
    phaseId?: string;
  };
  tags?: string[];
  createdFromUAT?: boolean;
  history?: {
      timestamp: string;
      event: string;
      details?: string;
  }[];
}

export interface Checkpoint {
  id: string;
  timestamp: string;
  cursor: Workspace["cursor"];
  description: string;
  source?: "manual" | "auto";
}

export interface Milestone {
  id: string;
  name: string;
  description: string;
  phases: string[];
  status: "planned" | "in-progress" | "completed";
  targetDate: string;
  definitionOfDone: string[];
}

export interface ProjectSpec {
  name: string;
  description: string;
  version: string;
  owner: string;
  repo: string;
  milestones: string[];
  createdAt: string;
  updatedAt: string;
  tags: string[];
  constraints: string[];
}

export interface TaskPlan {
  taskId: string;
  fileScope: string[];
  steps: { order: number; description: string; done: boolean }[];
  verification: { command: string; type: "automated" | "manual"; passed?: boolean }[];
  definitionOfDone: string[];
}

export interface WorkspaceSummary extends Workspace {
  id: string;
  alias?: string;
  pinned: boolean;
  isGitRepo: boolean;
  gitRemote?: string;
  gitBranch?: string;
  aheadCount?: number;
  behindCount?: number;
  gitLastSync?: string;
  status: "healthy" | "needs-init" | "invalid" | "repair-needed" | "missing-path";
  lastScanned: string;
  lastOpened: string;
}
