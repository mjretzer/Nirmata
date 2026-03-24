## ADDED Requirements

### Requirement: Get all projects
The system SHALL provide an endpoint to retrieve a list of all projects.

#### Scenario: Successful retrieval of all projects
- **WHEN** a GET request is made to `/v1/projects`
- **THEN** the system returns a 200 OK response with a list of project DTOs

### Requirement: Search projects by name
The system SHALL allow searching for projects by name using a query string.

#### Scenario: Search for projects with matching name
- **WHEN** a GET request is made to `/v1/projects/search/{query}`
- **THEN** the system returns a 200 OK response with projects matching the query

### Requirement: Get project by ID
The system SHALL provide an endpoint to retrieve a specific project's details by its unique identifier.

#### Scenario: Successful retrieval of a specific project
- **WHEN** a GET request is made to `/v1/projects/{projectId}` with a valid ID
- **THEN** the system returns a 200 OK response with the project details

### Requirement: Create a new project
The system SHALL allow users to create a new project by providing required details.

#### Scenario: Successful project creation
- **WHEN** a POST request is made to `/v1/projects` with valid project data
- **THEN** the system returns a 201 Created response with the new project details
