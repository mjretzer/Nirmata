# web-uat-page Specification

## Purpose
Provides a web interface for UAT (User Acceptance Testing) verification, including a "Verify work" wizard, checklist building from acceptance criteria, pass/fail recording, and issue creation for failures.

## ADDED Requirements

### Requirement: UAT List Page
The `Gmsd.Web` project SHALL provide a `/Uat` page that displays all UAT specifications and verification results.

The implementation MUST:
- Read UAT specs from `.aos/spec/uat/index.json` and individual UAT files
- Read verification results from `.aos/evidence/runs/*/artifacts/uat-results.json`
- Display UAT items in a table with columns: UAT ID, Task, Status, Last Verified, Result
- Show status indicators: not_started, in_progress, passed, failed
- Link each UAT to its verify page (`/Uat/Verify?id={uatId}`)
- Link to associated task (`/Tasks/Details?id={taskId}`)
- Filter by status, task, or result

#### Scenario: Display UAT list
- **GIVEN** UAT specs UAT-TSK-000001 and UAT-TSK-000002 with various statuses
- **WHEN** a user navigates to `/Uat`
- **THEN** the page displays all UAT items with their status and last verification result

#### Scenario: Filter UAT by status
- **GIVEN** UAT items with statuses "passed" and "failed"
- **WHEN** the user filters by result "failed"
- **THEN** only failed UAT items are displayed

### Requirement: Verify Work Wizard
The `Gmsd.Web` project SHALL provide a `/Uat/Verify` page with a wizard interface for executing UAT verification.

The implementation MUST:
- Accept UAT ID via query parameter (`?id={uatId}`)
- Read UAT spec from `.aos/spec/uat/{uatId}.json`
- Build checklist from task's acceptance criteria
- Display each check with type (file-exists, content-contains, build-succeeds, test-passes)
- Provide pass/fail toggle for each check
- Collect repro notes for failed checks
- Show progress through the wizard (step X of Y)
- Provide "Submit Verification" button to finalize

#### Scenario: Build checklist from acceptance criteria
- **GIVEN** UAT-TSK-000001 with acceptance criteria for file-exists and build-succeeds checks
- **WHEN** a user navigates to `/Uat/Verify?id=UAT-TSK-000001`
- **THEN** the wizard displays a checklist with both criteria ready for verification

#### Scenario: Display check details
- **GIVEN** a file-exists check for "src/Models/User.cs"
- **WHEN** the verify wizard displays the checklist
- **THEN** the check shows its type, target file, and expected result

### Requirement: Pass/Fail Recording
The verify wizard SHALL record pass/fail results for each acceptance criterion.

The implementation MUST:
- Allow marking each check as passed or failed
- Require repro notes for failed checks
- Validate all checks have been evaluated before submission
- Write results to `.aos/evidence/runs/{runId}/artifacts/uat-results.json`
- Include timestamp, check type, target, status, message, duration
- Calculate overall pass/fail status

#### Scenario: Record passing checks
- **GIVEN** all checks pass during verification
- **WHEN** the user submits the verification
- **THEN** uat-results.json is written with status "passed" and all checks marked passed

#### Scenario: Record failing checks with repro notes
- **GIVEN** one check fails during verification
- **WHEN** the user marks it failed, enters repro notes "File not found at expected path", and submits
- **THEN** uat-results.json records the failed check with repro notes included

### Requirement: Issue Creation on Fail
The verify wizard SHALL create issues for failed acceptance criteria.

The implementation MUST:
- Create issue artifact in `.aos/spec/issues/ISS-{n}.json` for each failed check
- Issue includes: parent UAT ID, scope, severity, repro steps, expected vs actual behavior
- Severity is "error" for failed checks
- Update `.aos/spec/issues/index.json`
- Display created issue IDs after verification submission
- Link to created issues

#### Scenario: Create issues for failed verification
- **GIVEN** verification of UAT-TSK-000001 fails with 2 failed checks
- **WHEN** the user submits the verification
- **THEN** 2 issues (ISS-0001, ISS-0002) are created with repro details from the failed checks

#### Scenario: Display created issues
- **GIVEN** verification completed with created issues
- **WHEN** the verification results page displays
- **THEN** the created issue IDs are shown with links to `/Issues/Details?id={issueId}`

### Requirement: Re-run Verification
The UAT pages SHALL support re-running verification against the same checks.

The implementation MUST:
- Provide "Re-run Verification" button on UAT detail for previously verified items
- Load previous verification results as reference
- Allow re-evaluating each check
- Write new uat-results.json with new timestamp
- Maintain history of verification runs
- Compare results with previous runs

#### Scenario: Re-run verification
- **GIVEN** UAT-TSK-000001 was previously verified with status "passed"
- **WHEN** the user clicks "Re-run Verification"
- **THEN** the wizard opens with the same checks, allowing new pass/fail evaluation

#### Scenario: Display verification history
- **GIVEN** UAT-TSK-000001 has been verified 3 times
- **WHEN** the UAT list or detail page displays
- **THEN** a history of the 3 verification runs is shown with timestamps and results

### Requirement: Cross-Linking Between UAT, Issues, and Runs
The UAT pages SHALL provide navigation links between related UAT, issues, and runs.

The implementation MUST:
- Link from UAT to its associated task (`/Tasks/Details?id={taskId}`)
- Link from UAT to verification run (`/Runs/Details?id={runId}`)
- Link from UAT to created issues (`/Issues/Details?id={issueId}`)
- Link from Issues to parent UAT when applicable
- Link from Runs to associated UAT results

#### Scenario: Navigate from UAT to task
- **GIVEN** the UAT detail page for UAT-TSK-000001
- **WHEN** the user clicks the linked task ID
- **THEN** they navigate to `/Tasks/Details?id=TSK-000001`

#### Scenario: Navigate from UAT to created issues
- **GIVEN** UAT verification created issues ISS-0001 and ISS-0002
- **WHEN** the user clicks on an issue ID in the results
- **THEN** they navigate to `/Issues/Details?id=ISS-0001`
