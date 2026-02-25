## 1. Proposal acceptance criteria
- [x] 1.1 Change contains proposal + delta specs for every affected capability
- [x] 1.2 Every requirement has at least one `#### Scenario:`
- [x] 1.3 `openspec validate add-cursor-kind-id-invariants --strict` passes

## 2. Implementation (apply stage)
- [x] 2.1 Update `Gmsd.Aos/Resources/Schemas/state-snapshot.schema.json` to allow optional `cursor.kind` and `cursor.id` strings
- [x] 2.2 Extend `Gmsd.Aos/Engine/Validation/AosWorkspaceValidator.cs` invariants:
  - [x] 2.2.1 If only one of `cursor.kind`/`cursor.id` is present → fail
  - [x] 2.2.2 Validate kind is recognized (aligned to `Gmsd.Aos.Public.Catalogs.ArtifactKinds`)
  - [x] 2.2.3 Validate id parses + is canonical for kind (aligned to routing rules)
  - [x] 2.2.4 Validate referenced artifact exists at canonical contract path
  - [x] 2.2.5 If catalog index exists for kind, require id present in index
- [x] 2.3 Add tests for cursor invariants (new file or extend existing invariant tests)
- [x] 2.4 Run engine test suite (`dotnet test`) and ensure determinism-focused snapshots remain stable

