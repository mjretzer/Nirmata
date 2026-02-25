# Improvements to implement-subagent-execution OpenSpec Change

## Overview
This document summarizes the enhancements made to the implement-subagent-execution proposal to improve clarity, completeness, and implementability.

## Key Improvements

### 1. Design Document Enhancements

#### Scope Firewall Clarification
- **Added**: Explicit design pattern specification (decorator pattern wrapping `ITool` instances)
- **Added**: Path normalization strategy using `Path.GetFullPath()`
- **Added**: Clarification that firewall applies to **both read and write operations** to prevent information leakage
- **Added**: Concrete code example showing `ScopedTool` decorator implementation with `ValidatePathsInCall()` method

#### Verification Tools Specification
- **Added**: Detailed specifications for `run_build` tool:
  - 5-minute timeout enforcement
  - Structured output format: `{ success: bool, logs: string, duration: TimeSpan, errors: string[] }`
  - Scope firewall application to build output paths
- **Added**: Detailed specifications for `run_test` tool:
  - `--logger:trx` flag for structured test result parsing
  - 10-minute timeout enforcement
  - Structured output format with test counts and failure details
  - Automatic UAT evidence artifact capture

#### Evidence Capture Specification
- **Added**: Detailed format specifications for all evidence artifacts:
  - `tool-calls.ndjson`: ISO8601 timestamps, tool names, inputs/outputs, duration, success/error tracking
  - `changes.patch`: RFC 3881 unified diff format with deterministic output
  - `execution-summary.json`: Complete JSON schema with example structure
  - `deterministic-hash`: SHA256 algorithm and input specification
- **Added**: Clarification on determinism: same inputs always produce identical outputs

### 2. Tasks Document Enhancements

#### Granular Task Breakdown
- **Expanded** Section 1 (Scope Firewall) from 4 to 10 tasks with specific implementation details
- **Expanded** Section 2 (Verification Tools) from 4 to 7 tasks with tool-specific requirements
- **Expanded** Section 3 (Evidence Enhancement) from 4 to 8 tasks with detailed artifact specifications
- **Expanded** Section 4 (Loop Refinement) from 3 to 8 tasks with error handling and E2E test scenarios
- **Expanded** Section 5 (Verification) from 3 to 7 verification steps with artifact validation

#### Implementation Guidance
- **Added**: Specific class names and interfaces to implement (e.g., `IScopeFirewall`, `ScopedTool`, `DiffGenerator`)
- **Added**: Specific method signatures and return types
- **Added**: Edge case testing requirements (symlink traversal, relative path normalization)
- **Added**: Timeout and resource limit specifications
- **Added**: Test scenario descriptions (success, failure, timeout cases)

### 3. Specification Document Enhancements

#### Scope Firewall Requirements
- **Added**: Explicit requirement for path normalization using `Path.GetFullPath()`
- **Added**: Clarification on read/write operation coverage
- **Added**: New scenario: "Symlink traversal is blocked" with specific example
- **Added**: Deterministic error handling requirement

#### Verification Tools Requirements
- **Added**: Detailed tool specifications with timeouts and output formats
- **Added**: New scenario: "Build tool enforces timeout" demonstrating timeout behavior
- **Added**: Structured output format specifications for both tools

#### Evidence Artifact Requirements
- **Added**: Detailed format specifications for each artifact type
- **Added**: Complete JSON schema example for `execution-summary.json`
- **Added**: Determinism requirements and scenarios
- **Added**: New scenario: "Evidence is deterministic" ensuring reproducibility

#### Completion and Error Handling
- **Added**: New requirement section: "Loop handles completion and error states"
- **Added**: Specific completion criteria: natural completion, max iterations, scope violations
- **Added**: Error handling strategies for different failure modes
- **Added**: Three new scenarios covering completion, max iterations, and scope violations

## Impact on Implementation

### Reduced Ambiguity
- Clear specifications for all interfaces and classes to implement
- Explicit timeout values (5 min for build, 10 min for tests)
- Deterministic output requirements prevent implementation variations

### Improved Testability
- Specific test scenarios with given/when/then format
- Edge cases explicitly documented (symlink traversal, path normalization)
- Evidence artifact validation criteria clearly defined

### Better Error Handling
- Explicit error states and completion criteria
- Scope violation handling strategy defined
- Timeout behavior specified for all tools

### Audit and Compliance
- Evidence artifact formats standardized
- Deterministic hash for integrity verification
- Complete execution history captured in structured format

## Files Modified

1. **design.md**: Enhanced with concrete implementation patterns and detailed specifications
2. **tasks.md**: Expanded from 30 to 73 lines with granular, actionable tasks
3. **specs/agents-subagent-execution/spec.md**: Enhanced with detailed requirements and scenarios

## Next Steps

The proposal is now ready for:
1. OpenSpec validation: `openspec validate implement-subagent-execution --strict`
2. Implementation following the detailed task breakdown
3. Verification against the comprehensive specification scenarios
