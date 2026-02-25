# Change: add-llm-contracts-adapters

## Why
The AOS engine needs a provider-agnostic LLM abstraction layer to enable multi-provider support (OpenAI, Anthropic, Azure OpenAI, Ollama) without coupling the orchestration logic to any single vendor. Currently, there is no standardized LLM contract in Gmsd.Aos, which blocks the planning and execution workflows from leveraging LLM capabilities. Additionally, prompt templates must be stored outside code for versioning and customization.

## What Changes
- **ADDED** Vendor-neutral LLM contracts (`ILlmProvider`, message types, tool-call normalization) in `Gmsd.Aos/Contracts/Llm/`
- **ADDED** Provider adapter scaffolds for OpenAI, Anthropic, Azure OpenAI, and Ollama in `Gmsd.Aos/Adapters/{Provider}/`
- **ADDED** Configuration-driven provider selection via DI registration
- **ADDED** Prompt asset loader and template system in `Gmsd.Aos/Templates/Prompts/` and `Gmsd.Aos/Resources/Prompts/`
- **ADDED** `LlmCallEnvelope` evidence recording for auditable LLM invocations
- **ADDED** Specification `aos-llm-provider-abstraction` documenting the contracts and behaviors

## Impact
- **Affected specs:** New capability `aos-llm-provider-abstraction`
- **Affected code:**
  - `Gmsd.Aos/Contracts/Llm/**` (new)
  - `Gmsd.Aos/Adapters/**` (new)
  - `Gmsd.Aos/Templates/Prompts/**` (new)
  - `Gmsd.Aos/Resources/Prompts/**` (new)
  - `Gmsd.Aos/Contracts/Tools/` (may be extended for tool-call normalization)
- **No breaking changes** — purely additive capability
