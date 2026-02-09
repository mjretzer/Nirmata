## ADDED Requirements
### Requirement: AOS engine fixture/snapshot regression coverage
The repository SHALL include deterministic fixture/snapshot regression coverage for the AOS engine so that nondeterministic drift is detected automatically.

#### Scenario: CI fails on drift
- **WHEN** `dotnet test` runs in CI
- **THEN** AOS engine fixture/snapshot regression tests run and fail if produced outputs differ from the approved fixtures

