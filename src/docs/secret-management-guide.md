# Secret Management Guide

## Overview

The nirmata orchestrator uses secure secret management to protect sensitive credentials like API keys. Secrets are stored in the Windows Credential Manager (or equivalent OS keychain on other platforms) rather than in plaintext configuration files.

## Workflow

### 1. Setting Up Secrets

#### Using the CLI

```bash
# Set a new secret
aos secret set openai-api-key "sk-..."

# Set an Azure OpenAI key
aos secret set azure-openai-key "your-azure-key"

# Set an Anthropic API key
aos secret set anthropic-api-key "your-anthropic-key"
```

#### Verifying Secrets

```bash
# List all secrets (names only, values are masked)
aos secret list

# Check if a specific secret exists
aos secret get openai-api-key
# Output: Secret 'openai-api-key' is set (value masked)
```

### 2. Configuring Applications to Use Secrets

In your `.aos/config/llm-providers.json` or application configuration, reference secrets using the `$secret:` syntax:

```json
{
  "nirmataAgents": {
    "SemanticKernel": {
      "Provider": "OpenAi",
      "OpenAi": {
        "ApiKey": "$secret:openai-api-key",
        "ModelId": "gpt-4",
        "Endpoint": "https://api.openai.com/v1"
      }
    }
  }
}
```

**Supported Providers:**
- **OpenAI:** Use `$secret:openai-api-key`
- **Azure OpenAI:** Use `$secret:azure-openai-key`
- **Anthropic:** Use `$secret:anthropic-api-key`
- **Ollama:** No API key required (local deployment)

### 3. Migrating Existing Plaintext Secrets

If you have existing plaintext API keys in configuration files:

```bash
# The system will automatically detect and migrate plaintext keys
# Run the migration utility (built into the initialization process)
aos migrate-secrets

# This will:
# 1. Scan all configuration files for plaintext API keys
# 2. Store them in Windows Credential Manager
# 3. Replace plaintext values with $secret: references
# 4. Log all changes for audit trail
```

### 4. Secret Rotation

#### Rotating a Secret

```bash
# Update the secret in Windows Credential Manager
aos secret set openai-api-key "sk-new-key-value"

# Restart the service to apply the new secret
# (Hot-reload is not supported; restart is required for safety)
net stop nirmataService
net start nirmataService
```

#### Rotation Procedure

1. **Generate new credentials** in your provider's dashboard (OpenAI, Azure, etc.)
2. **Update the secret** using `aos secret set`
3. **Test the new credential** with a test run
4. **Restart the service** to apply changes to all running processes
5. **Revoke the old credential** in your provider's dashboard

### 5. Troubleshooting

#### Secret Not Found Error

```
Error: Secret 'openai-api-key' not found in credential store
```

**Solution:**
1. Verify the secret exists: `aos secret list`
2. Check the secret name matches your configuration (case-sensitive)
3. Set the secret: `aos secret set openai-api-key "your-key"`

#### Permission Denied Error

```
Error: Access denied when accessing credential store
```

**Solution:**
1. Ensure you're running with appropriate permissions
2. On Windows, the process must have access to Windows Credential Manager
3. Check that the nirmata service account has credential store permissions

#### Secret Exposed in Logs

The system automatically masks secret values in logs. If you see a plaintext secret in logs:

1. **Immediately rotate the secret** using the rotation procedure above
2. **Check log files** for exposure scope
3. **Audit access logs** to identify potential compromise

## Security Best Practices

### Do's
- ✅ Store all API keys in the credential store, never in configuration files
- ✅ Rotate secrets regularly (at least quarterly)
- ✅ Use strong, unique API keys from your provider
- ✅ Restrict credential store access to authorized users/services
- ✅ Monitor logs for unauthorized secret access attempts

### Don'ts
- ❌ Never commit plaintext API keys to version control
- ❌ Don't share credential store access across multiple users
- ❌ Don't disable secret validation in configuration
- ❌ Don't log or print secret values (the system prevents this)
- ❌ Don't use the same API key across multiple environments

## Architecture

### Secret Store Abstraction

The system uses an abstraction layer (`ISecretStore`) to support multiple backend implementations:

- **Windows Credential Manager** (production): Secure OS-level storage
- **Mock Store** (testing): In-memory storage for unit tests
- **Future:** Support for HashiCorp Vault, AWS Secrets Manager, etc.

### Configuration Resolution

When the application starts:

1. Configuration files are loaded
2. `SecretConfigurationResolver` scans for `$secret:` references
3. Each reference is resolved from the credential store
4. Resolved values are injected into the configuration (never written to disk)
5. Application uses the resolved configuration

### Logging and Masking

All logging automatically masks secret values:

```
Original: "apiKey": "sk-1234567890abcdef"
Logged:   "apiKey": "sk-****...****" (masked)
```

## Integration with CI/CD

For CI/CD pipelines, secrets can be injected via environment variables:

```bash
# In your CI/CD configuration
export nirmata_SECRET_OPENAI_API_KEY="sk-..."

# The system will automatically load from environment if available
# Priority: Environment Variables > Credential Store > Configuration File
```

## Compliance and Auditing

### Audit Trail

All secret operations are logged:
- Secret creation/update/deletion
- Secret access attempts
- Failed authentication attempts
- Configuration resolution events

### Compliance

This secret management system supports:
- **GDPR:** Secure credential storage with access controls
- **HIPAA:** Encryption at rest (OS responsibility)
- **SOC 2:** Audit logging and access controls
- **PCI DSS:** No plaintext storage of sensitive data

## Advanced Configuration

### Custom Secret Store Implementation

To implement a custom secret store (e.g., HashiCorp Vault):

1. Implement `ISecretStore` interface
2. Register in dependency injection:
   ```csharp
   services.AddScoped<ISecretStore, MyCustomSecretStore>();
   ```
3. Update configuration to use your store

### Per-Environment Secrets

Use different secret names for different environments:

```json
{
  "nirmataAgents": {
    "SemanticKernel": {
      "Provider": "OpenAi",
      "OpenAi": {
        "ApiKey": "$secret:openai-api-key-prod",
        "ModelId": "gpt-4"
      }
    }
  }
}
```

Then set environment-specific secrets:
```bash
aos secret set openai-api-key-prod "sk-prod-key"
aos secret set openai-api-key-dev "sk-dev-key"
```

## Support and Questions

For issues or questions about secret management:

1. Check the troubleshooting section above
2. Review application logs for detailed error messages
3. Contact the nirmata team with:
   - Error message (without secret values)
   - Configuration structure (without secret values)
   - Steps to reproduce
