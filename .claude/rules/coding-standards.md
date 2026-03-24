---
description: Coding standards and conventions for this project
---

# Coding Standards

## General
- **Backend Stack**: .NET 10, C# 13.
- **Frontend Stack**: React 18, TypeScript 5.9, Vite 6, Tailwind CSS 4.
- **Linting & Formatting**: 
  - Frontend uses ESLint (`typescript-eslint`, `react-hooks`). `any` is temporarily allowed during development, but unused variables should be prefixed with `_`.
  - Backend uses standard .NET SDK formatting and rules (e.g., `WarningsNotAsErrors` configurations).

## Naming Conventions
- **Backend (C#)**:
  - **Classes, Methods, Properties, Records**: `PascalCase`
  - **Interfaces**: `PascalCase` prefixed with `I` (e.g., `IAgentService`)
  - **Parameters, Local Variables**: `camelCase`
  - **Private Fields**: `_camelCase`
- **Frontend (TypeScript/React)**:
  - **Components, Interfaces, Types**: `PascalCase` (e.g., `UserProfile`, `ButtonProps`)
  - **Functions, Variables, Hooks**: `camelCase` (e.g., `useUserData`, `handleFetch`)
  - **Constants**: `UPPER_SNAKE_CASE`

## File Structure
- **Backend**:
  - Source projects in `src/nirmata.*`
  - Test projects in `tests/nirmata.*.Tests`
  - Organize files by feature or domain rather than technical type when possible.
- **Frontend**:
  - Code resides in `nirmata.frontend/src/`.
  - Prefer flat component structures and co-locate tests, styles, and utilities when tightly coupled.

## Error Handling
- **Backend**:
  - Use specific exceptions rather than throwing generic `Exception`.
  - Avoid exceptions for regular control flow.
  - Rely on `Microsoft.Extensions.Logging` for structured logging.
- **Frontend**:
  - Catch API errors in the data fetching layer.
  - Surface user-facing errors via toast notifications (e.g., Sonner) or Error Boundaries for rendering crashes.

## Comments & Documentation
- **Backend**:
  - Use `///` XML documentation for public interfaces and complex logic.
- **Frontend**:
  - Use JSDoc (`/** ... */`) for shared utilities and complex component props.
- **General**:
  - Focus on the "Why" and "How" rather than the "What" when commenting. Code should be self-documenting through good naming conventions.
