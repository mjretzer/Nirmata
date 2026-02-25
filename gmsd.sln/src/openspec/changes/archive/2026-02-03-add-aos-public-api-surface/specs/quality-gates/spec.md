# quality-gates Specification (Delta)

## ADDED Requirements
### Requirement: AOS public API boundary is enforced at build time
The solution SHALL fail the build when the `Gmsd.Aos` public API boundary is violated:

- Consumers MUST NOT compile against internal AOS engine namespaces (e.g., `Gmsd.Aos.Engine.*`, `Gmsd.Aos._Shared.*`).
- The `Gmsd.Aos` public surface (`Gmsd.Aos.Public.*`) MUST NOT expose internal engine types through public members.

#### Scenario: Consumer cannot compile against engine internals
- **WHEN** a consumer project attempts to reference a type from `Gmsd.Aos.Engine.*` or `Gmsd.Aos._Shared.*`
- **THEN** the build fails with an actionable error indicating the forbidden dependency

#### Scenario: Public API does not leak internal types
- **WHEN** the `Gmsd.Aos` assembly is built and its public API is evaluated
- **THEN** no public member signature references a type in `Gmsd.Aos.Engine.*` or `Gmsd.Aos._Shared.*`

