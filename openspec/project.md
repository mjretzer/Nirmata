# Project Context

## Purpose
Nirmata is a spec-driven agent orchestration platform for planning, executing, and verifying software work with deterministic artifacts and strong workflow gating.

Primary goals:
- Capture project intent and plans as durable, structured artifacts.
- Orchestrate LLM-assisted execution through a controlled AOS pipeline.
- Enforce verification and traceability (spec -> execution evidence -> UAT outcomes).
- Support both greenfield and brownfield development workflows.

## Tech Stack
- Backend: .NET 10, C# 13
- Frontend: React 18, TypeScript 5.9, Vite 6, Tailwind CSS 4
- LLM abstraction and orchestration: Semantic Kernel + Nirmata Agent Orchestration System (AOS)
- Backend testing: xUnit, FluentAssertions, Moq or NSubstitute
- Frontend testing: Vitest, React Testing Library

## Project Conventions

### Code Style
- Backend naming:
	- Types/methods/properties/records: PascalCase
	- Interfaces: IPascalCase
	- Locals/parameters: camelCase
	- Private fields: _camelCase
- Frontend naming:
	- Components/types/interfaces: PascalCase
	- Functions/variables/hooks: camelCase
	- Constants: UPPER_SNAKE_CASE
- Frontend linting: typescript-eslint + react-hooks; unused variables must be prefixed with _.
- Comments should explain why/how rather than restating what the code already says.
- Use specific exceptions; do not use generic Exception for normal flows.

### Architecture Patterns
- Clean/Hexagonal architecture:
	- Core domain logic (notably nirmata.Aos and nirmata.Agents) must not depend on infrastructure details.
	- Interfaces are defined in core layers; implementations live at the edge.
- Dependency injection via Microsoft.Extensions.DependencyInjection and constructor injection across services/agents.
- Agents should remain as stateless as possible; required state should be passed explicitly via envelopes/artifacts.
- Frontend follows atomic boundaries:
	- components/ui for reusable primitives
	- components/features for domain-specific components
- Prefer local state or React Context before introducing heavier global state managers.

### Testing Strategy
- Use Arrange-Act-Assert (AAA).
- Bug-fix workflow:
	- Write a failing test that reproduces the issue.
	- Implement the fix.
	- Confirm the test passes.
- Backend:
	- Prefer FluentAssertions over raw xUnit Assert APIs.
	- Mock external dependencies to keep tests deterministic.
- Frontend:
	- Test user-observable behavior using React Testing Library.
	- Avoid testing implementation details.
- Commands:
	- Backend: dotnet test
	- Frontend: npm run test (from nirmata.frontend)

### Git Workflow
- Branch naming:
	- feature/<issue-number>-<short-description>
	- bugfix/<issue-number>-<short-description>
	- hotfix/<short-description>
	- chore/<short-description>
- Commit style: Conventional Commits (feat, fix, docs, chore).
- Keep commit subjects concise (under 50 characters when possible).
- Before merge:
	- Run backend and frontend tests.
	- Run frontend lint.
	- Ensure no secrets or credentials are committed.

## Domain Context
- Nirmata revolves around an AOS model with three durable truth layers:
	- Intended truth: spec artifacts (project/roadmap/task plans/UAT definitions)
	- Operational truth: state cursor and event stream
	- Provable truth: execution evidence and verification artifacts
- The orchestrator enforces a gated state machine:
	- New project definition -> roadmap -> phase planning -> execution -> verification -> fix loop (if needed)
- Task execution is scoped and traceable:
	- Plans are decomposed into small tasks.
	- Execution writes evidence and advances state.
	- Verification explicitly passes or fails against acceptance criteria.
- Brownfield mode includes codebase mapping to ground planning in existing repository realities.

## Important Constraints
- Security:
	- Never log API keys, secrets, or PII.
	- Validate boundary inputs before passing them into AOS/LLM flows.
- Architecture:
	- Preserve core/edge separation and avoid leaking infrastructure concerns into domain logic.
- Reliability:
	- Keep workflows deterministic and artifact-driven.
	- Avoid implicit state; prefer explicit persisted artifacts/events.
- Change management:
	- Keep modifications focused and avoid unrelated refactors.
	- Maintain compatibility unless a change intentionally evolves an interface.

## External Dependencies
- LLM providers integrated through Semantic Kernel adapters (for example OpenAI, Azure OpenAI, Anthropic, Ollama).
- Git as the source-of-truth VCS for task-level commits and traceability.
- Node.js ecosystem for frontend build/test/lint tooling.
- .NET SDK and associated packages for backend runtime, DI, and testing.
