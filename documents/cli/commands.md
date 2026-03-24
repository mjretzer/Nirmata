# AOS CLI Command Reference

Source: AOS_CLI_Commands.pdf (Section 12)

Usage: `aos <group> <command> [args]`

> **Important — two command layers:**
> - **`aos` commands** (this file): low-level workspace management — spec authoring, state, validation, runs, codebase intelligence, context packs, maintenance. These are deterministic, artifact-scoped operations you can run directly.
> - **Workflow intents** (see `workflows/` and `agents/`): higher-level orchestration commands like `plan-phase`, `execute-plan`, `verify-work`, `pause-work`, `create-roadmap`, etc. These are dispatched to the Orchestrator (via CLI freeform or the Agent Manager API) and trigger full multi-step agent workflows. They are **not** `aos` subcommands.

---

## Base / Repository Lifecycle

```
aos init                          # Initialize workspace
aos status                        # Show current workspace status
aos config show                   # Display current configuration
aos config set <key> <value>      # Set a configuration value
```

---

## Validation

```
aos validate                      # Run all validators
aos validate schemas              # Validate JSON schema files
aos validate spec                 # Validate spec artifacts
aos validate state                # Validate state artifacts
aos validate evidence             # Validate evidence artifacts
aos validate codebase             # Validate codebase intelligence pack
aos repair indexes                # Repair broken index files
```

---

## Spec Authoring — Project + Roadmap

```
aos spec project set <file|json>  # Set project spec from file or inline JSON
aos spec project show             # Display current project spec
aos spec roadmap set <file|json>  # Set roadmap from file or inline JSON
aos spec roadmap show             # Display current roadmap
```

---

## Spec Authoring — Milestones

```
aos spec milestone create <title> [--id MS-####]          # Create a milestone
aos spec milestone update <MS-####> <file|json>           # Update a milestone
aos spec milestone show <MS-####>                         # Show a milestone
aos spec milestone list                                   # List all milestones
aos spec milestone delete <MS-####>                       # Delete a milestone
```

---

## Spec Authoring — Phases

```
aos spec phase create <title> [--id PH-####] [--milestone MS-####]   # Create a phase
aos spec phase update <PH-####> <file|json>                          # Update a phase
aos spec phase show <PH-####>                                        # Show a phase
aos spec phase delete <PH-####>                                      # Delete a phase
aos spec phase assumptions set <PH-####> <file|json>                 # Set phase assumptions
aos spec phase research set <PH-####> <file|json>                    # Set phase research
```

---

## Spec Authoring — Tasks

```
aos spec task create <title> [--id TSK-######] [--phase PH-####] [--milestone MS-####]
aos spec task update <TSK-######> <file|json>
aos spec task show <TSK-######>
aos spec task list [--phase PH-####] [--milestone MS-####] [--status <status>]
aos spec task delete <TSK-######>
aos spec task assign <TSK-######> --phase PH-#### [--milestone MS-####]
aos spec task status <TSK-######> <Backlog|Planned|InProgress|Blocked|Done>
aos spec task plan set <TSK-######> <file|json>
aos spec task plan show <TSK-######>
aos spec task uat set <TSK-######> <file|json>
aos spec task uat show <TSK-######>
aos spec task link add <TSK-######> <type> <targetId>
aos spec task link remove <TSK-######> <type> <targetId>
aos spec task link list <TSK-######>
```

---

## Spec Authoring — Issues

```
aos spec issue create <title>
aos spec issue update <ISS-####> <file|json>
aos spec issue show <ISS-####>
aos spec issue list [--status <status>] [--type <type>] [--severity <severity>] \
                    [--task TSK-######] [--phase PH-####] [--milestone MS-####]
aos spec issue delete <ISS-####>
aos spec issue status <ISS-####> <open|investigating|resolved|deferred|wontfix>
```

---

## Spec Authoring — UAT (Global Objects)

```
aos spec uat create <title> [--id UAT-####] [--task TSK-######]
aos spec uat update <UAT-####> <file|json>
aos spec uat show <UAT-####>
aos spec uat list
aos spec uat delete <UAT-####>
aos spec uat bind <UAT-####> --task TSK-######
```

---

## State + Events

```
aos state show                                     # Show current state
aos state rebuild                                  # Rebuild state from events
aos event append <type> <file|json>                # Append an event
aos event tail [--n 50]                            # Tail recent events
aos event list [--since <timestamp>] [--type <type>]   # List events with filters
```

---

## Checkpoints

```
aos checkpoint create                              # Create a checkpoint snapshot
aos checkpoint list                               # List all checkpoints
aos checkpoint show <timestamp>                   # Show a specific checkpoint
aos checkpoint restore <timestamp>                # Restore to a checkpoint
```

---

## Runs

```
aos run start [--task TSK-######]                 # Open a new run record
aos run command add <argv...>                     # Add a command to the run log
aos run log add <path> [--kind build|test|other]  # Attach a log file to the run
aos run artifact add <path>                       # Attach an artifact to the run
aos run finish --status pass|fail                 # Close the run with a result
aos run show <RUN-*>                              # Show a specific run
aos run list                                      # List all runs
```

---

## Codebase Intelligence

```
aos codebase scan                                 # Scan repo for structure/targets
aos codebase map build                            # Build the full codebase map pack
aos codebase symbols build                        # Build symbol index cache
aos codebase graph build                          # Build file dependency graph
aos codebase summary                              # Show summary of codebase
aos codebase show <map|stack|architecture|structure|conventions|testing|integrations|concerns>
```

---

## Context Packs

```
aos pack build --task TSK-###### --budget <n>     # Build context pack for a task
aos pack build --phase PH-#### --budget <n>       # Build context pack for a phase
aos pack show <TSK-######|PH-####>                # Show a built context pack
aos pack diff --task TSK-###### --since RUN-*     # Diff a pack against a prior run
```

---

## Maintenance

```
aos cache clear                                   # Clear all caches
aos cache prune                                   # Prune stale cache entries
aos lock list                                     # List active locks
aos lock release --force                          # Force-release a lock
```

---

## Import / Export

```
aos export <path>                                 # Export workspace slice to path
aos import <path>                                 # Import workspace slice from path
```

---

## Help / Version

```
aos help                                          # Show help
aos version                                       # Show CLI version
```

---

## Task Status Values

| Value | Meaning |
|---|---|
| `Backlog` | Captured but not yet planned |
| `Planned` | Plan written, not started |
| `InProgress` | Currently being executed |
| `Blocked` | Cannot proceed without resolution |
| `Done` | Completed and verified |

---

## ID Formats

| Type | Format | Example |
|---|---|---|
| Milestone | `MS-####` | `MS-0001` |
| Phase | `PH-####` | `PH-0003` |
| Task | `TSK-######` | `TSK-000013` |
| Issue | `ISS-####` | `ISS-0002` |
| UAT | `UAT-####` | `UAT-0001` |
| Run | `RUN-<timestamp>` | `RUN-2026-01-13T021500Z` |
