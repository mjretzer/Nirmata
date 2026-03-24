## ADDED Requirements

### Requirement: Verification page uses workspace issue and UAT data
The system SHALL render verification state from workspace-backed issue and UAT summaries instead of local mock data.

#### Scenario: Open issues appear in verification view
- **WHEN** a user opens `VerificationPage` for a workspace with open issues
- **THEN** the page shows the issues with their persisted severity and status

#### Scenario: UAT failures are visible in verification view
- **WHEN** the current task or phase has failed UAT checks in the workspace summary
- **THEN** the page reflects the failed state without relying on client-side mock derivation

### Requirement: Failed UAT checks can create issues
The system SHALL allow the verification workflow to create a persisted issue from a failed UAT result.

#### Scenario: Issue creation from UAT failure
- **WHEN** a user creates an issue from a failed UAT result in `VerificationPage`
- **THEN** the issue is posted to the workspace issue endpoint
- **AND** the created issue is persisted under `.aos/spec/issues/`

### Requirement: Fix page is reachable and issue-driven
The system SHALL expose `FixPage` in the router and allow it to render fix work derived from open issues.

#### Scenario: Fix page route loads
- **WHEN** a user navigates to the fix workflow route
- **THEN** the application renders `FixPage`

#### Scenario: Open issues seed fix work
- **WHEN** `FixPage` loads for a workspace with open issues
- **THEN** the page can display fix tasks derived from those issues
