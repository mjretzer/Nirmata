## Context
Multiple engine components emit JSON artifacts under `.aos/**` (workspace bootstrap, run evidence, execute-plan logs). Today these writers normalize line endings and use `System.Text.Json` with indentation, but do not guarantee canonical key ordering across runtimes/hosts and do not guarantee atomic writes.

This change introduces a single **canonical deterministic JSON writer** for AOS artifacts that provides:
- canonical ordering (recursive ordinal key sorting)
- stable formatting and bytes
- atomic write semantics (prevent partial/corrupt artifacts)
- no-churn behavior when canonical bytes are unchanged

## Goals / Non-Goals
### Goals
- Deterministic JSON bytes for all **AOS-emitted** JSON artifacts under `.aos/**` across runs/hosts.
- Prevent partially-written artifacts via atomic write semantics.
- Centralize JSON-write behavior behind one engine-owned abstraction used by all AOS writers.

### Non-Goals
- Changing product API response JSON formatting/ordering.
- Rewriting or “pretty-printing” arbitrary user-supplied JSON beyond AOS-emitted artifacts.
- Introducing schema-dependent canonicalization rules (this is a generic JSON canonicalizer with ordinal ordering only).

## Decisions
### Decision: Canonicalize JSON before writing
**Approach**
- Convert the to-be-written value to a JSON DOM (`JsonElement`/`JsonDocument` or `JsonNode`).
- Recursively re-emit JSON using `Utf8JsonWriter`, ordering object properties by **ordinal string ordering** at every object boundary.
- Preserve array ordering exactly as provided (arrays are ordered collections).

**Why**
- `System.Text.Json` does not guarantee stable property ordering for all input types (e.g., dictionaries, reflection ordering differences across hosts/runtimes).
- A canonicalizer makes ordering deterministic regardless of input type or runtime reflection behavior.

**Alternatives considered**
- Rely on `JsonSerializer` property order: rejected (not reliably deterministic across hosts/runtimes and input types).
- Use a different JSON library: rejected (unnecessary dependency for this milestone; `System.Text.Json` + explicit canonicalization is sufficient).

### Decision: Fix formatting to a single canonical shape
**Approach**
- Always write indented JSON using a fixed indentation style.
- Always normalize line endings to LF (`\n`) and ensure a trailing newline at EOF.
- Always write UTF-8 without BOM.

**Why**
- Enables byte-for-byte fixture comparison and cross-platform determinism.

### Decision: Atomic write implementation using temp + replace/move
**Approach**
- Ensure parent directory exists.
- Write bytes to a temp file in the same directory as the target.
- If the target exists: atomically replace the target with the temp file (e.g., `File.Replace`).
- If the target does not exist: move temp into place (e.g., `File.Move`).
- Best-effort cleanup temp files on failure.

**Why**
- Prevents partial writes and reduces risk of corrupt artifacts when a process crashes mid-write.

**Alternatives considered**
- Direct overwrite (`WriteAllText`): rejected (can leave partial/corrupt files).
- Write temp then `Move` over existing file: rejected (platform-dependent semantics; explicit replace is clearer for “existing target” behavior).

### Decision: No-churn writes
**Approach**
- Compare existing file bytes with the newly produced canonical bytes.
- If identical, do not rewrite.

**Why**
- Reduces noisy diffs, preserves stable timestamps where useful, and avoids unnecessary IO.

## Risks / Trade-offs
- Canonicalization adds CPU overhead vs naïve `JsonSerializer.Serialize`. For AOS artifact sizes in this milestone, determinism is prioritized over micro-optimizations.
- Care must be taken to ensure escaping behavior is stable and consistent (stick to a single encoder choice for the canonical writer).

## Migration Plan
- Introduce a new canonical JSON writer utility in the engine.
- Update all existing `.aos/**` JSON producers to delegate to that utility.
- Add/extend determinism tests to cover canonical ordering and atomic/no-churn behavior.
