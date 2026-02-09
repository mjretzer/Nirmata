## ADDED Requirements

### Requirement: Razor Pages Infrastructure
The Gmsd.Web project SHALL provide Razor Pages infrastructure with proper view imports, layout templates, and static file support.

#### Scenario: View imports configured
- **WHEN** the application renders any Razor page
- **THEN** common namespaces (Gmsd.Data.Dto.Models.Projects, Gmsd.Services.Interfaces) are available without explicit @using directives

#### Scenario: Layout template renders
- **WHEN** any page loads
- **THEN** it uses the shared _Layout.cshtml with navigation header and footer

### Requirement: Static Asset Pipeline
The Gmsd.Web project SHALL serve static assets (CSS, JavaScript) from wwwroot with proper organization.

#### Scenario: CSS styling applied
- **WHEN** the homepage renders
- **THEN** site.css styles are applied to layout, navigation, and content areas

#### Scenario: JavaScript available
- **WHEN** pages load
- **THEN** site.js is included and any validation scripts work for future forms

### Requirement: Project List Page
The Gmsd.Web project SHALL provide a read-only list page displaying all projects.

#### Scenario: Projects displayed in table
- **GIVEN** projects exist in the database
- **WHEN** a user navigates to /Projects
- **THEN** the page displays a table with Project Name, Description, and Created Date columns

#### Scenario: Empty state shown
- **GIVEN** no projects exist in the database
- **WHEN** a user navigates to /Projects
- **THEN** the page displays a friendly empty state message

#### Scenario: Links to detail pages
- **GIVEN** the project list is displayed
- **WHEN** the page renders
- **THEN** each project row includes a link to its detail page (/Projects/Details?id={id})

### Requirement: Project Detail Page
The Gmsd.Web project SHALL provide a read-only detail page for viewing a single project.

#### Scenario: Project details displayed
- **GIVEN** a project exists with ID "abc-123"
- **WHEN** a user navigates to /Projects/Details?id=abc-123
- **THEN** the page displays the project's Name, Description, CreatedAt, and UpdatedAt fields

#### Scenario: Not found handling
- **GIVEN** no project exists with ID "nonexistent"
- **WHEN** a user navigates to /Projects/Details?id=nonexistent
- **THEN** the service throws NotFoundException
- **AND** the page returns HTTP 404 status with a friendly error message

#### Scenario: Navigation back to list
- **GIVEN** a user is viewing a project detail page
- **WHEN** the page renders
- **THEN** a link back to the project list is visible
