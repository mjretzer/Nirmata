# secret-management Specification

## Purpose
TBD - created by archiving change harden-orchestrator-governance. Update Purpose after archive.
## Requirements
### Requirement: Secret store abstraction
The system SHALL provide `ISecretStore` abstraction for credential management.

The interface MUST support:
- `SetSecret(name: string, value: string)` - Store a secret
- `GetSecret(name: string)` - Retrieve a secret
- `DeleteSecret(name: string)` - Remove a secret
- `ListSecrets()` - Enumerate secret names (not values)
- `SecretExists(name: string)` - Check if secret exists

Implementations MUST:
- Never log secret values
- Support both synchronous and asynchronous operations
- Provide clear error messages for missing/invalid secrets
- Be testable with mock implementations

#### Scenario: Secret is stored and retrieved
- **GIVEN** a secret store implementation
- **WHEN** `SetSecret("openai-key", "sk-...")` is called
- **THEN** `GetSecret("openai-key")` returns the stored value
- **AND** the value is not logged or exposed in error messages

#### Scenario: Missing secret returns clear error
- **GIVEN** a secret store
- **WHEN** `GetSecret("nonexistent")` is called
- **THEN** an error is raised indicating secret not found
- **AND** error message does not suggest the value

#### Scenario: Secret names are listed without values
- **GIVEN** secrets "openai-key" and "anthropic-key" stored
- **WHEN** `ListSecrets()` is called
- **THEN** result includes ["openai-key", "anthropic-key"]
- **AND** no secret values are returned

### Requirement: OS keychain backend
The system SHALL implement `ISecretStore` using OS keychain (Windows Credential Manager, macOS Keychain, Linux Secret Service).

The implementation MUST:
- Use platform-native APIs for secure storage
- Encrypt secrets at rest (OS responsibility)
- Prevent access from other processes (OS responsibility)
- Support secret deletion
- Provide clear errors for permission issues

#### Scenario: Secret is stored in OS keychain
- **GIVEN** Windows Credential Manager is available
- **WHEN** `SetSecret("openai-key", "sk-...")` is called
- **THEN** the secret is stored in Credential Manager
- **AND** it is retrievable via `GetSecret("openai-key")`

#### Scenario: Secret is encrypted at rest
- **GIVEN** a secret stored in OS keychain
- **WHEN** the keychain file is inspected
- **THEN** the secret value is encrypted (not plaintext)
- **AND** only the OS can decrypt it

#### Scenario: Permission error is clear
- **GIVEN** insufficient permissions to access keychain
- **WHEN** `GetSecret(...)` is called
- **THEN** an error is raised indicating permission denied
- **AND** error message suggests checking OS permissions

### Requirement: Configuration references secrets by name
The system SHALL support secret references in configuration files.

Configuration syntax MUST be:
- `$secret:<name>` to reference a secret by name
- Example: `"apiKey": "$secret:openai-key"`

The system MUST:
- Resolve `$secret:` references at configuration load time
- Replace references with actual secret values in memory
- Never write resolved values to disk
- Fail clearly if referenced secret does not exist

#### Scenario: Configuration references secret
- **GIVEN** `.aos/config/providers.json` contains `"apiKey": "$secret:openai-key"`
- **WHEN** configuration is loaded
- **THEN** the `apiKey` field is populated with the actual secret value
- **AND** the resolved value is not written back to the config file

#### Scenario: Missing referenced secret fails at load time
- **GIVEN** configuration references `$secret:missing-key`
- **WHEN** configuration is loaded
- **THEN** an error is raised indicating secret "missing-key" not found
- **AND** configuration load fails (does not proceed with null/empty value)

#### Scenario: Secret references are not logged
- **GIVEN** configuration with secret references
- **WHEN** configuration is logged for debugging
- **THEN** secret references are shown as `$secret:openai-key` (not resolved)
- **AND** actual secret values are never logged

### Requirement: Secret management CLI commands
The system SHALL provide CLI commands to manage secrets.

Commands MUST include:
- `aos secret set <name> <value>` - Store a secret
- `aos secret get <name>` - Retrieve a secret (output masked)
- `aos secret list` - Enumerate secret names
- `aos secret delete <name>` - Remove a secret

All commands MUST:
- Require explicit confirmation for destructive operations
- Never output secret values in plaintext
- Provide clear success/failure messages
- Be usable in scripts (with `--yes` flag for automation)

#### Scenario: Secret is set via CLI
- **GIVEN** the CLI is available
- **WHEN** `aos secret set openai-key sk-...` is executed
- **THEN** the secret is stored in the keychain
- **AND** the command returns success message

#### Scenario: Secret is retrieved with masked output
- **GIVEN** a secret "openai-key" is stored
- **WHEN** `aos secret get openai-key` is executed
- **THEN** output shows `openai-key: ****...` (masked)
- **AND** the actual value is not displayed

#### Scenario: Secret list shows names only
- **GIVEN** secrets "openai-key" and "anthropic-key" stored
- **WHEN** `aos secret list` is executed
- **THEN** output shows:
  ```
  openai-key
  anthropic-key
  ```
- **AND** no values are shown

#### Scenario: Secret deletion requires confirmation
- **GIVEN** a secret "openai-key" stored
- **WHEN** `aos secret delete openai-key` is executed without `--yes`
- **THEN** the command prompts for confirmation
- **AND** deletion only proceeds if confirmed

### Requirement: Secret rotation support
The system SHALL support secret rotation without service restart (for configuration reload).

Rotation workflow MUST:
1. Update secret in keychain via `aos secret set <name> <new-value>`
2. Optionally restart service to reload configuration
3. Old secret remains available until explicitly deleted

The system MUST:
- Support multiple secret versions (old and new) during transition
- Provide clear guidance on rotation procedure
- Log rotation events (name, timestamp, not value)

#### Scenario: Secret is rotated
- **GIVEN** old secret "openai-key" is stored
- **WHEN** `aos secret set openai-key <new-value>` is executed
- **THEN** the secret is updated in keychain
- **AND** `GetSecret("openai-key")` returns the new value
- **AND** the old value is no longer accessible

#### Scenario: Rotation is logged
- **GIVEN** a secret is rotated
- **WHEN** rotation occurs
- **THEN** service logs include entry: "Secret rotated: openai-key at 2026-02-20T10:30:00Z"
- **AND** log does not include old or new values

#### Scenario: Documentation guides rotation procedure
- **GIVEN** a user needs to rotate secrets
- **WHEN** they consult documentation
- **THEN** they find clear steps: set new secret, restart service, delete old secret

### Requirement: Plaintext secret migration
The system SHALL provide tooling to migrate plaintext secrets to keychain.

Migration MUST:
- Scan configuration files for plaintext secrets (API keys, tokens)
- Prompt user to migrate each secret
- Store migrated secrets in keychain
- Update configuration to use `$secret:` references
- Preserve backup of original configuration

#### Scenario: Migration tool identifies plaintext secrets
- **GIVEN** configuration contains `"apiKey": "sk-..."`
- **WHEN** migration tool runs
- **THEN** tool identifies the plaintext secret
- **AND** prompts user to migrate it

#### Scenario: Migration updates configuration
- **GIVEN** user confirms migration of plaintext secret
- **WHEN** migration completes
- **THEN** configuration is updated to `"apiKey": "$secret:openai-key"`
- **AND** secret is stored in keychain
- **AND** original configuration is backed up

#### Scenario: Migration is safe and reversible
- **GIVEN** migration is in progress
- **WHEN** migration fails or is interrupted
- **THEN** original configuration is preserved
- **AND** user can retry or rollback safely

### Requirement: Secret store test/mock implementation
The system SHALL provide in-memory mock implementation for testing.

Mock implementation MUST:
- Support all `ISecretStore` operations
- Store secrets in memory (not persisted)
- Be usable in unit tests without OS keychain
- Provide methods to inspect stored secrets (for testing only)

#### Scenario: Mock secret store is used in tests
- **GIVEN** unit tests for LLM provider
- **WHEN** tests run
- **THEN** mock secret store is used (not OS keychain)
- **AND** tests can set/get secrets without external dependencies

#### Scenario: Mock store is isolated per test
- **GIVEN** multiple unit tests using mock secret store
- **WHEN** tests run in parallel
- **THEN** each test has isolated secrets
- **AND** tests do not interfere with each other

