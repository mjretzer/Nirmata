---
description: Testing strategy, frameworks, and conventions
---

# Testing

## Framework
- **Backend (C#)**: `xUnit` for the test runner. `FluentAssertions` for assertions. `Moq` and `NSubstitute` for mocking.
- **Frontend (React/TS)**: `Vitest` for the test runner. `React Testing Library` for component rendering and interactions.

## Test Location
- **Backend**: Tests reside in the `tests/` directory within corresponding project folders (e.g., `tests/nirmata.Agents.Tests/`).
  - Unit/Integration tests are grouped by feature/domain.
  - End-to-End (E2E) tests reside in dedicated `E2E/` subdirectories within the test projects.
- **Frontend**: Tests are typically co-located with their respective components/modules or inside `nirmata.frontend/src/test/`. Use `.test.ts` or `.test.tsx` extensions.

## Coverage Requirements
- Aim for high coverage on core business logic, agents, and complex UI state.
- 100% coverage is not strictly mandated, but any new features or bug fixes must include accompanying tests to prevent regressions.

## Conventions
- **Structure**: Follow the **Arrange-Act-Assert** (AAA) pattern for both frontend and backend tests.
- **Backend Assertions**: Use `FluentAssertions` syntax (e.g., `result.Should().BeTrue();`) instead of standard xUnit `Assert` methods where possible.
- **Backend Mocking**: Use `Moq` or `NSubstitute` to mock external dependencies, APIs, and file systems to keep unit tests fast and deterministic.
- **Frontend Testing**: Test behavior from the user's perspective using `@testing-library/react`. Avoid testing implementation details or internal state directly.
- **E2E Tests**: E2E tests should spin up required resources or use high-level application entry points. Ensure they clean up after themselves (teardown).

## Running Tests
- **Backend**: Run `dotnet test` from the root or within specific test project directories.
- **Frontend**: Run `npm run test` or `npm run test:watch` (which executes `vitest`) from the `nirmata.frontend/` directory.
