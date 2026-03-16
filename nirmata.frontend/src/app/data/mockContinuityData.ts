import { Workspace, Task, Run, Checkpoint, Issue } from "./mockData";

export interface HandoffSnapshot {
  timestamp: string;
  cursor: {
    milestone: string;
    phase: string;
    task: string;
    step: string;
  };
  inFlight: {
    task: string;
    step: string;
    files: string[];
  };
  nextCommand: string;
  integrity: {
    matchesCursor: boolean;
    diffSummary?: string;
  };
}

export interface ContextPack {
  id: string;
  name: string;
  size: string;
  mode: "plan" | "execute" | "verify" | "fix";
  created: string;
  runId?: string;
  artifacts: string[];
}

export interface Event {
  id: string;
  timestamp: string;
  type: "work.paused" | "work.resumed" | "task.resumed" | "phase.planned" | "uat.completed" | "history.written" | "task.completed" | "task.failed";
  summary: string;
  references: string[];
}

export interface ContinuityState {
  handoff: HandoffSnapshot | null;
  events: Event[];
  packs: ContextPack[];
  nextCommand: {
    command: string;
    reason: string;
  };
}

export const mockHandoff: HandoffSnapshot = {
  timestamp: "2026-02-22T15:30:00Z",
  cursor: {
    milestone: "MS-001",
    phase: "PH-0002",
    task: "TSK-0012",
    step: "2/3",
  },
  inFlight: {
    task: "TSK-0012",
    step: "Build login UI components",
    files: ["src/app/components/auth/LoginForm.tsx", "src/app/components/auth/AuthLayout.tsx"],
  },
  nextCommand: "aos execute-plan TSK-0012 --step 2",
  integrity: {
    matchesCursor: true,
  },
};

export const mockEvents: Event[] = [
  {
    id: "EVT-105",
    timestamp: "2026-02-22T15:30:00Z",
    type: "work.paused",
    summary: "Paused work during OAuth integration",
    references: [".aos/state/handoff.json"],
  },
  {
    id: "EVT-104",
    timestamp: "2026-02-22T14:35:18Z",
    type: "task.completed",
    summary: "Completed TSK-0012 Step 1",
    references: ["TSK-0012", "RUN-20260222-143022"],
  },
  {
    id: "EVT-103",
    timestamp: "2026-02-22T14:30:22Z",
    type: "task.resumed",
    summary: "Started TSK-0012",
    references: ["TSK-0012"],
  },
  {
    id: "EVT-102",
    timestamp: "2026-02-22T11:24:11Z",
    type: "history.written",
    summary: "Updated summary.md with recent changes",
    references: [".aos/spec/summary.md"],
  },
  {
    id: "EVT-101",
    timestamp: "2026-02-22T11:20:33Z",
    type: "phase.planned",
    summary: "Planned Phase PH-0002",
    references: ["PH-0002"],
  },
];

export const mockPacks: ContextPack[] = [
  {
    id: "CP-TSK-0012-EXEC",
    name: "TSK-0012 Execution Context",
    size: "12KB",
    mode: "execute",
    created: "2026-02-22T14:30:22Z",
    runId: "RUN-20260222-143022",
    artifacts: ["src/auth/OAuthProvider.ts", "src/components/LoginButton.tsx"],
  },
  {
    id: "CP-PH-0002-PLAN",
    name: "PH-0002 Planning Context",
    size: "45KB",
    mode: "plan",
    created: "2026-02-22T11:15:00Z",
    artifacts: [".aos/spec/roadmap.json", ".aos/spec/requirements.md"],
  },
];

export const mockContinuityState: ContinuityState = {
  handoff: mockHandoff,
  events: mockEvents,
  packs: mockPacks,
  nextCommand: {
    command: "aos execute-plan TSK-0012 --resume",
    reason: "Task TSK-0012 is in progress and was paused.",
  },
};
