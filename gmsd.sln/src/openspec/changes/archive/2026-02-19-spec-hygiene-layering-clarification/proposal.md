# Change: Spec Hygiene + Layering Clarification

## Why
A large portion of the stable specs under `openspec/specs/` still contain mechanically-generated placeholder Purpose text and several spec families (notably `engine-*` vs `aos-*` and `web-*`) have drift that makes the stable spec set read like competing or outdated truth.

## What Changes
- MODIFIED: Normalize placeholder `## Purpose` sections across `openspec/specs/**/spec.md` so each spec reads as durable, long-lived truth.
- MODIFIED: Clarify explicit layering between `engine-*` specs (public DI/interface surface) and `aos-*` specs (canonical behavioral semantics) without renames or merges.
- MODIFIED: Align `web-*` specs with current `Gmsd.Web` routing and `.aos/**` file access behavior; move “target UI” expectations out of stable specs.

## Impact
- Affected specs: all specs with placeholder Purpose stubs; `engine-*` specs; `web-*` specs.
- Affected code: None (spec edits only).
- **BREAKING**: None (spec hygiene / documentation alignment only).
