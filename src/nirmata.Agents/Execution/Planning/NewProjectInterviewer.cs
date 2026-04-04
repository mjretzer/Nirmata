#pragma warning disable CS0618 // Intentionally using obsolete ILlmProvider contracts pending migration

using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using nirmata.Aos.Public;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace nirmata.Agents.Execution.Planning;

/// <summary>
/// Default implementation of the new project interviewer using LLM-driven interviews.
/// Each phase sends context to the LLM, parses the structured JSON response,
/// and persists session state to evidence artifacts between phases.
/// </summary>
public sealed class NewProjectInterviewer : INewProjectInterviewer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly ILlmProvider _llmProvider;
    private readonly IProjectSpecGenerator _specGenerator;
    private readonly IInterviewEvidenceWriter _evidenceWriter;
    private readonly ISpecStore _specStore;
    private readonly IWorkspace _workspace;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewProjectInterviewer"/> class.
    /// </summary>
    public NewProjectInterviewer(
        ILlmProvider llmProvider,
        IProjectSpecGenerator specGenerator,
        IInterviewEvidenceWriter evidenceWriter,
        ISpecStore specStore,
        IWorkspace workspace)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _specGenerator = specGenerator ?? throw new ArgumentNullException(nameof(specGenerator));
        _evidenceWriter = evidenceWriter ?? throw new ArgumentNullException(nameof(evidenceWriter));
        _specStore = specStore ?? throw new ArgumentNullException(nameof(specStore));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    /// <inheritdoc />
    public async Task<InterviewResult> ConductInterviewAsync(InterviewSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        try
        {
            session.State = InterviewState.Discovery;
            session.ProjectDraft ??= new ProjectSpecDraft();

            // Run through the interview phases, persisting state after each
            await RunDiscoveryPhaseAsync(session, ct);
            await PersistSessionStateAsync(session, ct);

            await RunClarificationPhaseAsync(session, ct);
            await PersistSessionStateAsync(session, ct);

            await RunConfirmationPhaseAsync(session, ct);
            await PersistSessionStateAsync(session, ct);

            // Generate the project specification
            var spec = _specGenerator.GenerateFromSession(session);
            var validationResult = _specGenerator.Validate(spec);

            if (!validationResult.IsValid)
            {
                session.State = InterviewState.Failed;
                return new InterviewResult
                {
                    Success = false,
                    ErrorMessage = $"Generated spec validation failed: {string.Join(", ", validationResult.Errors)}",
                    Session = session
                };
            }

            var specJson = _specGenerator.SerializeToJson(spec);

            // Write evidence files
            var artifacts = await WriteEvidenceAsync(session, spec, specJson, ct);

            // Write project.json to spec store
            await WriteProjectSpecAsync(spec, specJson, ct);

            session.State = InterviewState.Complete;
            session.CompletedAt = DateTimeOffset.UtcNow;

            return new InterviewResult
            {
                Success = true,
                ProjectSpec = spec,
                ProjectSpecJson = specJson,
                TranscriptMarkdown = GenerateTranscriptMarkdown(session),
                SummaryMarkdown = GenerateSummaryMarkdown(session, spec),
                Session = session,
                Artifacts = artifacts
            };
        }
        catch (Exception ex)
        {
            session.State = InterviewState.Failed;
            return new InterviewResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Session = session
            };
        }
    }

    private async Task RunDiscoveryPhaseAsync(InterviewSession session, CancellationToken ct)
    {
        session.CurrentPhase = InterviewPhase.Discovery;
        session.State = InterviewState.Discovery;

        var systemPrompt = InterviewPrompts.GetSystemPrompt(InterviewPhase.Discovery);
        var userPrompt = InterviewPrompts.CreateUserPrompt(session);

        var response = await CallLlmAsync(systemPrompt, userPrompt, ct);
        var parsed = ParsePhaseResponse(response);

        if (parsed == null)
        {
            // Fallback: treat the raw response as a single discovery answer
            session.QAPairs.Add(new InterviewQAPair
            {
                Question = "Describe the project based on available context.",
                Answer = response ?? "No response from LLM.",
                Phase = InterviewPhase.Discovery
            });
            return;
        }

        // Add Q&A pairs from the LLM response
        foreach (var qa in parsed.QAPairs ?? [])
        {
            if (!string.IsNullOrWhiteSpace(qa.Question) && !string.IsNullOrWhiteSpace(qa.Answer))
            {
                session.QAPairs.Add(new InterviewQAPair
                {
                    Question = qa.Question,
                    Answer = qa.Answer,
                    Phase = InterviewPhase.Discovery
                });
            }
        }

        // Apply the draft from the LLM's discovery output
        if (parsed.Draft != null)
        {
            ApplyDraft(session.ProjectDraft!, parsed.Draft);
        }
    }

    private async Task RunClarificationPhaseAsync(InterviewSession session, CancellationToken ct)
    {
        session.CurrentPhase = InterviewPhase.Clarification;
        session.State = InterviewState.Clarification;

        var systemPrompt = InterviewPrompts.GetSystemPrompt(InterviewPhase.Clarification);
        var userPrompt = InterviewPrompts.CreateUserPrompt(session);

        var response = await CallLlmAsync(systemPrompt, userPrompt, ct);
        var parsed = ParsePhaseResponse(response);

        if (parsed == null)
        {
            session.QAPairs.Add(new InterviewQAPair
            {
                Question = "Are there additional clarifications needed?",
                Answer = response ?? "No additional clarification available.",
                Phase = InterviewPhase.Clarification
            });
            return;
        }

        foreach (var qa in parsed.QAPairs ?? [])
        {
            if (!string.IsNullOrWhiteSpace(qa.Question) && !string.IsNullOrWhiteSpace(qa.Answer))
            {
                session.QAPairs.Add(new InterviewQAPair
                {
                    Question = qa.Question,
                    Answer = qa.Answer,
                    Phase = InterviewPhase.Clarification
                });
            }
        }

        // Apply incremental draft updates from clarification
        if (parsed.DraftUpdates != null)
        {
            AppendDraftUpdates(session.ProjectDraft!, parsed.DraftUpdates);
        }
    }

    private async Task RunConfirmationPhaseAsync(InterviewSession session, CancellationToken ct)
    {
        session.CurrentPhase = InterviewPhase.Confirmation;
        session.State = InterviewState.Confirmation;

        var systemPrompt = InterviewPrompts.GetSystemPrompt(InterviewPhase.Confirmation);
        var userPrompt = InterviewPrompts.CreateUserPrompt(session);

        var response = await CallLlmAsync(systemPrompt, userPrompt, ct);
        var parsed = ParsePhaseResponse(response);

        if (parsed == null)
        {
            session.QAPairs.Add(new InterviewQAPair
            {
                Question = "Do these requirements accurately capture the project needs?",
                Answer = response ?? "Requirements confirmed.",
                Phase = InterviewPhase.Confirmation
            });
            return;
        }

        foreach (var qa in parsed.QAPairs ?? [])
        {
            if (!string.IsNullOrWhiteSpace(qa.Question) && !string.IsNullOrWhiteSpace(qa.Answer))
            {
                session.QAPairs.Add(new InterviewQAPair
                {
                    Question = qa.Question,
                    Answer = qa.Answer,
                    Phase = InterviewPhase.Confirmation
                });
            }
        }

        // Confirmation produces the final complete draft — replace current draft
        if (parsed.ConfirmedDraft != null)
        {
            ApplyDraft(session.ProjectDraft!, parsed.ConfirmedDraft);
        }
    }

    private async Task<string?> CallLlmAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var messages = new List<LlmMessage>
        {
            LlmMessage.System(systemPrompt),
            LlmMessage.User(userPrompt)
        };

        var request = new LlmCompletionRequest
        {
            Messages = messages,
            Options = new LlmProviderOptions
            {
                Temperature = 0.7f,
                MaxTokens = 4000
            }
        };

        var result = await _llmProvider.CompleteAsync(request, ct);
        return result.Message.Content;
    }

    /// <summary>
    /// Parses the structured JSON response from the LLM for any phase.
    /// The response may contain "draft", "draftUpdates", or "confirmedDraft" depending on the phase.
    /// </summary>
    private static PhaseResponse? ParsePhaseResponse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        // Strip markdown fencing if present
        var json = content.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline > 0)
                json = json[(firstNewline + 1)..];
            if (json.EndsWith("```"))
                json = json[..^3];
            json = json.Trim();
        }

        try
        {
            return JsonSerializer.Deserialize<PhaseResponse>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Replaces the current draft with values from the LLM-generated draft.
    /// </summary>
    private static void ApplyDraft(ProjectSpecDraft target, DraftData source)
    {
        if (!string.IsNullOrWhiteSpace(source.Name))
            target.Name = source.Name;
        if (!string.IsNullOrWhiteSpace(source.Description))
            target.Description = source.Description;
        if (!string.IsNullOrWhiteSpace(source.TechnologyStack))
            target.TechnologyStack = source.TechnologyStack;
        if (!string.IsNullOrWhiteSpace(source.TargetAudience))
            target.TargetAudience = source.TargetAudience;

        if (source.Goals is { Count: > 0 })
        {
            target.Goals.Clear();
            target.Goals.AddRange(source.Goals);
        }
        if (source.KeyFeatures is { Count: > 0 })
        {
            target.KeyFeatures.Clear();
            target.KeyFeatures.AddRange(source.KeyFeatures);
        }
        if (source.Constraints is { Count: > 0 })
        {
            target.Constraints.Clear();
            target.Constraints.AddRange(source.Constraints);
        }
        if (source.Assumptions is { Count: > 0 })
        {
            target.Assumptions.Clear();
            target.Assumptions.AddRange(source.Assumptions);
        }
    }

    /// <summary>
    /// Appends incremental updates from the clarification phase.
    /// </summary>
    private static void AppendDraftUpdates(ProjectSpecDraft target, DraftData source)
    {
        if (!string.IsNullOrWhiteSpace(source.TechnologyStack))
            target.TechnologyStack = source.TechnologyStack;
        if (!string.IsNullOrWhiteSpace(source.TargetAudience))
            target.TargetAudience = source.TargetAudience;

        if (source.Goals is { Count: > 0 })
            target.Goals.AddRange(source.Goals.Where(g => !target.Goals.Contains(g)));
        if (source.KeyFeatures is { Count: > 0 })
            target.KeyFeatures.AddRange(source.KeyFeatures.Where(f => !target.KeyFeatures.Contains(f)));
        if (source.Constraints is { Count: > 0 })
            target.Constraints.AddRange(source.Constraints.Where(c => !target.Constraints.Contains(c)));
        if (source.Assumptions is { Count: > 0 })
            target.Assumptions.AddRange(source.Assumptions.Where(a => !target.Assumptions.Contains(a)));
    }

    /// <summary>
    /// Persists the current interview session state to evidence for resumability.
    /// </summary>
    private async Task PersistSessionStateAsync(InterviewSession session, CancellationToken ct)
    {
        try
        {
            var runId = session.RunId ?? "unknown";
            var evidenceDir = Path.Combine(
                _workspace.AosRootPath, "evidence", "runs", runId, "artifacts");

            if (!Directory.Exists(evidenceDir))
                Directory.CreateDirectory(evidenceDir);

            var sessionStatePath = Path.Combine(evidenceDir, "interview.session.json");
            var sessionJson = JsonSerializer.Serialize(new
            {
                sessionId = session.SessionId,
                state = session.State.ToString(),
                currentPhase = session.CurrentPhase.ToString(),
                startedAt = session.StartedAt,
                qaPairCount = session.QAPairs.Count,
                draft = session.ProjectDraft,
                qaPairs = session.QAPairs.Select(qa => new
                {
                    question = qa.Question,
                    answer = qa.Answer,
                    phase = qa.Phase.ToString(),
                    timestamp = qa.Timestamp
                })
            }, JsonOptions);

            await File.WriteAllTextAsync(sessionStatePath, sessionJson, ct);
        }
        catch
        {
            // Session persistence is best-effort — do not fail the interview if evidence write fails
        }
    }

    private async Task<IReadOnlyList<InterviewArtifact>> WriteEvidenceAsync(
        InterviewSession session,
        ProjectSpecification spec,
        string specJson,
        CancellationToken ct)
    {
        var artifacts = new List<InterviewArtifact>();

        // Write transcript
        var transcriptPath = await _evidenceWriter.WriteTranscriptAsync(session, ct);
        artifacts.Add(new InterviewArtifact
        {
            ArtifactId = "interview-transcript",
            FileName = "interview.transcript.md",
            FilePath = transcriptPath,
            ContentType = "text/markdown",
            Content = GenerateTranscriptMarkdown(session)
        });

        // Write summary
        var summaryPath = await _evidenceWriter.WriteSummaryAsync(session, spec, ct);
        artifacts.Add(new InterviewArtifact
        {
            ArtifactId = "interview-summary",
            FileName = "interview.summary.md",
            FilePath = summaryPath,
            ContentType = "text/markdown",
            Content = GenerateSummaryMarkdown(session, spec)
        });

        return artifacts;
    }

    private async Task WriteProjectSpecAsync(ProjectSpecification spec, string specJson, CancellationToken ct)
    {
        // Get the workspace path for the project spec
        var specPath = _workspace.GetAbsolutePathForArtifactId("project");
        var specDir = Path.GetDirectoryName(specPath);

        if (!string.IsNullOrEmpty(specDir) && !Directory.Exists(specDir))
        {
            Directory.CreateDirectory(specDir);
        }

        // Write with deterministic formatting (already handled by SerializeToJson)
        await File.WriteAllTextAsync(specPath, specJson, ct);
    }

    private static string GenerateTranscriptMarkdown(InterviewSession session)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Interview Transcript");
        builder.AppendLine();
        builder.AppendLine($"- **Session ID:** {session.SessionId}");
        builder.AppendLine($"- **Started:** {session.StartedAt:yyyy-MM-dd HH:mm:ss UTC}");
        if (session.CompletedAt.HasValue)
        {
            builder.AppendLine($"- **Completed:** {session.CompletedAt.Value:yyyy-MM-dd HH:mm:ss UTC}");
        }
        builder.AppendLine($"- **Run ID:** {session.RunId ?? "N/A"}");
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine();

        foreach (var qa in session.QAPairs)
        {
            builder.AppendLine($"## {qa.Phase} Phase");
            builder.AppendLine();
            builder.AppendLine($"**Q:** {qa.Question}");
            builder.AppendLine();
            builder.AppendLine($"**A:** {qa.Answer}");
            builder.AppendLine();
            builder.AppendLine("---");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string GenerateSummaryMarkdown(InterviewSession session, ProjectSpecification spec)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Interview Summary");
        builder.AppendLine();
        builder.AppendLine($"- **Session ID:** {session.SessionId}");
        builder.AppendLine($"- **Project:** {spec.Name}");
        builder.AppendLine($"- **Completed:** {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        builder.AppendLine();

        builder.AppendLine("## Key Decisions");
        builder.AppendLine();
        builder.AppendLine($"- **Technology Stack:** {spec.TechnologyStack ?? "Not specified"}");
        builder.AppendLine($"- **Target Audience:** {spec.TargetAudience ?? "Not specified"}");
        builder.AppendLine();

        builder.AppendLine("## Project Goals");
        builder.AppendLine();
        foreach (var goal in spec.Goals)
        {
            builder.AppendLine($"- {goal}");
        }
        builder.AppendLine();

        builder.AppendLine("## Key Features");
        builder.AppendLine();
        foreach (var feature in spec.KeyFeatures)
        {
            builder.AppendLine($"- {feature}");
        }
        builder.AppendLine();

        if (spec.Constraints.Count > 0)
        {
            builder.AppendLine("## Constraints");
            builder.AppendLine();
            foreach (var constraint in spec.Constraints)
            {
                builder.AppendLine($"- {constraint}");
            }
            builder.AppendLine();
        }

        if (spec.Assumptions.Count > 0)
        {
            builder.AppendLine("## Assumptions");
            builder.AppendLine();
            foreach (var assumption in spec.Assumptions)
            {
                builder.AppendLine($"- {assumption}");
            }
            builder.AppendLine();
        }

        builder.AppendLine("## Requirements Summary");
        builder.AppendLine();
        builder.AppendLine(spec.Description);
        builder.AppendLine();

        return builder.ToString();
    }

    /// <summary>
    /// Internal DTO for parsing the structured JSON response from the LLM.
    /// </summary>
    private sealed class PhaseResponse
    {
        [JsonPropertyName("qaPairs")]
        public List<QAPairDto>? QAPairs { get; set; }

        [JsonPropertyName("draft")]
        public DraftData? Draft { get; set; }

        [JsonPropertyName("draftUpdates")]
        public DraftData? DraftUpdates { get; set; }

        [JsonPropertyName("confirmedDraft")]
        public DraftData? ConfirmedDraft { get; set; }
    }

    private sealed class QAPairDto
    {
        [JsonPropertyName("question")]
        public string? Question { get; set; }

        [JsonPropertyName("answer")]
        public string? Answer { get; set; }
    }

    private sealed class DraftData
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("technologyStack")]
        public string? TechnologyStack { get; set; }

        [JsonPropertyName("goals")]
        public List<string>? Goals { get; set; }

        [JsonPropertyName("targetAudience")]
        public string? TargetAudience { get; set; }

        [JsonPropertyName("keyFeatures")]
        public List<string>? KeyFeatures { get; set; }

        [JsonPropertyName("constraints")]
        public List<string>? Constraints { get; set; }

        [JsonPropertyName("assumptions")]
        public List<string>? Assumptions { get; set; }
    }
}

#pragma warning restore CS0618
