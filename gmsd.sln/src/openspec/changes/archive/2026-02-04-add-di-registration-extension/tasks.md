## 1. Scaffold Composition Layer
- [x] 1.1 Create `Gmsd.Aos/Composition/ServiceCollectionExtensions.cs` with `AddGmsdAos` extension method
- [x] 1.2 Create `Gmsd.Aos/Configuration/AosOptions.cs` for engine configuration binding

## 2. Service Registrations
- [x] 2.1 Register `IWorkspace` → internal workspace implementation (Singleton)
- [x] 2.2 Register `ISpecStore` → `AosSpecStore` (Singleton)
- [x] 2.3 Register `IStateStore` → `AosStateStore` (Singleton)
- [x] 2.4 Register `IEvidenceStore` → `AosEvidenceStore` (Singleton)
- [x] 2.5 Register `IValidator` → workspace validator (Singleton)
- [x] 2.6 Register `CommandCatalog` (Singleton)
- [x] 2.7 Register `ICommandRouter` → `CommandRouter` (Scoped/Transient)

## 3. Configuration Binding
- [x] 3.1 Define `AosOptions` class with `RepositoryRootPath` property
- [x] 3.2 Add `IOptions<AosOptions>` binding from configuration

## 4. Validation
- [x] 4.1 Write DI registration tests: verify all services can be resolved
- [x] 4.2 Write lifetime tests: verify singletons share state, scoped services are isolated
- [x] 4.3 Verify `Gmsd.Agents` project can call `services.AddGmsdAos()` and resolve all services

## 5. Documentation
- [x] 5.1 Document lifetime conventions in code comments
