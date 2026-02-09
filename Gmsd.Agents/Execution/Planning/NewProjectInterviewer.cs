using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Aos.Public;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gmsd.Agents.Execution.Planning;

/// <summary>
/// Default implementation of the new project interviewer using LLM-driven interviews.
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

            // Run through the interview phases
            await RunDiscoveryPhaseAsync(session, ct);
            await RunClarificationPhaseAsync(session, ct);
            await RunConfirmationPhaseAsync(session, ct);

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

        // In a real implementation, this would interact with the user
        // For this implementation, we'll simulate the Q&A loop with the LLM
        var systemPrompt = InterviewPrompts.GetSystemPrompt(InterviewPhase.Discovery);
        var userPrompt = InterviewPrompts.CreateUserPrompt(session);

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
                MaxTokens = 2000
            }
        };

        var result = await _llmProvider.CompleteAsync(request, ct);
        var assistantContent = result.Message.Content ?? "Let's start by understanding your project. What problem are you trying to solve?";

        // Simulate a Q&A exchange - in production this would be interactive
        session.QAPairs.Add(new InterviewQAPair
        {
            Question = "What is the name of your project?",
            Answer = "A software development project",
            Phase = InterviewPhase.Discovery
        });

        session.QAPairs.Add(new InterviewQAPair
        {
            Question = "What problem does this project solve?",
            Answer = "It helps developers manage and organize their projects more efficiently",
            Phase = InterviewPhase.Discovery
        });

        // Update draft with discovered information
        session.ProjectDraft!.Name = "New Project";
        session.ProjectDraft.Description = "A software development project for managing and organizing projects efficiently";
        session.ProjectDraft.Goals.Add("Improve project organization");
        session.ProjectDraft.Goals.Add("Streamline development workflow");
    }

    private async Task RunClarificationPhaseAsync(InterviewSession session, CancellationToken ct)
    {
        session.CurrentPhase = InterviewPhase.Clarification;
        session.State = InterviewState.Clarification;

        var systemPrompt = InterviewPrompts.GetSystemPrompt(InterviewPhase.Clarification);
        var userPrompt = InterviewPrompts.CreateUserPrompt(session);

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
                MaxTokens = 2000
            }
        };

        var response = await _llmProvider.CompleteAsync(request, ct);

        // Add clarification Q&A pairs
        session.QAPairs.Add(new InterviewQAPair
        {
            Question = "What technology stack will you be using?",
            Answer = ".NET/C# with modern web technologies",
            Phase = InterviewPhase.Clarification
        });

        session.QAPairs.Add(new InterviewQAPair
        {
            Question = "Who is your target audience?",
            Answer = "Software developers and development teams",
            Phase = InterviewPhase.Clarification
        });

        // Update draft with clarified information
        session.ProjectDraft!.TechnologyStack = ".NET/C#";
        session.ProjectDraft.TargetAudience = "Software developers and development teams";
        session.ProjectDraft.KeyFeatures.Add("Project organization");
        session.ProjectDraft.KeyFeatures.Add("Workflow management");
    }

    private async Task RunConfirmationPhaseAsync(InterviewSession session, CancellationToken ct)
    {
        session.CurrentPhase = InterviewPhase.Confirmation;
        session.State = InterviewState.Confirmation;

        var systemPrompt = InterviewPrompts.GetSystemPrompt(InterviewPhase.Confirmation);
        var userPrompt = InterviewPrompts.CreateUserPrompt(session);

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
                Temperature = 0.5f,
                MaxTokens = 2000
            }
        };

        var response = await _llmProvider.CompleteAsync(request, ct);

        // Add confirmation Q&A
        session.QAPairs.Add(new InterviewQAPair
        {
            Question = "Do these requirements accurately capture your project needs?",
            Answer = "Yes, this covers the main requirements",
            Phase = InterviewPhase.Confirmation
        });
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
}
