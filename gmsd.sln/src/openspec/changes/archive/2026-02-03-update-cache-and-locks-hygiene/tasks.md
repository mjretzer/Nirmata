## 1. Spec changes
- [x] 1.1 Add delta spec for `aos-cache-hygiene` (new capability)
- [x] 1.2 Add delta spec updates for `aos-lock-manager` (lock CLI surface + contention behavior)
- [x] 1.3 Run `openspec validate update-cache-and-locks-hygiene --strict` and fix all issues

## 2. CLI: cache command group
- [x] 2.1 Add `aos cache` command group to `Gmsd.Aos/Composition/Program.cs`
- [x] 2.2 Implement `aos cache clear` (scope-limited deletion under `.aos/cache/**`)
- [x] 2.3 Implement `aos cache prune` (age-based pruning; default `--days 30`)
- [x] 2.4 Ensure cache commands acquire exclusive workspace lock (exit code 4 on contention)

## 3. Engine module (cache hygiene)
- [x] 3.1 Introduce a minimal cache hygiene module (e.g., `Gmsd.Aos/Engine/Cache/**`)
- [x] 3.2 Ensure clear/prune never deletes `.aos/cache/` itself
- [x] 3.3 Ensure prune uses filesystem timestamps consistently (documented behavior)

## 4. Tests
- [x] 4.1 Add CLI tests for `aos cache clear` and `aos cache prune`
- [x] 4.2 Add tests for lock contention on cache commands (second acquisition fails deterministically)
- [x] 4.3 Add tests ensuring cache removal does not break `aos validate workspace`

