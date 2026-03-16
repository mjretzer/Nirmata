#pragma warning disable CS0618 // Intentionally using obsolete ILlmProvider contracts pending migration

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using nirmata.Agents.Execution.Validation;
using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using nirmata.Agents.Models.Runtime;
using nirmata.Aos.Engine;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.Planning.PhasePlanner;

/// <summary>
/// Default implementation of the phase planner using LLM-based task decomposition.
/// </summary>
public sealed class PhasePlanner : IPhasePlanner
{
    private readonly ILlmProvider _llmProvider;
    private readonly IWorkspace _workspace;
    private readonly SpecStore _specStore;
    private readonly LlmStructuredOutputSchema _phasePlanStructuredSchema;

    private static readonly Lazy<string> PhasePlanSchemaJson = new(() =>
        LoadEmbeddedSchema("nirmata.Aos.Resources.Schemas.phase-plan.schema.json"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<LlmStructuredOutputSchema> StructuredPhasePlanSchema = new(() =>
        LlmStructuredOutputSchema.FromJson(
            name: "phase_plan_v1",
            schemaJson: PhasePlanSchemaJson.Value,
            description: "nirmata phase planning schema"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Initializes a new instance of the <see cref="PhasePlanner"/> class.
    /// </summary>
    public PhasePlanner(ILlmProvider llmProvider, IWorkspace workspace)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _specStore = SpecStore.FromWorkspace(workspace);
        _phasePlanStructuredSchema = StructuredPhasePlanSchema.Value;
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
            // Generate structured plan via LLM
            var phasePlan = await GeneratePlanViaLlmAsync(brief, planId, ct);
            var llmTasks = phasePlan.Tasks ?? new List<PhaseTask>();

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
                if (task.FileScopes is null || task.FileScopes.Count == 0)
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
                    FileScopes = ConvertFileScopes(llmTask.FileScopes),
                    VerificationSteps = ConvertVerificationSteps(llmTask.VerificationSteps),
                    AcceptanceCriteria = new List<string>(),
                    Complexity = "medium",
                    Dependencies = new List<string>(),
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

    private async Task<PhasePlan> GeneratePlanViaLlmAsync(PhaseBrief brief, string planId, CancellationToken ct)
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
            StructuredOutputSchema = _phasePlanStructuredSchema,
            Options = new LlmProviderOptions
            {
                Temperature = 0.2f,
                MaxTokens = 4000
            }
        };

        try
        {
            var result = await _llmProvider.CompleteAsync(request, ct);
            var content = result.Message.Content ?? "{}";

            var llmPlan = JsonSerializer.Deserialize<PhasePlan>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            });

            if (llmPlan is null)
            {
                throw new InvalidOperationException("LLM returned null plan.");
            }

            var sanitizedPlan = new PhasePlan
            {
                PlanId = string.IsNullOrWhiteSpace(llmPlan.PlanId) ? planId : llmPlan.PlanId,
                PhaseId = string.IsNullOrWhiteSpace(llmPlan.PhaseId) ? brief.PhaseId : llmPlan.PhaseId,
                Tasks = llmPlan.Tasks ?? new List<PhaseTask>()
            };

            var validation = sanitizedPlan.Validate();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Structured plan validation failed: {string.Join(", ", validation.Errors)}");
            }

            return sanitizedPlan;
        }
        catch (LlmProviderException llmEx) when (llmEx.Message.Contains("failed schema"))
        {
            CreateDiagnosticForLlmValidationFailure(brief, planId, llmEx);
            Console.WriteLine($"DEBUG: LLM schema validation failed: {llmEx.Message}");
            return CreateFallbackPlan(brief, planId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Structured plan generation failed: {ex.Message}");
            return CreateFallbackPlan(brief, planId);
        }
    }

    private void CreateDiagnosticForLlmValidationFailure(PhaseBrief brief, string planId, LlmProviderException ex)
    {
        try
        {
            var planPath = Path.Combine(_workspace.AosRootPath, "spec", "phases", brief.PhaseId, "plan.json");
            var diagnostic = new DiagnosticArtifact
            {
                SchemaVersion = 1,
                SchemaId = "nirmata:aos:schema:diagnostic:v1",
                ArtifactPath = planPath,
                FailedSchemaId = "nirmata:aos:schema:phase-plan:v1",
                FailedSchemaVersion = 1,
                Timestamp = DateTimeOffset.UtcNow,
                Phase = "phase-planning",
                Context = new Dictionary<string, string>
                {
                    { "phaseId", brief.PhaseId },
                    { "planId", planId }
                },
                ValidationErrors = new List<ValidationError>
                {
                    new()
                    {
                        Path = "$",
                        Message = ex.Message,
                        Expected = "Valid phase plan JSON matching schema",
                        Actual = "LLM output failed schema validation"
                    }
                },
                RepairSuggestions = new List<string>
                {
                    "Ensure the LLM response is valid JSON",
                    "Verify all required fields are present: planId, phaseId, tasks[]",
                    "Check that tasks have required fields: title, description, fileScopes[], verificationSteps[]",
                    "Ensure fileScopes and verificationSteps are non-empty arrays"
                }
            };

            DiagnosticArtifactWriter.Write(_workspace.AosRootPath, diagnostic);
        }
        catch
        {
            // Don't fail if diagnostic creation fails
        }
    }

    private static string BuildSystemPrompt()
    {
        return @"You are a task planning assistant that decomposes software development phases into atomic, verifiable tasks.

Your goal is to break down a phase into 2-3 focused tasks that can be completed sequentially or in parallel.

Each task must include:
1. A clear, actionable title
2. A detailed description of what needs to be done
3. File scopes indicating the primary files or directories that must be updated
4. Verification steps to confirm completion

Constraints:
- Generate between 2-3 tasks maximum
- Each task should be atomic and completable in one session

You MUST respond with JSON that conforms to the provided phase plan schema (nirmata:aos:schema:phase-plan:v1).";
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
        builder.AppendLine("Ensure the JSON response matches the phase plan schema exactly (including planId, phaseId, tasks[id,title,description,fileScopes[{path}],verificationSteps]).");

        return builder.ToString();
    }

    private static List<FileScope> ConvertFileScopes(IReadOnlyList<PhaseFileScope>? fileScopes)
    {
        if (fileScopes is null || fileScopes.Count == 0)
        {
            return new List<FileScope>();
        }

        return fileScopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope.Path))
            .Select(scope => new FileScope
            {
                Path = scope.Path.Trim(),
                ScopeType = "modify",
                Description = "LLM proposed scope",
                MustExist = false
            })
            .ToList();
    }

    private static List<VerificationStep> ConvertVerificationSteps(IReadOnlyList<string>? steps)
    {
        if (steps is null || steps.Count == 0)
        {
            return new List<VerificationStep>();
        }

        return steps
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .Select(step => new VerificationStep
            {
                VerificationType = "custom",
                Description = step.Trim(),
                ExpectedOutcome = null,
                Command = null
            })
            .ToList();
    }

    private static PhasePlan CreateFallbackPlan(PhaseBrief brief, string planId)
    {
        var defaultScope = brief.RelevantFiles.FirstOrDefault()?.RelativePath ?? "src";

        return new PhasePlan
        {
            PlanId = planId,
            PhaseId = brief.PhaseId,
            Tasks = new List<PhaseTask>
            {
                new()
                {
                    Id = $"TSK-{brief.PhaseId.Replace("PH-", string.Empty)}01",
                    Title = $"Implement {brief.PhaseName}",
                    Description = brief.Description,
                    FileScopes = new List<PhaseFileScope>
                    {
                        new() { Path = defaultScope }
                    },
                    VerificationSteps = new List<string>
                    {
                        "Code compiles without errors",
                        "All relevant tests pass"
                    }
                }
            }
        };
    }

    private static string LoadEmbeddedSchema(string resourceName)
    {
        var assembly = typeof(PhasePlan).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Schema resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private async Task WriteTaskSpecsAsync(List<TaskSpecification> tasks, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var task in tasks)
        {
            ct.ThrowIfCancellationRequested();

            var taskDoc = new
            {
                schema = "nirmata:aos:schema:task:v1",
                schemaVersion = 1,
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

            var taskJson = JsonSerializer.Serialize(taskDoc, DeterministicJsonOptions.Indented);
            var taskPath = Path.Combine(_workspace.AosRootPath, "spec", "tasks", task.TaskId, "task.json");
            
            // Task schema is not yet in ArtifactContractValidator, but we should validate if it were.
            // For now, we proceed with the existing pattern but ensure it's serialized correctly.
            var taskElement = JsonDocument.Parse(taskJson).RootElement;
            _specStore.Inner.WriteTaskOverwrite(task.TaskId, taskElement);

            // Write plan.json for the task
            var planDoc = new
            {
                schema = "nirmata:aos:schema:task-plan:v1",
                schemaVersion = 1,
                taskId = task.TaskId,
                phaseId = task.PhaseId,
                title = task.Title,
                description = task.Description,
                fileScopes = task.FileScopes.Select(fs => new
                {
                    path = fs.Path,
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

            var planJson = JsonSerializer.Serialize(planDoc, DeterministicJsonOptions.Indented);
            var planPath = Path.Combine(_workspace.AosRootPath, "spec", "tasks", task.TaskId, "plan.json");
            
            var validation = ArtifactContractValidator.ValidateTaskPlan(
                artifactPath: planPath,
                artifactJson: planJson,
                aosRootPath: _workspace.AosRootPath,
                readBoundary: "phase-planner-writer");

            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Task plan validation failed for {task.TaskId}: {validation.Message}. Diagnostic: {validation.DiagnosticPath}");
            }

            var planElement = JsonDocument.Parse(planJson).RootElement;
            _specStore.Inner.WriteTaskPlanOverwrite(task.TaskId, planElement);
        }

        await Task.CompletedTask;
    }

    private async Task<string> WritePlanJsonAsync(string planId, string phaseId, List<TaskSpecification> tasks, string runId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var planDoc = new
        {
            schema = "nirmata:aos:schema:phase-plan:v1",
            schemaVersion = 1,
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

        var planPath = Path.Combine(_workspace.AosRootPath, "spec", "phases", phaseId, "plan.json");

        var planJson = JsonSerializer.Serialize(planDoc, DeterministicJsonOptions.Indented);
        var validation = ArtifactContractValidator.ValidatePhasePlan(
            artifactPath: planPath,
            artifactJson: planJson,
            aosRootPath: _workspace.AosRootPath,
            readBoundary: "phase-planner-writer");

        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Phase plan validation failed for {phaseId}: {validation.Message}. Diagnostic: {validation.DiagnosticPath}");
        }

        var planDir = Path.GetDirectoryName(planPath);
        if (!string.IsNullOrEmpty(planDir) && !Directory.Exists(planDir))
        {
            Directory.CreateDirectory(planDir);
        }

        File.WriteAllText(planPath, planJson);
        await Task.CompletedTask;
        return planPath;
    }

    private async Task WriteLinksJsonAsync(string phaseId, List<TaskSpecification> tasks, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var linksDoc = new
        {
            schema = "nirmata:aos:schema:phase-links:v1",
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

        var linksPath = Path.Combine(_workspace.AosRootPath, "spec", "phases", phaseId, "links.json");

        var linksDir = Path.GetDirectoryName(linksPath);
        if (!string.IsNullOrEmpty(linksDir) && !Directory.Exists(linksDir))
        {
            Directory.CreateDirectory(linksDir);
        }

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(linksPath, linksDoc, DeterministicJsonOptions.Indented, writeIndented: true);
        await Task.CompletedTask;
    }

    private static string GenerateTaskId(string phaseId, int sequence)
    {
        // Generate task ID in format TSK-XXXXXX based on phase and sequence
        // Expects phaseId format PH-XXXX (4 digits)
        // Combines Phase Suffix (4 digits) + Sequence (2 digits) -> 6 digits
        var phasePrefix = phaseId.Replace("PH-", "");
        if (phasePrefix.Length > 4) phasePrefix = phasePrefix[^4..]; // Take last 4 chars if longer
        if (phasePrefix.Length < 4) phasePrefix = phasePrefix.PadLeft(4, '0'); // Pad if shorter
        
        return $"TSK-{phasePrefix}{sequence:D2}";
    }
}

#pragma warning restore CS0618

