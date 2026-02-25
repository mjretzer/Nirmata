# Gmsd.Agents

Agent orchestration and execution layer for the GMSD platform. Implements workflow management, subagent coordination, task execution, and tool calling protocols.

## Overview

Gmsd.Agents provides the execution engine for GMSD workflows:

- **Workflow Orchestration** - Phase-based workflow management with gates and dispatchers
- **Subagent Management** - Multi-agent coordination with context isolation
- **Task Execution** - Atomic task execution with evidence capture
- **Tool Calling Protocol** - Multi-step tool calling conversation loops
- **Event System** - Observable execution with typed events
- **Evidence Capture** - Comprehensive audit trail for all operations

## Architecture

### Core Components

```
Gmsd.Agents/
├── Configuration/        # Service registration and options
├── Execution/          # Workflow and execution implementations
│   ├── Backlog/        # Task backlog management
│   ├── Brownfield/     # Brownfield analysis workflows
│   ├── Context/        # Context pack management
│   ├── ControlPlane/   # Orchestrator and dispatchers
│   │   ├── Tools/      # Standard tools (FileRead, FileWrite, etc.)
│   ├── Continuity/     # State persistence
│   ├── Planning/       # Roadmap and planning workflows
│   ├── Preflight/      # Pre-execution validation
│   ├── SubagentRuns/   # Subagent orchestration
│   ├── TaskExecution/  # Task executor
│   └── ToolCalling/    # Tool calling protocol
├── Models/             # Data contracts and runtime models
├── Observability/      # Logging, metrics, and tracing
├── Persistence/        # State and evidence storage
├── Public/             # Public API surface
└── Workers/            # Background workers
```

## Subagent Orchestration

The Subagent Orchestrator manage the execution of specialized subagents with:
- **Fresh Context Isolation** - Each subagent runs in a unique working directory with its own set of context pack files.
- **Budget Enforcement** - Strict limits on iterations, tool calls, tokens, and execution time.
- **Standard Tool Set** - Built-in tools for file operations (scoped), process execution, and git management.
- **Evidence Tracking** - Automatic capture of tool calls, file diffs (via git), and execution summaries.

### Standard Subagent Tools

- `standard.file_read` - Scoped file reading.
- `standard.file_write` - Scoped file writing.
- `standard.process_runner` - Execution of shell commands (e.g., tests, build).
- `standard.git` - Git operations (status, commit) for workflow tracking.

## Tool Calling Protocol

The Tool Calling Protocol provides a reusable, observable, and evidence-capturing implementation of the multi-step tool calling conversation loop.

### The 5-Step Protocol

```
┌─────────────────────────────────────────────────────────────────┐
│                     TOOL CALLING LOOP                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Step 1: Send conversation to LLM with available tools          │
│       ↓                                                         │
│  Step 2: Check if LLM requested tool calls                       │
│       ↓                                                         │
│  Step 3: Execute tool calls (parallel if multiple)              │
│       ↓                                                         │
│  Step 4: Collect results from all tool executions               │
│       ↓                                                         │
│  Step 5: Send tool results back to LLM                          │
│       ↓                                                         │
│  (Loop repeats from Step 1 if LLM requests more tools)          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Key Features

- **Multi-turn conversations** - LLM can request additional tool calls after receiving results
- **Parallel execution** - Multiple tool calls in a single turn execute concurrently
- **Budget controls** - Enforced limits on iterations, timeout, and token usage
- **Event-driven observability** - Typed events emitted for each state transition
- **Evidence capture** - Complete conversation history persisted for audit

### Core Interfaces

```csharp
/// <summary>
/// Entry point for executing tool calling conversations.
/// </summary>
public interface IToolCallingLoop
{
    /// <summary>
    /// Executes a tool calling conversation loop.
    /// </summary>
    Task<ToolCallingResult> ExecuteAsync(
        ToolCallingRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional event emitter for tool calling observability.
/// </summary>
public interface IToolCallingEventEmitter
{
    void Emit(ToolCallingEvent @event);
}
```

### Request/Result Models

```csharp
// Build a tool calling request
var request = new ToolCallingRequest
{
    Messages = new List<ToolCallingMessage>
    {
        ToolCallingMessage.System("You are a helpful assistant with access to tools."),
        ToolCallingMessage.User("What files are in the workspace?")
    },
    Tools = new List<ToolCallingToolDefinition>
    {
        new ToolCallingToolDefinition
        {
            Name = "list_files",
            Description = "Lists files in a directory",
            ParametersSchema = new
            {
                type = "object",
                properties = new
                {
                    path = new { type = "string", description = "Directory path" }
                },
                required = new[] { "path" }
            }
        }
    },
    Options = new ToolCallingOptions
    {
        MaxIterations = 10,
        Timeout = TimeSpan.FromMinutes(5),
        EnableParallelToolExecution = true,
        MaxParallelToolExecutions = 32
    }
};

// Execute the loop
var result = await toolCallingLoop.ExecuteAsync(request);

// Inspect the result
Console.WriteLine($"Final response: {result.FinalMessage.Content}");
Console.WriteLine($"Iterations: {result.IterationCount}");
Console.WriteLine($"Completion reason: {result.CompletionReason}");
Console.WriteLine($"Total tokens: {result.Usage?.TotalTokens}");
```

## Code Examples

### Example 1: Simple Calculator Tool Calling Loop

This example demonstrates a basic calculator tool with add, subtract, multiply, and divide operations.

```csharp
using Gmsd.Agents.Execution.ToolCalling;
using Gmsd.Agents.Execution.ControlPlane.Tools.Registry;
using Microsoft.SemanticKernel;

// Step 1: Define the calculator tool
public class CalculatorTool : ITool
{
    public string Name => "calculator";
    public string Description => "Performs basic arithmetic operations";

    public Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken ct)
    {
        var operation = request.Parameters["operation"].ToString();
        var a = Convert.ToDouble(request.Parameters["a"]);
        var b = Convert.ToDouble(request.Parameters["b"]);

        double result = operation switch
        {
            "add" => a + b,
            "subtract" => a - b,
            "multiply" => a * b,
            "divide" => b != 0 ? a / b : throw new DivideByZeroException(),
            _ => throw new ArgumentException($"Unknown operation: {operation}")
        };

        return Task.FromResult(ToolResult.Success(new { result }));
    }
}

// Step 2: Register the tool
var toolRegistry = new ToolRegistry();
toolRegistry.Register(new CalculatorTool());

// Step 3: Configure the tool calling loop
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
var toolCallingLoop = new ToolCallingLoop(chatCompletionService, toolRegistry);

// Step 4: Define the tool schema for the LLM
var calculatorToolDef = new ToolCallingToolDefinition
{
    Name = "calculator",
    Description = "Performs basic arithmetic operations: add, subtract, multiply, divide",
    ParametersSchema = new
    {
        type = "object",
        properties = new
        {
            operation = new
            {
                type = "string",
                enum = new[] { "add", "subtract", "multiply", "divide" },
                description = "The arithmetic operation to perform"
            },
            a = new { type = "number", description = "First operand" },
            b = new { type = "number", description = "Second operand" }
        },
        required = new[] { "operation", "a", "b" }
    }
};

// Step 5: Execute a calculation conversation
var request = new ToolCallingRequest
{
    Messages = new List<ToolCallingMessage>
    {
        ToolCallingMessage.System(@"You are a calculator assistant. When asked to perform calculations,
            use the calculator tool to compute results accurately."),
        ToolCallingMessage.User("Calculate (15 * 7) + (42 / 6)")
    },
    Tools = new List<ToolCallingToolDefinition> { calculatorToolDef },
    Options = new ToolCallingOptions
    {
        MaxIterations = 5,  // Limit iterations for simple calculations
        EnableParallelToolExecution = true
    }
};

// Step 6: Execute and get results
var result = await toolCallingLoop.ExecuteAsync(request);

// Output: The assistant will use the calculator tool twice:
// - First call: multiply(15, 7) = 105
// - Second call: divide(42, 6) = 7
// - Then: add(105, 7) = 112
// Final response will explain the calculation and result

Console.WriteLine($"Answer: {result.FinalMessage.Content}");
Console.WriteLine($"Completed in {result.IterationCount} iterations");
```

### Example 2: File System Operations with Loop

This example shows how to use file system tools within a tool calling loop, including reading, writing, and listing files.

```csharp
using Gmsd.Agents.Execution.ToolCalling;
using Gmsd.Agents.Execution.ControlPlane.Tools.Registry;
using Microsoft.SemanticKernel;

// Step 1: Define file system tools
public class FileSystemTools : ITool
{
    private readonly string _basePath;

    public FileSystemTools(string basePath) => _basePath = basePath;

    public string Name => "file_system";
    public string Description => "File system operations: read, write, list, delete";

    public async Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken ct)
    {
        var operation = request.Parameters["operation"].ToString();
        var path = Path.Combine(_basePath, request.Parameters["path"].ToString()!);

        // Security: Ensure path is within base directory
        if (!path.StartsWith(_basePath))
            return ToolResult.Failure("SecurityError", "Path traversal detected");

        try
        {
            return operation switch
            {
                "read" => ToolResult.Success(new
                {
                    content = await File.ReadAllTextAsync(path, ct),
                    exists = File.Exists(path)
                }),

                "write" =>
                    await WriteFileAsync(path, request.Parameters["content"].ToString()!, ct),

                "list" => ToolResult.Success(new
                {
                    files = Directory.GetFiles(path).Select(f => Path.GetFileName(f)),
                    directories = Directory.GetDirectories(path).Select(d => Path.GetFileName(d))
                }),

                "delete" =>
                    await DeleteFileAsync(path, ct),

                _ => ToolResult.Failure("InvalidOperation", $"Unknown operation: {operation}")
            };
        }
        catch (Exception ex)
        {
            return ToolResult.Failure("FileSystemError", ex.Message);
        }
    }

    private async Task<ToolResult> WriteFileAsync(string path, string content, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(path, content, ct);
        return ToolResult.Success(new { path, bytesWritten = content.Length });
    }

    private Task<ToolResult> DeleteFileAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return Task.FromResult(ToolResult.Failure("FileNotFound", "File does not exist"));

        File.Delete(path);
        return Task.FromResult(ToolResult.Success(new { deleted = true, path }));
    }
}

// Step 2: Set up the tool registry with file system tools
var toolRegistry = new ToolRegistry();
toolRegistry.Register(new FileSystemTools("/workspace"));

// Step 3: Define tool schemas for the LLM
var fileSystemToolDef = new ToolCallingToolDefinition
{
    Name = "file_system",
    Description = "Performs file system operations including read, write, list, and delete",
    ParametersSchema = new
    {
        type = "object",
        properties = new
        {
            operation = new
            {
                type = "string",
                enum = new[] { "read", "write", "list", "delete" },
                description = "The file operation to perform"
            },
            path = new
            {
                type = "string",
                description = "File or directory path (relative to workspace root)"
            },
            content = new
            {
                type = "string",
                description = "Content to write (required for write operation)"
            }
        },
        required = new[] { "operation", "path" }
    }
};

// Step 4: Create an event emitter for monitoring
public class ConsoleEventEmitter : IToolCallingEventEmitter
{
    public void Emit(ToolCallingEvent @event)
    {
        switch (@event)
        {
            case ToolCallDetectedEvent detected:
                Console.WriteLine($"[{detected.Iteration}] LLM requested {detected.ToolCalls.Count} tool(s)");
                foreach (var tc in detected.ToolCalls)
                    Console.WriteLine($"  → {tc.ToolName}({tc.ArgumentsJson})");
                break;

            case ToolCallStartedEvent started:
                Console.WriteLine($"[{started.Iteration}] Starting {started.ToolName}...");
                break;

            case ToolCallCompletedEvent completed:
                Console.WriteLine($"[{completed.Iteration}] ✓ {completed.ToolName} completed in {completed.Duration.TotalMilliseconds}ms");
                break;

            case ToolCallFailedEvent failed:
                Console.WriteLine($"[{failed.Iteration}] ✗ {failed.ToolName} failed: {failed.ErrorMessage}");
                break;

            case ToolLoopCompletedEvent loopCompleted:
                Console.WriteLine($"Loop completed: {loopCompleted.CompletionReason} " +
                    $"({loopCompleted.TotalIterations} iterations, {loopCompleted.TotalToolCalls} tool calls)");
                break;
        }
    }
}

// Step 5: Execute a file-based workflow
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
var eventEmitter = new ConsoleEventEmitter();
var toolCallingLoop = new ToolCallingLoop(chatCompletionService, toolRegistry, eventEmitter);

var request = new ToolCallingRequest
{
    Messages = new List<ToolCallingMessage>
    {
        ToolCallingMessage.System(@"You are a file management assistant. Help users organize
            and manipulate files in their workspace. Always confirm destructive operations."),
        ToolCallingMessage.User(@"Please:
            1. List all files in the root directory
            2. Create a new file called 'summary.txt' with a list of those files
            3. Read back the contents to confirm it was written correctly")
    },
    Tools = new List<ToolCallingToolDefinition> { fileSystemToolDef },
    Options = new ToolCallingOptions
    {
        MaxIterations = 10,
        Timeout = TimeSpan.FromMinutes(2),
        EnableParallelToolExecution = false,  // Sequential for this workflow
        Context = new Dictionary<string, object?>
        {
            ["workspace"] = "/workspace",
            ["user"] = "developer"
        }
    },
    CorrelationId = Guid.NewGuid().ToString("N")
};

var result = await toolCallingLoop.ExecuteAsync(request);

// Expected execution flow:
// 1. LLM calls file_system/list with path="."
// 2. Gets file list, calls file_system/write to create summary.txt
// 3. Calls file_system/read to verify the file
// 4. Returns final response confirming success

Console.WriteLine($"\nFinal Response:\n{result.FinalMessage.Content}");
Console.WriteLine($"\nTotal iterations: {result.IterationCount}");
Console.WriteLine($"Total duration: {result.Metadata["DurationMs"]}ms");
```

## Tool Calling Events

The protocol emits typed events for observability and frontend integration:

| Event | Description | When Emitted |
|-------|-------------|--------------|
| `ToolCallDetected` | LLM requested tool calls | After LLM response with tool calls |
| `ToolCallStarted` | Tool execution began | Before invoking a tool |
| `ToolCallCompleted` | Tool execution succeeded | After successful tool execution |
| `ToolCallFailed` | Tool execution failed | When tool throws or returns error |
| `ToolResultsSubmitted` | Results sent to LLM | After all tools executed in a turn |
| `ToolLoopIterationCompleted` | One full iteration done | After each LLM call + tool execution cycle |
| `ToolLoopCompleted` | Loop finished normally | When conversation completes or hits limits |
| `ToolLoopFailed` | Loop encountered error | On unrecoverable errors |

### Event Handling Example

```csharp
public class MetricsEventEmitter : IToolCallingEventEmitter
{
    private readonly IMetrics _metrics;

    public void Emit(ToolCallingEvent @event)
    {
        switch (@event)
        {
            case ToolCallCompletedEvent completed:
                _metrics.RecordToolDuration(
                    completed.ToolName,
                    completed.Duration.TotalMilliseconds);
                break;

            case ToolLoopCompletedEvent loopCompleted:
                _metrics.RecordLoopMetrics(
                    loopCompleted.TotalIterations,
                    loopCompleted.TotalToolCalls,
                    loopCompleted.TotalDuration.TotalMilliseconds);
                break;

            case ToolCallFailedEvent failed:
                _metrics.RecordToolFailure(
                    failed.ToolName,
                    failed.ErrorCode);
                break;
        }
    }
}
```

## Structured Output Schemas

Planners can enforce strict JSON schema validation on LLM responses to ensure reliable, parseable artifacts.

### Defining a Structured Output Schema

```csharp
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

// Define your schema as JSON
var schemaJson = """
{
    "type": "object",
    "properties": {
        "fixes": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "issueId": { "type": "string" },
                    "description": { "type": "string" },
                    "proposedChanges": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "file": { "type": "string" },
                                "changeDescription": { "type": "string" }
                            },
                            "required": ["file", "changeDescription"],
                            "additionalProperties": false
                        }
                    }
                },
                "required": ["issueId", "description", "proposedChanges"],
                "additionalProperties": false
            }
        }
    },
    "required": ["fixes"],
    "additionalProperties": false
}
""";

// Create a schema instance
var schema = LlmStructuredOutputSchema.FromJson(
    name: "fix_plan_v1",
    schemaJson: schemaJson,
    description: "Schema for fix planning output",
    strictValidation: true);
```

### Using Schema in Planner Workflows

```csharp
// Create a completion request with the schema
var request = new LlmCompletionRequest
{
    Messages = new[]
    {
        LlmMessage.System("You are a fix planning assistant..."),
        LlmMessage.User("Generate a fix plan for the following issues...")
    },
    StructuredOutputSchema = schema,
    Options = new LlmProviderOptions
    {
        Temperature = 0.1f,
        MaxTokens = 4000
    }
};

// Execute the request
var response = await llmProvider.CompleteAsync(request);

// The response is guaranteed to match the schema
var fixPlan = JsonSerializer.Deserialize<FixPlan>(response.Message.Content);
```

### Schema Validation Features

- **Strict Validation**: When `strictValidation: true`, responses are validated against the schema before returning
- **Schema Caching**: Compiled schemas are cached by name for performance (target < 50ms per validation)
- **Clear Error Messages**: Validation failures include specific field locations and error descriptions
- **additionalProperties: false**: Prevents LLM from adding unexpected fields

### Troubleshooting Validation Failures

| Error | Cause | Solution |
|-------|-------|----------|
| "empty content" | LLM returned empty response | Increase max_tokens, adjust prompt |
| "not valid JSON" | Response is not valid JSON | Add JSON format requirement to prompt |
| "failed schema validation" | Response doesn't match schema | Review schema constraints, adjust prompt |
| "required property missing" | Missing required field | Add field to prompt instructions |
| "additional properties not allowed" | Extra fields in response | Use `additionalProperties: false` in schema |

### Performance Considerations

- Schema compilation is cached by schema name
- Validation typically completes in < 50ms
- Cache hit rate should exceed 90% for repeated schemas
- For high-throughput scenarios, consider disabling strict validation for non-critical workflows

## Configuration

### Service Registration

```csharp
// Program.cs
builder.Services.AddGmsdAgents(builder.Configuration);
```

### Semantic Kernel LLM Provider Configuration

The Semantic Kernel integration provides a unified abstraction for multiple LLM providers. Configuration is validated at startup with clear error messages for missing or invalid settings.

#### Supported Providers

- **OpenAI** - GPT-4, GPT-4 Turbo, GPT-3.5-Turbo
- **Azure OpenAI** - Enterprise deployments on Azure
- **Ollama** - Local models (Llama, Mistral, CodeLlama)
- **Anthropic** - Claude models

#### OpenAI Configuration

```json
{
  "GmsdAgents": {
    "SemanticKernel": {
      "Provider": "OpenAi",
      "OpenAi": {
        "ApiKey": "sk-...",
        "ModelId": "gpt-4",
        "Temperature": 0.7,
        "MaxTokens": 4096,
        "TopP": 0.9,
        "FrequencyPenalty": 0.0,
        "PresencePenalty": 0.0,
        "Seed": null,
        "EnableParallelToolCalls": true,
        "OrganizationId": null,
        "BaseUrl": null
      }
    }
  }
}
```

**Required Fields:**
- `ApiKey` - Your OpenAI API key (obtain from https://platform.openai.com/account/api-keys)
- `ModelId` - Model to use (gpt-4, gpt-4-turbo, gpt-4o, gpt-3.5-turbo)

**Optional Fields:**
- `Temperature` - Sampling temperature (0.0-2.0, default: 1.0)
- `MaxTokens` - Maximum tokens to generate (default: 2048)
- `TopP` - Nucleus sampling (0.0-1.0, default: 1.0)
- `FrequencyPenalty` - Penalty for repeating tokens (-2.0 to 2.0, default: 0.0)
- `PresencePenalty` - Penalty for new tokens (-2.0 to 2.0, default: 0.0)
- `Seed` - Deterministic sampling seed (optional)
- `EnableParallelToolCalls` - Allow parallel tool execution (default: true)
- `OrganizationId` - Enterprise organization ID (optional)
- `BaseUrl` - Custom endpoint URL for proxy/enterprise (optional)

#### Azure OpenAI Configuration

```json
{
  "GmsdAgents": {
    "SemanticKernel": {
      "Provider": "AzureOpenAi",
      "AzureOpenAi": {
        "Endpoint": "https://your-resource.openai.azure.com",
        "ApiKey": "your-azure-key",
        "DeploymentName": "gpt-4-deployment",
        "ApiVersion": "2024-02-01"
      }
    }
  }
}
```

**Required Fields:**
- `Endpoint` - Azure OpenAI resource endpoint URL
- `ApiKey` - Azure OpenAI API key
- `DeploymentName` - Name of your deployed model

**Optional Fields:**
- `ApiVersion` - API version (default: 2024-02-01)

#### Ollama Configuration

```json
{
  "GmsdAgents": {
    "SemanticKernel": {
      "Provider": "Ollama",
      "Ollama": {
        "BaseUrl": "http://localhost:11434",
        "ModelId": "llama3"
      }
    }
  }
}
```

**Required Fields:**
- `ModelId` - Model to use (llama3, mistral, codellama, etc.)

**Optional Fields:**
- `BaseUrl` - Ollama server URL (default: http://localhost:11434)

#### Anthropic Configuration

```json
{
  "GmsdAgents": {
    "SemanticKernel": {
      "Provider": "Anthropic",
      "Anthropic": {
        "ApiKey": "sk-ant-...",
        "ModelId": "claude-3-opus-20240229",
        "ApiVersion": "2023-06-01",
        "BaseUrl": null
      }
    }
  }
}
```

**Required Fields:**
- `ApiKey` - Your Anthropic API key (obtain from https://console.anthropic.com/account/keys)
- `ModelId` - Model to use (claude-3-opus-20240229, claude-3-sonnet-20240229, etc.)

**Optional Fields:**
- `ApiVersion` - API version (default: 2023-06-01)
- `BaseUrl` - Custom endpoint URL (optional)

### Configuration Validation

Configuration is validated at DI registration time. Invalid or missing configuration throws `InvalidOperationException` with clear guidance:

```csharp
// Missing configuration
InvalidOperationException: Semantic Kernel configuration is missing. 
Ensure the 'GmsdAgents:SemanticKernel' section is configured in appsettings.json.

// Missing API key
InvalidOperationException: OpenAI API key is required but not configured. 
Ensure 'GmsdAgents:SemanticKernel:OpenAi:ApiKey' is set to your OpenAI API key.

// Unsupported model
InvalidOperationException: OpenAI model 'gpt-2' is not in the list of supported models. 
Supported models: gpt-4, gpt-4-turbo, gpt-4-turbo-preview, gpt-4o, gpt-3.5-turbo.
```

### Troubleshooting Configuration Errors

| Error | Cause | Solution |
|-------|-------|----------|
| "configuration is missing" | `GmsdAgents:SemanticKernel` section not found | Add section to appsettings.json |
| "provider is required" | Provider field is empty or missing | Set `Provider` to one of: OpenAi, AzureOpenAi, Ollama, Anthropic |
| "Unknown LLM provider" | Invalid provider name | Check spelling and case of provider name |
| "API key is required" | API key field is empty or missing | Set the appropriate ApiKey field for your provider |
| "model ID is required" | ModelId field is empty or missing | Set ModelId to a valid model name |
| "not in the list of supported models" | Model is not supported | Use a supported model (see provider docs) |
| "endpoint must be a valid URL" | Endpoint is not a valid URL | Ensure endpoint is a properly formatted URL |

### General Agents Options

```json
{
  "GmsdAgents": {
    "WorkspacePath": "C:/Gmsd/Workspace",
    "ToolCalling": {
      "DefaultMaxIterations": 10,
      "DefaultTimeoutSeconds": 300,
      "MaxParallelToolExecutions": 32
    }
  }
}
```

## Dependencies

- `Microsoft.SemanticKernel` - LLM provider integration
- `Gmsd.Aos` - AOS (Agent Operating System) contracts and state management
- `Gmsd.Common` - Shared utilities and exceptions

## Related Documentation

- [Streaming Events](../docs/streaming-events.md) - SSE event contract for frontend developers
- [Tool Calling Protocol Design](../openspec/changes/implement-tool-calling-protocol/design.md) - Implementation design document
- [LLM Troubleshooting Guide](./LLM_TROUBLESHOOTING.md) - Diagnosis and resolution for common LLM issues
- [Provider Expansion Guide](./PROVIDER_EXPANSION.md) - How to add support for new LLM providers
