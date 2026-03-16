# User Migration Guide: Upgrading to Unified Data Contracts

## What's Changing?

nirmata is upgrading to unified data contracts across all workflow phases. This ensures reliable artifact chaining between Phase Planning, Task Execution, Verification, and Fix Planning.

**Key Changes:**
- All artifacts now include `schemaVersion` and `schemaId` fields
- File scopes are now objects with `path` field (not strings)
- Acceptance criteria field renamed to `acceptanceCriteria` in some artifacts
- All artifacts include ISO-8601 timestamps

## Do I Need to Migrate?

**Yes, if you have:**
- Existing workspaces with artifacts created before this change
- Custom scripts that read or write nirmata artifacts
- Automated workflows that depend on artifact formats

**No, if you:**
- Are starting a new workspace
- Have only used the latest nirmata version

## How to Migrate

### Step 1: Back Up Your Workspace

```bash
# Automatic backup is created by default
nirmata migrate-schemas --workspace-path C:\path\to\workspace
```

Or manually:
```bash
# Copy your entire workspace to a safe location
xcopy C:\path\to\workspace C:\backups\workspace-backup /E /I
```

### Step 2: Preview Changes (Dry-Run)

```bash
nirmata migrate-schemas --workspace-path C:\path\to\workspace --dry-run
```

This shows what will be changed without modifying files.

### Step 3: Run Migration

```bash
nirmata migrate-schemas --workspace-path C:\path\to\workspace
```

The tool will:
1. Discover all artifacts in the workspace
2. Create a backup (unless `--backup false` is specified)
3. Transform each artifact to the new format
4. Validate transformed artifacts
5. Write updated artifacts to disk

### Step 4: Verify Migration

After migration completes:

1. **Check for errors**: Review the migration output for any failures
2. **Test workflows**: Run a test execution to ensure everything works
3. **Validate artifacts**: Use the validation CLI command:
   ```bash
   nirmata validate-artifact --path .aos/spec/tasks/TSK-0001/plan.json
   ```

## Understanding the Changes

### Before (Old Format)

```json
{
  "taskId": "TSK-0001",
  "fileScopes": ["src/", "tests/"],
  "verificationSteps": [
    {
      "id": "V1",
      "description": "Check file exists"
    }
  ]
}
```

### After (New Format)

```json
{
  "schemaVersion": 1,
  "schemaId": "nirmata:aos:schema:task-plan:v1",
  "taskId": "TSK-0001",
  "fileScopes": [
    {"path": "src/"},
    {"path": "tests/"}
  ],
  "verificationSteps": [
    {
      "id": "V1",
      "description": "Check file exists"
    }
  ],
  "timestamp": "2026-02-19T18:07:00Z"
}
```

**Key Differences:**
- `schemaVersion` and `schemaId` added for validation
- `fileScopes` changed from string array to object array
- `timestamp` added for audit trail

## Rollback (If Needed)

If something goes wrong, you can restore from the backup:

```bash
# List available backups
dir C:\path\to\workspace\..  # Look for backup-YYYYMMDD-HHMMSS folders

# Restore from backup
nirmata restore-backup --backup-path C:\path\to\workspace\..\backup-20260219-180700
```

Or manually:
```bash
# Copy backup back to workspace
xcopy C:\backups\workspace-backup .aos /E /I /Y
```

## Custom Scripts and Integrations

If you have custom scripts that read or write nirmata artifacts:

### Reading Artifacts

**Before:**
```csharp
var json = File.ReadAllText("plan.json");
var plan = JsonSerializer.Deserialize<TaskPlan>(json);
```

**After:**
```csharp
var json = File.ReadAllText("plan.json");
var plan = JsonSerializer.Deserialize<TaskPlan>(json);

// Validate against schema
var validation = ArtifactContractValidator.ValidateTaskPlan(
    artifactPath: "plan.json",
    artifactJson: json,
    aosRootPath: ".aos",
    readBoundary: "custom-reader");

if (!validation.IsValid)
{
    throw new InvalidOperationException($"Artifact validation failed: {validation.DiagnosticPath}");
}
```

### Writing Artifacts

**Before:**
```csharp
var plan = new TaskPlan { ... };
var json = JsonSerializer.Serialize(plan);
File.WriteAllText("plan.json", json);
```

**After:**
```csharp
var plan = new TaskPlan 
{ 
    SchemaVersion = 1,
    SchemaId = "nirmata:aos:schema:task-plan:v1",
    Timestamp = DateTimeOffset.UtcNow,
    // ... other fields
};
var json = JsonSerializer.Serialize(plan);

// Validate before writing
var validation = ArtifactContractValidator.ValidateTaskPlan(
    artifactPath: "plan.json",
    artifactJson: json,
    aosRootPath: ".aos",
    readBoundary: "custom-writer");

if (!validation.IsValid)
{
    throw new InvalidOperationException($"Artifact validation failed: {validation.DiagnosticPath}");
}

File.WriteAllText("plan.json", json);
```

## Troubleshooting

### Q: Migration fails with "Missing required field"

**A:** The artifact is missing a field that's required in the new schema. Options:
1. Restore from backup and investigate the root cause
2. Manually add the missing field to the artifact
3. Delete the artifact and regenerate it

### Q: My custom script breaks after migration

**A:** Update your script to handle the new schema fields:
- Check for `schemaVersion` and `schemaId`
- Handle `fileScopes` as objects, not strings
- Use the validation API to ensure compatibility

### Q: How long does migration take?

**A:** Typically:
- Small workspace (< 100 artifacts): < 10 seconds
- Medium workspace (100-1000 artifacts): 10-60 seconds
- Large workspace (> 1000 artifacts): 1-5 minutes

### Q: Can I migrate just some artifacts?

**A:** The migration tool migrates all artifacts in the workspace. To migrate selectively:
1. Move artifacts you want to keep to a temporary location
2. Run migration
3. Move artifacts back (they won't be migrated)

Or use the programmatic API:
```csharp
var migrator = new SchemaMigrator(workspace);
var artifact = new ArtifactFormatInfo { ... };
var result = await migrator.MigrateArtifactAsync(artifact);
```

### Q: What if I have custom artifact types?

**A:** The migration tool only handles standard nirmata artifact types. For custom artifacts:
1. Manually update them to include `schemaVersion` and `schemaId`
2. Or create a custom migration script using the `ArtifactTransformer` API

## Timeline and Support

**Current Status:** Migration tool available, old format still supported

**Deprecation Schedule:**
- v1.1 (2-3 months): Old format deprecated in new code
- v1.2 (3-4 months): Old format support removed
- v2.0 (6+ months): Breaking change - old artifacts no longer supported

**Recommendation:** Migrate your workspaces within the next 2-3 months to avoid issues.

## Getting Help

If you encounter issues:

1. **Check the troubleshooting section** above
2. **Review the migration process document** for detailed technical information
3. **Check diagnostic artifacts** created during validation failures
4. **Contact support** with:
   - Your workspace path
   - The migration command you ran
   - Any error messages
   - The backup location (if available)

## Summary

The migration to unified data contracts is straightforward:

1. Run `nirmata migrate-schemas --workspace-path <path> --dry-run` to preview
2. Run `nirmata migrate-schemas --workspace-path <path>` to migrate
3. Test your workflows to ensure everything works
4. Update any custom scripts to handle the new schema

Your workspace will be fully compatible with the new unified contract system, enabling reliable artifact chaining across all workflow phases.
