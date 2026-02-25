# Schema Migration Process and Deprecation Timeline

## Overview

This document describes the process for migrating existing GMSD workspaces from old artifact formats to the new unified canonical schemas defined in the unify-data-contracts change.

## Migration Phases

### Phase 1: Preparation (Current)
- Canonical schemas are defined in aos-schema-registry
- Schema validation infrastructure is implemented
- Migration detection and transformation rules are available
- Migration CLI tool is available

### Phase 2: Gradual Adoption (Recommended)
1. Update Phase Planner to emit new schema format
2. Add reader validation to Task Executor (accept both old and new)
3. Update Task Executor to emit new evidence format
4. Add reader validation to UAT Verifier
5. Update UAT Verifier to emit new results format
6. Update Fix Planner to read new formats and emit new fix plans

### Phase 3: Migration Execution
1. Run migration CLI on existing workspaces
2. Validate all migrated artifacts
3. Archive old artifacts (keep for rollback)

### Phase 4: Cleanup (Future)
1. Remove old format support from readers
2. Archive old schema definitions
3. Update documentation

## Migration Tooling

### CLI Command: `gmsd migrate-schemas`

```bash
gmsd migrate-schemas --workspace-path <path> [--dry-run] [--backup]
```

#### Options

- `--workspace-path, -w` (required): Path to the workspace root directory
- `--dry-run, -d` (optional): Perform migration without writing changes (default: false)
- `--backup, -b` (optional): Create backup before migration (default: true)

#### Examples

**Dry-run migration to preview changes:**
```bash
gmsd migrate-schemas --workspace-path C:\projects\my-workspace --dry-run
```

**Perform migration with backup:**
```bash
gmsd migrate-schemas --workspace-path C:\projects\my-workspace --backup
```

**Perform migration without backup (not recommended):**
```bash
gmsd migrate-schemas --workspace-path C:\projects\my-workspace --backup false
```

## Artifact Format Detection Rules

The migration tool automatically detects artifact formats based on:

1. **Schema ID field**: Presence of `schemaId` field indicates new format
2. **Schema Version field**: Presence of `schemaVersion` field with canonical format
3. **File path patterns**: Artifact location indicates type
4. **Structure analysis**: Field presence and structure indicate old vs new format

### Detected Artifact Types

- **Phase Plan**: `.aos/spec/phases/{phase-id}/plan.json`
- **Task Plan**: `.aos/spec/tasks/{task-id}/plan.json`
- **Verifier Input**: `.aos/spec/uat/UAT-{task-id}.json`
- **Verifier Output**: `.aos/evidence/runs/{run-id}/artifacts/uat-results.json`
- **Fix Plan**: `.aos/spec/fixes/{fix-task-id}/plan.json`
- **Diagnostic**: `.aos/diagnostics/{phase}/{artifact-id}.diagnostic.json`

## Transformation Rules

### Task Plan Transformation

**Old Format:**
```json
{
  "taskId": "TSK-0001",
  "fileScopes": ["src/", "tests/"],
  "verificationSteps": []
}
```

**New Format:**
```json
{
  "schemaVersion": 1,
  "schemaId": "gmsd:aos:schema:task-plan:v1",
  "taskId": "TSK-0001",
  "fileScopes": [
    {"path": "src/"},
    {"path": "tests/"}
  ],
  "verificationSteps": [],
  "timestamp": "2026-02-19T18:07:00Z"
}
```

**Transformation Rules:**
- Add `schemaVersion: 1` and `schemaId` fields
- Transform `fileScopes` from string array to object array with `path` field
- Add `timestamp` if not present
- Preserve all other fields

### Verifier Input Transformation

**Old Format:**
```json
{
  "taskId": "TSK-0001",
  "criteria": [{"id": "C1"}],
  "fileScopes": []
}
```

**New Format:**
```json
{
  "schemaVersion": 1,
  "schemaId": "gmsd:aos:schema:verifier-input:v1",
  "taskId": "TSK-0001",
  "acceptanceCriteria": [{"id": "C1"}],
  "fileScopes": [],
  "timestamp": "2026-02-19T18:07:00Z"
}
```

**Transformation Rules:**
- Add `schemaVersion: 1` and `schemaId` fields
- Rename `criteria` to `acceptanceCriteria` if present
- Add `timestamp` if not present

### Verifier Output Transformation

**Old Format:**
```json
{
  "taskId": "TSK-0001",
  "status": "passed",
  "checks": []
}
```

**New Format:**
```json
{
  "schemaVersion": 1,
  "schemaId": "gmsd:aos:schema:verifier-output:v1",
  "taskId": "TSK-0001",
  "status": "passed",
  "checks": [],
  "timestamp": "2026-02-19T18:07:00Z"
}
```

**Transformation Rules:**
- Add `schemaVersion: 1` and `schemaId` fields
- Add `timestamp` if not present
- Preserve all other fields

### Fix Plan Transformation

**Old Format:**
```json
{
  "taskId": "TSK-0001",
  "fixes": [],
  "verificationSteps": []
}
```

**New Format:**
```json
{
  "schemaVersion": 1,
  "schemaId": "gmsd:aos:schema:fix-plan:v1",
  "taskId": "TSK-0001",
  "fixes": [],
  "verificationSteps": [],
  "timestamp": "2026-02-19T18:07:00Z"
}
```

**Transformation Rules:**
- Add `schemaVersion: 1` and `schemaId` fields
- Add `timestamp` if not present
- Preserve all other fields

### Phase Plan Transformation

**Old Format:**
```json
{
  "tasks": [],
  "fileScopes": ["src/"],
  "verificationSteps": []
}
```

**New Format:**
```json
{
  "schemaVersion": 1,
  "schemaId": "gmsd:aos:schema:phase-plan:v1",
  "tasks": [],
  "fileScopes": [{"path": "src/"}],
  "verificationSteps": [],
  "timestamp": "2026-02-19T18:07:00Z"
}
```

**Transformation Rules:**
- Add `schemaVersion: 1` and `schemaId` fields
- Transform `fileScopes` from string array to object array with `path` field
- Add `timestamp` if not present

## Backup and Rollback

### Automatic Backup

When running migration with `--backup` (default), the tool creates a timestamped backup:

```
backup-20260219-180700/
  ├── spec/
  ├── evidence/
  └── diagnostics/
```

### Manual Rollback

To restore from a backup:

```csharp
var migrator = new SchemaMigrator(workspace);
await migrator.RestoreFromBackupAsync("path/to/backup-20260219-180700");
```

### Backup Location

Backups are created in the parent directory of the `.aos` folder:

```
workspace-root/
  ├── .aos/
  ├── backup-20260219-180700/  ← Backup created here
  └── ...
```

## Validation After Migration

All migrated artifacts are validated against canonical schemas before being written:

1. **Schema Structure Validation**: Ensures all required fields are present
2. **Data Type Validation**: Verifies field types match schema
3. **Format Validation**: Confirms transformed format is correct

If validation fails, the migration is aborted and a diagnostic artifact is created.

## Deprecation Timeline

### v1.0 (Current)
- New canonical schemas available
- Old format support remains
- Migration tool available
- Recommendation: Begin migration planning

### v1.1 (Recommended: 2-3 months)
- Phase Planner emits new format only
- Task Executor accepts both formats
- Recommendation: Run migrations on non-production workspaces

### v1.2 (Recommended: 3-4 months)
- All components emit new format
- Old format support deprecated
- Recommendation: Complete all migrations

### v2.0 (Recommended: 6+ months)
- Old format support removed
- Breaking change: Old artifacts no longer supported
- Recommendation: All workspaces must be migrated

## Migration Checklist

- [ ] Back up workspace (automatic with `--backup`)
- [ ] Run migration in dry-run mode: `gmsd migrate-schemas --workspace-path <path> --dry-run`
- [ ] Review dry-run output for any errors
- [ ] Run actual migration: `gmsd migrate-schemas --workspace-path <path>`
- [ ] Verify all artifacts migrated successfully
- [ ] Test workflow with migrated artifacts
- [ ] Archive backup after successful migration (optional)

## Troubleshooting

### Migration Fails with "Invalid JSON"

**Cause**: Artifact file contains malformed JSON

**Solution**: 
1. Check the artifact file for syntax errors
2. Fix the JSON manually or restore from backup
3. Re-run migration

### Migration Fails with "Missing Required Field"

**Cause**: Old format artifact is missing expected fields

**Solution**:
1. Review the artifact structure
2. Add missing fields manually if possible
3. Or restore from backup and investigate root cause

### Rollback After Failed Migration

```bash
# Restore from backup
gmsd restore-backup --backup-path path/to/backup-20260219-180700
```

## Performance Considerations

- Migration time depends on number of artifacts
- Typical workspace: < 1 second per artifact
- Large workspaces (1000+ artifacts): 1-2 minutes total
- Backup creation: 1-5 seconds depending on workspace size

## Support and Questions

For issues or questions about migration:

1. Check the troubleshooting section above
2. Review the transformation rules for your artifact type
3. Consult the diagnostic artifacts created during validation failures
4. Contact the GMSD team with:
   - Workspace path
   - Migration command used
   - Error messages
   - Backup location (if available)
