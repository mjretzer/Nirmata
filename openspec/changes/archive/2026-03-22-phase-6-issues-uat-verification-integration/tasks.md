## 1. Backend issue API

- [x] 1.1 Define `IIssueService` and `IssueService` for workspace-scoped CRUD against `.aos/spec/issues/`
- [x] 1.2 Implement issue filtering by status, severity, phaseId, taskId, and milestoneId
- [x] 1.3 Add `IssuesController` endpoints for list, get, create, update, delete, and status update
- [x] 1.4 Add workspace validation and 404 handling for unknown workspace IDs

## 2. Backend UAT API

- [x] 2.1 Define `IUatService` and `UatService` for reading `.aos/spec/uat/`
- [x] 2.2 Implement derived UAT summaries for task and phase pass/fail state
- [x] 2.3 Add `UatController` with `GET /v1/workspaces/{wsId}/uat`
- [x] 2.4 Add tests for UAT aggregation and workspace scoping

## 3. Frontend verification and fix workflow

- [x] 3.1 Replace `useIssues` mock data with the real workspace issues endpoint
- [x] 3.2 Replace `useVerificationState` mock derivation with real issues, tasks, and runs data
- [x] 3.3 Wire issue creation from `VerificationPage` to `POST /v1/workspaces/{wsId}/issues`
- [x] 3.4 Register `FixPage` in the router and ensure the route renders correctly

## 4. Verification

- [x] 4.1 Add or update tests covering issue persistence, filters, and status updates
- [x] 4.2 Add or update tests covering UAT summary responses and failure derivation
- [x] 4.3 Validate that `VerificationPage` shows persisted issues and correct severity/status
- [x] 4.4 Validate that `FixPage` can display fix tasks derived from open issues
