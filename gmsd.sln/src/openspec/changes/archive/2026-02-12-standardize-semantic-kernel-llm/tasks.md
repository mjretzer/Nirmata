# Tasks: Standardize on Microsoft Semantic Kernel for LLM Integration

## Phase 1: Adapter Implementation

- [x] **Create SemanticKernelLlmProvider adapter class**
   - File: `Gmsd.Agents/Execution/ControlPlane/Llm/Adapters/SemanticKernelLlmProvider.cs`
   - Implements `ILlmProvider`
   - Injects `IChatCompletionService` via constructor
   - Implement `CompleteAsync()` with translation logic
   - Implement `StreamCompletionAsync()` with streaming translation
   - Handle tool calling via SK's `AutoFunctionInvocationOptions`
   - **Validation:** Unit tests verify translation correctness
   - **Depends on:** None

- [x] **Mark ILlmProvider contract types as obsolete**
   - Files:
     - `Gmsd.Agents/Execution/ControlPlane/Llm/Contracts/ILlmProvider.cs`
     - `Gmsd.Agents/Execution/ControlPlane/Llm/Contracts/LlmCompletionRequest.cs`
     - `Gmsd.Agents/Execution/ControlPlane/Llm/Contracts/LlmCompletionResponse.cs`
     - `Gmsd.Agents/Execution/ControlPlane/Llm/Contracts/LlmMessage.cs`
     - `Gmsd.Agents/Execution/ControlPlane/Llm/Contracts/LlmDelta.cs`
     - `Gmsd.Agents/Execution/ControlPlane/Llm/Contracts/LlmProviderOptions.cs`
     - `Gmsd.Agents/Execution/ControlPlane/Llm/Contracts/LlmToolCall.cs`
     - `Gmsd.Agents/Execution/ControlPlane/Llm/Contracts/LlmToolDefinition.cs`
   - Add `[Obsolete("Use IChatCompletionService directly", false)]` attribute to each type
   - **Validation:** Build produces warnings (not errors); no functional changes
   - **Depends on:** None

- [x] **Remove custom provider adapter implementations**
   - Delete files:
     - `Gmsd.Agents/Execution/ControlPlane/Llm/Adapters/OpenAi/OpenAiLlmAdapter.cs`
     - `Gmsd.Agents/Execution/ControlPlane/Llm/Adapters/Anthropic/AnthropicLlmAdapter.cs`
     - `Gmsd.Agents/Execution/ControlPlane/Llm/Adapters/AzureOpenAi/AzureOpenAiLlmAdapter.cs`
     - `Gmsd.Agents/Execution/ControlPlane/Llm/Adapters/Ollama/OllamaLlmAdapter.cs`
   - Clean up empty adapter directories if needed
   - **Validation:** Solution builds; tests pass
   - **Depends on:** Task 1 (adapter must be ready first)

## Phase 2: DI and Configuration

- [x] **Update AddLlmProvider to use Semantic Kernel**
   - File: `Gmsd.Agents/Configuration/LlmServiceCollectionExtensions.cs`
   - Modify `AddLlmProvider()` to:
     - Call `AddSemanticKernel()` to register SK services
     - Register `SemanticKernelLlmProvider` as `ILlmProvider` implementation
   - Remove individual `AddOpenAiLlm()`, `AddAnthropicLlm()`, etc. extension methods
   - **Validation:** DI tests resolve `ILlmProvider` correctly
   - **Depends on:** Task 1, Task 3

- [x] **Add configuration backward compatibility**
   - File: `Gmsd.Agents/Configuration/LlmServiceCollectionExtensions.cs`
   - In `AddLlmProvider()`, check `Agents:Llm:Provider` first
   - If legacy key found, log warning and map to `Agents:SemanticKernel:Provider`
   - Support both configuration paths for one release cycle
   - **Validation:** Integration tests with both old and new configuration
   - **Depends on:** Task 4

- [x] **Update appsettings.json example**
   - File: `Gmsd.Agents/appsettings.json`
   - Update example configuration to use `Agents:SemanticKernel` section
   - Add comment documenting legacy path deprecation
   - **Validation:** Manual inspection
   - **Depends on:** Task 5

## Phase 3: Testing and Validation

- [x] **Create unit tests for SemanticKernelLlmProvider**
   - File: `tests/Gmsd.Agents.Tests/Execution/ControlPlane/Llm/SemanticKernelLlmProviderTests.cs`
   - Test `CompleteAsync()` translation logic
   - Test `StreamCompletionAsync()` streaming behavior
   - Test error handling and timeout scenarios
   - Mock `IChatCompletionService` for isolation
   - **Validation:** All tests pass
   - **Depends on:** Task 1

- [x] **Update integration tests for DI resolution**
   - File: `tests/Gmsd.Agents.Tests/Configuration/LlmProviderDiTests.cs` (exists)
   - Update tests to verify `SemanticKernelLlmProvider` is resolved
   - Add tests for backward compatibility configuration path
   - **Validation:** All tests pass
   - **Depends on:** Task 4, Task 5

- [x] **Verify end-to-end with fake LLM**
   - Use existing test infrastructure
   - Ensure `LlmChatResponder` works with new adapter
   - Verify evidence capture still functions
   - **Validation:** E2E tests pass
   - **Depends on:** Task 7, Task 8

## Phase 4: Documentation and Cleanup

- [x] **Update spec delta: agents-llm-provider-abstraction**
    - File: `openspec/specs/agents-llm-provider-abstraction/spec.md`
    - Add MODIFIED requirements noting SK backing
    - Mark custom adapter requirements as superseded
    - Cross-reference new `semantic-kernel-integration` spec
    - **Validation:** `openspec validate` passes
    - **Depends on:** None (spec work parallelizable)

- [x] **Validate with openspec --strict**
    - Run `openspec validate standardize-semantic-kernel-llm --strict`
    - Fix any validation errors
    - Ensure spec cross-references are correct
    - **Validation:** Zero validation errors
    - **Depends on:** All spec work complete

## Dependencies Summary

```
Task 1 (Adapter) ──────────────────┐
                                   ├──→ Task 4 (DI) ──→ Task 5 (Config compat) ──→ Task 8 (Tests)
Task 2 (Obsolete attributes) ──────┤
                                   │
Task 3 (Remove adapters) ──────────┘
                                   │
                                   └──→ Task 7 (Unit tests) ──→ Task 9 (E2E)

Task 6 (appsettings) depends on Task 5
Task 10 (Spec update) parallelizable
Task 11 (Validation) final gate
```

## Estimates

- **Phase 1 (Adapter + Obsolete):** 2-3 hours
- **Phase 2 (DI + Config):** 1-2 hours
- **Phase 3 (Testing):** 2-3 hours
- **Phase 4 (Docs + Validation):** 1 hour
- **Total:** ~6-9 hours of focused development
