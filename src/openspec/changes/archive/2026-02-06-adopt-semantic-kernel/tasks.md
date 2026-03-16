## 1. Package and Dependency Setup
- [x] 1.1 Add Semantic Kernel packages to `nirmata.Agents.csproj`
  - Add `Microsoft.SemanticKernel` (core)
  - Add `Microsoft.SemanticKernel.Connectors.OpenAI`
  - Add `Microsoft.SemanticKernel.Connectors.AzureOpenAI`
  - Add `Microsoft.SemanticKernel.Connectors.Ollama` (community) or mark for custom implementation
  - Verify `dotnet build nirmata.Agents.csproj` passes

- [x] 1.2 Update `nirmata.Agents/Composition/ServiceCollectionExtensions.cs`
  - Add `using Microsoft.SemanticKernel;`
  - Keep existing `AddnirmataAgents()` method for backward compatibility

## 2. Semantic Kernel Infrastructure
- [x] 2.1 Create `nirmata.Agents/Configuration/SemanticKernelOptions.cs`
  - Define `SemanticKernelOptions` class with `Provider` property
  - Add nested options classes for each provider (OpenAi, AzureOpenAi, Ollama, Anthropic)
  - Include validation attributes for required fields

- [x] 2.2 Create `nirmata.Agents/Configuration/SemanticKernelServiceCollectionExtensions.cs`
  - Implement `AddSemanticKernel(this IServiceCollection, IConfiguration)` extension
  - Implement provider-specific builder methods:
    - `AddOpenAiChatCompletion(IKernelBuilder, IConfiguration)`
    - `AddAzureOpenAiChatCompletion(IKernelBuilder, IConfiguration)`
    - `AddOllamaChatCompletion(IKernelBuilder, IConfiguration)`
    - `AddAnthropicChatCompletion(IKernelBuilder, IConfiguration)` (stub for now)
  - Register `Kernel` as singleton or scoped based on usage patterns
  - Register `IChatCompletionService` as resolved from `Kernel`

- [x] 2.3 Create `nirmata.Agents/Execution/ControlPlane/Llm/Filters/AosEvidenceFunctionFilter.cs`
  - Implement `IFunctionInvocationFilter` interface
  - Inject `IAosEvidenceWriter` and run context
  - Capture pre-invocation: timestamp, provider, model, request summary
  - Capture post-invocation: response summary, token usage, duration, finish reason
  - Write `LlmCallEnvelope` to evidence store
  - Handle both success and failure cases

## 3. Tool System Integration
- [x] 3.1 Create `nirmata.Agents/Execution/ControlPlane/Llm/Tools/ToolToKernelFunctionAdapter.cs`
  - Implement method `KernelFunction FromITool(ITool tool)`
  - Map `ITool` metadata to `KernelFunctionMetadata`
  - Map `ITool.ExecuteAsync` to SK function delegate
  - Handle parameter schema conversion from tool definition to JSON schema

- [x] 3.2 Create `nirmata.Agents/Execution/ControlPlane/Llm/Tools/KernelPluginFactory.cs`
  - Implement `KernelPlugin CreateFromTools(IEnumerable<ITool> tools)`
  - Group tools by category if applicable
  - Register all tools as `KernelFunction` instances in a plugin

- [x] 3.3 Update kernel configuration to register tools
  - Add method to scan and register all `ITool` implementations with the kernel
  - Ensure tools are available for auto-function-calling

## 4. Prompt Template Migration
- [x] 4.1 Create `nirmata.Agents/Execution/ControlPlane/Llm/Prompts/SemanticKernelPromptFactory.cs`
  - Implement `KernelFunction CreateFromEmbeddedResource(string resourceId)`
  - Load embedded resources from `nirmata.Agents.Resources.Prompts`
  - Support both `.prompt.txt` and `.prompt.yaml` extensions
  - Return SK-ready `KernelFunction` from template content

- [x] 4.2 Review and update existing prompt files for SK compatibility
  - Migrated prompt from `nirmata.Aos` to `nirmata.Agents/Resources/Prompts`
  - Updated template syntax from `{{variable}}` to `{{$variable}}` for SK compatibility
  - Updated YAML frontmatter to use SK-supported attributes (`name`, `description`)

## 5. Provider Connector Implementation
- [x] 5.1 Implement or configure OpenAI connector
  - Use `Microsoft.SemanticKernel.Connectors.OpenAI`
  - Map configuration from `Agents:SemanticKernel:OpenAi:*` to `OpenAIPromptExecutionSettings`
  - Test basic chat completion

- [x] 5.2 Implement or configure Azure OpenAI connector
  - Use `Microsoft.SemanticKernel.Connectors.AzureOpenAI`
  - Map configuration from `Agents:SemanticKernel:AzureOpenAi:*`
  - Test with Azure endpoint and key

- [x] 5.3 Implement or configure Ollama connector
  - Using `Microsoft.SemanticKernel.Connectors.Ollama` package (v1.33.0-alpha)
  - Configuration complete in `SemanticKernelOptions.cs` and `SemanticKernelServiceCollectionExtensions.cs`
  - Tested with `dotnet build nirmata.Agents.csproj` - build successful

- [x] 5.4 Implement Anthropic connector (custom)
  - Create `AnthropicChatCompletionService` implementing `IChatCompletionService`
  - Implement `GetChatMessageContentsAsync` with Anthropic API client
  - Map `ChatHistory` to Anthropic message format
  - Handle tool-use/tool-result format conversion
  - Map `ClaudePromptExecutionSettings` for Anthropic-specific options

## 6. Update Roadmap Documentation
- [x] 6.1 Update `PH-ENG-0010` in `openspec/roadmap.md`
  - Change description from "custom provider adapters" to "Semantic Kernel integration"
  - Update code paths target to reflect SK namespaces
  - Update verify steps to mention SK configuration

## 7. Testing and Validation
- [x] 7.1 Create unit tests for `SemanticKernelServiceCollectionExtensions`
  - Test configuration binding for each provider
  - Test exception on missing provider configuration
  - Test service registration in DI container

- [x] 7.2 Create unit tests for `AosEvidenceFunctionFilter`
  - Mock `IFunctionInvocationContext` for pre/post invocation
  - Verify `LlmCallEnvelope` is written with correct data
  - Verify evidence file is created at expected path

- [x] 7.3 Create integration tests for provider connectors
  - Test OpenAI connector (with fake/mocked client)
  - Test Azure OpenAI connector configuration
  - Test tool invocation with auto-function-calling

- [x] 7.4 Validate with `openspec validate adopt-semantic-kernel --strict`
  - Fix any validation errors
  - Ensure spec delta format is correct

## 8. Migration Verification
- [x] 8.1 Verify no compile-time errors in `nirmata.Agents`
  - Run `dotnet build nirmata.Agents.csproj`
  - Run `dotnet build nirmata.slnx`

- [x] 8.2 Update workflow files (separate follow-up proposals)
  - Create migration tasks for `NewProjectInterviewer`
  - Create migration tasks for `PhasePlanner` workflows
  - Mark as dependent on this proposal completion
