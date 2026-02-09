## ADDED Requirements
### Requirement: Canonical non-ID contract paths are centralized
The system SHALL centralize canonical non-ID contract paths under `.aos/*` alongside ID-based routing so callers do not build paths ad-hoc.

At minimum, the routing source of truth MUST define the workspace lock contract path:
- `.aos/locks/workspace.lock.json`

#### Scenario: Workspace lock contract path is canonical
- **WHEN** the system needs the workspace lock file
- **THEN** it uses the centralized contract path `.aos/locks/workspace.lock.json`

### Requirement: Contract paths are platform-neutral and safe
Canonical contract paths MUST:
- use forward slashes (`/`) as separators (platform-neutral)
- start with `.aos/`
- not contain `.` or `..` path segments

#### Scenario: Contract paths are rejected when they contain backslashes
- **WHEN** a contract path containing `\\` is provided to the path resolver
- **THEN** the contract path is rejected with an actionable error

#### Scenario: Contract paths are rejected when they contain dot segments
- **WHEN** a contract path contains `.` or `..` segments
- **THEN** the contract path is rejected with an actionable error

