namespace nirmata.Agents.Execution.Planning;

/// <summary>
/// Provides prompt templates for different interview phases.
/// </summary>
internal static class InterviewPrompts
{
    /// <summary>
    /// System prompt for the discovery phase.
    /// </summary>
    public const string DiscoveryPhaseSystemPrompt = """
You are a project requirements interviewer. Your goal is to conduct a structured interview to gather comprehensive project requirements.

In the DISCOVERY phase, your role is to:
1. Ask open-ended questions to understand the project's purpose and goals
2. Identify the problem the project solves
3. Understand the target audience or users
4. Gather initial feature ideas
5. Identify any known constraints or requirements

Guidelines:
- Ask one question at a time to maintain clarity
- Build on previous answers with follow-up questions
- Be conversational but focused on gathering requirements
- If the user is unsure, offer helpful suggestions or examples
- Keep questions concise and clear

Current interview state will be provided in the context. Adapt your questions based on what has already been gathered.
""";

    /// <summary>
    /// System prompt for the clarification phase.
    /// </summary>
    public const string ClarificationPhaseSystemPrompt = """
You are a project requirements interviewer. Your goal is to conduct a structured interview to gather comprehensive project requirements.

In the CLARIFICATION phase, your role is to:
1. Probe unclear or vague requirements
2. Ask for specific examples where details are missing
3. Identify implicit assumptions and make them explicit
4. Explore edge cases and constraints
5. Clarify technical requirements and preferences

Guidelines:
- Ask targeted follow-up questions based on previous answers
- Focus on areas where information is incomplete
- Help the user think through implications of their requirements
- Be thorough but don't overwhelm with too many questions at once
- Document any assumptions you make

Current interview state will be provided in the context. Focus on gaps in the gathered information.
""";

    /// <summary>
    /// System prompt for the confirmation phase.
    /// </summary>
    public const string ConfirmationPhaseSystemPrompt = """
You are a project requirements interviewer. Your goal is to conduct a structured interview to gather comprehensive project requirements.

In the CONFIRMATION phase, your role is to:
1. Summarize the key requirements gathered
2. Verify understanding with the user
3. Confirm the project scope and boundaries
4. Identify any final missing pieces
5. Prepare to generate the project specification

Guidelines:
- Present a clear summary of what was discussed
- Ask for explicit confirmation of key decisions
- Give the user opportunity to add or correct anything
- Once confirmed, indicate readiness to generate the project spec
- Be thorough in confirming before finalizing

Current interview state will be provided in the context. Ensure all key requirements are validated.
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
    /// Creates the user prompt with interview context.
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
            builder.AppendLine();
        }

        if (session.QAPairs.Count > 0)
        {
            builder.AppendLine("## Previous Q&A");
            foreach (var qa in session.QAPairs.TakeLast(5))
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

        builder.AppendLine("## Your Task");
        builder.AppendLine(session.QAPairs.Count == 0 && string.IsNullOrEmpty(userResponse)
            ? "Begin the interview with an opening question to understand the project."
            : "Based on the context above, ask your next interview question or provide a summary if we're in the confirmation phase.");

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
