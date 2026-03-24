// Shared type definitions for the Chat / Orchestrator dialogue layer.
// Runtime mock values have been removed — all data is now sourced from the backend via useAosData.ts hooks.

import type { GateState, TimelineStep } from "./mockOrchestratorData";

// ── Chat message types ────────────────────────────────────────

export type ChatRole = "user" | "assistant" | "system" | "result";

export type AgentId =
  | "orchestrator"
  | "interviewer"
  | "roadmapper"
  | "planner"
  | "executor"
  | "verifier";

export interface ChatArtifactRef {
  path: string;
  label: string;
  action: "created" | "updated" | "deleted" | "referenced";
}

export interface ChatMessage {
  id: string;
  role: ChatRole;
  content: string;
  timestamp: Date;
  /** Which agent produced this message */
  agent?: AgentId;
  /** Gate state at time of message */
  gate?: GateState;
  /** Inline command detected */
  command?: string;
  /** Artifacts referenced or produced */
  artifacts?: ChatArtifactRef[];
  /** Execution timeline (for result messages) */
  timeline?: TimelineStep[];
  /** Run ID (for result messages) */
  runId?: string;
  /** Whether the message is still streaming */
  streaming?: boolean;
  /** Log lines attached to a result */
  logs?: string[];
  /** Next recommended `aos` command surfaced by the orchestrator */
  nextCommand?: string;
}

// ── Suggested commands ────────────────────────────────────────

export interface CommandSuggestion {
  command: string;
  description: string;
  group: string;
}

// ── Quick action buttons for the input bar ────────────────────

export interface QuickAction {
  label: string;
  command: string;
  variant: "default" | "primary" | "destructive";
}
