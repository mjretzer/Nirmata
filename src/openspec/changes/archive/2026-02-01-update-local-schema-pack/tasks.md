## 1. OpenSpec
- [x] 1.1 Add spec deltas for `aos-schema-registry` (local schema pack validation contract).
- [x] 1.2 Add spec deltas for `aos-workspace-bootstrap` (init seeds `.aos/schemas/**` files).
- [x] 1.3 Run `openspec validate update-local-schema-pack --strict` and fix any issues.

## 2. Engine implementation
- [x] 2.1 Update `aos init` to seed `.aos/schemas/*.schema.json` templates and a non-empty `.aos/schemas/registry.json`.
- [x] 2.2 Implement a local schema pack loader for `.aos/schemas/**`.
- [x] 2.3 Update `aos validate schemas` to validate the local schema pack (add `--root` option; update help text).

## 3. Tests / fixtures
- [x] 3.1 Update approved `.aos` init fixture to include seeded schemas and updated registry.json.
- [x] 3.2 Add/adjust tests for `aos validate schemas` validating the local pack.
- [x] 3.3 Run `dotnet test` and fix failures.

