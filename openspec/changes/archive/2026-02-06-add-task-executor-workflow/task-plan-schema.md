# Task Plan JSON Schema

This document defines the JSON schema for task plans used by the Task Executor.

## Location

Task plans are stored at: `.aos/spec/tasks/TSK-*/plan.json`

## Schema Definition

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Task Plan",
  "type": "object",
  "required": ["title", "steps", "fileScopes"],
  "properties": {
    "title": {
      "type": "string",
      "description": "Human-readable title of the task plan"
    },
    "steps": {
      "type": "array",
      "description": "Ordered list of execution steps",
      "items": {
        "$ref": "#/definitions/step"
      },
      "minItems": 1
    },
    "fileScopes": {
      "type": "array",
      "description": "File paths or directories the task is allowed to modify",
      "items": {
        "$ref": "#/definitions/fileScope"
      },
      "minItems": 1
    },
    "metadata": {
      "type": "object",
      "description": "Optional metadata for the task plan",
      "properties": {
        "priority": {
          "type": "string",
          "enum": ["low", "medium", "high", "critical"],
          "default": "medium"
        },
        "estimatedDuration": {
          "type": "number",
          "description": "Estimated execution time in minutes"
        },
        "tags": {
          "type": "array",
          "items": { "type": "string" }
        }
      }
    }
  },
  "definitions": {
    "step": {
      "type": "object",
      "required": ["id", "type", "description"],
      "properties": {
        "id": {
          "type": "string",
          "pattern": "^step-[a-z0-9-]+$",
          "description": "Unique identifier for the step"
        },
        "type": {
          "type": "string",
          "enum": ["file_edit", "file_create", "file_delete", "command", "verify"],
          "description": "Type of step operation"
        },
        "description": {
          "type": "string",
          "description": "Human-readable description of the step"
        },
        "targetPath": {
          "type": "string",
          "description": "Target file path for file operations"
        },
        "dependencies": {
          "type": "array",
          "items": { "type": "string" },
          "description": "List of step IDs that must complete before this step"
        }
      }
    },
    "fileScope": {
      "type": "object",
      "required": ["path"],
      "properties": {
        "path": {
          "type": "string",
          "description": "File or directory path. Can include wildcards for directories ending with /"
        },
        "permissions": {
          "type": "string",
          "enum": ["read", "write", "read-write"],
          "default": "read-write",
          "description": "Permission level for this scope"
        }
      }
    }
  }
}
```

## Example

See [example-task-plan.md](example-task-plan.md) for a complete example.

## Scope Enforcement

The Task Executor validates that:

1. All file scopes in `plan.fileScopes` are within the request's `allowedFileScope`
2. Each step's `targetPath` (if provided) is within the allowed scope
3. Scope violations result in immediate execution failure

## Path Formats

- Use forward slashes (`/`) for cross-platform compatibility
- Directory scopes should end with `/` (e.g., `src/`)
- Relative paths are resolved from the workspace root
- Leading slashes are normalized (stripped)
