# Brownfield Agent

Source: Brownfield_existing_codebase.pdf (Section 7)

---

## Codebase Mapper Agent

### Responsibilities

- Run the brownfield entrypoint for existing repositories (`aos codebase scan`) and produce a canonical, reusable codebase intelligence pack.
- Fan-out analysis across the repo and synthesize standardized outputs so planning is grounded in the actual codebase, not assumptions.
- Persist the codebase pack into `.aos/codebase/**` as authoritative "how this repo works" references for roadmap/phase planning.
- Maintain derived/fast-path artifacts (symbols and file graph) under `.aos/codebase/cache/**`.
- Ensure outputs are structurally valid and consumable via `aos codebase show …` and `aos validate codebase`.

### Step Format (single run)

1. Receive `map-codebase` intent (CLI equivalent) and run `aos codebase scan` to enumerate repository structure and targets.
2. Fan-out analyzers (parallel) and build the canonical docs via `aos codebase map build`, producing:
   - `.aos/codebase/map.json`
   - `.aos/codebase/stack.json`
   - `.aos/codebase/architecture.json`
   - `.aos/codebase/structure.json`
   - `.aos/codebase/conventions.json`
   - `.aos/codebase/testing.json`
   - `.aos/codebase/integrations.json`
   - `.aos/codebase/concerns.json`
3. Build derived intelligence for fast reuse:
   - `aos codebase symbols build` → `.aos/codebase/cache/symbols.json`
   - `aos codebase graph build` → `.aos/codebase/cache/file-graph.json`
4. Validate the pack (`aos validate codebase`) and ensure each artifact is viewable through `aos codebase show <map|stack|architecture|structure|conventions|testing|integrations|concerns>`.
5. Attach the produced artifacts to the current run evidence (`aos run artifact add <path>`) and return control to Orchestrator so planning can proceed with codebase grounding.

### Summary

Codebase Mapper is the brownfield initializer. It scans the repository, fans out analysis, and persists a standardized codebase intelligence pack into `.aos/codebase/**` (map, stack, architecture, structure, conventions, testing, integrations, concerns) plus derived caches in `.aos/codebase/cache/**` (symbols and file graph). After validation, this pack becomes the grounding layer for roadmap and phase planning, ensuring subsequent decisions reflect the actual repo rather than assumptions.

---

## Codebase Intelligence Artifacts

| Artifact | Path | Purpose |
|---|---|---|
| Map | `.aos/codebase/map.json` | High-level repo overview |
| Stack | `.aos/codebase/stack.json` | Technology stack details |
| Architecture | `.aos/codebase/architecture.json` | System design patterns |
| Structure | `.aos/codebase/structure.json` | Directory/file organization |
| Conventions | `.aos/codebase/conventions.json` | Coding standards in use |
| Testing | `.aos/codebase/testing.json` | Test patterns and tooling |
| Integrations | `.aos/codebase/integrations.json` | External service dependencies |
| Concerns | `.aos/codebase/concerns.json` | Known risks and issues |
| Symbols (cache) | `.aos/codebase/cache/symbols.json` | Fast symbol lookup |
| File graph (cache) | `.aos/codebase/cache/file-graph.json` | File dependency graph |
