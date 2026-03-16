/**
 * Mock data for the Orchestrator gate system.
 *
 * Types represent the domain model for gate checks, timeline steps,
 * and orchestrator messages. Static presets provide demo scenarios
 * for the "blocked" and "runnable" gate states.
 */

// ── Gate model ────────────────────────────────────────────────

export type GateKind = "dependency" | "uat" | "evidence";
export type GateCheckStatus = "pass" | "fail" | "warn";

export interface GateCheck {
  id: string;
  kind: GateKind;
  label: string;
  detail: string;
  status: GateCheckStatus;
  /** Clickable action label, e.g. "View task" */
  actionLabel?: string;
}

export interface NextTaskGate {
  taskId: string;
  taskName: string;
  phaseId: string;
  phaseTitle: string;
  runnable: boolean;
  checks: GateCheck[];
  recommendedAction: string;
}

// ── Orchestrator state types ──────────────────────────────────

export type GateState =
  | "idle"
  | "interviewer"
  | "roadmapper"
  | "planner"
  | "executor"
  | "verifier"
  | "fix-loop";

export type OrchestratorMode = "chat" | "command" | "auto";

export interface TimelineStep {
  id: string;
  label: string;
  status: "pending" | "running" | "completed" | "failed";
}

export interface OrchestratorMessage {
  role: "user" | "system" | "assistant" | "result";
  content: string;
  gate?: GateState;
  runId?: string;
  timestamp: Date;
  artifacts?: {
    changed: string[];
    produced: string[];
  };
  timeline?: TimelineStep[];
  nextCommand?: string;
  logs?: string[];
}

// ── Gate kind metadata ────────────────────────────────────────
// Note: icons are intentionally NOT stored here — they live in the
// consuming component so lucide imports stay explicit per-file.

export interface GateKindMeta {
  label: string;
  /** Icon key for the consuming page to map to a lucide component */
  iconKey: "git-merge" | "clipboard-check" | "file-search";
}

export const GATE_KIND_META: Record<GateKind, GateKindMeta> = {
  dependency: { label: "Dependency", iconKey: "git-merge" },
  uat:        { label: "UAT",        iconKey: "clipboard-check" },
  evidence:   { label: "Evidence",   iconKey: "file-search" },
};

// ── Static gate presets (demo scenarios) ──────────────────────

export const mockBlockedGate: NextTaskGate = {
  taskId: "TSK-000013",
  taskName: "Implement OAuth Provider Integration",
  phaseId: "PH-0002",
  phaseTitle: "User Authentication",
  runnable: false,
  checks: [
    {
      id: "dep-1",
      kind: "dependency",
      label: "Predecessor task",
      detail: "TSK-000012 (Session Management) is still in-progress — must complete first.",
      status: "fail",
      actionLabel: "View task",
    },
    {
      id: "uat-1",
      kind: "uat",
      label: "UAT checklist",
      detail: "2 of 3 UAT items pending manual sign-off: login flow + OAuth callback.",
      status: "fail",
      actionLabel: "Open UAT",
    },
    {
      id: "ev-1",
      kind: "evidence",
      label: "Run evidence",
      detail: "No evidence file found for TSK-000012 last run — verification not recorded.",
      status: "fail",
      actionLabel: "View runs",
    },
  ],
  recommendedAction:
    "Complete TSK-000012, then sign off UAT-003 and UAT-004, and re-run verification to generate evidence.",
};

export const mockRunnableGate: NextTaskGate = {
  taskId: "TSK-000013",
  taskName: "Implement OAuth Provider Integration",
  phaseId: "PH-0002",
  phaseTitle: "User Authentication",
  runnable: true,
  checks: [
    {
      id: "dep-1",
      kind: "dependency",
      label: "Predecessor task",
      detail: "TSK-000012 completed and persisted.",
      status: "pass",
    },
    {
      id: "uat-1",
      kind: "uat",
      label: "UAT checklist",
      detail: "All 3 UAT items signed off for PH-0002.",
      status: "pass",
    },
    {
      id: "ev-1",
      kind: "evidence",
      label: "Run evidence",
      detail: ".aos/evidence/runs/TSK-000012/evidence.json — verified.",
      status: "pass",
    },
  ],
  recommendedAction: "All gates pass. Ready to execute TSK-000013.",
};

// ── Default execution timeline template ───────────────────────

export const defaultTimelineSteps: TimelineStep[] = [
  { id: "validate", label: "Validate", status: "pending" },
  { id: "roadmap",  label: "Roadmap",  status: "pending" },
  { id: "plan",     label: "Plan",     status: "pending" },
  { id: "execute",  label: "Execute",  status: "pending" },
  { id: "verify",   label: "Verify",   status: "pending" },
  { id: "persist",  label: "Persist",  status: "pending" },
];
