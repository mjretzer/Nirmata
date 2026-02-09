## 1. Interface Contracts
- [x] 1.1 Define `IRoadmapModifier` interface with Insert/Remove/Renumber operations
- [x] 1.2 Define `IRoadmapRenumberer` interface for ID sequencing logic
- [x] 1.3 Define `RoadmapModifyRequest` and `RoadmapModifyResult` models

## 2. Core Implementation
- [x] 2.1 Implement `RoadmapRenumberer` for consistent phase ID sequencing (PH-####)
- [x] 2.2 Implement `RoadmapModifier.InsertPhaseAsync` with position insertion
- [x] 2.3 Implement `RoadmapModifier.RemovePhaseAsync` with safety checks
- [x] 2.4 Implement cursor coherence preservation logic

## 3. Safety & Issue Handling
- [x] 3.1 Implement active phase detection (checks state.cursor.phaseId)
- [x] 3.2 Implement force flag handling for active phase removal
- [x] 3.3 Implement issue creation when removal is blocked (`ISS-####`)
- [x] 3.4 Implement roadmap validation after modifications

## 4. Event & State Management
- [x] 4.1 Implement `roadmap.modified` event emission to `events.ndjson`
- [x] 4.2 Implement `roadmap.blocker` event emission on removal failure
- [x] 4.3 Implement state.json updates for cursor adjustments
- [x] 4.4 Implement atomic spec writes (roadmap.json + state.json)

## 5. Orchestrator Integration
- [x] 5.1 Implement `RoadmapModifierHandler` following handler pattern
- [x] 5.2 Integrate with `IRunLifecycleManager` for evidence capture
- [x] 5.3 Add gating logic for roadmap modification commands

## 6. Testing
- [x] 6.1 Write unit tests for `RoadmapRenumberer` logic
- [x] 6.2 Write unit tests for `RoadmapModifier` models
- [x] 6.3 Write unit tests for `RoadmapValidator`
- [x] 6.4 Write integration tests for full modification workflow
- [x] 6.5 Write tests for active phase removal blocker scenario
- [x] 6.6 Validate all tests pass with `AosWorkspaceValidator`
