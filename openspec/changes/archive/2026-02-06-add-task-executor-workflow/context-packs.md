# Context Pack Integration Patterns

This document describes how context packs are integrated with the Subagent Orchestrator for task execution.

## Overview

Context packs provide isolated, pre-loaded context for subagent execution. The Subagent Orchestrator loads these packs with strict budget enforcement to prevent resource exhaustion.

## Context Pack Location

Context packs are stored at: `.aos/context/packs/{packId}.json`

## Context Pack Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Context Pack",
  "type": "object",
  "required": ["id", "name"],
  "properties": {
    "id": {
      "type": "string",
      "description": "Unique identifier for the context pack"
    },
    "name": {
      "type": "string",
      "description": "Human-readable name"
    },
    "files": {
      "type": "array",
      "description": "Files included in this context pack",
      "items": {
        "type": "object",
        "required": ["relativePath", "content"],
        "properties": {
          "relativePath": {
            "type": "string",
            "description": "Path relative to workspace root"
          },
          "content": {
            "type": "string",
            "description": "File content"
          }
        }
      }
    },
    "metadata": {
      "type": "object",
      "properties": {
        "description": { "type": "string" },
        "tags": {
          "type": "array",
          "items": { "type": "string" }
        },
        "createdAt": {
          "type": "string",
          "format": "date-time"
        }
      }
    }
  }
}
```

## Budget Enforcement

The Subagent Orchestrator enforces the following budgets per invocation:

| Budget Type | Default | Description |
|-------------|---------|-------------|
| `MaxIterations` | 50 | Maximum execution loop iterations |
| `MaxToolCalls` | 100 | Maximum tool invocations |
| `MaxExecutionTimeSeconds` | 300 | Maximum wall-clock execution time |
| `MaxTokens` | 8000 | Maximum LLM token consumption |

### Token Budget Calculation

```
MaxBytes = MaxTokens × 4  // Rough approximation: ~4 bytes per token
```

Context packs exceeding the budget are rejected with a `SubagentBudgetExceededException`.

## Loading Pattern

```csharp
// 1. Request specifies context pack IDs
var request = new SubagentRunRequest
{
    ContextPackIds = new[] { "api-contracts", "domain-models" },
    Budget = new SubagentBudget
    {
        MaxTokens = 4000,
        MaxToolCalls = 50
    }
};

// 2. Orchestrator loads packs with budget validation
var packs = await LoadContextPacksAsync(request, runId, ct);

// 3. Packs are copied to isolated working directory
var isolatedContext = CreateIsolatedExecutionContext(request, packs);
```

## Isolation Pattern

Each subagent run creates a fresh isolated context:

1. **New Working Directory**: `temp/subagent-{guid}/`
2. **Fresh Environment**: No inherited env vars (clean dictionary)
3. **Copied Files**: Context pack files copied to isolated directory
4. **No Shared State**: Completely isolated from parent and other runs

## Error Handling

| Exception | Trigger | Result |
|-----------|---------|--------|
| `SubagentBudgetExceededException` | Budget limit exceeded | Run closed with failure, `errorCategory: budget_exceeded` |
| `SubagentContextLoadException` | Pack file not found or invalid | Run closed with failure, `errorCategory: context_load_failed` |
| `SubagentTimeoutException` | Execution time exceeded | Run closed with failure, `errorCategory: timeout` |
| `SubagentScopeViolationException` | File outside allowed scope | Run closed with failure, `errorCategory: scope_violation` |

## Best Practices

1. **Keep packs focused**: Single responsibility per context pack
2. **Size appropriately**: Large packs consume budget quickly
3. **Version packs**: Include version in pack metadata for reproducibility
4. **Cache wisely**: Packs can be reused across runs, but are copied for isolation
