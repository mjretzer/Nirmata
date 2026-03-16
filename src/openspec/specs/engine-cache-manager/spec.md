# engine-cache-manager Specification

## Purpose

Defines the public DI/interface surface for cache hygiene operations in the AOS engine. Canonical cache semantics and safety constraints are defined by `aos-cache-hygiene`, and this capability MUST conform.

- **Lives in:** `nirmata.Aos/Public/ICacheManager.cs`, `nirmata.Aos/Public/Composition/*`, `.aos/cache/**`
- **Owns:** Public interface shape and DI registration for cache clear/prune operations
- **Does not own:** Policy decisions outside `.aos/cache/**` (enforced by canonical AOS cache contracts)
## Requirements
### Requirement: Cache manager interface exists
The system SHALL define `ICacheManager` as a public interface in `nirmata.Aos/Public/`.

The interface SHALL provide methods to clear and prune the `.aos/cache/` directory.

#### Scenario: Clear removes all cache entries
- **GIVEN** a workspace with files under `.aos/cache/`
- **WHEN** `ICacheManager.Clear()` is called
- **THEN** all files and subdirectories under `.aos/cache/` are removed
- **AND** the `.aos/cache/` directory itself still exists

#### Scenario: Clear returns count of removed entries
- **GIVEN** a workspace with N cache entries
- **WHEN** `ICacheManager.Clear()` is called
- **THEN** the call returns the count of removed entries

#### Scenario: Clear empty cache succeeds
- **GIVEN** a workspace with an empty `.aos/cache/` directory
- **WHEN** `ICacheManager.Clear()` is called
- **THEN** the call succeeds and returns 0

#### Scenario: Prune removes entries older than threshold
- **GIVEN** a workspace with cache entries of various ages
- **WHEN** `ICacheManager.Prune(TimeSpan.FromDays(30))` is called
- **THEN** entries older than 30 days are removed
- **AND** entries 30 days or newer remain

#### Scenario: Prune returns count of removed entries
- **GIVEN** a workspace with old cache entries
- **WHEN** `ICacheManager.Prune(TimeSpan.FromDays(30))` is called
- **THEN** the call returns the count of removed entries

#### Scenario: Prune with zero threshold removes all entries
- **GIVEN** a workspace with cache entries
- **WHEN** `ICacheManager.Prune(TimeSpan.Zero)` is called
- **THEN** all cache entries are removed (equivalent to Clear)

### Requirement: Cache operations only affect cache directory
The interface SHALL ensure cache operations never affect other `.aos/` directories.

#### Scenario: Clear does not affect spec directory
- **GIVEN** a workspace with both `.aos/cache/` and `.aos/spec/` content
- **WHEN** `ICacheManager.Clear()` is called
- **THEN** `.aos/spec/` content is unchanged

#### Scenario: Prune does not affect state directory
- **GIVEN** a workspace with both `.aos/cache/` and `.aos/state/` content
- **WHEN** `ICacheManager.Prune(TimeSpan.FromDays(30))` is called
- **THEN** `.aos/state/` content is unchanged

### Requirement: Service is registered in DI
The system SHALL register `ICacheManager` as a Singleton in `AddnirmataAos()`.

#### Scenario: Plane resolves the service via DI
- **GIVEN** a configured service collection with `AddnirmataAos()` called
- **WHEN** `serviceProvider.GetRequiredService<ICacheManager>()` is called
- **THEN** a non-null implementation is returned

