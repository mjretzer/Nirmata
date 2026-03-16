## MODIFIED Requirements
### Requirement: Subagent orchestrator uses tool calling loop
The system SHALL modify `ISubagentOrchestrator` to use `IToolCallingLoop` as its execution mechanism.

Previously: `SubagentOrchestrator` directly invoked tools or had ad-hoc LLM interaction logic.

Now: `SubagentOrchestrator.RunSubagentAsync` MUST:
1. Construct `ToolCallingRequest` from `SubagentRunRequest` context
2. Invoke `IToolCallingLoop.ExecuteAsync`
3. Map `ToolCallingResult` to `SubagentRunResult`
4. Include tool calling evidence in subagent evidence capture

`SubagentRunRequest` MUST accept optional `ToolCallingOptions? ToolOptions` for loop configuration.

#### Scenario: Subagent runs with tool calling loop
- **GIVEN** a `SubagentRunRequest` for task TSK-0001 with file scope
- **WHEN** `RunSubagentAsync` executes
- **THEN** the subagent uses `IToolCallingLoop` to handle LLM interactions and tool execution
- **AND** evidence includes both subagent metadata and tool calling conversation details

#### Scenario: Subagent passes tool options to loop
- **GIVEN** a `SubagentRunRequest` with `ToolOptions.MaxIterations = 5`
- **WHEN** the subagent runs
- **THEN** the tool calling loop respects the 5-iteration limit

#### Scenario: Subagent uses comprehensive prompt template
- **GIVEN** a subagent execution request with bounded context pack PCK-0001
- **WHEN** the tool calling loop constructs initial messages
- **THEN** the system prompt includes bounded context pack, task plan, allowed file scopes, and required verifications
- **AND** the prompt enforces file scope boundaries and verification requirements

#### Scenario: Subagent evidence capture is comprehensive
- **GIVEN** a subagent execution that modifies files and runs commands
- **WHEN** the execution completes
- **THEN** evidence includes all tool calls, file diffs, command outputs, final summary hash, and deterministic outputs
- **AND** evidence is stored under `.aos/evidence/runs/<run-id>/` in deterministic JSON format

## ADDED Requirements
### Requirement: Subagent prompt template includes bounded context
The system SHALL construct comprehensive prompt templates that include bounded context pack information for subagent execution.

The prompt template MUST include:
- Bounded context pack contents with file contents and metadata
- Task plan with specific steps and acceptance criteria
- Allowed file scopes with explicit boundaries
- Required verifications and success criteria
- Tool usage guidelines and constraints

#### Scenario: Prompt template enforces file scope boundaries
- **GIVEN** a subagent with allowed file scope `src/components/*`
- **WHEN** the subagent attempts to modify files outside scope
- **THEN** the prompt template explicitly instructs the subagent to stay within allowed scopes
- **AND** tool calling loop enforces scope violations at execution time

### Requirement: Subagent tool set is comprehensive and scoped
The system SHALL provide a complete tool set for subagent execution with proper scoping and enforcement.

The tool set MUST include:
- File read/write tools scoped to allowed file paths
- Process runner tools for tests and build operations
- Git status/commit tools when enabled by configuration
- Evidence capture tools for logging and diff tracking

#### Scenario: File tools respect scope boundaries
- **GIVEN** a subagent with allowed file scope `src/**/*.ts`
- **WHEN** the subagent attempts to read `config/database.json`
- **THEN** the file read tool fails with scope violation error
- **AND** the error is captured in evidence with clear boundary information

### Requirement: Subagent budget controller enforces all limits
The system SHALL enforce comprehensive budget controls for subagent execution to prevent resource exhaustion.

Budget controls MUST enforce:
- Maximum iterations of the tool calling loop
- Maximum tool calls per execution
- Maximum token consumption (input + output)
- Wall-clock timeout limits

#### Scenario: Budget limits prevent runaway execution
- **GIVEN** a subagent budget with MaxIterations = 10 and MaxTokens = 50000
- **WHEN** the subagent reaches 10 iterations
- **THEN** execution stops with clear budget exceeded error
- **AND** evidence includes iteration count and token usage details

### Requirement: Subagent evidence capture includes deterministic outputs
The system SHALL capture comprehensive evidence from subagent execution with deterministic output generation.

Evidence capture MUST include:
- All tool calls with arguments and results
- File diffs for all modified files
- Command outputs and error streams
- Final summary hash of execution results
- Deterministic JSON output formatting

#### Scenario: Evidence provides complete execution audit trail
- **GIVEN** a completed subagent execution
- **WHEN** examining the evidence folder
- **THEN** it contains complete tool call history, file modifications, command outputs, and a deterministic hash
- **AND** the evidence can be used to reproduce or verify the execution results
