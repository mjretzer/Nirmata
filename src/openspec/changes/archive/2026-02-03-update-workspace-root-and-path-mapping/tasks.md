## 1. Specification updates
- [x] 1.1 Update `aos-workspace-bootstrap` deltas to include `.aos/config/` and `.aos/locks/` and to clarify repo-root discovery failure semantics.
- [x] 1.2 Update `aos-workspace-validation` deltas to include the `config` layer in defaults and `--layers` parsing/allowed set.
- [x] 1.3 Update `aos-path-routing` deltas to add non-ID contract paths (e.g., workspace lock) and contract-path invariants.
- [x] 1.4 Update `aos-public-api-surface` deltas to require a public workspace abstraction that exposes root discovery and canonical path resolution.

## 2. Implementation (apply stage)
- [x] 2.1 Move repository-root discovery into an engine-owned helper (public or internal) and ensure “no marker found” yields an actionable failure.
- [x] 2.2 Align workspace bootstrap and compliance checks with the updated canonical top-level contract (including `config/` and `locks/`).
- [x] 2.3 Align workspace validation layer defaults and CLI `--layers` allowed values with the updated spec (including `config`).
- [x] 2.4 Expand `nirmata.Aos.Public.IWorkspace` to include root paths and safe canonical path resolution helpers.

## 3. Verification
- [x] 3.1 Add deterministic path routing tests (IDs + non-ID contract paths).
- [x] 3.2 Add repo-root discovery tests: `.git` marker, `nirmata.slnx` marker, and failure outside a repo.
- [x] 3.3 Run `openspec validate update-workspace-root-and-path-mapping --strict` and fix any issues.

