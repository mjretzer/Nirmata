# Change: Add engine project skeletons and enforce reference boundaries

## Why
The solution needs an early, provable separation between the **Engine (AOS)** and the **Product Application** so that future milestones can evolve independently without cross-plane coupling.

## What Changes
- Add a build-time enforcement mechanism that fails the build when project references violate the solution dependency direction.
- Validate that Engine libraries compile without any Product dependencies and that Product projects compile in strict layer order.
- Update OpenSpec requirements for solution structure and quality gates to explicitly cover these boundaries.

## Impact
- **Affected specs**: `solution-structure`, `quality-gates`
- **Affected code**: solution-level build tooling (MSBuild targets/props), CI pipeline (`.github/workflows/ci.yml`), and (if needed) project reference adjustments in `.csproj` files

