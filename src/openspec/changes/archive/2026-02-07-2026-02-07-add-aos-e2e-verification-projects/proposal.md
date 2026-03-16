# Proposal: Add E2E Test Projects for AOS Verification

**Change ID:** `2026-02-07-add-aos-e2e-verification-projects`

**Status:** Draft

---

## Summary

Establish dedicated E2E test infrastructure under `tests/` that proves the full AOS control-loop works end-to-end, using real filesystem artifacts under `.aos/` and captured run evidence. This addresses **PH-ENG-0013** from the roadmap.

## Outcomes

1. **TestTarget fixture system** — deterministic disposable repos for E2E tests
2. **Init verification tests** — prove `aos init` creates valid workspaces and passes validation gates
3. **Full control-loop E2E** — deterministic test proving spec → roadmap → plan → execute → verify → fix cycle

## Scope

- `tests/TestTargets/` — fixture repo templates + test harness utilities
- `tests/nirmata.Aos.Tests/E2E/` — AOS workspace and init verification tests
- `tests/nirmata.Agents.Tests/E2E/` — full agent orchestration E2E tests

## Non-Goals

- No changes to product apps (Web/API/Data/Services)
- No changes to engine core contracts (Aos/Agents source)
- No production feature work — this is test infrastructure only

## References

- Roadmap: `PH-ENG-0013` (lines 522-569)
- Projects: `nirmata.Aos.Tests`, `nirmata.Agents.Tests`

## Checklist

- [x] proposal.md (this file)
- [ ] tasks.md
- [ ] design.md
- [ ] spec deltas
- [ ] Validate with `openspec validate`
