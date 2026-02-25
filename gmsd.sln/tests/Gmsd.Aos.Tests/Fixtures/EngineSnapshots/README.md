## Engine snapshot fixture layout

This folder is the **source of truth** for AOS engine regression snapshot tests.

### Conventions

- **Versioned corpus**: fixtures live under `v1/`, `v2/`, etc so we can evolve snapshots without rewriting history.
- **Inputs vs approved**:
  - `v*/inputs/<case>/` contains the minimal on-disk inputs required to run the scenario (usually a workspace root containing a `.aos/` tree, and optionally additional files like codebase/context inputs).
  - `v*/approved/<case>/` contains the expected outputs for the scenario (e.g. produced `.aos/**` artifacts, plus captured `stdout.txt`, `stderr.txt`, and `exit-code.txt` when relevant).
- **Shared baseline workspace fixture**: prefer reusing `../Approved/.aos/**` as the canonical “fresh workspace” when a case only needs a clean `.aos` tree. Add per-case input overlays only when needed.

### Directory skeleton (v1)

- `v1/inputs/` (scenario inputs)
- `v1/approved/` (approved snapshots)

