# Spec Delta: workspace-config-alignment

## MODIFIED Requirements
### Requirement: Workspace-specific configuration is stored in .aos/config.json
The system SHALL prioritize configuration found in `.aos/config.json` for workspace-level settings.

#### Scenario: Reading workspace-specific preferences
- **GIVEN** a valid workspace with a `.aos/config.json` file containing `agentPreferences`
- **WHEN** the workspace is opened via the API
- **THEN** those preferences are loaded and applied to the session context

#### Scenario: Bootstrapping configuration
- **GIVEN** a new workspace is being initialized
- **WHEN** `POST /api/v1/workspaces/init` is executed
- **THEN** a default `.aos/config.json` is created with baseline settings
