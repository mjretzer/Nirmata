## 1. Proposal artifacts
- [x] 1.1 Add `proposal.md`, `design.md`, and `tasks.md` under `openspec/changes/add-context-pack-builder/`
- [x] 1.2 Add spec deltas under `openspec/changes/add-context-pack-builder/specs/**`
- [x] 1.3 Run `openspec validate add-context-pack-builder --strict` and fix all findings

## 2. Schema + contracts (implementation)
- [x] 2.1 Expand `Gmsd.Aos/Resources/Schemas/context-pack.schema.json` to model a self-contained pack:
  - pack id (`PCK-####`)
  - mode (`task`|`phase`) + driving id (`TSK-######` or `PH-####`)
  - deterministic budget summary (bytes/items)
  - embedded entries (contractPath + content + contentType + sha256/bytes)
- [x] 2.2 Add/confirm `SchemaIds.ContextPackV1` is used for validating `.aos/context/packs/*.json`

## 3. Path routing (implementation)
- [x] 3.1 Extend ID parsing to include `PCK-####`
- [x] 3.2 Add canonical routing for `PCK-####` to `.aos/context/packs/PCK-####.json`
- [x] 3.3 Decide public kind label exposure (new constant in a public catalog) and document it

## 4. Pack builder (implementation)
- [x] 4.1 Add pack build API in `Gmsd.Aos/Context/**` (inputs: mode + id + budget; output: pack object)
- [x] 4.2 Implement deterministic selection rules for task-mode and phase-mode (allowed artifacts only)
- [x] 4.3 Enforce budgets deterministically (stable inclusion order; stable truncation behavior)
- [x] 4.4 Write pack with canonical deterministic JSON writer to `.aos/context/packs/PCK-####.json`

## 5. Workspace validation (implementation)
- [x] 5.1 Update `aos validate workspace` to validate `.aos/context/packs/PCK-####.json` against the local schema pack when present
- [x] 5.2 Ensure validation errors use the normalized schema issue report shape (contract path + schema id + instance location + message)

## 6. CLI (implementation)
- [x] 6.1 Add `aos pack build --task <TSK-######>` and `aos pack build --phase <PH-####>` commands
- [x] 6.2 Default output creates a new pack id deterministically (formatting) and prints the created `PCK-####`
- [x] 6.3 Ensure commands acquire workspace lock before writing to `.aos/**`

## 7. Tests / fixtures (implementation)
- [x] 7.1 Add golden fixtures proving deterministic pack bytes for the same inputs
- [x] 7.2 Add validation tests for schema failures and budget enforcement

