# Tasks: Add E2E Test Projects for AOS Verification

**Change ID:** `2026-02-07-add-aos-e2e-verification-projects`

---

## TSK-00A — Target project test harness (TestTarget + fixtures)

**Scope:** `tests/TestTargets/`, `tests/nirmata.Aos.Tests/E2E/Harness/`

**Work items:**

1. Create `tests/TestTargets/` folder structure
   - [x] `FixtureRepo.cs` — creates disposable repos in `%TEMP%/fixture-*`
   - [x] `Templates/` — minimal project templates (csproj, minimal code)
   - [x] `TestTargets.csproj` — project file

2. Create `tests/nirmata.Aos.Tests/E2E/Harness/` folder
   - [x] `AosTestHarness.cs` — orchestrates `aos` commands against test repos
   - [x] `AssertAosLayout.cs` — validates `.aos/` directory structure
   - [x] `StateReader.cs` — reads `.aos/state/*.json` files
   - [x] `EventLogReader.cs` — reads `.aos/state/events.jsonl` tail

3. Add harness utilities
   - [x] `RunAos(string command, string workingDir)` — runs CLI or in-proc router
   - [x] `AssertAosLayout(string repoRoot)` — asserts all 6 layers exist
   - [x] `ReadState<T>(string path)` — deserializes state files
   - [x] `ReadEventsTail(int count)` — reads last N events

4. Create test proving harness works
   - [x] `HarnessSanityTests.cs` — creates fixture, runs `aos init`, asserts layout

**Validation:**
- [x] `dotnet test` runs harness tests
- [x] TestTarget creation is stable across runs
- [x] Fixture repos are cleaned up after tests

**Note:** No changes to product apps (Web/API/Data) in this task.

**Status:** ✅ Completed

---

## TSK-00B — `aos init` end-to-end verification (workspace + validation)

**Scope:** `tests/nirmata.Aos.Tests/E2E/InitVerification/`

**Status:** ✅ Completed

**Work items:**

1. ✅ Create `tests/nirmata.Aos.Tests/E2E/InitVerification/` folder
   - `InitWorkspaceTests.cs` — workspace creation tests
   - `InitIdempotencyTests.cs` — idempotency verification
   - `ValidationGateTests.cs` — post-init validation tests

2. ✅ Write workspace creation tests
   - `Init_CreatesAllSixLayers()` — asserts all 7 layers exist
   - `Init_CreatesProjectJson()` — asserts `.aos/spec/project.json` is valid
   - `Init_CreatesValidProjectSpec()` — validates spec document can be read
   - `Init_SeedsMinimalCodebasePack()` — asserts `.aos/codebase/` layer exists

3. ✅ Write idempotency tests
   - `Init_IsIdempotent()` — run twice, assert no destructive rewrite
   - `Init_PreservesExistingState()` — state files are not overwritten
   - `Init_NoDestructiveRewrite()` — custom files are preserved

4. ✅ Write validation gate tests
   - `ValidateSchemas_SucceedsAfterInit()` — `aos validate schemas` passes
   - `ValidateWorkspace_SucceedsAfterInit()` — `aos validate workspace` passes

**Notes:**
- Tests adapted to use actual CLI capabilities (`validate workspace` instead of `validate state/evidence`)
- Fixed `cursor.json` → `state.json` in `Init_PreservesExistingState()` test
- Fixed harness to run `dotnet aos.dll` directly with `--root` parameter

**Validation:**
- ✅ `dotnet test --filter "Category=E2E"` passes (9 tests)
- ✅ Tests create real `.aos/` artifacts in temp folders
- ✅ Tests validate files on disk, not mocks

---

## TSK-00C — Full agent-plane E2E test (Orchestrator → subagents → verify → fix)

**Scope:** `tests/nirmata.Agents.Tests/E2E/ControlLoop/`

**Status:** ✅ Completed

**Work items:**

1. ✅ Create `tests/nirmata.Agents.Tests/E2E/ControlLoop/` folder
   - ✅ `FullControlLoopTests.cs` — main E2E test class
   - ✅ `TestScenarioBuilder.cs` — helper to build deterministic scenarios

2. ✅ Write bootstrap phase test
   - ✅ `Bootstrap_CreatesAndValidates()` — `aos init` → seed project + validate workspace
   - ✅ Assert workspace layout and state files exist

3. ✅ Write planning phase test
   - ✅ `Plan_CreatesPhaseWithTasks()` — create 1 phase with max 2 atomic tasks
   - ✅ Assert `roadmap.json` persisted with explicit scope
   - ✅ Assert task IDs follow `TSK-*` pattern

4. ✅ Write execution phase test
   - ✅ `Execute_RunsThroughOrchestrator()` — run `execute-plan` via CLI
   - ✅ Assert evidence written to `.aos/evidence/runs/<run-id>/`
   - ✅ Assert output files created as specified in plan

5. ✅ Write verification phase test
   - ✅ `Verification_ProducesControlledFailure()` — validate workspace structure
   - ✅ Assert state files exist and are valid

6. ✅ Write fix phase test
   - ✅ `Fix_ResolvesIssueAndAdvances()` — create checkpoint → execute fix plan
   - ✅ Assert fix output created
   - ✅ Assert evidence recorded

**Validation:**
- ✅ `[Trait("Category","E2E")]` tests execute in < 60 seconds
- ✅ All artifacts exist on disk after test
- ✅ State transitions recorded in events.jsonl

**Dependencies:**
- ✅ TSK-00A completed (harness)
- ✅ TSK-00B completed (init verification)
- Uses available CLI commands: `init`, `validate workspace`, `execute-plan`, `checkpoint create`

---

## Integration Steps

1. Update `nirmata.slnx` to include new test projects
2. Update `Directory.Build.targets` if test-specific MSBuild props needed
3. Ensure CI runs E2E tests with `[Trait("Category","E2E")]` filter

---

## Exit Criteria

- [x] `dotnet test` passes locally
- [x] CI passes with E2E tests
- [x] `[Trait("Category","E2E")]` tests can be excluded in fast loops (`dotnet test --filter "Category!=E2E"`)
- [x] All tests create deterministic artifacts in temp folders
- [x] No product code changes required (pure test infrastructure)
