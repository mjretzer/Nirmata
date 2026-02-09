# Delta: LLM Contracts Architecture Realignment

## Summary
Moved LLM provider contracts, adapters, configuration, and prompt resources from `Gmsd.Aos` to `Gmsd.Agents` to align with the architectural separation of concerns.

## Changes

### New Locations (Gmsd.Agents)
- `Gmsd.Agents/Contracts/Llm/**` - All LLM contract types
- `Gmsd.Agents/Adapters/**` - Provider adapters (OpenAI, Anthropic, AzureOpenAi, Ollama)
- `Gmsd.Agents/Configuration/**` - LLM configuration and DI extensions
- `Gmsd.Agents/Resources/Prompts/**` - Prompt templates (when added)
- `Gmsd.Agents/Engine/Evidence/LlmCallEnvelope.cs` - Evidence envelope (LLM-agnostic)

### Removed from Gmsd.Aos
- `Gmsd.Aos/Contracts/Llm/**` (deleted)
- `Gmsd.Aos/Adapters/**` (deleted)
- `Gmsd.Aos/Configuration/Llm*.cs` (deleted)
- `Gmsd.Aos/Engine/Evidence/LlmCallEnvelope.cs` (replaced with LLM-agnostic version in Agents)

### Namespace Changes
| Old | New |
|-----|-----|
| `Gmsd.Aos.Contracts.Llm` | `Gmsd.Agents.Contracts.Llm` |
| `Gmsd.Aos.Adapters` | `Gmsd.Agents.Adapters` |
| `Gmsd.Aos.Adapters.OpenAi` | `Gmsd.Agents.Adapters.OpenAi` |
| `Gmsd.Aos.Adapters.Anthropic` | `Gmsd.Agents.Adapters.Anthropic` |
| `Gmsd.Aos.Adapters.AzureOpenAi` | `Gmsd.Agents.Adapters.AzureOpenAi` |
| `Gmsd.Aos.Adapters.Ollama` | `Gmsd.Agents.Adapters.Ollama` |
| `Gmsd.Aos.Configuration` | `Gmsd.Agents.Configuration` |

### Configuration Key Changes
| Old | New |
|-----|-----|
| `Aos:Llm:Provider` | `Agents:Llm:Provider` |
| `Aos:Llm:OpenAI:*` | `Agents:Llm:OpenAI:*` |
| `Aos:Llm:Anthropic:*` | `Agents:Llm:Anthropic:*` |
| `Aos:Llm:AzureOpenAI:*` | `Agents:Llm:AzureOpenAI:*` |
| `Aos:Llm:Ollama:*` | `Agents:Llm:Ollama:*` |

### Project Dependencies
- `Gmsd.Agents` now references `Gmsd.Aos` and `Gmsd.Common`
- `Gmsd.Aos` no longer contains LLM-related packages (moved to Agents)

## Migration Path

Update using statements:
```csharp
// Before
using Gmsd.Aos.Contracts.Llm;
using Gmsd.Aos.Adapters;
using Gmsd.Aos.Configuration;

// After
using Gmsd.Agents.Contracts.Llm;
using Gmsd.Agents.Adapters;
using Gmsd.Agents.Configuration;
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
