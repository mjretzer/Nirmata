#pragma warning disable CS0618 // Intentionally using obsolete ILlmProvider contracts pending migration

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Json.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gmsd.Agents.Execution.Preflight.CommandSuggestion;

/// <summary>
/// LLM-based implementation of command suggestion that analyzes natural language
/// and suggests explicit commands using a lightweight LLM call with structured output.
/// </summary>
public sealed class LlmCommandSuggester : ICommandSuggester
{
    private const string CommandProposalSchemaResourceName = "Gmsd.Aos.Resources.Schemas.command-proposal.schema.json";
    private readonly ILlmProvider _llmProvider;
    private readonly ICommandRegistry _commandRegistry;
    private readonly CommandSuggestionOptions _options;
    private readonly ILogger<LlmCommandSuggester> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly LlmStructuredOutputSchema _commandProposalSchema;

    private static readonly Lazy<string> CommandProposalSchemaJson = new(() =>
        LoadEmbeddedSchema(CommandProposalSchemaResourceName),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<LlmStructuredOutputSchema> StructuredCommandProposalSchema = new(() =>
        LlmStructuredOutputSchema.FromJson(
            name: "command_proposal_v1",
            schemaJson: CommandProposalSchemaJson.Value,
            description: "GMSD command proposal schema"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public LlmCommandSuggester(
        ILlmProvider llmProvider,
        ICommandRegistry commandRegistry,
        IOptions<CommandSuggestionOptions> options,
        ILogger<LlmCommandSuggester> logger)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _commandProposalSchema = StructuredCommandProposalSchema.Value;
    }

    /// <inheritdoc />
    public async Task<CommandProposal?> SuggestAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            _logger.LogDebug("Input is empty or whitespace; no suggestion possible");
            return null;
        }

        // Truncate input if it exceeds max length
        if (input.Length > _options.MaxInputLength)
        {
            input = input[.._options.MaxInputLength];
            _logger.LogDebug("Input truncated to {MaxLength} characters", _options.MaxInputLength);
        }

        try
        {
            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(input);

            var request = new LlmCompletionRequest
            {
                Messages =
                [
                    LlmMessage.System(systemPrompt),
                    LlmMessage.User(userPrompt)
                ],
                StructuredOutputSchema = _commandProposalSchema,
                Options = new LlmProviderOptions
                {
                    Temperature = 0.1f,
                    MaxTokens = 500
                }
            };

            _logger.LogDebug("Sending command suggestion request to LLM provider {Provider}", _llmProvider.ProviderName);

            var response = await _llmProvider.CompleteAsync(request, cancellationToken);

            if (string.IsNullOrWhiteSpace(response.Message.Content))
            {
                _logger.LogDebug("LLM returned empty response; no suggestion");
                return null;
            }

            return ParseSuggestionResponse(response.Message.Content, input);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get command suggestion from LLM; falling back to no suggestion");
            return null;
        }
    }

    private string BuildSystemPrompt()
    {
        var availableCommands = _commandRegistry.GetAllCommands()
            .Select(c => $"- /{c.Name}: {c.Description}")
            .ToList();

        var commandsList = availableCommands.Count > 0
            ? string.Join("\n", availableCommands)
            : "- /run: Execute the current task or workflow\n- /plan [options]: Create or update a plan\n- /status: Check current workflow status\n- /verify: Verify task execution\n- /fix: Create a fix plan for issues\n- /pause: Pause workflow execution\n- /resume: Resume paused workflow\n- /help: Show available commands";

        return $"You are a command suggestion assistant. Analyze the user's natural language input and determine if it maps to one of the available commands.\n\n" +
               $"Available commands:\n{commandsList}\n\n" +
               "When suggesting a command, return JSON that matches the provided command proposal schema with these required fields:\n" +
               "- intent.goal\n" +
               "- intent.parameters (string-to-string map)\n" +
               "- command (must start with /)\n" +
               "- group\n" +
               "- rationale\n" +
               "- expectedOutcome\n\n" +
               "Only suggest a command when:\n" +
               "1. The input clearly indicates a command intent\n" +
               "2. You can map specific arguments from the input\n" +
               "3. The command exists in the available command list\n\n" +
               "If no clear command intent is detected, return: {\"intent\": {\"goal\": \"no-op\"}, \"command\": \"/status\", \"group\": \"chat\", \"rationale\": \"No command should be suggested for this input.\", \"expectedOutcome\": \"System remains in chat mode.\"}\n\n" +
               "Be conservative - when in doubt, do not suggest.";
    }

    private static string BuildUserPrompt(string input)
    {
        return $"Analyze this user input and suggest a command if applicable:\n\n" +
               $"\"\"\"{input}\"\"\"\n\n" +
               "Respond with JSON only. If no command should be suggested, set intent.goal to 'no-op' and command to '/status'.";
    }

    private CommandProposal? ParseSuggestionResponse(string content, string originalInput)
    {
        try
        {
            // Try to extract JSON from the response (in case there's surrounding text)
            var jsonContent = ExtractJsonFromResponse(content);

            if (LooksLikeLegacySuggestion(jsonContent))
            {
                return ParseLegacySuggestionResponse(jsonContent);
            }

            using var document = JsonDocument.Parse(jsonContent);
            var schemaErrors = ValidateStructuredProposalSchema(document.RootElement);
            if (schemaErrors.Count > 0)
            {
                _logger.LogWarning(
                    "Structured command proposal failed schema validation: {Errors}",
                    string.Join("; ", schemaErrors));
                return null;
            }

            var structured = JsonSerializer.Deserialize<StructuredCommandProposalResponse>(jsonContent, _jsonOptions);
            if (structured is null)
            {
                _logger.LogWarning("Structured command proposal deserialized to null");
                return null;
            }

            if (string.Equals(structured.Intent?.Goal, "no-op", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Structured response indicated no command suggestion (intent.goal = no-op)");
                return null;
            }

            if (string.IsNullOrWhiteSpace(structured.Command) || !structured.Command.StartsWith('/'))
            {
                _logger.LogWarning("Structured command proposal has invalid command value '{Command}'", structured.Command);
                return null;
            }

            var normalizedCommand = structured.Command[1..];
            var parameters = structured.Intent?.Parameters;
            var arguments = parameters is { Count: > 0 }
                ? parameters
                    .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                    .SelectMany(kvp => new[] { $"--{kvp.Key}", kvp.Value })
                    .ToArray()
                : Array.Empty<string>();

            if (_commandRegistry.GetAllCommands().Any() && !_commandRegistry.IsKnownCommand(normalizedCommand))
            {
                _logger.LogWarning(
                    "LLM suggested unknown structured command '{Command}'; rejecting suggestion",
                    normalizedCommand);
                return null;
            }

            var formattedCommand = arguments.Length == 0
                ? structured.Command
                : $"{structured.Command} {string.Join(' ', arguments)}";

            var proposal = new CommandProposal
            {
                CommandName = normalizedCommand,
                Arguments = arguments,
                Confidence = 1.0,
                Reasoning = $"{structured.Rationale} Expected: {structured.ExpectedOutcome}",
                FormattedCommand = formattedCommand
            };

            if (!proposal.IsValid())
            {
                var errors = string.Join("; ", proposal.GetValidationErrors());
                _logger.LogWarning("Generated structured proposal failed validation: {Errors}", errors);
                return null;
            }

            _logger.LogInformation(
                "Suggested structured command '{Command}' for input '{Input}'",
                proposal.CommandName,
                originalInput);

            return proposal;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM suggestion response as JSON");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error parsing suggestion response");
            return null;
        }
    }

    private CommandProposal? ParseLegacySuggestionResponse(string jsonContent)
    {
        try
        {
            var suggestion = JsonSerializer.Deserialize<CommandSuggestionResponse>(jsonContent, _jsonOptions);

            if (suggestion == null || !suggestion.Suggested)
            {
                _logger.LogDebug("LLM indicated no command suggestion (suggested: false or null)");
                return null;
            }

            // Validate confidence threshold
            if (suggestion.Confidence < _options.ConfidenceThreshold)
            {
                _logger.LogDebug(
                    "Confidence {Confidence:F2} below threshold {Threshold:F2}; rejecting suggestion",
                    suggestion.Confidence,
                    _options.ConfidenceThreshold);
                return null;
            }

            // Validate command exists in registry (if registry has entries)
            if (_commandRegistry.GetAllCommands().Any() &&
                !_commandRegistry.IsKnownCommand(suggestion.Command ?? ""))
            {
                _logger.LogWarning(
                    "LLM suggested unknown command '{Command}'; rejecting suggestion",
                    suggestion.Command);
                return null;
            }

            var proposal = new CommandProposal
            {
                CommandName = suggestion.Command ?? "",
                Arguments = suggestion.Arguments ?? Array.Empty<string>(),
                Confidence = suggestion.Confidence,
                Reasoning = suggestion.Reasoning,
                FormattedCommand = suggestion.Formatted ?? $"/{suggestion.Command}"
            };

            // Final validation
            if (!proposal.IsValid())
            {
                var errors = string.Join("; ", proposal.GetValidationErrors());
                _logger.LogWarning("Generated proposal failed validation: {Errors}", errors);
                return null;
            }

            _logger.LogInformation(
                "Suggested command '{Command}' with confidence {Confidence:F2}",
                proposal.CommandName,
                proposal.Confidence);

            return proposal;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse legacy command suggestion JSON payload");
            return null;
        }
        catch
        {
            _logger.LogWarning("Legacy command suggestion payload could not be parsed");
            return null;
        }
    }

    private static bool LooksLikeLegacySuggestion(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return false;
        }

        return jsonContent.Contains("\"suggested\"", StringComparison.OrdinalIgnoreCase) ||
               jsonContent.Contains("\"confidence\"", StringComparison.OrdinalIgnoreCase) ||
               jsonContent.Contains("\"formatted\"", StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> ValidateStructuredProposalSchema(JsonElement rootElement)
    {
        var evaluation = _commandProposalSchema.GetCompiledSchema().Evaluate(
            rootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.List });

        if (evaluation.IsValid)
        {
            return Array.Empty<string>();
        }

        return CollectValidationErrors(evaluation).ToArray();
    }

    private static IEnumerable<string> CollectValidationErrors(EvaluationResults results)
    {
        if (results.Errors is not null)
        {
            foreach (var error in results.Errors)
            {
                var location = string.IsNullOrEmpty(results.InstanceLocation.ToString())
                    ? "$"
                    : results.InstanceLocation.ToString();
                yield return $"{location}: {error.Value}";
            }
        }

        if (results.Details is null)
        {
            yield break;
        }

        foreach (var detail in results.Details)
        {
            foreach (var issue in CollectValidationErrors(detail))
            {
                yield return issue;
            }
        }
    }

    private static string ExtractJsonFromResponse(string content)
    {
        // Try to find JSON between code fences
        var codeFenceStart = content.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (codeFenceStart >= 0)
        {
            var jsonStart = codeFenceStart + 7;
            var codeFenceEnd = content.IndexOf("```", jsonStart, StringComparison.OrdinalIgnoreCase);
            if (codeFenceEnd > jsonStart)
            {
                return content[jsonStart..codeFenceEnd].Trim();
            }
        }

        // Try generic code fence
        codeFenceStart = content.IndexOf("```", StringComparison.OrdinalIgnoreCase);
        if (codeFenceStart >= 0)
        {
            var jsonStart = codeFenceStart + 3;
            var codeFenceEnd = content.IndexOf("```", jsonStart, StringComparison.OrdinalIgnoreCase);
            if (codeFenceEnd > jsonStart)
            {
                var inner = content[jsonStart..codeFenceEnd].Trim();
                if (inner.StartsWith('{'))
                    return inner;
            }
        }

        // Return trimmed content (may already be just JSON)
        return content.Trim();
    }

    private static string LoadEmbeddedSchema(string resourceName)
    {
        var assembly = typeof(LlmCommandSuggester).Assembly;
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            var fallbackResourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("command-proposal.schema.json", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(fallbackResourceName))
            {
                stream = assembly.GetManifestResourceStream(fallbackResourceName);
            }
        }

        if (stream is null)
        {
            throw new InvalidOperationException($"Schema resource '{resourceName}' not found.");
        }

        using (stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// Internal DTO for deserializing LLM response.
    /// </summary>
    private sealed class CommandSuggestionResponse
    {
        [JsonPropertyName("suggested")]
        public bool Suggested { get; init; }

        [JsonPropertyName("command")]
        public string? Command { get; init; }

        [JsonPropertyName("arguments")]
        public string[]? Arguments { get; init; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }

        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; init; }

        [JsonPropertyName("formatted")]
        public string? Formatted { get; init; }
    }

    private sealed class StructuredCommandProposalResponse
    {
        [JsonPropertyName("intent")]
        public StructuredCommandIntent? Intent { get; init; }

        [JsonPropertyName("command")]
        public string? Command { get; init; }

        [JsonPropertyName("group")]
        public string? Group { get; init; }

        [JsonPropertyName("rationale")]
        public string? Rationale { get; init; }

        [JsonPropertyName("expectedOutcome")]
        public string? ExpectedOutcome { get; init; }
    }

    private sealed class StructuredCommandIntent
    {
        [JsonPropertyName("goal")]
        public string? Goal { get; init; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, string>? Parameters { get; init; }
    }
}

#pragma warning restore CS0618

