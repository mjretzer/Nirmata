## Context
The subagent orchestrator currently has a placeholder implementation that simulates execution instead of using the real tool-calling loop. While the real implementation exists and is being used, the placeholder code creates confusion and potential for regression. The remediation item calls for implementing a real subagent loop with comprehensive prompt templates, tool sets, budget control, and evidence capture.

## Goals / Non-Goals
- Goals: Remove placeholder code, ensure all subagent execution uses real tool-calling loop, enhance prompt templates and evidence capture
- Non-Goals: Change the overall subagent orchestration architecture, modify the tool-calling loop interface

## Decisions
- Decision: Remove the unused `ExecuteSubagentLogicAsync` placeholder method entirely
  - Alternatives considered: Keep method for backward compatibility, mark as obsolete
  - Rationale: Method is not called anywhere and creates confusion; real implementation already exists

- Decision: Enhance the existing `ExecuteSubagentLogicWithBudgetAsync` method with comprehensive prompt templates
  - Alternatives considered: Create separate prompt template service, use external template files
  - Rationale: Keep implementation simple and self-contained; can evolve to external templates later if needed

- Decision: Strengthen evidence capture within the existing tool-calling loop integration
  - Alternatives considered: Add separate evidence capture layer, use external logging system
  - Rationale: Evidence capture is already integrated with tool-calling loop; enhance existing implementation

## Risks / Trade-offs
- Risk: Removing placeholder method may break tests that reference it
  - Mitigation: Search codebase for references before removal, update tests accordingly
- Risk: Enhanced prompt templates may increase token consumption
  - Mitigation: Keep prompts concise and focused, monitor token usage in tests
- Trade-off: More comprehensive evidence capture increases storage requirements
  - Mitigation: Evidence is already being captured; enhancements are incremental

## Migration Plan
1. Search codebase for references to placeholder method
2. Remove placeholder method from SubagentOrchestrator.cs
3. Update any tests that reference the removed method
4. Enhance prompt templates in existing method
5. Strengthen evidence capture in existing integration
6. Run comprehensive tests to ensure no regression
7. Update documentation

## Open Questions
- Should prompt templates be externalized to configuration files in the future?
- Should evidence capture compression be considered for long-running subagent executions?
- Are there additional tools that should be included in the standard subagent tool set?
