#pragma warning disable CS0618 // Intentionally using obsolete ILlmProvider contracts pending migration

using System.Text.Json;
using System.Text.Json.Serialization;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.Preflight;
using Microsoft.Extensions.Logging;

namespace Gmsd.Agents.Execution.ControlPlane;

/// <summary>
/// Interface for generating structured ProposedAction outputs using LLM tool calling.
/// This ensures the LLM always returns a valid, schema-compliant action proposal.
/// </summary>
public interface ILlmStructuredActionGenerator
{
    /// <summary>
    /// Generates a structured ProposedAction using LLM tool calling.
    /// </summary>
    /// <param name="intent">The classified intent to base the proposal on.</param>
    /// <param name="context">The gating context with workspace state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A structured ProposedAction or null if generation failed.</returns>
    Task<ProposedAction?> GenerateAsync(
        IntentClassificationResult intent,
        GatingContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a structured ProposedAction with forced validation.
    /// </summary>
    /// <param name="phase">The target phase.</param>
    /// <param name="description">A description template.</param>
    /// <param name="riskLevel">The risk level.</param>
    /// <param name="affectedResources">List of affected resources.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A validated ProposedAction.</returns>
    Task<ProposedAction> GenerateValidatedAsync(
        string phase,
        string description,
        RiskLevel riskLevel,
        IReadOnlyList<string> affectedResources,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// LLM-based structured output generator for ProposedAction.
/// Uses tool calling to force schema-compliant responses from the LLM.
/// </summary>
public sealed class LlmStructuredActionGenerator : ILlmStructuredActionGenerator
{
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<LlmStructuredActionGenerator> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Tool name for structured action generation.
    /// </summary>
    public const string ToolName = "generate_proposed_action";

    /// <summary>
    /// Creates a new instance of the structured action generator.
    /// </summary>
    public LlmStructuredActionGenerator(
        ILlmProvider llmProvider,
        ILogger<LlmStructuredActionGenerator> logger)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <inheritdoc />
    public async Task<ProposedAction?> GenerateAsync(
        IntentClassificationResult intent,
        GatingContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var toolDefinition = CreateProposedActionToolDefinition();

            var request = new LlmCompletionRequest
            {
                Messages =
                [
                    LlmMessage.System(BuildSystemPrompt()),
                    LlmMessage.User(BuildUserPrompt(intent, context))
                ],
                Options = new LlmProviderOptions
                {
                    Temperature = 0.1f,
                    MaxTokens = 1000
                },
                Tools = [toolDefinition],
                ToolChoice = ToolName
            };

            _logger.LogDebug(
                "Requesting structured ProposedAction for intent '{Intent}' with tool '{ToolName}'",
                intent.Intent.Kind,
                ToolName);

            var response = await _llmProvider.CompleteAsync(request, cancellationToken);

            // Extract tool call from response
            var toolCall = response.ToolCalls?.FirstOrDefault(tc => tc.Name == ToolName);
            if (toolCall == null)
            {
                _logger.LogWarning("LLM did not return expected tool call for {ToolName}", ToolName);
                return null;
            }

            var proposedAction = ParseProposedAction(toolCall.ArgumentsJson);
            if (proposedAction == null)
            {
                _logger.LogWarning("Failed to parse ProposedAction from tool call arguments");
                return null;
            }

            // Validate completeness
            var validationResult = proposedAction.ValidateCompleteness();
            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Generated ProposedAction failed completeness validation: {Errors}",
                    string.Join("; ", validationResult.Errors));
                return null;
            }

            _logger.LogInformation(
                "Successfully generated structured ProposedAction for phase '{Phase}' with {ResourceCount} affected resources",
                proposedAction.Phase,
                proposedAction.AffectedResources.Count);

            return proposedAction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate structured ProposedAction");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ProposedAction> GenerateValidatedAsync(
        string phase,
        string description,
        RiskLevel riskLevel,
        IReadOnlyList<string> affectedResources,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var intent = new IntentClassificationResult
        {
            Intent = new Intent
            {
                Kind = IntentKind.WorkflowCommand,
                SideEffect = riskLevel == RiskLevel.Read ? SideEffect.ReadOnly : SideEffect.Write,
                Confidence = 1.0,
                Reasoning = $"Generate validated proposed action for {phase} phase"
            },
            ParsedCommand = new ParsedCommand
            {
                RawInput = description,
                CommandName = phase.ToLowerInvariant(),
                SideEffect = riskLevel == RiskLevel.Read ? SideEffect.ReadOnly : SideEffect.Write,
                Confidence = 1.0,
                IsKnownCommand = true
            }
        };

        var context = new GatingContext
        {
            CurrentCursor = null,
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true
        };

        var result = await GenerateAsync(intent, context, cancellationToken);

        // Fall back to creating a validated action directly if LLM generation fails
        if (result == null)
        {
            _logger.LogDebug(
                "LLM generation failed, falling back to direct creation for phase '{Phase}'",
                phase);

            var fallbackAction = new ProposedAction
            {
                Phase = phase,
                Description = description,
                RiskLevel = riskLevel,
                AffectedResources = affectedResources,
                SideEffects = GetSideEffectsForRiskLevel(riskLevel)
            };

            // Validate the fallback
            var validation = fallbackAction.ValidateCompleteness();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Fallback ProposedAction failed validation: {string.Join("; ", validation.Errors)}");
            }

            return fallbackAction;
        }

        return result;
    }

    /// <summary>
    /// Creates the tool definition for the proposed_action generator.
    /// </summary>
    private static LlmToolDefinition CreateProposedActionToolDefinition()
    {
        return new LlmToolDefinition
        {
            Name = ToolName,
            Description = "Generates a structured proposed action for user confirmation. " +
                         "Must include all required fields: phase, description, riskLevel, affectedResources.",
            ParametersSchema = new
            {
                type = "object",
                properties = new
                {
                    phase = new
                    {
                        type = "string",
                        description = "The phase to execute (e.g., Interviewer, Planner, Executor)",
                        minLength = 1,
                        maxLength = 50,
                        pattern = "^[A-Za-z]+$"
                    },
                    description = new
                    {
                        type = "string",
                        description = "Human-readable description of what will be done (minimum 20 characters)",
                        minLength = 10,
                        maxLength = 500
                    },
                    riskLevel = new
                    {
                        type = "string",
                        description = "Risk level: Read, WriteSafe, WriteDestructive, WriteDestructiveGit, WorkspaceDestructive",
                        @enum = new[] { "Read", "WriteSafe", "WriteDestructive", "WriteDestructiveGit", "WorkspaceDestructive" }
                    },
                    affectedResources = new
                    {
                        type = "array",
                        description = "List of resource identifiers that will be affected (file paths, DB entities, etc.)",
                        items = new { type = "string", minLength = 1 },
                        minItems = 1,
                        maxItems = 100
                    },
                    sideEffects = new
                    {
                        type = "array",
                        description = "Types of side effects (optional)",
                        items = new { type = "string" },
                        maxItems = 10
                    },
                    estimatedImpact = new
                    {
                        type = "string",
                        description = "Estimated impact scope (optional, max 100 chars)",
                        maxLength = 100
                    }
                },
                required = new[] { "phase", "description", "riskLevel", "affectedResources" }
            }
        };
    }

    /// <summary>
    /// Builds the system prompt for structured action generation.
    /// </summary>
    private static string BuildSystemPrompt()
    {
        return "You are an action proposal generator. Your task is to create structured proposed actions " +
               "that describe operations the system will perform on behalf of the user.\n\n" +
               "You MUST use the 'generate_proposed_action' tool to return your response. " +
               "This ensures the output follows the exact schema required.\n\n" +
               "Guidelines:\n" +
               "- Phase must be one of: Interviewer, Roadmapper, Planner, Executor, Verifier, FixPlanner, Responder\n" +
               "- Description should be clear, specific, and at least 20 characters\n" +
               "- RiskLevel should accurately reflect the operation's destructiveness:\n" +
               "  * Read: No modifications, only reading/querying\n" +
               "  * WriteSafe: Modifications that are easily reversible\n" +
               "  * WriteDestructive: Modifications that may be hard to undo\n" +
               "  * WriteDestructiveGit: Git operations like commits that are permanent\n" +
               "  * WorkspaceDestructive: Operations that could corrupt workspace state\n" +
               "- AffectedResources must include all files, paths, or entities that will be modified\n" +
               "- SideEffects should list the types of systems affected (filesystem, database, git, etc.)\n\n" +
               "Always provide complete, accurate information. The user will review this before confirming.";
    }

    /// <summary>
    /// Builds the user prompt from intent and context.
    /// </summary>
    private static string BuildUserPrompt(IntentClassificationResult intent, GatingContext context)
    {
        var sideEffect = intent.Intent.SideEffect.ToString();
        var confidence = intent.Intent.Confidence.ToString("F2");

        return $"Generate a proposed action based on the following:\n\n" +
               $"Intent: {intent.Intent.Kind}\n" +
               $"Side Effect: {sideEffect}\n" +
               $"Confidence: {confidence}\n" +
               $"Reasoning: {intent.Intent.Reasoning}\n" +
               $"Input: {intent.ParsedCommand?.RawInput ?? "N/A"}\n\n" +
               $"Workspace Context:\n" +
               $"- Has Project: {context.HasProject}\n" +
               $"- Has Roadmap: {context.HasRoadmap}\n" +
               $"- Has Plan: {context.HasPlan}\n" +
               $"- Current Cursor: {context.CurrentCursor ?? "none"}\n\n" +
               $"Use the generate_proposed_action tool to provide your response.";
    }

    /// <summary>
    /// Parses the ProposedAction from tool call arguments.
    /// </summary>
    private ProposedAction? ParseProposedAction(string argumentsJson)
    {
        try
        {
            var arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson, _jsonOptions);
            if (arguments == null)
            {
                _logger.LogWarning("Failed to parse tool call arguments as JSON");
                return null;
            }

            if (!arguments.TryGetValue("phase", out var phaseObj) || phaseObj?.ToString() is not string phase)
                return null;

            if (!arguments.TryGetValue("description", out var descObj) || descObj?.ToString() is not string description)
                return null;

            if (!arguments.TryGetValue("riskLevel", out var riskObj) || riskObj?.ToString() is not string riskStr)
                return null;

            if (!Enum.TryParse<RiskLevel>(riskStr, out var riskLevel))
                return null;

            var affectedResources = new List<string>();
            if (arguments.TryGetValue("affectedResources", out var resourcesObj) && resourcesObj is JsonElement resourcesElement)
            {
                affectedResources.AddRange(resourcesElement.EnumerateArray()
                    .Select(e => e.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            else if (arguments.TryGetValue("affectedResources", out var resourcesList) && resourcesList is IEnumerable<object> resources)
            {
                affectedResources.AddRange(resources.Select(r => r?.ToString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            var sideEffects = new List<string>();
            if (arguments.TryGetValue("sideEffects", out var effectsObj) && effectsObj is JsonElement effectsElement)
            {
                sideEffects.AddRange(effectsElement.EnumerateArray()
                    .Select(e => e.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            else if (arguments.TryGetValue("sideEffects", out var effectsList) && effectsList is IEnumerable<object> effects)
            {
                sideEffects.AddRange(effects.Select(e => e?.ToString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            string? estimatedImpact = null;
            if (arguments.TryGetValue("estimatedImpact", out var impactObj) && impactObj != null)
            {
                estimatedImpact = impactObj.ToString();
            }

            return new ProposedAction
            {
                Phase = phase,
                Description = description,
                RiskLevel = riskLevel,
                AffectedResources = affectedResources,
                SideEffects = sideEffects,
                EstimatedImpact = estimatedImpact
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing ProposedAction from tool arguments");
            return null;
        }
    }

    /// <summary>
    /// Gets default side effects based on risk level.
    /// </summary>
    private static IReadOnlyList<string> GetSideEffectsForRiskLevel(RiskLevel riskLevel)
    {
        return riskLevel switch
        {
            RiskLevel.Read => new[] { "read" },
            RiskLevel.WriteSafe => new[] { "write" },
            RiskLevel.WriteDestructive => new[] { "write", "filesystem" },
            RiskLevel.WriteDestructiveGit => new[] { "write", "git" },
            RiskLevel.WorkspaceDestructive => new[] { "write", "filesystem", "workspace" },
            _ => Array.Empty<string>()
        };
    }
}

#pragma warning restore CS0618

