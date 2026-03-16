## 1. Schema Definition and Registry
- [x] 1.1 Define canonical schema for phase plan artifacts
- [x] 1.2 Define canonical schema for task plan artifacts  
- [x] 1.3 Define canonical schema for verifier input artifacts
- [x] 1.4 Define canonical schema for verifier output artifacts
- [x] 1.5 Define canonical schema for fix plan artifacts
- [x] 1.6 Define canonical schema for diagnostic artifacts (CRITICAL)
- [x] 1.7 Register all 6 schemas in aos-schema-registry with versioning
- [x] 1.8 Implement schema version compatibility policies (supported versions, migration rules)
- [x] 1.9 Add schema validation tests for all canonical schemas
- [x] 1.10 Verify schema registry resolves all schemas deterministically

## 2. Writer Validation Implementation
- [x] 2.1 Update phase planner to validate output before writing
- [x] 2.2 Update task executor to validate evidence before writing
- [x] 2.3 Update verifier to validate results before writing
- [x] 2.4 Update fix planner to validate plans before writing
- [x] 2.5 Add writer validation tests with failure scenarios

## 3. Diagnostic Artifact Implementation
- [x] 3.1 Implement DiagnosticArtifact model with canonical schema
- [x] 3.2 Create DiagnosticArtifactWriter for persisting diagnostics to `.aos/diagnostics/{phase}/{id}.diagnostic.json`
- [x] 3.3 Implement diagnostic generation for all validation failure types
- [x] 3.4 Add repair suggestion generation for common validation errors
- [x] 3.5 Create DiagnosticArtifactReader for UI discovery
- [x] 3.6 Add tests for diagnostic artifact generation and persistence
- [ ] 3.7 Verify diagnostic artifacts are discoverable and renderable by UI

## 4. Reader Validation Implementation  
- [x] 4.1 Add schema validation to phase plan readers
- [x] 4.2 Add schema validation to task plan readers
- [x] 4.3 Add schema validation to verifier input readers
- [x] 4.4 Add schema validation to fix plan readers
- [x] 4.5 Implement diagnostic artifact generation on read validation failures
- [x] 4.6 Add reader validation tests with diagnostic verification

## 5. Workflow Component Updates
- [x] 5.1 Update phase planner output format to use canonical schema
- [x] 5.2 Update task executor scope extraction for unified contracts
- [x] 5.3 Update verifier acceptance criteria extraction logic
- [x] 5.4 Update fix planner to consume unified task plan format
- [x] 5.5 Add LLM structured output integration for phase planner
- [x] 5.6 Add LLM structured output integration for fix planner
- [x] 5.7 Add end-to-end workflow tests with unified contracts

## 6. UI and Tooling Updates
- [x] 6.1 Create DiagnosticArtifactRenderer component for UI display
- [x] 6.2 Update PhasePlanViewer to show diagnostic artifacts alongside invalid plans
- [x] 6.3 Update TaskPlanViewer to display validation status and diagnostics
- [x] 6.4 Update VerificationResultsViewer to show diagnostic artifacts
- [x] 6.5 Update FixPlanViewer to display diagnostic artifacts
- [x] 6.6 Add "Repair Suggestions" UI panel with actionable guidance
- [x] 6.7 Implement validation status indicator in all artifact views
- [x] 6.8 Create CLI command `nirmata validate-artifact --path <path>` for manual validation
- [x] 6.9 Create CLI command `nirmata list-diagnostics --workspace <path>` for diagnostic discovery

## 7. Migration and Compatibility
- [x] 7.1 Define artifact format detection rules for each artifact type
- [x] 7.2 Define transformation rules from old → new schema for each artifact type
- [x] 7.3 Implement migration CLI command: `nirmata migrate-schemas --workspace-path <path> [--dry-run] [--backup]`
- [x] 7.4 Add rollback capability to restore original artifacts from backup
- [x] 7.5 Validate all migrated artifacts against canonical schemas
- [x] 7.6 Add migration tests with sample workspaces
- [x] 7.7 Document migration process and deprecation timeline
- [x] 7.8 Create migration guide for users

## 8. LLM Integration and Validation
- [x] 8.1 Implement schema passing to LLM via structured output mode
- [x] 8.2 Add retry logic for LLM validation failures (up to 3 retries)
- [x] 8.3 Implement diagnostic creation for LLM validation failures
- [x] 8.4 Add provider-specific documentation (OpenAI, Anthropic, etc.)
- [x] 8.5 Test LLM structured output with phase planner
- [x] 8.6 Test LLM structured output with fix planner
- [x] 8.7 Test LLM validation with verifier

## 9. Verification and Testing
- [x] 9.1 Add unit tests for all schema validators
- [x] 9.2 Add unit tests for diagnostic artifact generation
- [x] 9.3 Add integration tests for reader validation with diagnostics
- [x] 9.4 Add integration tests for writer validation with diagnostics
- [x] 9.5 Add E2E test: Full workflow (Planner → Executor → Verifier → FixPlanner) with unified contracts
- [x] 9.6 Add E2E test: Artifact chaining without manual patching
- [x] 9.7 Add E2E test: Diagnostic discovery and rendering
- [x] 9.8 Run full agent workflow test suite with unified contracts
- [x] 9.9 Validate strict openspec validation passes
- [x] 9.10 Performance testing for validation overhead
- [x] 9.11 Create verification notes document with test results
