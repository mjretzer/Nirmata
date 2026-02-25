# GMSD AI Engine Roadmap and Remediation Report

## Scope, artifacts reviewed, and current goal

This report is based on static review of the uploaded ZIP (`GMSD-main.zip`) and its extracted solution/projects (notably `Gmsd.Web`, `Gmsd.Agents`, and `Gmsd.Aos`). I attempted to open the repository URL you provided on GitHub but the web fetch failed in this environment, so I could not cross-check branch/head differences against the remote at the time of writing.

Your stated blockers are clear and consistent with what’s currently in the ZIP:

- The “main chat session” behaves like an orchestrator-only runner, and **it effectively creates a run regardless of user intent**, rather than supporting freeform conversation and explicit workflow commands.
- The system’s “reasoning gates” (the gate → phase selection) either **aren’t being reached** due to classification/routing, **aren’t being surfaced** to the user, or the “chat phase” is currently a stub rather than a real orchestration+conversation experience.

The remainder of this report explains *why this happens*, what dependencies are missing to make the engine truly “agentic,” and a large, prioritized roadmap to get to a fully functioning AI orchestration engine with a real chat UX.

## What the system is architected to be

At a high level, the solution is shaped like an Agent Orchestration System (AOS-style) with these responsibilities:

- **A workspace contract** (`.aos/*`) for spec (intended truth), state (operational truth), and evidence (provable truth).
- An **orchestrator loop** that classifies input, decides whether to take side effects, gates into a phase, dispatches a phase handler, validates outputs, and persists run artifacts.
- A **web UI** that sends a “command” or text string to the backend and streams updates over Server-Sent Events (SSE).

Conceptually, this is a strong architecture for an orchestration engine. The main reason it *feels* non-agentic today is that several of the most important “thinking and talking” layers are **scaffolded/stubbed** rather than connected to a real LLM runtime, tool execution loop, and conversational UX.

## Why the chat always becomes a run and why gates feel absent

### The UI transport is streaming, but it streams “execution status,” not agent dialogue [X]

Your web layer is built around streaming updates. The browser’s SSE model is intended exactly for “server pushes events whenever it wants,” using `text/event-stream` semantics and an `EventSource`-like consumer.

However, the current server-side streaming endpoint effectively:

- Accepts `command` from the UI,
- Executes the orchestrator,
- And then returns a synthesized “✅ Execution Complete” (run id, phase, artifacts) message.

That’s a valid “job runner” UX, but **not** a “chat with an orchestrator agent” UX. So even if the orchestrator made nuanced decisions, the user wouldn’t see them as dialogue.

### Intent classification currently over-triggers "write/run" behavior [x]

Your orchestrator front door uses a classifier that decides whether an input has side effects (`None`, `ReadOnly`, `Write`). The problematic behavior is that the classifier treats many ordinary English requests as “workflow writes” instead of “conversation.”

The net effect is: if a message contains common workflow verbs like “create”, “plan”, “run”, “execute”, “fix”, etc., the system tends to treat it as a write-command request. This is a classic *false positive intent classification* problem.

Because the orchestrator only starts a run lifecycle for write operations, this creates the “no matter what you say, it creates a run” experience.

### The "chat" path exists, but it's stubbed [x]

Even when the orchestrator does decide “this is just chat,” the response layer is currently a placeholder (fixed strings rather than LLM-backed dialogue). That means:

- You don’t get a real assistant response.
- You don’t get tool-augmented conversational assistance.
- You don’t get a visible “reasoning gate” explanation or a negotiation step (e.g., “I think you want to plan phase PH-0002; should I proceed?”).

### The gating engine exists, but the system doesn't "converse through gates" [x]

You *do* have a gating engine conceptually: it picks phases like Interviewer → Roadmapper → Planner → Executor → Verifier → FixPlanner, and it has a default “Responder” when no workflow is triggered.

But today, the gating decision is primarily used to route *execution*, not to produce a conversational turn that a user experiences as an “agent thinking” moment.

In practical agent UX terms, you need a “gate explanation + proposal + confirmation” loop. This is where the orchestrator becomes *agentic* instead of feeling like a command runner.

## Immediate remediation for the orchestrator chat experience

This is the smallest set of changes that will make the system *feel* like a real orchestrator agent, even before the full multi-agent execution engine is complete.

### Replace "command-in, run-summary-out" with a conversational streaming contract [x]

Keep SSE (it’s a good fit), but change what you stream.

SSE is explicitly designed for a stream of typed events arriving over time. The orchestrator should therefore emit events like:

- [x] `intent.classified` (chat/read-only/write + confidence)
- [x] `gate.selected` (target phase + reason + derived context summary)
- [x] `phase.started` / `phase.completed`
- [x] `tool.call` / `tool.result` (when tools are used)
- [x] `assistant.delta` (streaming assistant tokens)
- [x] `assistant.final` (final assistant message and any structured artifacts)
- [x] `run.started` / `run.finished` (if and only if a write op is approved)

This single change makes “reasoning gates” visible and turns the orchestrator from a silent router into a conversational agent.

### Introduce a strict command grammar and stop overloading English [x]

You want “accepts freeform and cmd.” That requires the system to *know* which is which.

Implement:

- [x] **Explicit commands** with a stable prefix (`/run`, `/plan`, `/status`, `/help`, etc.)
- [x] **Freeform chat** as the default when no prefix is present

Then, optionally, add “natural language to command” as a second-stage **suggestion**, not an automatic trigger:

- If the user says “plan the foundation phase,” the agent responds:
  - “I can do that as `/plan --phase-id PH-0001`. Do you want me to proceed?”

This avoids accidental writes, surprises, and phantom run creation.

### Add a confirmation gate before any write-side-effect run [x]

Even if classification says "write," implement a gate:

- [x] If the request is ambiguous, require confirmation.
- [x] If the request is destructive (files, git commits), require confirmation.
- [x] If the workspace status is missing prerequisites, the agent should *ask* rather than failing.

This is also where structured outputs become valuable: you can force the model to output a `ProposedAction` object that your code validates before executing.

## LLM runtime and tool calling: the engine cannot “think” until this layer is real

Right now, the biggest functional blocker to a “fully functioning AI engine” is that the provider-neutral LLM adapter layer is scaffolded but not implemented for major providers.

### Pick a single “blessed” LLM integration path and complete it end-to-end [x]


#### Direction: Standardize on Microsoft Semantic Kernel as the “LLM substrate” [x]

You already have a Semantic Kernel setup path (Kernel builder, provider-specific chat completion wiring, prompt templates, filters, etc.). Microsoft’s docs show canonical patterns like building a `Kernel` and adding OpenAI chat completion, as well as attaching filters for observability/governance.

If you choose this path, the most important engineering decision is:

- [x] Either **replace** `ILlmProvider` usage with `IChatCompletionService` usage across workflows, or
- [x] Implement an `ILlmProvider` adapter that wraps `IChatCompletionService`.

Both are valid; the core requirement is: *one path must be production-grade and fully integrated.*

### Tool calling must be treated as a first-class, multi-step protocol [x]

In modern agentic systems, tool calling is not “a helper function,” it’s a conversation loop:

1) [x] send tools + messages
2) [x] model emits a tool call
3) [x] app executes tool call
4) [x] app sends tool results back
5) [x] model produces next response (or more tool calls)

Your orchestrator architecture (phases, dispatchers, evidence) is well suited to capture this—but only if your LLM layer can actually do it.

### Enforce structured outputs for critical planning artifacts [x]

Your system generates and consumes JSON artifacts for:

- project spec
- roadmap
- phase plans
- task specs and task plans
- verification criteria / UAT artifacts
- run summaries

Where the model must produce JSON that your engine executes, use strict schema conformance (structured outputs / strict function tool schemas) so the system is resilient:

- [ ] Phase planning output format (tasks + file scopes + verification steps)
- [ ] Fix planning output format (issues → proposed diffs/tests)
- [ ] Command proposal output format (what the agent intends to do next)

This is one of the highest-leverage changes you can make to eliminate “reasoning gates aren’t existent” feelings—because the agent’s “reasoning outputs” become structured, validated, and visible.

## Data contracts and workflow cohesion: key mismatches to fix

A second major reason the engine can’t yet “fully function” is that several components disagree about the shape of the same artifacts.

### Normalize and version the JSON contracts once, then generate typed models from them [x]

Right now, you have multiple parts that interpret `plan.json` or “fileScopes” differently (strings vs objects, `relativePath` vs `path`, etc.). This creates downstream failures like:

- executor can’t correctly enforce scope
- verifier can’t reliably find acceptance criteria
- UI can’t render “what happened” in a stable way

The fix is to establish:

- [x] A single JSON Schema per artifact type
- [x] A schema versioning policy
- [x] A typed model generated/validated against those schemas
- [x] A canonical file writer (you already trend toward deterministic JSON writing)

Then enforce:

- [x] All writers must emit schema-valid JSON
- [x] All readers must validate on read and fail with a friendly diagnostic artifact

### Make state initialization and snapshot derivation deterministic [x]

Your system expects “state snapshot” behavior and run evidence. Your tests already indicate failures around snapshot readiness (e.g., “Snapshot not set”). The production engine needs:

- [x] A startup hook: `EnsureWorkspaceInitialized()`
- [x] Populate `state.json` baseline if missing
- [x] Ensure `events.ndjson` exists
- [x] Optionally derive state snapshot from events when state is missing or stale

This becomes part of your “preflight gate.” If preflight fails, the orchestrator should respond conversationally with a fix suggestion.

### Replace placeholder execution with a real subagent loop [x]

Your task execution currently delegates to a subagent orchestration layer, but that subagent core is still placeholder/simulated.

To make it real, implement:

- [x] a subagent prompt that includes:
  - [x] bounded context pack
  - [x] task plan
  - [x] allowed file scopes
  - [x] required verifications
- [x] a tool set:
  - [x] file read/write (scoped)
  - [x] process runner (tests/build)
  - [x] git status/commit (if enabled)
- [x] a budget controller:
  - [x] max iterations, max tool calls, max tokens, wall-clock timeout
- [x] evidence capture:
  - [x] tool calls
  - [x] diffs
  - [x] command outputs
  - [x] final summary hash and deterministic outputs

This is where a functioning tool-calling loop matters.

### Phase 2: Conversational Orchestrator [x]
**Objective:** Unify chat and command into a single coherent UX.
**Change ID:** `refactor-chat-orchestration`

Done means: the orchestrator can chat freely and also execute explicit commands; the user can see when a run is created and why.

- [x] Implement a strict command parser:
  - [x] `/help`, `/status`, `/run …`, `/plan …`, `/verify`, `/fix`, etc.
- [x] Default behavior: **no prefix → chat**
- [x] Add "command suggestion" mode for freeform:
  - [x] model proposes command + asks confirmation
- [x] Add confirmation gating for write operations:
  - [x] confirmation required unless the command was explicit and non-destructive
- [x] Replace stub ChatResponder/ResponderHandler with a real LLM-backed responder:
  - [x] include workspace awareness ("what specs exist?")
  - [x] include a small tool set (read-only by default)

To support safe command proposals, make the assistant output a structured “proposal” object with schema validation.

### Phase 3: Streaming and Observability [x]
**Change ID:** `implement-streaming-events`

Done means: the UI shows gate selection, phase progress, tool calls, and the model's final response—incrementally.

- [x] Redefine SSE events into a stable protocol (typed events).
- [x] Stream orchestration steps as they happen:
  - [x] classification
  - [x] gating
  - [x] dispatch start/stop
  - [x] tool calls/results
  - [x] assistant deltas + final
- [x] Add tracing hooks:
  - [x] correlation ID per request
  - [x] run ID only when write begins
  - [x] attach filters/interceptors at the LLM boundary for logging and safety checks

### Phase 4: LLM Layer Completion [x]
**Objective:** Make at least one provider fully production-capable.
**Change ID:** `finalize-llm-provider`

Done means: planners and interviewers can produce structured artifacts; chat responds naturally; tool calling works.

Pick one:

- [x] Implement ILlmProvider for OpenAI using the official OpenAI .NET library, including streaming and tool calling.
  - [x] Implement strict structured outputs for the planner outputs.
- [ ] Or standardize on Semantic Kernel's OpenAI connector and use SK's chat completion service uniformly. Microsoft shows canonical Kernel builder usage and OpenAI chat completion configuration patterns.

Then add provider expansion:

- [x] Anthropic support (second) once the abstraction is proven.
- [x] Azure OpenAI and local models after.

### Phase 5: Contract Unification [x]
**Objective:** Eliminate plan/spec mismatches so phases can actually chain.
**Change ID:** `unify-data-contracts`

Done means: Roadmapper → Planner → Executor → Verifier works on real artifacts without manual patching.

- [x] Define schemas for:
  - [x] phase plan
  - [x] task plan
  - [x] verifier inputs/outputs
  - [x] fix plan
- [x] Enforce:
  - [x] writer validates before writing
  - [x] reader validates on read and produces a normalized diagnostic artifact on failure
- [x] Update:
  - [x] phase planner output format
  - [x] task executor scope extraction logic
  - [x] verifier acceptance criteria extraction logic
  - [x] UI rendering of artifacts

Once you use tool calling + structured outputs, you can require the model to output JSON that matches your schemas exactly (strict mode) and reject/repair otherwise.

### Phase 6: Real Execution [x]
**Objective:** Implement the subagent loop that edits code, runs checks, and produces evidence.
**Change ID:** `implement-subagent-execution`

Done means: given a task with file scopes and verification steps, the engine can modify files (within scope), run `dotnet test`, and emit evidence artifacts.

- [x] Implement subagent "plan-step-execute" loop with tool calling:
  - [x] decide next step
  - [x] call tools
  - [x] evaluate results
  - [x] continue until acceptance criteria met or budget exhausted
- [x] Add "scope firewall":
  - [x] tool layer refuses reads/writes outside allowed scope
- [x] Verification:
  - [x] build/test commands as tools
  - [x] parse results into UAT artifacts
- [x] Evidence:
  - [x] persist diffs, tool logs, final summary, deterministic hash

### Phase 7: Hardening and Governance [x]
**Objective:** Make it safe, reliable, and productizable.
**Change ID:** `harden-orchestrator-governance`

Done means: long-running sessions don’t corrupt state; failures are explainable; recovery is possible.

- [x] Add crash-safe run finalization:
  - [x] unfinished runs marked "abandoned" after timeout
- [x] Add workspace locks for write operations
- [x] Add resumability:
  - [x] restart from cursor + events
  - [x] implement pause/resume with user-visible status
- [x] Add rate limiting / concurrency bounds
- [x] Add secret handling:
  - [x] don't store API keys in plaintext
  - [x] rotate secrets via OS keychain or a secure vault

## Summary of highest-impact fixes for your reported “main” bug

If you want the fastest path to “this feels like a real orchestrator agent”:

1) [x] **Stop auto-turning English into write operations.** Introduce explicit commands + confirmation.
2) [x] **Replace stub chat responders with a real LLM-backed chat path.**
3) [x] **Stream gate decisions and phase reasoning as SSE events, not only run summaries.**
4) [x] **Complete one LLM provider end-to-end** (recommend implementing via the official OpenAI .NET library or fully adopting Semantic Kernel, but not half-and-half).
5) [x] **Use strict structured outputs for planner/gating artifacts** so the “reasoning gates” become explicit objects the system validates and the UI displays.
