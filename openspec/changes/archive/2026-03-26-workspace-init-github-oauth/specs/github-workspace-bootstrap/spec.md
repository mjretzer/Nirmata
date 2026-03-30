## ADDED Requirements

### Requirement: GitHub-connected workspace creation initiates OAuth
The system SHALL let a user start workspace creation by connecting a GitHub account through an OAuth flow.

#### Scenario: User starts GitHub-connected init
- **WHEN** a user chooses the GitHub-connected workspace creation path
- **THEN** the system redirects the user through GitHub OAuth
- **AND** the system preserves the workspace creation context for the callback

#### Scenario: User cancels GitHub authorization
- **WHEN** the user cancels or denies GitHub authorization
- **THEN** the system reports the failure clearly
- **AND** the workspace is not marked as created

### Requirement: GitHub-connected init creates a remote repository and sets origin
The system SHALL create or reuse a GitHub repository for the workspace and configure the local workspace `origin` to point to that repository after authorization succeeds.

#### Scenario: New GitHub repo is created
- **WHEN** the user authorizes the app and the requested repository does not yet exist
- **THEN** the system creates the repository in the authenticated GitHub account
- **AND** the system initializes the local workspace if needed
- **AND** the system sets the local `origin` remote to the created repository URL

#### Scenario: Existing GitHub repo is reused
- **WHEN** the requested repository already exists for the authenticated GitHub account
- **THEN** the system reuses the existing repository
- **AND** the system still configures the local `origin` remote to that repository URL

### Requirement: GitHub-connected init remains idempotent and recoverable
The system SHALL allow the GitHub-connected workspace creation flow to be retried without destroying existing local git history or requiring the user to manually clean up partial state.

#### Scenario: Retry after partial bootstrap
- **WHEN** a previous GitHub-connected init attempt created the repository but failed before the workspace was fully registered
- **THEN** a retry reuses the existing GitHub repository when possible
- **AND** the system completes the remaining local bootstrap and origin setup steps without overwriting history

#### Scenario: Retry after local git already exists
- **WHEN** the local workspace already contains a git repository
- **THEN** the system preserves the existing repository
- **AND** the system only updates the local `origin` configuration if needed
