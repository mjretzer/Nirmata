## 1. Spec deltas
- [x] 1.1 Update `aos-schema-registry` requirements for `$id`-based schema identity and public schema ID catalog
- [x] 1.2 Update `aos-workspace-validation` requirements for schema-based validation, normalized reporting, and roadmap reference invariants

## 2. Engine implementation: schema registry by `$id`
- [x] 2.1 Add JSON Schema validator dependency (`JsonSchema.Net`)
- [x] 2.2 Implement embedded schema registry that loads embedded `*.schema.json` deterministically and indexes by `$id`
- [x] 2.3 Extend local schema pack loader/validator to detect missing/duplicate `$id` in the local pack
- [x] 2.4 Fill `Gmsd.Aos.Public.Catalogs.SchemaIds` with canonical `$id` constants

## 3. Engine implementation: schema-based workspace validation
- [x] 3.1 Add schema-based validation for baseline artifacts written by `aos init` (project, roadmap, catalog indexes, state snapshot, run index)
- [x] 3.2 Ensure config/policy validation participates in workspace validation using the local schema pack (when present / required)
- [x] 3.3 Emit normalized schema validation issues (contract path + schema id + instance location + message)
- [x] 3.4 Add roadmap cross-file invariant validation for `roadmap.items[]` references
- [x] 3.5 Fill `Gmsd.Aos.Public.Catalogs.ArtifactKinds` with stable kind strings aligned to routing rules

## 4. Schema assets
- [x] 4.1 Add `roadmap.schema.json` matching the current roadmap document shape
- [x] 4.2 Add schema(s) for baseline evidence artifacts (`runs/index.json`, `logs/commands.json`)
- [x] 4.3 Add schema-light placeholders for upcoming artifacts: milestone, phase, task, uat, issue, event, context-pack, evidence

## 5. Tests
- [x] 5.1 Add fixture(s) with schema violations and assert `aos validate workspace` fails deterministically
- [x] 5.2 Add fixture(s) with roadmap references to missing artifacts and assert deterministic failure
- [x] 5.3 Add fixture(s) with roadmap references missing from catalog index and assert deterministic failure

## 6. Validation
- [x] 6.1 Run `openspec validate add-schema-based-workspace-validation --strict`
- [x] 6.2 Address any strict validation failures until the change is clean

