## 1. Public surface skeleton (`Gmsd.Aos/Public/**`)
- [x] 1.1 Create `Gmsd.Aos/Public/**` folder and `Gmsd.Aos.Public.*` namespaces.
- [x] 1.2 Add public service interfaces: `IWorkspace`, `ISpecStore`, `IStateStore`, `IEvidenceStore`, `IValidator`, `ICommandRouter`.
- [x] 1.3 Add public catalogs stubs under `Gmsd.Aos.Public.Catalogs`: `SchemaIds`, `CommandIds`, `ArtifactKinds`.
- [x] 1.4 Ensure `dotnet build Gmsd.Aos.csproj` succeeds.

## 2. Contracts folder (`Gmsd.Aos/Contracts/**`)
- [x] 2.1 Create `Gmsd.Aos/Contracts/**` with `Gmsd.Aos.Contracts.*` namespaces for stable contract types used by the public surface.
- [x] 2.2 Ensure contracts are dependency-light and do not reference engine internals.

## 3. Internal engine core namespaces (`Gmsd.Aos/Engine/**`, `Gmsd.Aos/_Shared/**`)
- [x] 3.1 Introduce internal namespaces for implementations (`Gmsd.Aos.Engine.*`) and shared internals (`Gmsd.Aos._Shared.*`).
- [x] 3.2 Ensure internal implementations are not exposed via public types/members.

## 4. Enforce “no direct internal reference” rule
- [x] 4.1 Add a build-time gate (test and/or analyzer) that fails if `Gmsd.Aos` public API exposes types in `Gmsd.Aos.Engine.*` or `Gmsd.Aos._Shared.*`.
- [x] 4.2 Add coverage to ensure consumer projects cannot compile against engine internals (at minimum, a regression test project that attempts the forbidden reference and must fail).
- [x] 4.3 Ensure CI runs the gate and fails with actionable error output when violated.

