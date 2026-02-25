# roadmap.md — Major Change: Razor Pages UI → Nuxt (Delete Legacy UI)

## Context (current state)
GMSD currently ships a server-rendered ASP.NET Core **Razor Pages** UI (`Gmsd.Web`) with:
- **Two layouts** (classic + chat-forward) selected at runtime (feature flag + session preference).
- A route-driven set of pages (Roadmap/Milestones/Phases/Tasks/UAT/Issues/Runs/etc.).
- A newer chat-forward 3-panel experience (Context Sidebar + Chat + Detail Panel) implemented as Razor partials + JS + HTMX-style refresh and streaming endpoints.
- UI actions that still rely heavily on **Razor Page handlers** (e.g., `?handler=...`) and partial rendering.

This roadmap transitions UI ownership to **Nuxt**, while keeping the backend (agents/spec/state/evidence/workspace engine) authoritative. The outcome is **Nuxt as the only UI** and **Razor UI deleted**.

## Why this is a “major change”
This is not a reskin. It changes:
- Rendering model (Razor SSR → Nuxt SPA/SSR)
- Interaction model (Razor handlers + partial HTML → explicit JSON UI APIs + client state)
- Deployment/build pipeline (dotnet-only CI → dotnet + Node)
- The codebase’s “UI boundary” (tight coupling → BFF-style contract)

## Guiding principles (AOS-aligned)
- **Spec-first sequencing**: no implementation without an accepted phase proposal and a task plan (plan → execute → verify → fix). :contentReference[oaicite:0]{index=0}
- **Atomic execution**: “one task = one commit,” strict file scope per task, verification evidence per run. :contentReference[oaicite:1]{index=1}
- **Deterministic artifact truth**:
  - Intended truth: `.aos/spec/**`
  - Operational truth: `.aos/state/**`
  - Provable truth: `.aos/evidence/**` :contentReference[oaicite:2]{index=2}
- **Strangler approach**: run Nuxt alongside Razor, migrate route-by-route, then delete Razor only after parity + usage validation.
- **Deletion is a first-class deliverable**: if we migrate but don’t delete Razor, we haven’t finished.

## Early decisions (must lock in Phase 0)
1) **Nuxt rendering mode**
- Default recommendation: **SPA-first** (`ssr: false`) to cut hosting complexity; revisit SSR later if required.

2) **Hosting topology**
- Option A (SPA-first): Nuxt static bundle served behind existing host + proxy `/api` to ASP.NET.
- Option B (SSR): Nuxt Node server behind reverse proxy.

3) **Streaming protocol**
- Choose one canonical approach for chat streaming (SSE vs fetch streaming) and enforce a single client contract.

## Operational tooling (how we’ll drive this work)
We’ll use AOS workflow primitives to keep this migration resumable and auditable:
- Roadmap/spec/state lifecycle and validation commands. :contentReference[oaicite:3]{index=3}
- Orchestrator gating rules and run evidence capture. :contentReference[oaicite:4]{index=4}
- Verification and fix-loop discipline (UAT verifier + fix planner). :contentReference[oaicite:5]{index=5}

---

# Phase checklist (each phase = its own OpenSpec proposal)

> Each phase below is intended to become an independent proposal with its own acceptance criteria, task plans, verification steps, and rollback posture.

- [ ] **Phase 0 — Inventory + Surface Area Freeze + Migration Contract**
  - **Objective:** Enumerate the Razor UI surface area and define the Nuxt migration contract.
  - **Deliverables:**
    - Full list of Razor routes + handler actions (`?handler=...`) and their Nuxt targets.
    - Full list of UI-used `/api/*` endpoints and streaming behaviors.
    - Parity matrix (feature-by-feature).
    - UI API/BFF blueprint: endpoints required to replace Razor handlers.
    - Locked decisions: SPA vs SSR, hosting topology, streaming protocol (e.g., SSE with specific event types).
    - A “Razor UI freeze” rule: no new UI feature work in Razor.
    - Identification of shared logic between Razor Page Models and potential BFF controllers.
  - **Acceptance:**
    - Every write-action has a planned API contract.
    - Streaming framing documented and testable.
    - BFF route structure (`/api/ui/*` vs `/api/v1/*`) finalized.
  - **Rollback:** No production impact; revert docs/proposal only.

- [ ] **Phase 1 — Nuxt Scaffold + DX + CI Baseline**
  - **Objective:** Add Nuxt as a first-class codebase component without changing prod routes.
  - **Deliverables:**
    - `/Gmsd.Ui` Nuxt 3 + TypeScript scaffold (using Nuxt UI v3/v4).
    - Directory structure:
      - `components/`: Atomic UI components (shadcn-inspired).
      - `composables/`: Business logic and API abstraction.
      - `pages/`: Mirrored Razor hierarchy.
      - `server/api/`: Nuxt Nitro server routes for BFF shimming if needed.
    - Lint + unit test harness (Vitest).
    - Dev proxy to backend (`/api`).
    - CI updates: Node install, cache, lint/test/build.
    - “Run locally” docs.
  - **Acceptance:** CI green for dotnet + Node pipelines.
  - **Rollback:** Remove `/ui` + CI steps.

- [ ] **Phase 2 — Strangler Shell: Routing + Layout Parity**
  - **Objective:** Create a navigable Nuxt shell that mirrors Razor routes and layout behaviors.
  - **Deliverables:**
    - Nuxt route skeleton matching existing IA (deep-links preserved).
    - Nuxt layouts:
      - Classic layout (equiv `_Layout`)
      - Chat-forward layout (equiv `_MainLayout`)
    - Feature flag plumbing for layout selection + rollout.
    - Workspace-required guard behavior.
    - Nuxt hosted behind `/app` or feature flag.
  - **Acceptance:** All primary routes resolve; baseline navigation works.
  - **Rollback:** Disable flag or route mapping; Razor stays default.

- [ ] **Phase 3 — UI API (BFF) Extraction: Kill Razor Handlers**
  - **Objective:** Replace Razor Page handlers with explicit JSON UI APIs so Nuxt can fully own interactions.
  - **Deliverables:**
    - New `/api/ui/*` endpoints (or extend existing `/api/*`) to replace:
      - Workspace: selection and project mounting.
      - Phases: add assumptions / set research / plan phase.
      - Tasks: execute plan / mark status.
      - UAT: pass/fail/complete/restart verification (migrate `ArtifactValidationController` logic).
      - Issues: route-to-fix / mark status.
      - Runs: pause/resume/cancel etc. (migrate `RunPauseResumeController` logic).
      - Sidebar/Detail Panel: migrate `SidebarController` and `DetailPanelController` to stable JSON APIs.
    - Contract tests for endpoints (shape + status codes).
    - Backward-compat shim layer (temporary) if needed.
  - **Acceptance:** Nuxt can execute all writes without any Razor handler dependency.
  - **Rollback:** Keep Razor handlers reachable until cutover.

- [ ] **Phase 4 — Chat-Forward Migration: Streaming, Sidebar, Detail Panel**
  - **Objective:** Port the entire chat-forward experience into Nuxt with a stable streaming implementation.
  - **Deliverables:**
    - Nuxt implementations of:
      - Context Sidebar refresh (polling or push)
      - Chat thread rendering (typed events)
      - Detail panel entity viewer
    - Canonical streaming protocol + cancellation semantics.
    - Renderer registry port (event-type → Vue component map).
    - E2E streaming test suite (start → stream → cancel → recover).
  - **Acceptance:** Orchestrator/chat flows work end-to-end with verified stability.
  - **Rollback:** Keep Razor Orchestrator behind fallback route during stabilization.

- [ ] **Phase 5 — Domain Pages Migration (Vertical Slices)**
  - **Objective:** Migrate feature domains in slices; each slice ends with Nuxt parity + Razor deprecation for that domain.
  - **Slices (recommended order):**
    1. Workspace
    2. Roadmap
    3. Milestones + Phases
    4. Tasks
    5. UAT
    6. Issues
    7. Runs
    8. Specs/State/Dashboard/Validation/Fix/Context/Codebase/Checkpoints/Command
  - **Per-slice deliverables:**
    - Read APIs + write APIs + deep-links + E2E tests.
    - Razor route removed from navigation (soft deprecate).
  - **Acceptance:** Parity matrix turns green slice-by-slice; slice has E2E coverage.
  - **Rollback:** Route slice back to Razor via proxy/flag if regression.

- [ ] **Phase 6 — Default Cutover (Nuxt becomes primary UI)**
  - **Objective:** Make Nuxt the default for all UI routes with a time-boxed observation window.
  - **Deliverables:**
    - Root route and all feature routes mapped to Nuxt.
    - Razor accessible only via explicit fallback path (temporary).
    - Telemetry/logging to confirm Razor traffic drops to ~0.
  - **Acceptance:** No critical regressions during observation window.
  - **Rollback:** Re-route to last stable Razor-tagged release.

- [ ] **Phase 7 — Delete Razor UI (Legacy Removal)**
  - **Objective:** Remove Razor UI code and Razor-only assets permanently.
  - **Deliverables:**
    - Delete Razor Pages + layouts + shared partials.
    - Remove Razor-specific JS/CSS under `wwwroot` that Nuxt replaced.
    - Remove Layout selector filter and any Razor-only conventions.
    - Remove HTMX dependencies where no longer used.
    - Tag the last Razor-capable commit (e.g., `ui-razor-final`).
  - **Acceptance:** Repo contains **no unused/legacy Razor UI**, CI green.
  - **Rollback:** Only by redeploying the tagged Razor release.

- [ ] **Phase 8 — Hardening: Security, Performance, Contracts, DX**
  - **Objective:** Consolidate post-migration architecture, enforce budgets, and remove transition shims.
  - **Deliverables:**
    - Security headers + CSP strategy and CSRF approach (Synchronizer Token Pattern or similar for Nuxt).
    - Auth integration: ensure session-based workspace tracking works seamlessly with Nuxt.
    - Bundle size/perf budgets enforced in CI.
    - Frontend error reporting + correlation IDs into backend logs.
    - Type generation strategy (OpenAPI/JSON schema → TypeScript/Nuxt content-type generation).
    - Documentation refresh: architecture, dev workflow, troubleshooting.
  - **Acceptance:** Signed-off security review + perf budgets enforced.

---

# Definition of “Legacy/Unused UI” (Deletion gates)
We only delete Razor UI when it’s both:
1) **Legacy-by-replacement**: Nuxt owns the route/feature with parity.
2) **Unused-in-practice**: No observed hits for a defined window (or for internal-only, a deterministic cutover confirmation).

Deletion PRs must include:
- Proof of route cutover
- Test coverage updates
- Evidence/telemetry summary
- Removal of all related assets (not just pages)

---

# Verification posture (non-negotiable)
- Every migrated slice must have:
  - API contract tests
  - UI E2E tests
  - Explicit pass/fail verification recorded (UAT gate behavior) :contentReference[oaicite:6]{index=6}
- Migration work follows the execution discipline:
  - task plan → execute → record evidence → commit (atomic) :contentReference[oaicite:7]{index=7}

---

# Suggested “next action” (start here)
- Execute **Phase 0** as the next proposal.
- Use the Orchestrator gating pattern to keep work deterministic and resumable. :contentReference[oaicite:8]{index=8}
- Validate artifacts early/often using AOS validation commands. :contentReference[oaicite:9]{index=9}