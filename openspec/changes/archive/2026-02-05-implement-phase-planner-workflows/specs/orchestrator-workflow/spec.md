## MODIFIED Requirements

### Requirement: Gating engine evaluates 6-phase routing logic
The system SHALL provide an `IGatingEngine` interface that evaluates workspace state in priority order:
1. Missing project spec → route to **Interviewer**
2. Missing roadmap → route to **Roadmapper**
3. Missing phase plan → route to **Planner** (now implemented)
4. Ready to execute → route to **Executor**
5. Execution complete, verification pending → route to **Verifier**
6. Verification failed → route to **FixPlanner**

#### Scenario: Gating routes to Planner when plan missing
- **GIVEN** a workspace with project spec and roadmap, but no tasks planned for current phase at cursor
- **WHEN** `EvaluateAsync` is called
- **THEN** result indicates `TargetPhase: Planner` with reason "No plan exists for current cursor position" and routes to working PhasePlannerHandler
