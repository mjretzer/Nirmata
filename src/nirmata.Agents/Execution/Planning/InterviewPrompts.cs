namespace nirmata.Agents.Execution.Planning;

/// <summary>
/// Provides prompt templates for different interview phases.
/// </summary>
internal static class InterviewPrompts
{
    /// <summary>
    /// System prompt for the discovery phase — returns structured JSON.
    /// </summary>
    public const string DiscoveryPhaseSystemPrompt = """
You are a project requirements interviewer. Analyze the provided context and conduct a thorough discovery of the project's purpose, goals, constraints, and scope.

Produce a JSON object with this exact structure (no markdown fencing):
{
  "qaPairs": [
    { "question": "What problem does this project solve?", "answer": "..." },
    { "question": "Who is the target audience?", "answer": "..." }
  ],
  "draft": {
    "name": "Project name inferred from context",
    "description": "Concise project description",
    "technologyStack": "Primary tech stack or null",
    "goals": ["goal 1", "goal 2"],
    "targetAudience": "Who will use this or null",
    "keyFeatures": ["feature 1"],
    "constraints": ["constraint 1"],
    "assumptions": ["assumption 1"]
  }
}

Rules:
- Generate 3-5 Q&A pairs covering: purpose/problem, target users, goals, initial features, known constraints.
- For each question, synthesize the best answer from the provided context (codebase intelligence, user input, project hints).
- The "draft" object must be populated from the synthesized answers — never leave required fields empty.
- If context is sparse, make reasonable assumptions and list them explicitly in "assumptions".
- Output ONLY the JSON object. No markdown, no explanation.
""";

    /// <summary>
    /// System prompt for the clarification phase — returns structured JSON.
    /// </summary>
    public const string ClarificationPhaseSystemPrompt = """
You are a project requirements interviewer in the CLARIFICATION phase. You have discovery results and must probe for gaps, resolve ambiguities, and make implicit assumptions explicit.

Produce a JSON object with this exact structure (no markdown fencing):
{
  "qaPairs": [
    { "question": "Clarification question...", "answer": "Synthesized answer..." }
  ],
  "draftUpdates": {
    "technologyStack": "refined value or null to keep current",
    "goals": ["additional goals to append"],
    "keyFeatures": ["additional features to append"],
    "constraints": ["additional constraints to append"],
    "assumptions": ["additional assumptions to append"]
  }
}

Rules:
- Generate 2-4 Q&A pairs that probe unclear areas, missing details, and implicit assumptions from the discovery phase.
- "draftUpdates" contains ONLY new items to append (not the full list). Use null or empty arrays for fields that need no updates.
- Focus on: technical requirements, integration boundaries, security/compliance, timeline, non-goals, and edge cases.
- Output ONLY the JSON object. No markdown, no explanation.
""";

    /// <summary>
    /// System prompt for the confirmation phase — returns structured JSON.
    /// </summary>
    public const string ConfirmationPhaseSystemPrompt = """
You are a project requirements interviewer in the CONFIRMATION phase. Review all gathered information, resolve any remaining conflicts, and produce the final confirmed project specification.

Produce a JSON object with this exact structure (no markdown fencing):
{
  "qaPairs": [
    { "question": "confirmation question...", "answer": "confirmed answer..." }
  ],
  "confirmedDraft": {
    "name": "Final project name",
    "description": "Final comprehensive project description",
    "technologyStack": "Confirmed technology stack",
    "goals": ["all confirmed goals"],
    "targetAudience": "Confirmed target audience",
    "keyFeatures": ["all confirmed features"],
    "constraints": ["all confirmed constraints"],
    "assumptions": ["all confirmed assumptions"]
  }
}

Rules:
- Generate 1-3 Q&A pairs that confirm the key decisions and boundaries.
- "confirmedDraft" is the FINAL, COMPLETE specification. Include ALL goals, features, constraints, and assumptions — not just new ones.
- Resolve any conflicts between discovery and clarification answers.
- The confirmed draft will be used directly to generate the canonical project.json.
- Output ONLY the JSON object. No markdown, no explanation.
""";

    /// <summary>
    /// Gets the appropriate system prompt for the given interview phase.
    /// </summary>
    public static string GetSystemPrompt(InterviewPhase phase) => phase switch
    {
        InterviewPhase.Discovery => DiscoveryPhaseSystemPrompt,
        InterviewPhase.Clarification => ClarificationPhaseSystemPrompt,
        InterviewPhase.Confirmation => ConfirmationPhaseSystemPrompt,
        _ => DiscoveryPhaseSystemPrompt
    };

    /// <summary>
    /// Creates the user prompt with interview context for the given phase.
    /// </summary>
    public static string CreateUserPrompt(InterviewSession session, string? userResponse = null)
    {
        var builder = new System.Text.StringBuilder();

        builder.AppendLine("## Interview Context");
        builder.AppendLine($"- Session ID: {session.SessionId}");
        builder.AppendLine($"- Current Phase: {session.CurrentPhase}");
        builder.AppendLine($"- State: {session.State}");
        builder.AppendLine();

        if (session.ProjectDraft != null)
        {
            builder.AppendLine("## Current Draft Information");
            if (!string.IsNullOrEmpty(session.ProjectDraft.Name))
                builder.AppendLine($"- Project Name: {session.ProjectDraft.Name}");
            if (!string.IsNullOrEmpty(session.ProjectDraft.Description))
                builder.AppendLine($"- Description: {session.ProjectDraft.Description}");
            if (!string.IsNullOrEmpty(session.ProjectDraft.TechnologyStack))
                builder.AppendLine($"- Technology Stack: {session.ProjectDraft.TechnologyStack}");
            if (session.ProjectDraft.Goals.Count > 0)
                builder.AppendLine($"- Goals: {string.Join(", ", session.ProjectDraft.Goals)}");
            if (!string.IsNullOrEmpty(session.ProjectDraft.TargetAudience))
                builder.AppendLine($"- Target Audience: {session.ProjectDraft.TargetAudience}");
            if (session.ProjectDraft.KeyFeatures.Count > 0)
                builder.AppendLine($"- Key Features: {string.Join(", ", session.ProjectDraft.KeyFeatures)}");
            if (session.ProjectDraft.Constraints.Count > 0)
                builder.AppendLine($"- Constraints: {string.Join(", ", session.ProjectDraft.Constraints)}");
            if (session.ProjectDraft.Assumptions.Count > 0)
                builder.AppendLine($"- Assumptions: {string.Join(", ", session.ProjectDraft.Assumptions)}");
            builder.AppendLine();
        }

        if (session.QAPairs.Count > 0)
        {
            builder.AppendLine("## Previous Q&A");
            foreach (var qa in session.QAPairs)
            {
                builder.AppendLine($"Q ({qa.Phase}): {qa.Question}");
                builder.AppendLine($"A: {qa.Answer}");
                builder.AppendLine();
            }
        }

        if (!string.IsNullOrEmpty(userResponse))
        {
            builder.AppendLine("## User's Latest Response");
            builder.AppendLine(userResponse);
            builder.AppendLine();
        }

        if (session.ContextData.Count > 0)
        {
            builder.AppendLine("## Additional Context");
            foreach (var (key, value) in session.ContextData)
            {
                builder.AppendLine($"- {key}: {value}");
            }
            builder.AppendLine();
        }

        builder.AppendLine("## Your Task");
        builder.AppendLine($"Conduct the {session.CurrentPhase} phase of the interview. Return the structured JSON output as specified in the system prompt.");

        return builder.ToString();
    }

    /// <summary>
    /// Prompt for generating the project specification JSON.
    /// </summary>
    public const string ProjectSpecGenerationPrompt = """
You are a project specification generator. Based on the interview transcript provided, generate a complete project.json specification.

The output must be valid JSON conforming to the schema "nirmata:aos:schema:project:v1" with these fields:
{
    "schema": "nirmata:aos:schema:project:v1",
    "name": "project name",
    "description": "detailed project description",
    "technologyStack": "primary technologies/languages",
    "goals": ["goal 1", "goal 2", ...],
    "targetAudience": "who will use this",
    "keyFeatures": ["feature 1", "feature 2", ...],
    "constraints": ["constraint 1", "constraint 2", ...],
    "assumptions": ["assumption 1", "assumption 2", ...],
    "metadata": {}
}

Guidelines:
- Infer reasonable values for any missing fields based on context
- Be specific and actionable in descriptions
- Include all relevant goals and features identified
- List constraints and assumptions explicitly
- Ensure valid JSON output with no markdown formatting
- Use only the exact field names shown above
""";
}
