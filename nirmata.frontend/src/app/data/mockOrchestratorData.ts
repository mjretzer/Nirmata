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

// ── Default execution timeline template ───────────────────────

export const defaultTimelineSteps: TimelineStep[] = [
  { id: "validate", label: "Validate", status: "pending" },
  { id: "roadmap",  label: "Roadmap",  status: "pending" },
  { id: "plan",     label: "Plan",     status: "pending" },
  { id: "execute",  label: "Execute",  status: "pending" },
  { id: "verify",   label: "Verify",   status: "pending" },
  { id: "persist",  label: "Persist",  status: "pending" },
];
