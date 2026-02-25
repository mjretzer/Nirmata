## 1. Normalize Purpose stubs across specs
- [x] 1.1 Find all instances of placeholder Purpose text created during archive
- [x] 1.2 Replace each placeholder Purpose with durable, long-lived Purpose text (1–3 sentences + explicit scope bullets)
- [x] 1.3 Verify no placeholder Purpose text remains

## 2. Clarify engine-* vs aos-* layering (no merges)
- [x] 2.1 Update each `engine-*` spec Purpose to state it defines the public DI/interface surface
- [x] 2.2 Add an explicit conformance statement referencing the corresponding `aos-*` spec as canonical semantics
- [x] 2.3 Verify each `engine-*` spec points to exactly one canonical `aos-*` spec

## 3. Align web-* specs with current implementation
- [x] 3.1 Update `web-*` specs so routes match current Razor Pages route templates
- [x] 3.2 Update `web-*` specs so `.aos/**` file access patterns match current implementation (directory enumeration vs index files)
- [x] 3.3 Ensure stable `web-*` specs describe current implemented behavior; future UI work belongs in separate proposals

## 4. Verification
- [x] 4.1 Run OpenSpec strict validation for this change
- [x] 4.2 Re-run OpenSpec strict validation for the full repository
- [x] 4.3 Record changed spec files list and validation output as verification notes
