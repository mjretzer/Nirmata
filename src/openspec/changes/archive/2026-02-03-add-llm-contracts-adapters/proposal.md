# Change: add-llm-contracts-adapters

## Why
The AOS engine needs a provider-agnostic LLM abstraction layer to enable multi-provider support (OpenAI, Anthropic, Azure OpenAI, Ollama) without coupling the orchestration logic to any single vendor. Currently, there is no standardized LLM contract in nirmata.Aos, which blocks the planning and execution workflows from leveraging LLM capabilities. Additionally, prompt templates must be stored outside code for versioning and customization.

## What Changes
- **ADDED** Vendor-neutral LLM contracts (`ILlmProvider`, message types, tool-call normalization) in `nirmata.Aos/Contracts/Llm/`
- **ADDED** Provider adapter scaffolds for OpenAI, Anthropic, Azure OpenAI, and Ollama in `nirmata.Aos/Adapters/{Provider}/`
- **ADDED** Configuration-driven provider selection via DI registration
- **ADDED** Prompt asset loader and template system in `nirmata.Aos/Templates/Prompts/` and `nirmata.Aos/Resources/Prompts/`
- **ADDED** `LlmCallEnvelope` evidence recording for auditable LLM invocations
- **ADDED** Specification `aos-llm-provider-abstraction` documenting the contracts and behaviors

## Impact
- **Affected specs:** New capability `aos-llm-provider-abstraction`
- **Affected code:**
  - `nirmata.Aos/Contracts/Llm/**` (new)
  - `nirmata.Aos/Adapters/**` (new)
  - `nirmata.Aos/Templates/Prompts/**` (new)
  - `nirmata.Aos/Resources/Prompts/**` (new)
  - `nirmata.Aos/Contracts/Tools/` (may be extended for tool-call normalization)
- **No breaking changes** — purely additive capability
