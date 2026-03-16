/**
 * Mock Chat Data
 *
 * Realistic conversation history demonstrating the AOS orchestrator
 * workflow: freeform prompts, command routing, agent handoffs,
 * streaming results, and artifact references.
 */

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
}

// ── Suggested commands ────────────────────────────────────────

export interface CommandSuggestion {
  command: string;
  description: string;
  group: string;
}

export const mockCommandSuggestions: CommandSuggestion[] = [
  { command: "aos status", description: "Show workspace health and cursor position", group: "workspace" },
  { command: "aos plan", description: "Open the project spec and roadmap", group: "planning" },
  { command: "aos run", description: "Execute the current task", group: "execution" },
  { command: "aos run --task TSK-000013", description: "Execute a specific task", group: "execution" },
  { command: "aos verify", description: "Run verification on last execution", group: "verification" },
  { command: "aos fix", description: "Enter the fix loop for failing tests", group: "verification" },
  { command: "aos next", description: "Advance cursor to the next task", group: "workflow" },
  { command: "aos checkpoint", description: "Create a continuity checkpoint", group: "continuity" },
  { command: "aos handoff", description: "Generate agent handoff snapshot", group: "continuity" },
  { command: "aos config list", description: "List all configuration keys", group: "config" },
  { command: "aos config set engine.provider openai", description: "Set a config value", group: "config" },
  { command: "aos scan", description: "Re-index codebase intelligence", group: "codebase" },
  { command: "aos validate", description: "Validate all .aos schemas", group: "workspace" },
  { command: "aos init", description: "Initialize a new .aos workspace", group: "workspace" },
  { command: "aos interview start", description: "Begin the project interview", group: "planning" },
  { command: "aos roadmap generate", description: "Generate roadmap from spec", group: "planning" },
];

// ── Mock conversation ─────────────────────────────────────────

const now = new Date();
const ago = (mins: number) => new Date(now.getTime() - mins * 60000);

export const mockChatMessages: ChatMessage[] = [
  {
    id: "msg-001",
    role: "system",
    content: "Workspace **my-app** loaded. Cursor at `M1/PH-0002/TSK-000012`. Engine idle.",
    timestamp: ago(45),
    agent: "orchestrator",
    gate: "idle",
  },
  {
    id: "msg-002",
    role: "user",
    content: "What's the status of the current phase?",
    timestamp: ago(44),
  },
  {
    id: "msg-003",
    role: "assistant",
    content: `**Phase PH-0002 — User Authentication** is in-progress.

- **4 of 7 tasks** completed
- **TSK-000012** (Session Management) is the active task — currently executing
- **3 tasks** remain: OAuth Integration, Password Reset, Account Settings
- **UAT:** 5 of 8 checklist items signed off
- **Gate status:** Next task (TSK-000013) is blocked — predecessor not yet complete

The phase is on track. Once TSK-000012 passes verification, the gate for TSK-000013 will clear automatically.`,
    timestamp: ago(43),
    agent: "orchestrator",
    gate: "idle",
    artifacts: [
      { path: ".aos/spec/roadmap.json", label: "Roadmap", action: "referenced" },
      { path: ".aos/spec/phases/PH-0002.json", label: "Phase PH-0002", action: "referenced" },
    ],
  },
  {
    id: "msg-004",
    role: "user",
    content: "aos run",
    timestamp: ago(38),
    command: "aos run",
  },
  {
    id: "msg-005",
    role: "assistant",
    content: "Starting execution for **TSK-000012 — Session Management**. I'll hand off to the executor agent.",
    timestamp: ago(37),
    agent: "orchestrator",
    gate: "executor",
  },
  {
    id: "msg-006",
    role: "result",
    content: "Execution completed successfully.",
    timestamp: ago(32),
    agent: "executor",
    gate: "executor",
    runId: "RUN-2026-0309-004",
    timeline: [
      { id: "validate", label: "Validate", status: "completed" },
      { id: "plan", label: "Plan", status: "completed" },
      { id: "execute", label: "Execute", status: "completed" },
      { id: "verify", label: "Verify", status: "completed" },
      { id: "persist", label: "Persist", status: "completed" },
    ],
    artifacts: [
      { path: ".aos/evidence/runs/TSK-000012/run-004.json", label: "Run evidence", action: "created" },
      { path: "src/auth/session.ts", label: "session.ts", action: "updated" },
      { path: "src/auth/session.test.ts", label: "session.test.ts", action: "created" },
      { path: "src/middleware/auth.ts", label: "auth middleware", action: "updated" },
    ],
    logs: [
      "[executor] Cloning task context from .aos/intel/codebase.json",
      "[executor] Generating implementation plan for TSK-000012",
      "[executor] Writing src/auth/session.ts (342 lines)",
      "[executor] Writing src/auth/session.test.ts (89 lines)",
      "[executor] Updating src/middleware/auth.ts (12 lines changed)",
      "[verifier] Running test suite: 14 tests passed, 0 failed",
      "[verifier] Evidence persisted to .aos/evidence/runs/TSK-000012/",
      "[persist] Task status updated: TSK-000012 → completed",
    ],
  },
  {
    id: "msg-007",
    role: "system",
    content: "Task **TSK-000012** marked complete. Cursor advanced to **TSK-000013**. Gate check: all 3 checks now pass.",
    timestamp: ago(31),
    agent: "orchestrator",
    gate: "idle",
  },
  {
    id: "msg-008",
    role: "user",
    content: "Can you show me what changed in the session module?",
    timestamp: ago(28),
  },
  {
    id: "msg-009",
    role: "assistant",
    content: `Here's a summary of changes in the session module from **RUN-2026-0309-004**:

**\`src/auth/session.ts\`** (updated — 342 lines)
- Added \`SessionManager\` class with token rotation
- Implemented \`createSession()\`, \`validateSession()\`, \`revokeSession()\`
- Added Redis-backed session store with TTL support
- Integrated with the existing auth middleware

**\`src/auth/session.test.ts\`** (created — 89 lines)
- 14 test cases covering session lifecycle
- Token expiry and refresh edge cases
- Concurrent session limit enforcement

**\`src/middleware/auth.ts\`** (12 lines changed)
- Wired \`SessionManager.validateSession()\` into the request pipeline
- Added \`X-Session-Token\` header extraction`,
    timestamp: ago(27),
    agent: "orchestrator",
    artifacts: [
      { path: "src/auth/session.ts", label: "session.ts", action: "referenced" },
      { path: "src/auth/session.test.ts", label: "session.test.ts", action: "referenced" },
      { path: "src/middleware/auth.ts", label: "auth.ts", action: "referenced" },
    ],
  },
  {
    id: "msg-010",
    role: "user",
    content: "aos checkpoint",
    timestamp: ago(22),
    command: "aos checkpoint",
  },
  {
    id: "msg-011",
    role: "assistant",
    content: "Checkpoint created: **CP-2026-0309-002**. Continuity state, handoff context, and current cursor position have been persisted to `.aos/state/checkpoints/`.",
    timestamp: ago(21),
    agent: "orchestrator",
    artifacts: [
      { path: ".aos/state/checkpoints/CP-2026-0309-002.json", label: "Checkpoint", action: "created" },
      { path: ".aos/state/handoff.json", label: "Handoff snapshot", action: "updated" },
    ],
  },
  {
    id: "msg-012",
    role: "user",
    content: "Now let's work on the OAuth integration. What does the plan look like for TSK-000013?",
    timestamp: ago(18),
  },
  {
    id: "msg-013",
    role: "assistant",
    content: `**TSK-000013 — Implement OAuth Provider Integration**

The task plan calls for:

1. **OAuth provider abstraction** — Generic \`OAuthProvider\` interface supporting Google, GitHub, and Microsoft
2. **Callback handler** — \`/auth/callback/:provider\` route with PKCE verification
3. **Account linking** — Link OAuth identities to existing user accounts
4. **Token management** — Store and refresh OAuth tokens per-provider
5. **UI components** — "Sign in with..." buttons for each configured provider

**Estimated scope:** ~450 lines across 6 files
**Dependencies:** TSK-000012 (Session Management) — now complete
**UAT items:** 3 checklist items requiring sign-off after execution

Ready to execute when you give the word.`,
    timestamp: ago(17),
    agent: "planner",
    gate: "idle",
    artifacts: [
      { path: ".aos/spec/tasks/TSK-000013.json", label: "Task plan", action: "referenced" },
    ],
  },
];

// ── Quick action buttons for the input bar ────────────────────

export interface QuickAction {
  label: string;
  command: string;
  variant: "default" | "primary" | "destructive";
}

export const mockQuickActions: QuickAction[] = [
  { label: "Run Task", command: "aos run", variant: "primary" },
  { label: "Verify", command: "aos verify", variant: "default" },
  { label: "Status", command: "aos status", variant: "default" },
  { label: "Checkpoint", command: "aos checkpoint", variant: "default" },
];
