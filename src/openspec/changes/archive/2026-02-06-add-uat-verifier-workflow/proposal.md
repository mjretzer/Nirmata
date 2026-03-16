# Change: Add UAT Verifier Workflow

## Why
The orchestrator's gating engine routes to Verifier after execution completes, but there is no actual UAT verification implementation. We need a concrete Verifier workflow that runs acceptance checks against executed work, writes issues on failure, and routes appropriately (FixPlanner on failure, continue on success).

## What Changes
- **ADDED:** `IUatVerifier` interface and `UatVerifier` implementation in `nirmata.Agents/Execution/Verification/UatVerifier/`
- **ADDED:** UAT artifact schema and storage under `.aos/spec/uat/` and `.aos/evidence/runs/RUN-*/artifacts/uat-results.json`
- **ADDED:** Issue creation on UAT failure with structured `ISS-*.json` files under `.aos/spec/issues/`
- **ADDED:** Orchestrator integration: VerifierHandler that routes to FixPlanner on failure
- **MODIFIED:** Gating engine integration to route verification failures to FixPlanner

## Impact
- **Affected specs:** agents-orchestrator-workflow, agents-task-executor, aos-evidence-store
- **Affected code paths:** `nirmata.Agents/Execution/Verification/UatVerifier/**`, `nirmata.Agents/Execution/ControlPlane/`
- **Workspace outputs:** New `.aos/spec/uat/UAT-*.json` and `.aos/spec/issues/ISS-*.json` artifacts

## Related
- Roadmap item: PH-PLN-0009
- Predecessor: agents-task-executor (provides execution results for verification)
