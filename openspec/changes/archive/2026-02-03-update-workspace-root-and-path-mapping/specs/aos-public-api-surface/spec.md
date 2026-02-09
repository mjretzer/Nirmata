## MODIFIED Requirements
### Requirement: Public services are expressed as interfaces
The system SHALL provide public service interfaces under `Gmsd.Aos/Public/Services/**` (`Gmsd.Aos.Public.Services.*`) for the engine’s primary subsystems:
- Workspace
- Spec store
- State store
- Evidence store
- Validation
- Command routing

The system SHALL provide a public workspace abstraction in `Gmsd.Aos.Public.*` that allows consumers to:
- obtain the `RepositoryRootPath` and `AosRootPath` for the current workspace
- resolve supported artifact IDs to canonical contract paths under `.aos/*`
- resolve contract paths under `.aos/*` to absolute filesystem paths safely

#### Scenario: Public service contracts exist
- **WHEN** a consumer needs to integrate with the engine
- **THEN** it can compile against the subsystem service interfaces without referencing internal implementations

#### Scenario: Consumer resolves canonical paths using only public APIs
- **WHEN** a consumer needs to resolve an artifact id or contract path to a filesystem path
- **THEN** it can do so using only `Gmsd.Aos.Public.*` APIs without referencing `Gmsd.Aos.Engine.*` namespaces

