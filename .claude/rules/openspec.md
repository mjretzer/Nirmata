# openspec

General OpenSpec help and context. Use this to understand current project state or find available commands.

General OpenSpec help and context. Use this to understand current project state or find available commands.

## Guardrails
- Refer to `openspec/AGENTS.md` for detailed conventions.
- Use `openspec list` to see active change proposals.
- Use `openspec list --specs` to see current system capabilities.

## Common Commands
- `openspec list`: List active changes in `openspec/changes/`.
- `openspec list --specs`: List current specifications in `openspec/specs/`.
- `openspec show <id>`: View details for a specific change or spec.
- `openspec validate <id> --strict`: Validate a proposal.
- `openspec archive <id> --yes`: Finalize and archive a completed change.

## Workflows
Use these specific slash commands for OpenSpec stages:
- `/openspec-proposal`: Create a new change proposal.
- `/openspec-apply`: Implement an approved change.
- `/openspec-archive`: Archive a deployed change.
