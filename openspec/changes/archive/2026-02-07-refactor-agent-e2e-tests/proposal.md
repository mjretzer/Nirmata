# Proposal: Refactor Agent E2E Test Suite

## Change ID
`refactor-agent-e2e-tests`

## Summary
Comprehensive refactoring of the `Gmsd.Agents.Tests` end-to-end test suite to eliminate 188+ compilation errors, establish stable test patterns, and create a maintainable testing framework that evolves with the implementation.

## Problem Statement

### Current State (Broken)
The agent plane test suite has **188 compilation errors** caused by:

1. **Namespace drift**: Tests reference `Gmsd.Agents.Workflows.*` which was refactored to `Gmsd.Agents.Execution.*`
2. **Constructor signature mismatches**: `Orchestrator` now requires 7 handler dependencies, tests only provide 1
3. **Type definition changes**: 
   - `CommandRouteResult.ErrorMessage` → `ErrorOutput`
   - `StateEventEntry` converted from positional record to init-only properties
   - `StateEventTailResponse` simplified to `Items` only
4. **Interface evolutions**: `ICodebaseScanner`, `ISymbolCacheBuilder` signatures changed
5. **Missing fakes**: `FakeSymbolCacheBuilder` didn't exist, `FakeRunLifecycleManager` missing properties

### Root Cause Analysis
The tests were written against an evolving architecture without a synchronization mechanism. The implementation matured (namespaces consolidated, constructors clarified, records simplified) but tests remained stale.

### Impact
- **Zero runnable E2E tests** for the orchestrator workflow
- No verification of critical path: `StartRun → Gate → Dispatch → CloseRun`
- No confidence in agent plane changes
- Technical debt accumulates with each architectural improvement

## Goals

### Primary Goal
Establish a **maintainable, compilable, runnable** E2E test suite for the agent plane that:
1. Verifies the orchestrator workflow end-to-end
2. Survives implementation refactorings
3. Provides fast feedback on breaking changes

### Secondary Goals
1. **Pattern library**: Create reusable test patterns for handler testing
2. **Fixture stability**: Build durable test fixtures that don't break on rename
3. **Contract testing**: Verify interfaces rather than implementations
4. **CI integration**: Enable automated E2E test runs

## Success Criteria
- [ ] All existing E2E tests compile
- [ ] Orchestrator E2E tests pass (evidence folder structure, commands.json, summary.json)
- [ ] Test suite runs in under 30 seconds
- [ ] New test pattern documentation exists
- [ ] No test references `Gmsd.Agents.Workflows` namespace
- [ ] Fake implementations match interface contracts via compile-time checks

## Out of Scope
- Unit tests for individual handlers (separate effort)
- Integration tests requiring LLM/providers (use fakes)
- Performance/load testing
- Windows Service host testing (empty project currently)

## Related Specs
- `agents-orchestrator-workflow` - Core orchestrator behavior
- `aos-run-lifecycle` - Run lifecycle management
- `aos-evidence-store` - Evidence capture contracts

## Proposed Approach

1. **Layered Test Architecture**
   - Contract tests: Verify interface boundaries
   - Workflow tests: Verify orchestrator routing
   - Integration tests: Verify with real filesystem

2. **Stable Test Fixtures**
   - AutoMapper-style fake registration
   - Reflection-based interface compliance checks
   - Builder pattern for complex test data

3. **Namespace Consolidation**
   - All test utilities in `Gmsd.Agents.Tests.Fixtures`
   - Clear separation: `Fakes/` (in-memory) vs `Fixtures/` (filesystem)

4. **Continuous Validation**
   - Compile-time interface checking
   - Test run on every PR
   - Failing test = blocking merge

## Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Tests break again on next refactor | High | Medium | Interface contract tests; CI gate |
| Fake implementations diverge from real | Medium | High | Reflection-based compliance checker |
| Test suite becomes slow | Low | Medium | In-memory fakes default, filesystem opt-in |
| Maintenance burden | Medium | Low | Clear patterns; good examples |

## Approval
This proposal requires approval before implementation begins due to the scope of changes (~30+ files).

---

**Created**: 2026-02-06
**Status**: Draft - Pending Review
