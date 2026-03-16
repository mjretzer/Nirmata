# Verification Notes - Spec Hygiene and Layering Clarification

## Summary
Performed a repository-wide spec hygiene cleanup to normalize placeholder Purpose stubs, clarify layering between `engine-*` and `aos-*` specs, and align `web-*` specs with the current Razor Pages implementation.

## 1. Purpose Normalization
- All placeholder "TBD..." stubs in non-web specs were replaced with durable Purpose sections.
- Templates used consistent structure:
  - 1-3 sentences defining capability.
  - **Lives in:** project/file paths.
  - **Owns:** responsibilities.
  - **Does not own:** boundaries.
- Total specs updated via automation: 63

## 2. Engine/AOS Layering
- `engine-*` specs updated to explicitly state they define DI/interface surfaces.
- Added explicit conformance statements referencing `aos-*` specs for behavioral semantics.
- Affected specs:
  - `engine-run-manager` -> `aos-run-lifecycle`
  - `engine-cache-manager` -> `aos-cache-hygiene`
  - `engine-checkpoint-manager` -> `aos-checkpoints`
  - `engine-deterministic-json` -> `aos-deterministic-json-serialization`
  - `engine-event-store` -> `aos-state-store`
  - `engine-artifact-paths` -> `aos-path-routing`
  - `engine-lock-manager` -> `aos-lock-manager`
  - `engine-schema-registry-service` -> `aos-schema-registry`

## 3. Web Spec Alignment
- Updated routes to match Razor Pages implementation (route parameters vs query strings).
- Corrected `.aos` file access patterns (directory-based loading).
- Updated link targets to reflect current implementation.
- Affected specs: `web-tasks-page`, `web-issues-page`, `web-uat-page`, `web-milestones-page`, `web-phases-page`, `web-runs-dashboard`, `web-roadmap-page`, `web-razor-pages`, `web-orchestrator-page`, `web-agent-runner`, `web-advanced-pages`.

## 4. Validation
- `openspec validate --specs --strict` output:
  - `Totals: 86 passed, 0 failed (86 items)`
- Verified no placeholder stubs remain via `grep`.
