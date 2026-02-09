# Change: Add DI Registration Extension (AddGmsdAos)

## Why
Gmsd.Agents (the Plane/orchestration layer) needs to consume Engine services via dependency injection. Currently, there's no centralized way to register all AOS public services. Each consumer must manually wire up services, leading to inconsistent lifetimes and registration patterns.

## What Changes
- Add `ServiceCollectionExtensions.cs` in `Gmsd.Aos/Composition/` with `AddGmsdAos(this IServiceCollection)` extension method
- Register all public engine services with appropriate lifetimes:
  - Singleton: stores (IWorkspace, ISpecStore, IStateStore, IEvidenceStore), catalogs (CommandCatalog), and validator
  - Scoped/Transient: command handlers and router (per-invocation)
- Add `AosOptions` configuration class and bind via `IOptions<AosOptions>`
- Establish deterministic lifetime conventions documented in composition layer

## Impact
- **Affected specs:** `aos-public-api-surface`
- **Affected code:**
  - `Gmsd.Aos/Composition/ServiceCollectionExtensions.cs` (new)
  - `Gmsd.Aos/Configuration/AosOptions.cs` (new)
  - `Gmsd.Aos/Public/Services/**` (interface registrations)
- **Verify:** `Gmsd.Agents` can call `services.AddGmsdAos()` and resolve: `ICommandRouter`, `IWorkspace`, `ISpecStore`, `IStateStore`, `IEvidenceStore`, `IValidator`
