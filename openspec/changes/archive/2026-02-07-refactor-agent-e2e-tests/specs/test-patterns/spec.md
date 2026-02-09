## ADDED Requirements

### Requirement: Contract Testing for Fakes

All fake implementations SHALL have corresponding contract tests that verify interface compliance.

#### Scenario: Verify Fake Interface Compliance
Given a fake implementation `FakeX` that implements interface `IX`  
When the test suite runs  
Then a contract test validates that `FakeX` correctly implements all members of `IX`  
And the test fails if method signatures, return types, or parameter types differ.

---

### Requirement: E2E Test Evidence Verification

End-to-end tests SHALL verify the complete evidence folder structure is created correctly.

#### Scenario: Verify Evidence Folder Structure
Given an orchestrator workflow execution  
When the workflow completes successfully  
Then the evidence folder contains:
- `run.json` with run metadata
- `commands.json` with dispatched commands  
- `summary.json` with final status
- `logs/` directory for tool output
- `artifacts/` directory for run outputs

---

### Requirement: Handler Test Host

Tests requiring multiple handler dependencies MUST use a centralized test host for DI configuration.

#### Scenario: Configure Handler Dependencies
Given a test needs an `Orchestrator` with 7 handlers  
When the test initializes  
Then it uses `HandlerTestHost` to configure dependencies  
And it can override specific handlers with mocks  
And it disposes all resources properly on test completion.

---

### Requirement: Test Workspace Builder

Integration tests MUST use a fluent builder to create consistent `.aos/` workspace structures.

#### Scenario: Create Consistent Workspace Structure
Given an integration test needs a workspace with specific state  
When the test initializes  
Then it uses `AosTestWorkspaceBuilder` to create the structure  
And the workspace is disposable (cleans up on test completion)  
And the structure matches the canonical `.aos/` layout.

---

## MODIFIED Requirements

### Requirement: Fake Implementation Maintenance

Fake implementations SHALL be updated immediately when interface contracts change.

#### Scenario: Synchronize Fakes with Interface Changes
Given an interface `IX` changes its method signature  
When the codebase is compiled  
Then all fakes implementing `IX` must be updated to match  
And the build must fail until fakes are synchronized.

---

### Requirement: Test Namespace Consolidation

All agent plane tests MUST use the `Gmsd.Agents.Tests.Execution` namespace consistently.

#### Scenario: Use Consistent Test Namespaces
Given tests exist for the agent plane  
When they reference namespaces  
Then they must use `Gmsd.Agents.Tests.Execution.*`  
And the obsolete `Gmsd.Agents.Tests.Workflows` namespace must not be referenced.

---

## REMOVED Requirements

None - this is a refactoring effort, not a removal of capabilities.
