# Delta: LLM Contracts Architecture Realignment

## Summary
Moved LLM provider contracts, adapters, configuration, and prompt resources from `nirmata.Aos` to `nirmata.Agents` to align with the architectural separation of concerns.

## Changes

### New Locations (nirmata.Agents)
- `nirmata.Agents/Contracts/Llm/**` - All LLM contract types
- `nirmata.Agents/Adapters/**` - Provider adapters (OpenAI, Anthropic, AzureOpenAi, Ollama)
- `nirmata.Agents/Configuration/**` - LLM configuration and DI extensions
- `nirmata.Agents/Resources/Prompts/**` - Prompt templates (when added)
- `nirmata.Agents/Engine/Evidence/LlmCallEnvelope.cs` - Evidence envelope (LLM-agnostic)

### Removed from nirmata.Aos
- `nirmata.Aos/Contracts/Llm/**` (deleted)
- `nirmata.Aos/Adapters/**` (deleted)
- `nirmata.Aos/Configuration/Llm*.cs` (deleted)
- `nirmata.Aos/Engine/Evidence/LlmCallEnvelope.cs` (replaced with LLM-agnostic version in Agents)

### Namespace Changes
| Old | New |
|-----|-----|
| `nirmata.Aos.Contracts.Llm` | `nirmata.Agents.Contracts.Llm` |
| `nirmata.Aos.Adapters` | `nirmata.Agents.Adapters` |
| `nirmata.Aos.Adapters.OpenAi` | `nirmata.Agents.Adapters.OpenAi` |
| `nirmata.Aos.Adapters.Anthropic` | `nirmata.Agents.Adapters.Anthropic` |
| `nirmata.Aos.Adapters.AzureOpenAi` | `nirmata.Agents.Adapters.AzureOpenAi` |
| `nirmata.Aos.Adapters.Ollama` | `nirmata.Agents.Adapters.Ollama` |
| `nirmata.Aos.Configuration` | `nirmata.Agents.Configuration` |

### Configuration Key Changes
| Old | New |
|-----|-----|
| `Aos:Llm:Provider` | `Agents:Llm:Provider` |
| `Aos:Llm:OpenAI:*` | `Agents:Llm:OpenAI:*` |
| `Aos:Llm:Anthropic:*` | `Agents:Llm:Anthropic:*` |
| `Aos:Llm:AzureOpenAI:*` | `Agents:Llm:AzureOpenAI:*` |
| `Aos:Llm:Ollama:*` | `Agents:Llm:Ollama:*` |

### Project Dependencies
- `nirmata.Agents` now references `nirmata.Aos` and `nirmata.Common`
- `nirmata.Aos` no longer contains LLM-related packages (moved to Agents)

## Migration Path

Update using statements:
```csharp
// Before
using nirmata.Aos.Contracts.Llm;
using nirmata.Aos.Adapters;
using nirmata.Aos.Configuration;

// After
using nirmata.Agents.Contracts.Llm;
using nirmata.Agents.Adapters;
using nirmata.Agents.Configuration;
```

Update configuration keys in appsettings.json:
```json
// Before
{
  "Aos": {
    "Llm": {
      "Provider": "openai"
    }
  }
}

// After
{
  "Agents": {
    "Llm": {
      "Provider": "openai"
    }
  }
}
```
