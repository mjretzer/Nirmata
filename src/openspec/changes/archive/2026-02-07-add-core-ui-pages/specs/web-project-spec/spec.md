## ADDED Requirements

### Requirement: Project Spec Page

The `nirmata.Web` project SHALL provide a `/Specs/Project` page for viewing and editing `spec/project.json` with guided interview mode.

The implementation MUST:
- Display and edit project definition, constraints, and success criteria
- Provide "Interview Mode" UI that prompts questions and writes to `spec/project.json`
- Validate JSON against project schema and display schema errors
- Support import/export of project spec (JSON upload/download)
- Show validation status indicator (valid/pending/errors)
- Auto-save draft changes before explicit save

#### Scenario: View project spec in form mode
- **GIVEN** `spec/project.json` contains valid project data
- **WHEN** a user navigates to `/Specs/Project`
- **THEN** form fields display: Project Name, Description, Version, Constraints list, Success Criteria list
- **AND** a "JSON" toggle shows the raw file content

#### Scenario: Interview mode guides project definition
- **GIVEN** a user clicks "Start Interview"
- **WHEN** the interview begins
- **THEN** sequential questions appear: "What is the project name?", "Describe the project goal", "List key constraints"
- **AND** answers populate the form fields in real-time

#### Scenario: Schema validation shows errors
- **GIVEN** a user enters invalid JSON (missing required "name" field)
- **WHEN** they click "Validate"
- **THEN** validation errors appear: "Missing required field: name"
- **AND** the error location is highlighted in the JSON editor

#### Scenario: Export project spec
- **GIVEN** a user wants to backup the project spec
- **WHEN** they click "Export"
- **THEN** a `project.json` file downloads with current content
- **AND** a timestamp comment is included in the export

#### Scenario: Import project spec
- **GIVEN** a user has a `project.json` file to upload
- **WHEN** they click "Import" and select the file
- **THEN** the file content populates the form
- **AND** validation runs automatically before allowing save

#### Scenario: Auto-save draft
- **GIVEN** a user modifies a field
- **WHEN** they pause typing for 3 seconds
- **THEN** a draft is saved to browser localStorage
- **AND** a "Draft saved" indicator appears briefly
