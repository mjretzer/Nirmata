## ADDED Requirements

### Requirement: Specs Explorer Page

The `Gmsd.Web` project SHALL provide a `/Specs` page for browsing and searching AOS artifacts across the workspace.

The implementation MUST:
- Display tree view of `spec/` directory (project, roadmap, milestones, phases, tasks, issues, uat)
- Provide search across all spec files (filename + content)
- Support dual-mode viewing: Form editor + Raw JSON + Validate button
- Show "Open on disk path" link for each artifact
- Provide diff viewer comparing current vs previous version (git-backed)
- Group artifacts by type with collapsible sections

#### Scenario: Display spec tree structure
- **GIVEN** a workspace with `spec/project.json`, `spec/roadmap.md`, `spec/milestones/`, `spec/phases/`
- **WHEN** a user navigates to `/Specs`
- **THEN** the tree shows: project.json, roadmap.md, milestones/ (expandable), phases/ (expandable)
- **AND** clicking a file opens it in the editor pane

#### Scenario: Search across specs
- **GIVEN** multiple spec files contain "authentication"
- **WHEN** a user enters "authentication" in the search box
- **THEN** search results show matching files with context snippets
- **AND** clicking a result opens that file at the relevant line

#### Scenario: Dual-mode editing toggle
- **GIVEN** a spec file is open
- **WHEN** a user clicks "Raw JSON" mode
- **THEN** the editor switches to a text area with JSON content
- **AND** a "Validate" button appears to check schema compliance

#### Scenario: Form editor for structured specs
- **GIVEN** `spec/project.json` is open in Form mode
- **WHEN** the page loads
- **THEN** form fields appear for: Project Name, Description, Constraints, Success Criteria
- **AND** changes can be saved back to the file

#### Scenario: Show disk path link
- **GIVEN** a user is viewing `spec/milestones/M1.json`
- **WHEN** they look at the artifact header
- **THEN** an "Open on disk" link shows the full path: `C:\Projects\MyApp\.aos\spec\milestones\M1.json`
- **AND** clicking it opens the file in the default editor

#### Scenario: Diff viewer for version history
- **GIVEN** a spec file has been modified
- **WHEN** a user clicks "Show Diff"
- **THEN** a side-by-side diff appears showing changes vs last git commit
- **AND** additions are highlighted in green, deletions in red
