# Change: Unify Data Contracts Across Planning, Execution, Verification, and Fix Phases

## Why
Current workflow phases (Roadmapper → Planner → Executor → Verifier → FixPlanner) use incompatible JSON contracts, causing manual patching and preventing reliable artifact chaining between phases. This blocks the ability to run Roadmapper → Planner → Executor → Verifier → FixPlanner as a reliable automated loop.

## What Changes
- **Define canonical JSON schemas** for all workflow artifacts: phase plan, task plan, verifier inputs/outputs, fix plan, and diagnostic artifacts
- **Enforce schema validation** on both write (writer validates before persistence) and read (reader validates and produces normalized diagnostics on failure)  
- **Implement diagnostic artifacts** for all validation failures with actionable repair suggestions
- **Update workflow components** to use unified contracts:
  - Phase planner output format with strict LLM validation
  - Task executor scope extraction logic with read validation
  - Verifier acceptance criteria extraction logic with read/write validation
  - Fix planner input/output validation
  - UI rendering of artifacts and diagnostic artifacts
- **Provide migration tooling** to transform existing artifacts to unified schemas
- **BREAKING**: All existing artifact formats must migrate to unified schemas

## Scope
**In Scope:**
- Define 6 canonical schemas (phase-plan, task-plan, verifier-input, verifier-output, fix-plan, diagnostic)
- Implement write validation for all planners and verifiers
- Implement read validation for all executors and consumers
- Create diagnostic artifact generation and persistence
- Add migration CLI tooling
- Update UI to render diagnostics

**Out of Scope:**
- Backwards compatibility with broken contracts (migration is one-way)
- Runtime schema translation (fix at source instead)
- Support for custom/user-defined schemas

## Impact
- **Affected specs:** phase-planning, agents-task-executor, agents-uat-verifier, agents-fix-planner, aos-schema-registry
- **Affected code:** Gmsd.Agents planning/execution/verification workflows, Gmsd.Aos schema registry, UI artifact rendering, CLI tools
- **Breaking changes:** All artifact formats change; migration required for existing workspaces
- **Migration path:** Detect old formats → Transform → Validate → Archive old artifacts
