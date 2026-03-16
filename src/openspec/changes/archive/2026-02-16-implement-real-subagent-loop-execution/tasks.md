## 1. Remove Placeholder Implementation
- [x] 1.1 Remove unused `ExecuteSubagentLogicAsync` method from SubagentOrchestrator.cs
- [x] 1.2 Verify no references to the removed method exist in codebase
- [x] 1.3 Update any tests that may reference the placeholder method

## 2. Enhance Subagent Prompt Template
- [x] 2.1 Create comprehensive subagent prompt template with bounded context pack inclusion
- [x] 2.2 Add task plan context to prompt template
- [x] 2.3 Add allowed file scopes enforcement in prompt
- [x] 2.4 Add required verifications instructions to prompt

## 3. Implement Complete Tool Set
- [x] 3.1 Ensure file read/write tools are properly scoped to allowed file scopes
- [x] 3.2 Verify process runner tools for tests/build are available
- [x] 3.3 Add git status/commit tools when enabled by configuration
- [x] 3.4 Validate tool definitions match context pack specifications

## 4. Strengthen Budget Controller
- [x] 4.1 Verify max iterations enforcement in tool calling loop integration
- [x] 4.2 Verify max tool calls enforcement is working
- [x] 4.3 Verify max tokens budget enforcement is functional
- [x] 4.4 Verify wall-clock timeout enforcement is working

## 5. Enhance Evidence Capture
- [x] 5.1 Ensure all tool calls are captured in evidence
- [x] 5.2 Ensure file diffs are captured in evidence
- [x] 5.3 Ensure command outputs are captured in evidence
- [x] 5.4 Ensure final summary hash is computed and stored
- [x] 5.5 Ensure deterministic outputs are generated

## 6. Validation and Testing
- [x] 6.1 Run existing subagent orchestration tests to ensure no regression
- [x] 6.2 Add tests for enhanced prompt template functionality
- [x] 6.3 Add tests for budget controller enforcement
- [x] 6.4 Add tests for evidence capture completeness
- [x] 6.5 Run openspec validate --strict to ensure compliance

## 7. Documentation Updates
- [x] 7.1 Update any documentation referencing placeholder execution
- [x] 7.2 Add documentation for real subagent loop execution capabilities
- [x] 7.3 Update remediation.md to mark this item as completed
