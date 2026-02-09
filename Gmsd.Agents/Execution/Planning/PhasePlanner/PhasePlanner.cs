using System.Text;
using System.Text.Json;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Aos.Public;

namespace Gmsd.Agents.Execution.Planning.PhasePlanner;

/// <summary>
/// Default implementation of the phase planner using LLM-based task decomposition.
/// </summary>
public sealed class PhasePlanner : IPhasePlanner
{
    private readonly ILlmProvider _llmProvider;
    private readonly IWorkspace _workspace;
    private readonly SpecStore _specStore;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PhasePlanner"/> class.
    /// </summary>
    public PhasePlanner(ILlmProvider llmProvider, IWorkspace workspace)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _specStore = SpecStore.FromWorkspace(workspace);
    }

    /// <inheritdoc />
    public async Task<TaskPlan> CreateTaskPlanAsync(PhaseBrief brief, string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(brief);
        ArgumentException.ThrowIfNullOrEmpty(runId);

        var startedAt = DateTimeOffset.UtcNow;
        var planId = $"PLAN-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";

        try
        {
            // Generate task decomposition via LLM
            var llmTasks = await GenerateTasksViaLlmAsync(brief, ct);

            // Validate task count (enforce 2-3 task limit)
            var validationErrors = new List<string>();

            if (llmTasks.Count == 0)
            {
                validationErrors.Add("No tasks were generated from the phase brief.");
            }
            else if (llmTasks.Count > 3)
            {
                validationErrors.Add($"Too many tasks generated ({llmTasks.Count}). Maximum allowed is 3.");
            }
            else if (llmTasks.Count < 2)
            {
                // Warn but allow single task for simple phases
                // validationErrors.Add($"Too few tasks generated ({llmTasks.Count}). Minimum recommended is 2.");
            }

            // Validate each task has required fields
            foreach (var task in llmTasks)
            {
                if (string.IsNullOrEmpty(task.Title))
                {
                    validationErrors.Add("Task missing required field: Title");
                }
                if (string.IsNullOrEmpty(task.Description))
                {
                    validationErrors.Add("Task missing required field: Description");
                }
                if (task.FileScopes.Count == 0)
                {
                    validationErrors.Add($"Task '{task.Title}' has no file scopes defined.");
                }
            }

            // If validation failed, return early
            if (validationErrors.Count > 0)
            {
                return new TaskPlan
                {
                    PlanId = planId,
                    PhaseId = brief.PhaseId,
                    RunId = runId,
                    Tasks = Array.Empty<TaskSpecification>(),
                    IsValid = false,
                    ValidationErrors = validationErrors.AsReadOnly(),
                    CreatedAt = startedAt,
                    Summary = "Task planning failed validation."
                };
            }

            // Assign task IDs and build specifications
            var taskSpecifications = new List<TaskSpecification>();
            var sequence = 1;

            foreach (var llmTask in llmTasks)
            {
                var taskId = GenerateTaskId(brief.PhaseId, sequence);

                var taskSpec = new TaskSpecification
                {
                    TaskId = taskId,
                    PhaseId = brief.PhaseId,
                    Title = llmTask.Title,
                    Description = llmTask.Description,
                    SequenceOrder = sequence++,
                    FileScopes = llmTask.FileScopes?.Count > 0 ? llmTask.FileScopes : new List<FileScope>(),
                    VerificationSteps = llmTask.VerificationSteps?.Count > 0 ? llmTask.VerificationSteps : new List<VerificationStep>(),
                    AcceptanceCriteria = llmTask.AcceptanceCriteria?.Count > 0 ? llmTask.AcceptanceCriteria : new List<string>(),
                    Complexity = llmTask.Complexity ?? "medium",
                    Dependencies = llmTask.Dependencies?.Count > 0 ? llmTask.Dependencies : new List<string>(),
                    TaskJsonPath = $".aos/spec/tasks/{taskId}/task.json"
                };

                taskSpecifications.Add(taskSpec);
            }

            // Write task specs and plan
            await WriteTaskSpecsAsync(taskSpecifications, ct);
            var planPath = await WritePlanJsonAsync(planId, brief.PhaseId, taskSpecifications, runId, ct);
            await WriteLinksJsonAsync(brief.PhaseId, taskSpecifications, ct);

            return new TaskPlan
            {
                PlanId = planId,
                PhaseId = brief.PhaseId,
                RunId = runId,
                Tasks = taskSpecifications.AsReadOnly(),
                IsValid = true,
                ValidationErrors = Array.Empty<string>(),
                CreatedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                PlanJsonPath = planPath,
                Summary = $"Successfully created {taskSpecifications.Count} task(s) for phase {brief.PhaseId}"
            };
        }
        catch (Exception ex)
        {
            return new TaskPlan
            {
                PlanId = planId,
                PhaseId = brief.PhaseId,
                RunId = runId,
                Tasks = Array.Empty<TaskSpecification>(),
                IsValid = false,
                ValidationErrors = new[] { $"Planning failed: {ex.Message}" }.AsReadOnly(),
                CreatedAt = startedAt,
                Summary = "Task planning failed with exception."
            };
        }
    }

    private async Task<List<LlmTask>> GenerateTasksViaLlmAsync(PhaseBrief brief, CancellationToken ct)
    {
        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(brief);

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
                Temperature = 0.3f,
                MaxTokens = 4000
            }
        };

        try
        {
            var result = await _llmProvider.CompleteAsync(request, ct);
            var content = result.Message.Content ?? "{\"tasks\": []}";

            var response = JsonSerializer.Deserialize<TaskDecompositionResponse>(content, JsonOptions);
            return response?.Tasks ?? new List<LlmTask>();
        }
        catch
        {
            // Fallback to simple task generation if LLM fails
            return new List<LlmTask>
            {
                new()
                {
                    Title = $"Implement {brief.PhaseName}",
                    Description = brief.Description,
                    FileScopes = new List<FileScope>(),
                    VerificationSteps = new List<VerificationStep>
                    {
                        new() { VerificationType = "compile", Description = "Code compiles without errors" },
                        new() { VerificationType = "test", Description = "All tests pass" }
                    },
                    AcceptanceCriteria = new List<string> { "Feature works as described" },
                    Complexity = "medium"
                }
            };
        }
    }

    private static string BuildSystemPrompt()
    {
        return @"You are a task planning assistant that decomposes software development phases into atomic, verifiable tasks.

Your goal is to break down a phase into 2-3 focused tasks that can be completed sequentially or in parallel.

Each task must include:
1. A clear, actionable title
2. A detailed description of what needs to be done
3. File scopes indicating which files will be read, modified, or created
4. Verification steps to confirm completion
5. Acceptance criteria that define when the task is done

Constraints:
- Generate between 2-3 tasks maximum
- Each task should be atomic and completable in one session
- File scopes must reference actual files from the context
- Verification steps should be specific and testable

Respond with valid JSON in this format:
{
  ""tasks"": [
    {
      ""title"": ""Task title"",
      ""description"": ""Detailed description"",
      ""fileScopes"": [
        {
          ""relativePath"": ""path/to/file"",
          ""scopeType"": ""read|write|create|modify"",
          ""description"": ""What to do with this file"",
          ""mustExist"": true|false
        }
      ],
      ""verificationSteps"": [
        {
          ""verificationType"": ""compile|test|lint|manual_review"",
          ""description"": ""What to verify"",
          ""expectedOutcome"": ""Expected result""
        }
      ],
      ""acceptanceCriteria"": [""Criterion 1"", ""Criterion 2""],
      ""complexity"": ""low|medium|high"",
      ""dependencies"": []
    }
  ]
}";
    }

    private static string BuildUserPrompt(PhaseBrief brief)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Phase Brief");
        builder.AppendLine();
        builder.AppendLine($"Phase: {brief.PhaseName} ({brief.PhaseId})");
        builder.AppendLine($"Description: {brief.Description}");
        builder.AppendLine($"Milestone: {brief.MilestoneId}");
        builder.AppendLine();

        if (brief.Goals.Count > 0)
        {
            builder.AppendLine("## Goals");
            foreach (var goal in brief.Goals)
            {
                builder.AppendLine($"- {goal}");
            }
            builder.AppendLine();
        }

        if (brief.Constraints.Count > 0)
        {
            builder.AppendLine("## Constraints");
            foreach (var constraint in brief.Constraints)
            {
                builder.AppendLine($"- {constraint}");
            }
            builder.AppendLine();
        }

        if (brief.Scope.InScope.Count > 0 || brief.Scope.OutOfScope.Count > 0)
        {
            builder.AppendLine("## Scope");
            if (brief.Scope.InScope.Count > 0)
            {
                builder.AppendLine("In scope:");
                foreach (var item in brief.Scope.InScope)
                {
                    builder.AppendLine($"  - {item}");
                }
            }
            if (brief.Scope.OutOfScope.Count > 0)
            {
                builder.AppendLine("Out of scope:");
                foreach (var item in brief.Scope.OutOfScope)
                {
                    builder.AppendLine($"  - {item}");
                }
            }
            builder.AppendLine();
        }

        if (brief.RelevantFiles.Count > 0)
        {
            builder.AppendLine("## Relevant Files");
            foreach (var file in brief.RelevantFiles.Take(15))
            {
                builder.AppendLine($"- {file.RelativePath} ({file.FileType})");
                if (!string.IsNullOrEmpty(file.Relevance))
                {
                    builder.AppendLine($"  Notes: {file.Relevance}");
                }
            }
            builder.AppendLine();
        }

        if (!string.IsNullOrEmpty(brief.ProjectContext.TechnologyStack))
        {
            builder.AppendLine($"## Technology Stack\n{brief.ProjectContext.TechnologyStack}\n");
        }

        builder.AppendLine("Please decompose this phase into 2-3 atomic tasks with detailed file scopes and verification steps.");

        return builder.ToString();
    }

    private async Task WriteTaskSpecsAsync(List<TaskSpecification> tasks, CancellationToken ct)
    {
        foreach (var task in tasks)
        {
            var taskDoc = new
            {
                schema = "gmsd:aos:schema:task:v1",
                taskId = task.TaskId,
                phaseId = task.PhaseId,
                title = task.Title,
                description = task.Description,
                sequenceOrder = task.SequenceOrder,
                complexity = task.Complexity,
                dependencies = task.Dependencies,
                acceptanceCriteria = task.AcceptanceCriteria,
                status = "pending"
            };

            var taskJson = JsonSerializer.SerializeToElement(taskDoc, JsonOptions);
            _specStore.Inner.WriteTaskOverwrite(task.TaskId, taskJson);

            // Write plan.json for the task
            var planDoc = new
            {
                schema = "gmsd:aos:schema:task-plan:v1",
                taskId = task.TaskId,
                phaseId = task.PhaseId,
                fileScopes = task.FileScopes.Select(fs => new
                {
                    relativePath = fs.RelativePath,
                    scopeType = fs.ScopeType,
                    description = fs.Description,
                    mustExist = fs.MustExist
                }),
                verificationSteps = task.VerificationSteps.Select(vs => new
                {
                    verificationType = vs.VerificationType,
                    description = vs.Description,
                    expectedOutcome = vs.ExpectedOutcome,
                    command = vs.Command
                })
            };

            var planJson = JsonSerializer.SerializeToElement(planDoc, JsonOptions);
            _specStore.Inner.WriteTaskPlanOverwrite(task.TaskId, planJson);
        }
    }

    private async Task<string> WritePlanJsonAsync(string planId, string phaseId, List<TaskSpecification> tasks, string runId, CancellationToken ct)
    {
        var planDoc = new
        {
            schema = "gmsd:aos:schema:phase-plan:v1",
            planId,
            phaseId,
            runId,
            taskCount = tasks.Count,
            tasks = tasks.Select(t => new
            {
                taskId = t.TaskId,
                title = t.Title,
                description = t.Description,
                sequenceOrder = t.SequenceOrder,
                taskPath = t.TaskJsonPath
            }),
            createdAt = DateTimeOffset.UtcNow.ToString("O")
        };

        var planJson = JsonSerializer.Serialize(planDoc, JsonOptions);
        var planPath = Path.Combine(_workspace.AosRootPath, "spec", "phases", phaseId, "plan.json");

        var planDir = Path.GetDirectoryName(planPath);
        if (!string.IsNullOrEmpty(planDir) && !Directory.Exists(planDir))
        {
            Directory.CreateDirectory(planDir);
        }

        await File.WriteAllTextAsync(planPath, planJson, ct);
        return planPath;
    }

    private async Task WriteLinksJsonAsync(string phaseId, List<TaskSpecification> tasks, CancellationToken ct)
    {
        var linksDoc = new
        {
            schema = "gmsd:aos:schema:phase-links:v1",
            phaseId,
            taskLinks = tasks.Select(t => new
            {
                taskId = t.TaskId,
                links = new[]
                {
                    new { type = "task-spec", path = $".aos/spec/tasks/{t.TaskId}/task.json" },
                    new { type = "task-plan", path = $".aos/spec/tasks/{t.TaskId}/plan.json" }
                }
            }),
            updatedAt = DateTimeOffset.UtcNow.ToString("O")
        };

        var linksJson = JsonSerializer.Serialize(linksDoc, JsonOptions);
        var linksPath = Path.Combine(_workspace.AosRootPath, "spec", "phases", phaseId, "links.json");

        var linksDir = Path.GetDirectoryName(linksPath);
        if (!string.IsNullOrEmpty(linksDir) && !Directory.Exists(linksDir))
        {
            Directory.CreateDirectory(linksDir);
        }

        await File.WriteAllTextAsync(linksPath, linksJson, ct);
    }

    private static string GenerateTaskId(string phaseId, int sequence)
    {
        // Generate task ID in format TSK-XXXX based on phase and sequence
        var phasePrefix = phaseId.Replace("PH-", "");
        return $"TSK-{phasePrefix}-{sequence:D2}";
    }
}

// Internal DTOs for LLM interaction
internal record TaskDecompositionResponse
{
    public List<LlmTask> Tasks { get; init; } = new();
}

internal record LlmTask
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<FileScope> FileScopes { get; init; } = new();
    public List<VerificationStep> VerificationSteps { get; init; } = new();
    public List<string> AcceptanceCriteria { get; init; } = new();
    public string Complexity { get; init; } = "medium";
    public List<string> Dependencies { get; init; } = new();
}
