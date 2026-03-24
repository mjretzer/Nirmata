---
description: Version control, branch naming, and pull request conventions
---

# Git & Pull Requests

## Branch Naming
- `feature/<issue-number>-<short-description>` (e.g., `feature/123-add-ollama-support`)
- `bugfix/<issue-number>-<short-description>` (e.g., `bugfix/456-fix-agent-routing`)
- `hotfix/<short-description>` (for urgent production fixes)
- `chore/<short-description>` (for dependency updates, CI/CD tweaks, etc.)

## Commit Messages
- Use Conventional Commits format:
  - `feat: [description]` for new features.
  - `fix: [description]` for bug fixes.
  - `docs: [description]` for documentation changes.
  - `chore: [description]` for maintenance tasks.
- Keep the first line under 50 characters. Provide details in the body if necessary.

## Pull Requests
- Titles should clearly state the goal of the PR and reference the issue number if applicable.
- **Requirements before merging**:
  - All tests must pass (run `dotnet test` and `npm run test`).
  - Code must adhere to linting standards (run `npm run lint`).
  - Required reviewers must approve the changes.
  - No secrets or hardcoded credentials are included.
