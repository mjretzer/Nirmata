## ADDED Requirements

### Requirement: Hooks use backend data sources
The frontend AOS data hooks SHALL obtain their data from backend endpoints at runtime and SHALL NOT use placeholder/mock implementations.

#### Scenario: UI requests AOS data
- **WHEN** a UI view invokes an AOS data hook exported from `useAosData.ts`
- **THEN** the hook returns data sourced from backend endpoints rather than local mock generators

### Requirement: No fetching in pages/components
UI pages and presentational components SHALL NOT perform network fetching for AOS data.
All AOS data fetching SHALL be centralized in the hooks layer (and any hook-owned API helper it uses).

#### Scenario: Page renders AOS view
- **WHEN** an AOS UI page/component renders
- **THEN** it obtains AOS data exclusively via hooks and does not initiate its own network requests

### Requirement: Standard loading and error state
Each AOS data hook wired to backend endpoints SHALL provide a consistent way to observe loading and error state.

#### Scenario: Backend request is in progress
- **WHEN** an AOS data hook initiates a backend request
- **THEN** the hook indicates a loading state until the request completes

#### Scenario: Backend request fails
- **WHEN** an AOS data hook encounters a backend error (network failure or non-success response)
- **THEN** the hook surfaces an error state while keeping the UI-consumed return keys stable

### Requirement: Stable outward-facing hook shape
When replacing mocks with real endpoint calls, the outward-facing return keys consumed by existing UI pages/components SHALL remain stable.

#### Scenario: Existing UI consumes hook result
- **WHEN** existing UI pages/components consume a hook result
- **THEN** the same keys are available as before the wiring change

### Requirement: Real filesystem data via existing hook
The AOS virtual filesystem exposed to the UI via `useFileSystem()` SHALL be backed by real endpoint data rather than `mockFileSystem.ts`.

#### Scenario: UI requests filesystem view
- **WHEN** the UI invokes `useFileSystem()`
- **THEN** the returned filesystem data is derived from backend endpoints and not from mock filesystem generation
