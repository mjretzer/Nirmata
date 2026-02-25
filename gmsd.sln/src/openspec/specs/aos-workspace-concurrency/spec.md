# aos-workspace-concurrency Specification

## Purpose
TBD - created by archiving change harden-orchestrator-governance. Update Purpose after archive.
## Requirements
### Requirement: Concurrency configuration schema
The system SHALL read concurrency limits from `.aos/config/concurrency.json`.

The configuration file MUST include:
- `maxParallelTasks` (integer; default 3): maximum concurrent task executions
- `maxParallelLlmCalls` (integer; default 2): maximum concurrent LLM provider calls
- `taskQueueSize` (integer; default 10): maximum pending tasks in queue

All limits MUST be:
- Positive integers (>= 1)
- Configurable without code changes
- Applied consistently across all runs

#### Scenario: Default concurrency limits are applied
- **GIVEN** `.aos/config/concurrency.json` does not exist or is empty
- **WHEN** task executor starts
- **THEN** defaults are used: maxParallelTasks=3, maxParallelLlmCalls=2, taskQueueSize=10

#### Scenario: Custom concurrency limits are respected
- **GIVEN** `.aos/config/concurrency.json` specifies `maxParallelTasks: 5`
- **WHEN** task executor runs
- **THEN** at most 5 tasks execute in parallel

#### Scenario: Invalid concurrency limits are rejected
- **GIVEN** `.aos/config/concurrency.json` specifies `maxParallelTasks: 0`
- **WHEN** configuration is loaded
- **THEN** an error is raised indicating limit must be >= 1

### Requirement: Task queue with parallel execution limit
The system SHALL enforce `maxParallelTasks` limit on concurrent task execution.

The implementation MUST:
- Queue incoming tasks
- Execute up to `maxParallelTasks` tasks in parallel
- Block new task submissions if queue is full (size >= `taskQueueSize`)
- Return queue-full error with actionable message
- Dequeue and start next task when a task completes

#### Scenario: Tasks are queued when limit is reached
- **GIVEN** `maxParallelTasks: 2` and 5 tasks submitted
- **WHEN** first 2 tasks start executing
- **THEN** remaining 3 tasks are queued
- **AND** new tasks do not start until a running task completes

#### Scenario: Queue overflow is prevented
- **GIVEN** `taskQueueSize: 3` and 5 tasks submitted
- **WHEN** 2 tasks are running and 3 are queued
- **THEN** the 5th task submission fails with queue-full error
- **AND** the error message indicates current queue depth and limit

#### Scenario: Tasks start from queue when slot becomes available
- **GIVEN** 2 tasks running, 3 queued, `maxParallelTasks: 2`
- **WHEN** one running task completes
- **THEN** the next queued task immediately starts
- **AND** queue depth decreases by 1

### Requirement: LLM call rate limiting
The system SHALL enforce `maxParallelLlmCalls` limit on concurrent LLM provider calls.

The implementation MUST:
- Track active LLM calls per provider
- Block new LLM calls if limit reached
- Return rate-limit error with retry guidance
- Release slot when call completes (success or failure)
- Apply limit globally across all runs

#### Scenario: LLM calls are rate-limited
- **GIVEN** `maxParallelLlmCalls: 2` and 5 concurrent tasks requesting LLM calls
- **WHEN** first 2 tasks call LLM
- **THEN** remaining 3 tasks receive rate-limit error
- **AND** error message indicates current call count and limit

#### Scenario: LLM call slot is released on completion
- **GIVEN** 2 LLM calls active, `maxParallelLlmCalls: 2`
- **WHEN** one call completes
- **THEN** the next waiting task can immediately call LLM
- **AND** active call count decreases by 1

#### Scenario: Rate-limit error includes retry guidance
- **GIVEN** a task receives rate-limit error
- **WHEN** error is returned
- **THEN** error message includes suggested retry delay (e.g., "retry after 5 seconds")
- **AND** error is distinguishable from other LLM errors

### Requirement: Concurrency metrics and observability
The system SHALL expose concurrency metrics for monitoring and debugging.

Metrics MUST include:
- Current active task count
- Current queued task count
- Current active LLM call count
- Queue depth (as percentage of `taskQueueSize`)
- Task completion rate (tasks/minute)

Metrics MUST be:
- Available via `report-progress` command
- Logged periodically (default: every 5 minutes)
- Queryable without stopping execution

#### Scenario: Metrics are included in progress report
- **GIVEN** a running workflow
- **WHEN** `report-progress` is invoked
- **THEN** output includes current active tasks, queued tasks, LLM calls
- **AND** metrics are current (not stale)

#### Scenario: Metrics are logged periodically
- **GIVEN** a long-running workflow
- **WHEN** 5 minutes elapse
- **THEN** service logs include concurrency metrics
- **AND** logs are at INFO level (not DEBUG)

### Requirement: Concurrency configuration validation
The system SHALL validate concurrency configuration at startup and on config reload.

Validation MUST:
- Ensure all limits are positive integers
- Ensure `taskQueueSize >= maxParallelTasks` (queue must fit at least one batch)
- Reject invalid configurations with clear error messages
- Prevent runtime configuration changes (restart required)

#### Scenario: Configuration is validated at startup
- **GIVEN** invalid concurrency configuration
- **WHEN** service starts
- **THEN** startup fails with clear error message
- **AND** error indicates which limit is invalid and why

#### Scenario: Queue size must accommodate parallel tasks
- **GIVEN** `maxParallelTasks: 5` and `taskQueueSize: 3`
- **WHEN** configuration is validated
- **THEN** validation fails with error indicating queue too small
- **AND** suggested fix is provided (e.g., "increase taskQueueSize to >= 5")

### Requirement: Graceful degradation under load
The system SHALL degrade gracefully when concurrency limits are exceeded.

Behavior MUST:
- Queue tasks instead of rejecting (up to `taskQueueSize`)
- Return clear rate-limit errors when queue is full
- Not crash or corrupt state under overload
- Allow operator to increase limits and restart service

#### Scenario: System remains stable under overload
- **GIVEN** 100 tasks submitted with `maxParallelTasks: 3` and `taskQueueSize: 10`
- **WHEN** all tasks are submitted
- **THEN** first 3 execute, next 10 queue, remaining 87 receive queue-full error
- **AND** system remains responsive and stable
- **AND** no state corruption occurs

#### Scenario: Operator can increase limits and restart
- **GIVEN** system is overloaded with queue-full errors
- **WHEN** operator increases `maxParallelTasks` and restarts service
- **THEN** service starts with new limits
- **AND** queued tasks resume execution
- **AND** previously rejected tasks can be resubmitted

