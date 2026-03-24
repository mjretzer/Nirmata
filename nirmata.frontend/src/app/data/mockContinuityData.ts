// Shared type definitions for AOS continuity/state layer.
// Runtime mock values have been removed — all data is now sourced from the backend via useAosData.ts hooks.

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
