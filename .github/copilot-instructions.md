# Nirmata — GitHub Copilot Instructions

This file provides repository-wide context for GitHub Copilot. For full Claude-specific workflows and skills, see `CLAUDE.md` and `.claude/rules/`.

---

## Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10, C# 13 |
| Frontend | React 18, TypeScript 5.9, Vite 6, Tailwind CSS 4 |
| LLM Abstraction | Semantic Kernel |
| Test Runner (BE) | xUnit, FluentAssertions, Moq / NSubstitute |
| Test Runner (FE) | Vitest, React Testing Library |

---

## Architecture

- **Clean / Hexagonal Architecture**: Domain logic (`nirmata.Aos`, `nirmata.Agents`) must not depend on infrastructure or external frameworks. Define interfaces in the core; implementations live at the edge.
- **Dependency Injection**: All services, agents, and integrations are registered and resolved via `Microsoft.Extensions.DependencyInjection` constructor injection.
- **Agent Orchestration System (AOS)**: Core engine for routing and managing LLM interactions.
- **Semantic Kernel**: Used for LLM abstraction and agent execution.
- **State Management (agents)**: Agents should be stateless. State is passed explicitly through the message envelope or managed via dedicated state stores.
- **Frontend components**: Follow atomic design — `components/ui` for generic reusable components (shadcn/ui), `components/features` for domain-specific components. Prefer local state or React Context over heavy global state managers.

---

## Naming Conventions

### Backend (C#)
- Classes, Methods, Properties, Records: `PascalCase`
- Interfaces: `PascalCase` prefixed with `I` (e.g., `IAgentService`)
- Parameters, Local Variables: `camelCase`
- Private Fields: `_camelCase`

### Frontend (TypeScript / React)
- Components, Interfaces, Types: `PascalCase`
- Functions, Variables, Hooks: `camelCase` (e.g., `useUserData`, `handleFetch`)
- Constants: `UPPER_SNAKE_CASE`

---

## Project Structure

```
src/nirmata.*          # Backend source projects
tests/nirmata.*.Tests  # Backend test projects
nirmata.frontend/src/  # React frontend
docs/                  # Shared knowledge base — architecture, agents, workflows, CLI reference
openspec/              # Change proposals and specifications
.claude/rules/         # Claude coding rules and execution playbooks (team-shared)
.claude/skills/        # Claude task workflow skills (team-shared)
.github/               # Copilot instructions and GitHub workflows
```

- Organize backend files by feature or domain, not by technical type.
- Co-locate frontend tests, styles, and utilities when tightly coupled.

---

## Coding Standards

### Error Handling
- **Backend**: Use specific exceptions; never throw generic `Exception`. Avoid exceptions for control flow. Use `Microsoft.Extensions.Logging` for structured logging.
- **Frontend**: Catch API errors in the data-fetching layer. Surface errors via toast notifications (Sonner) or Error Boundaries for rendering crashes.

### Comments & Documentation
- **Backend**: Use `///` XML documentation for public interfaces and complex logic.
- **Frontend**: Use JSDoc (`/** ... */`) for shared utilities and complex component props.
- **General**: Comment the *why* and *how*, not the *what*. Code should be self-documenting through good naming.

### Linting
- Frontend uses ESLint (`typescript-eslint`, `react-hooks`). `any` is temporarily allowed during development; unused variables must be prefixed with `_`.

---

## Testing

- **Structure**: Always follow **Arrange–Act–Assert**.
- **Backend assertions**: Use FluentAssertions syntax (e.g., `result.Should().BeTrue()`), not raw xUnit `Assert`.
- **Backend mocks**: Use Moq or NSubstitute for external dependencies.
- **Frontend**: Test behavior from the user's perspective via `@testing-library/react`. Avoid testing internal state or implementation details.
- **E2E tests**: Live in `E2E/` subdirectories inside the test projects. Must clean up after themselves.
- **Bug fix methodology**: Write a failing test first, fix the bug, confirm the test passes.

### Running Tests
```bash
# Backend
dotnet test

# Frontend
npm run test          # from nirmata.frontend/
npm run test:watch
```

---

## Git & Commits

### Branch Naming
- `feature/<issue>-<short-description>`
- `bugfix/<issue>-<short-description>`
- `hotfix/<short-description>`
- `chore/<short-description>`

### Commit Messages (Conventional Commits)
- `feat:`, `fix:`, `docs:`, `chore:`
- Keep the subject line under 50 characters.

### Before Merging
- All tests must pass (`dotnet test`, `npm run test`)
- Linting must pass (`npm run lint`)
- No secrets or hardcoded credentials

---

## Security

- Never log sensitive API keys or PII.
- Validate all input at system boundaries (user input, external APIs) before passing to the LLM or AOS.
- Follow OWASP Top 10: no SQL injection, XSS, command injection, SSRF, broken access control, etc.
