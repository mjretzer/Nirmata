## 1. Specification updates
- [x] 1.1 Add delta spec updates for `solution-structure` clarifying bring-up order and boundary intent.
- [x] 1.2 Add delta spec updates for `quality-gates` requiring build-time enforcement of reference boundaries.

## 2. Build-time boundary enforcement
- [x] 2.1 Define a canonical layer classification for all baseline projects (Shared/Engine/Product sublayers/Hosts).
- [x] 2.2 Implement an MSBuild target that fails the build when a project references a forbidden layer/project.
- [x] 2.3 Ensure the error messages clearly identify the referencing project, referenced project, and the violated rule.

## 3. Validation (local + CI)
- [x] 3.1 Add/adjust CI steps to ensure boundary enforcement runs as part of `dotnet build` and fails the pipeline on violations.
- [x] 3.2 Verify locally:
  - `dotnet build "nirmata.slnx"`
  - `dotnet test "nirmata.slnx"`

