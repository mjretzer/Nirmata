## Context
The GMSD workflow engine consists of multiple phases that must exchange artifacts reliably:
- Phase Planner creates task plans (LLM-generated)
- Task Executor executes plans and produces evidence  
- UAT Verifier validates execution results (LLM-generated acceptance criteria)
- Fix Planner creates remediation tasks (LLM-generated)

Currently each phase uses its own JSON contract format, requiring manual translation and causing failures when artifacts don't match expected shapes. Additionally, LLM-generated artifacts lack strict validation, leading to malformed plans that downstream phases cannot process.

## Goals / Non-Goals
- **Goals:**
  - Single canonical schema per artifact type (6 schemas total)
  - Reader and writer validation enforcement at all phase boundaries
  - Normalized diagnostic artifacts on all validation failures with repair suggestions
  - Strict LLM output validation using structured schemas for LLM-generated artifacts
  - Deterministic artifact chaining: Planner → Executor → Verifier → FixPlanner works without manual patching
  - Clear diagnostic discovery and UI rendering for validation failures
- **Non-Goals:**
  - Backwards compatibility with existing broken contracts
  - Runtime schema translation (fix at source instead)
  - Support for custom/user-defined schemas

## Decisions
- **Decision:** Use JSON Schema with strict mode for LLM output validation
  - **Alternatives considered:** Manual validation, runtime translation, per-phase schemas
  - **Rationale:** JSON Schema provides deterministic validation and enables strict LLM mode

- **Decision:** Schema registry manages canonical versions and compatibility
  - **Alternatives considered:** File-based versioning, inline schema definitions
  - **Rationale:** Centralized registry ensures consistency and supports migration

- **Decision:** Validation on both write and read with different failure modes
  - **Alternatives considered:** Write-only validation, read-only validation
  - **Rationale:** Write validation prevents bad artifacts, read validation detects corruption/migration issues

- **Decision:** Diagnostic artifacts are separate files with canonical schema
  - **Alternatives considered:** Inline error responses, error logs, exception messages
  - **Rationale:** Separate diagnostic artifacts enable UI rendering, discovery, and repair workflows

- **Decision:** Diagnostic artifacts are written to `.aos/diagnostics/{phase}/{artifact-id}.diagnostic.json`
  - **Alternatives considered:** Same directory as artifact, centralized log, database
  - **Rationale:** Colocated with artifacts for easy discovery, deterministic naming for UI integration

- **Decision:** LLM-generated artifacts use strict schema validation; internal artifacts use standard validation
  - **Alternatives considered:** Uniform validation, no LLM validation, post-processing repair
  - **Rationale:** Strict validation prevents malformed LLM output early; internal artifacts have more flexibility

## Risks / Trade-offs
- **Risk:** Schema migration complexity for existing workspaces
  - **Mitigation:** Versioned schemas with migration tooling
- **Risk:** Strict validation may reject useful but slightly malformed artifacts
  - **Mitigation:** Detailed diagnostic artifacts with repair suggestions
- **Trade-off:** Increased upfront complexity vs long-term reliability
  - **Acceptance:** Reliability gains justify complexity for critical workflow engine

## Diagnostic Artifact Strategy
Validation failures at any phase boundary produce normalized diagnostic artifacts to enable UI rendering and repair workflows.

**Diagnostic Artifact Structure:**
```json
{
  "schemaVersion": 1,
  "schemaId": "gmsd:aos:schema:diagnostic:v1",
  "artifactPath": ".aos/spec/tasks/TSK-0001/plan.json",
  "failedSchemaId": "gmsd:aos:schema:task-plan:v1",
  "failedSchemaVersion": 1,
  "timestamp": "2026-02-19T18:07:00Z",
  "phase": "phase-planning",
  "context": {
    "taskId": "TSK-0001",
    "runId": "RUN-0001"
  },
  "validationErrors": [
    {
      "path": "$.fileScopes[0]",
      "message": "fileScopes[0] must be object with 'path' field, got string",
      "expected": "object with {path: string}",
      "actual": "string"
    }
  ],
  "repairSuggestions": [
    "Transform fileScopes from array of strings to array of objects: [{path: \"file1\"}, {path: \"file2\"}]",
    "Ensure all required fields are present: path, type, scope"
  ]
}
```

**Diagnostic Persistence:**
- Path: `.aos/diagnostics/{phase}/{artifact-id}.diagnostic.json`
- Example: `.aos/diagnostics/phase-planning/TSK-0001.diagnostic.json`
- Diagnostics are created whenever validation fails, before any artifact is written
- Diagnostics are discoverable by UI components via standard file enumeration

## LLM Integration Strategy
LLM-generated artifacts (Phase Planner, Fix Planner, UAT Verifier) use strict schema validation.

**For Phase Planner and Fix Planner:**
1. Pass canonical JSON Schema to LLM via structured output mode (provider-specific)
2. Require LLM output to match schema exactly
3. If LLM output fails validation:
   - Retry with schema clarification in prompt
   - After 3 retries, fail with diagnostic artifact
   - Do NOT attempt runtime repair

**For UAT Verifier:**
1. Extract acceptance criteria from task plan (already validated)
2. Pass criteria structure to LLM for verification logic generation
3. Validate LLM-generated verification results against verifier-output schema
4. If validation fails, create diagnostic and mark verification as failed

**Schema Passing Mechanism:**
- Use provider's native structured output support (e.g., OpenAI's `response_format`)
- Include schema as JSON Schema in system prompt as fallback
- Document provider-specific integration in implementation

## Migration Plan
**Phase 1: Preparation**
1. Define all 6 canonical schemas in aos-schema-registry
2. Implement schema validation infrastructure
3. Add migration detection and transformation rules

**Phase 2: Gradual Adoption**
1. Update Phase Planner to emit new schema format
2. Add reader validation to Task Executor (accept both old and new)
3. Update Task Executor to emit new evidence format
4. Add reader validation to UAT Verifier
5. Update UAT Verifier to emit new results format
6. Update Fix Planner to read new formats and emit new fix plans

**Phase 3: Migration Execution**
1. Run migration CLI on existing workspaces
2. Validate all migrated artifacts
3. Archive old artifacts (keep for rollback)

**Phase 4: Cleanup**
1. Remove old format support from readers
2. Archive old schema definitions
3. Update documentation

**Migration Tooling:**
- CLI command: `gmsd migrate-schemas --workspace-path <path> [--dry-run] [--backup]`
- Detects old artifact formats automatically
- Applies transformation rules
- Validates transformed artifacts
- Creates rollback archive

## Open Questions Resolved
- **Should validation failures be fatal or produce repairable diagnostics?**
  - Answer: Validation failures are fatal (prevent artifact write), but diagnostic artifacts enable repair workflows
- **How to handle schema version mismatches during migration?**
  - Answer: Schema registry defines supported versions; migration transforms to current version
- **What level of schema strictness for LLM outputs vs internal artifacts?**
  - Answer: LLM outputs use strict validation with retries; internal artifacts use standard validation with diagnostic generation
