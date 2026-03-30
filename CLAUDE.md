# Nirmata Project Context

@docs/architecture/solution.md
@docs/architecture/data-flow.md
@docs/architecture/schemas.md

@docs/workflows/gating.md
@docs/workflows/quick-start.md
@docs/workflows/routing.md

@docs/agents/orchestrator.md
@docs/agents/planning.md
@docs/agents/execution.md
@docs/agents/verification.md
@docs/agents/scope.md
@docs/agents/milestones.md
@docs/agents/brownfield.md
@docs/agents/continuity.md
@docs/agents/backlog.md
@docs/agents/help.md

@docs/cli/commands.md
@docs/cli/workspace-layout.md

## OpenSpec Commands
To use OpenSpec in this terminal, prefix commands with `!`:
- `!openspec list`: View active changes.
- `!openspec show <id>`: View change details.
- `!openspec validate <id> --strict`: Validate a proposal.
- `!openspec archive <id> --yes`: Finalize a change.

## OpenSpec Workflows
To trigger an OpenSpec workflow (proposal, apply, or archive), simply ask Claude by name:
- "Run `openspec-proposal`": Scaffold a new change.
- "Run `openspec-apply`": Implement an approved change.
- "Run `openspec-archive`": Archive a completed change.

Claude uses rules in `.claude/rules/` to execute these multi-step workflows.

## Claude IDE Bridge

The bridge is connected via MCP. Call `getToolCapabilities` at the start of each session to confirm which tools are available and note any that require the VS Code extension.

### Bug fix methodology

When a bug is reported, do NOT start by trying to fix it. Instead:
1. Write a test that reproduces the bug (the test should fail)
2. Fix the bug and confirm the test now passes
3. Only then consider the bug fixed

### Documentation & memory

Keep project documentation and Claude's memory in sync with the code:

- **After architectural changes** — update `CLAUDE.md` so future sessions have accurate context. If a pattern, rule, or constraint changes, the file should reflect it.
- **At the end of a work session** — if meaningful decisions were made (why a pattern was chosen, what was tried and rejected, what the next steps are), save a summary to memory: *"Remember that we chose X approach because Y."*
- **Prune stale instructions** — if `CLAUDE.md` contains outdated guidance, remove or correct it. Stale instructions cause confident mistakes in future sessions.

### Modular rules (optional)

For large projects, move individual rules out of CLAUDE.md into scoped files under `.claude/rules/`:

```
.claude/rules/testing.md     — applies when working with test files
.claude/rules/security.md    — applies to auth, payments, sensitive modules
.claude/rules/typescript.md  — TypeScript-specific conventions
```

Reference them from CLAUDE.md with:
```
@import .claude/rules/testing.md
```

Path globs on rule files mean Claude only loads them when working on matching files — keeps context focused and token-efficient.

### Workflow rules

- **After editing any file** — call `getDiagnostics` to catch errors introduced by the change
- **Running tests** — use `runTests` instead of shell commands; output streams in real time
- **Git operations** — use bridge git tools (`getGitStatus`, `gitAdd`, `gitCommit`, `gitPush`) for structured, auditable operations
- **Debugging** — use `setDebugBreakpoints` → `startDebugging` → `evaluateInDebugger` for interactive debugging
- **Navigating code** — prefer `goToDefinition`, `findReferences`, and `getCallHierarchy` over grep

### Quick reference

| Task | Tool |
|---|---|
| Check errors / warnings | `getDiagnostics` |
| Run tests | `runTests` |
| Git status / diff | `getGitStatus`, `getGitDiff` |
| Stage, commit, push | `gitAdd`, `gitCommit`, `gitPush` |
| Open a pull request | `githubCreatePR` |
| Navigate to definition | `goToDefinition` |
| Find all references | `findReferences` |
| Call hierarchy | `getCallHierarchy` |
| File tree / symbols | `getFileTree`, `getDocumentSymbols` |
| Run a shell command | `runInTerminal`, `getTerminalOutput` |
| Interactive debug | `setDebugBreakpoints`, `startDebugging`, `evaluateInDebugger` |
| Lint / format | `fixAllLintErrors`, `formatDocument` |
| Security audit | `getSecurityAdvisories`, `auditDependencies` |
| Unused code | `detectUnusedCode` |
