## Context
The engine’s `.aos/*` workspace is a deterministic, contract-driven filesystem layout used for spec/state/evidence/configuration. Several behaviors and contract paths already exist in code (e.g., `config/`, `locks/`, and a `config` validation layer) that are not yet fully captured in OpenSpec, creating spec/impl drift.

This change formalizes:
- how the repository root is discovered (when `--root` is not provided)
- the canonical workspace top-level folders
- a single source of truth for canonical contract paths (ID-based and non-ID)
- the minimal public compile-against surface for workspace/path resolution

## Goals / Non-Goals
### Goals
- Document the canonical `.aos/` workspace contract including `.aos/config/` and `.aos/locks/`.
- Make repository-root discovery behavior explicit and deterministic.
- Extend canonical routing to cover non-ID contract paths and contract-path invariants.
- Provide a public surface (`Gmsd.Aos.Public.*`) for root discovery + canonical path resolution that does not leak internal namespaces.

### Non-Goals
- Redesigning the entire public services layout under `Gmsd.Aos/Public/Services/**` in this change.
- Defining multi-workspace or multi-project workspace semantics (single-project invariants remain as-is).
- Introducing new artifact kinds or new spec catalogs beyond what routing already supports.

## Decisions
### Decision: Repository root discovery markers
- Default root discovery (when `--root` is absent) will be defined as walking parents from the current directory to find a repository marker.
- Accepted markers will include:
  - `.git/` directory (git repository)
  - `Gmsd.slnx` file (solution root marker used by this repo)
- If no marker is found, commands that rely on root discovery MUST fail with an actionable error (rather than silently using the starting directory).

### Decision: Contract path format is platform-neutral
- Canonical contract paths are defined using forward slashes (`/`) regardless of host OS.
- Contract paths MUST NOT contain `.` or `..` segments and MUST begin with `.aos/`.
- Conversion from contract paths to absolute filesystem paths is centralized and validated.

### Decision: `config` is a first-class workspace layer
- `.aos/config/` is part of the canonical workspace and is created by `aos init`.
- Workspace validation includes a `config` layer as a selectable layer, and by default validates it.
- Config artifacts remain optional unless present (validation reports issues only when files exist and are invalid / schema-invalid).

### Decision: Public `IWorkspace` focuses on discovery + path resolution
The public surface will include enough shape to:
- expose `RepositoryRootPath` and `AosRootPath`
- resolve artifact IDs → canonical contract paths
- resolve contract paths → absolute paths safely

It will not require consumers to reference `Gmsd.Aos.Engine.*` types.

## Alternatives considered
- **Only `.git/` as a marker**: simpler but fails for repos where `.git` is not present (e.g., exported worktrees or certain environments). Allowing `Gmsd.slnx` matches current CLI implementation and this repo’s layout.
- **Keep repo-root discovery CLI-only**: would block non-CLI consumers from using the engine deterministically without copying internal logic.

## Risks / Trade-offs
- Tightening “outside a repository MUST fail” could break existing ad-hoc usage where a user runs `aos init` in a non-repo folder. This is an intentional contract tightening to match spec-first behavior.
- Adding/standardizing additional contract paths increases surface area; mitigated by keeping them centralized in routing and covered by tests.

## Migration plan (apply stage)
- Update OpenSpec requirements first (this change) to remove ambiguity.
- Update code to match specs (notably repository-root discovery failure semantics and public `IWorkspace`).
- Add/adjust tests to lock deterministic path mapping and root discovery behavior.

