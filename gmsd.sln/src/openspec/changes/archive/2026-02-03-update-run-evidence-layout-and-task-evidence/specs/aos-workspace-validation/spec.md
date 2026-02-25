## MODIFIED Requirements
### Requirement: Workspace validation requires baseline evidence artifacts
Workspace validation MUST enforce that baseline evidence artifacts exist and are valid JSON:
- `.aos/evidence/logs/commands.json`
- `.aos/evidence/runs/index.json`

Workspace validation SHOULD validate additional evidence artifacts when present, including:
- `.aos/evidence/runs/<run-id>/summary.json`
- `.aos/evidence/runs/<run-id>/commands.json`
- `.aos/evidence/runs/<run-id>/artifacts/manifest.json`
- `.aos/evidence/task-evidence/<task-id>/latest.json`

During the transition period, workspace validation SHOULD tolerate legacy run layouts created before the restructured PH-ENG-0006 layout, while still enforcing the baseline evidence artifacts above.

#### Scenario: Missing evidence command log fails validation
- **GIVEN** `.aos/evidence/logs/commands.json` is missing
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the missing artifact

