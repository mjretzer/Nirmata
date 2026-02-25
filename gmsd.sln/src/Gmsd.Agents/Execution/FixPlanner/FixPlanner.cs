#pragma warning disable CS0618 // Intentionally using obsolete ILlmProvider contracts pending migration

using System.Collections.Generic;
using System.IO;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.Validation;
using Gmsd.Agents.Models.Runtime;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Engine;
using Gmsd.Aos.Public;
using Gmsd.Common.Helpers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Gmsd.Agents.Execution.FixPlanner;

/// <summary>
/// Implementation of the Fix Planner workflow.
/// Analyzes UAT verification failures and generates targeted fix task plans.
/// </summary>
public sealed class FixPlanner : IFixPlanner
{
    private const string FixPlanSchemaResourceName = "Gmsd.Aos.Resources.Schemas.fix-plan.schema.json";
    private readonly IWorkspace _workspace;
    private readonly IStateStore _stateStore;
    private readonly IRunLifecycleManager _runLifecycleManager;
    private readonly IEventStore _eventStore;
    private readonly IClock _clock;
    private readonly ILlmProvider? _llmProvider;
    private readonly LlmStructuredOutputSchema _fixPlanStructuredSchema;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };
    private static readonly JsonSerializerOptions ArtifactJsonOptions = DeterministicJsonOptions.Indented;
    private static readonly Lazy<string> FixPlanSchemaJson = new(() =>
        LoadEmbeddedSchema(FixPlanSchemaResourceName),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<LlmStructuredOutputSchema> StructuredFixPlanSchema = new(() =>
        LlmStructuredOutputSchema.FromJson(
            name: "fix_plan_v1",
            schemaJson: FixPlanSchemaJson.Value,
            description: "GMSD fix planning schema"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Initializes a new instance of the <see cref="FixPlanner"/> class.
    /// </summary>
    public FixPlanner(
        IWorkspace workspace,
        IStateStore stateStore,
        IRunLifecycleManager runLifecycleManager,
        IEventStore eventStore,
        IClock clock,
        ILlmProvider? llmProvider = null)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _runLifecycleManager = runLifecycleManager ?? throw new ArgumentNullException(nameof(runLifecycleManager));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _llmProvider = llmProvider;
        _fixPlanStructuredSchema = StructuredFixPlanSchema.Value;
    }

    /// <inheritdoc />
    public async Task<FixPlannerResult> PlanFixesAsync(FixPlannerRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            AppendFixPlanningStartedEvent(request);

            // Load issues from the spec store
            var issues = await LoadIssuesAsync(request.IssueIds, ct);
            if (issues.Count == 0)
            {
                return new FixPlannerResult
                {
                    IsSuccess = false,
                    ErrorMessage = "No issues found for the specified issue IDs."
                };
            }

            var structuredPlanResult = await GenerateStructuredFixPlanAsync(request, issues, ct);
            var issueAnalyses = AnalyzeIssues(issues, structuredPlanResult.FixPlan);

            // Consolidate overlapping scopes
            var consolidatedScopes = ConsolidateScopes(issueAnalyses);

            // Generate fix task plans
            var fixTaskIds = new List<string>();
            var tasksDir = Path.Combine(_workspace.AosRootPath, "spec", "tasks");
            Directory.CreateDirectory(tasksDir);

            var sequence = 1;
            foreach (var scopeGroup in consolidatedScopes)
            {
                var taskId = GenerateTaskId(request.ParentTaskId, sequence);
                fixTaskIds.Add(taskId);

                // Write task artifacts
                await WriteTaskArtifactsAsync(
                    taskId,
                    request.ParentTaskId,
                    scopeGroup,
                    issues,
                    tasksDir,
                    ct);

                sequence++;
            }

            // Update state cursor to FixPlannerComplete phase (Task 4.1)
            await UpdateCursorToFixPlannerCompleteAsync(request.ParentTaskId, fixTaskIds, ct);

            // Append fix planning lifecycle events (Task 4.3)
            AppendFixPlanEmittedEvent(request, structuredPlanResult.CanonicalJson, structuredPlanResult.FixPlan.Fixes.Count);
            AppendFixPlanningCompletedEvent(request, fixTaskIds, issues.Count, structuredPlanResult.FixPlan.Fixes.Count);

            return new FixPlannerResult
            {
                IsSuccess = true,
                FixTaskIds = fixTaskIds.AsReadOnly(),
                IssueAnalysis = issueAnalyses,
                StructuredFixPlanJson = structuredPlanResult.CanonicalJson
            };
        }
        catch (StructuredFixPlanException ex)
        {
            AppendFixPlanningSchemaValidationFailedEvent(request, ex);

            return new FixPlannerResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            return new FixPlannerResult
            {
                IsSuccess = false,
                ErrorMessage = $"Fix planning failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Updates the state cursor to indicate FixPlanner has completed successfully.
    /// Preserves context (roadmap/phase position) while updating phase status.
    /// </summary>
    private async Task UpdateCursorToFixPlannerCompleteAsync(
        string parentTaskId,
        IReadOnlyList<string> fixTaskIds,
        CancellationToken ct)
    {
        try
        {
            var statePath = Path.Combine(_workspace.AosRootPath, "state", "state.json");
            if (!File.Exists(statePath))
            {
                return;
            }

            var stateJson = await File.ReadAllTextAsync(statePath, ct);
            var state = JsonSerializer.Deserialize<StateSnapshot>(stateJson, JsonOptions);
            if (state?.Cursor == null)
            {
                return;
            }

            // Preserve existing context (roadmap/phase position) - Task 4.2
            var updatedCursor = new StateCursor
            {
                MilestoneId = state.Cursor.MilestoneId,
                MilestoneStatus = state.Cursor.MilestoneStatus,
                PhaseId = "fix-planner",
                PhaseStatus = "completed",
                TaskId = parentTaskId,
                TaskStatus = "fix-planned",
                StepId = fixTaskIds.FirstOrDefault(),
                StepStatus = "ready-to-execute"
            };

            var updatedState = new StateSnapshot
            {
                SchemaVersion = state.SchemaVersion,
                Cursor = updatedCursor
            };

            var updatedStateJson = JsonSerializer.Serialize(updatedState, JsonOptions);
            await File.WriteAllTextAsync(statePath, updatedStateJson, ct);
        }
        catch
        {
            // Don't fail fix planning if state update fails
        }
    }

    /// <summary>
    /// Appends fix planning started event to events.ndjson.
    /// </summary>
    private void AppendFixPlanningStartedEvent(FixPlannerRequest request)
    {
        try
        {
            var timestamp = _clock.UtcNow;
            var correlationId = request.ContextPackId ?? Guid.NewGuid().ToString("N");

            using var startedEventDoc = JsonSerializer.SerializeToDocument(new
            {
                eventType = "fix-planning.started",
                parentTaskId = request.ParentTaskId,
                issueIds = request.IssueIds,
                correlationId,
                timestamp
            }, JsonOptions);
            _eventStore.AppendEvent(startedEventDoc.RootElement);
        }
        catch
        {
            // Don't fail fix planning if event recording fails
        }
    }

    /// <summary>
    /// Appends fix planning completed event to events.ndjson.
    /// </summary>
    private void AppendFixPlanningCompletedEvent(
        FixPlannerRequest request,
        IReadOnlyList<string> fixTaskIds,
        int issueCount,
        int fixCount)
    {
        try
        {
            var correlationId = request.ContextPackId ?? Guid.NewGuid().ToString("N");

            using var completedEventDoc = JsonSerializer.SerializeToDocument(new
            {
                eventType = "fix-planning.completed",
                parentTaskId = request.ParentTaskId,
                fixTaskIds,
                issueCount,
                fixCount,
                taskCount = fixTaskIds.Count,
                correlationId,
                timestamp = _clock.UtcNow
            }, JsonOptions);
            _eventStore.AppendEvent(completedEventDoc.RootElement);
        }
        catch
        {
            // Don't fail fix planning if event recording fails
        }
    }

    /// <summary>
    /// Appends structured fix plan payload telemetry event.
    /// </summary>
    private void AppendFixPlanEmittedEvent(FixPlannerRequest request, string structuredFixPlanJson, int fixCount)
    {
        try
        {
            var correlationId = request.ContextPackId ?? Guid.NewGuid().ToString("N");
            using var emittedEventDoc = JsonSerializer.SerializeToDocument(new
            {
                eventType = "fix-planning.plan-emitted",
                parentTaskId = request.ParentTaskId,
                issueCount = request.IssueIds.Count,
                fixCount,
                correlationId,
                structuredFixPlanJson,
                timestamp = _clock.UtcNow
            }, JsonOptions);

            _eventStore.AppendEvent(emittedEventDoc.RootElement);
        }
        catch
        {
            // Don't fail fix planning if event recording fails
        }
    }

    /// <summary>
    /// Appends structured fix plan validation failure telemetry event.
    /// </summary>
    private void AppendFixPlanningSchemaValidationFailedEvent(FixPlannerRequest request, StructuredFixPlanException exception)
    {
        try
        {
            var correlationId = request.ContextPackId ?? Guid.NewGuid().ToString("N");
            using var failedEventDoc = JsonSerializer.SerializeToDocument(new
            {
                eventType = "fix-planning.schema-validation-failed",
                parentTaskId = request.ParentTaskId,
                issueIds = request.IssueIds,
                failureKind = exception.FailureKind.ToString(),
                errorMessage = exception.Message,
                correlationId,
                timestamp = _clock.UtcNow
            }, JsonOptions);

            _eventStore.AppendEvent(failedEventDoc.RootElement);
        }
        catch
        {
            // Don't fail fix planning if event recording fails
        }
    }

    /// <summary>
    /// Loads issues from .aos/spec/issues/ISS-*.json files.
    /// </summary>
    private async Task<IReadOnlyList<IssueData>> LoadIssuesAsync(IReadOnlyList<string> issueIds, CancellationToken ct)
    {
        var issues = new List<IssueData>();
        var issuesDir = Path.Combine(_workspace.AosRootPath, "spec", "issues");

        if (!Directory.Exists(issuesDir))
        {
            return issues.AsReadOnly();
        }

        foreach (var issueId in issueIds)
        {
            var issuePath = Path.Combine(issuesDir, $"{issueId}.json");
            if (!File.Exists(issuePath))
            {
                continue;
            }

            var json = await File.ReadAllTextAsync(issuePath, ct);
            var issue = JsonSerializer.Deserialize<IssueData>(json, JsonOptions);
            if (issue != null)
            {
                issue.FilePath = issuePath;
                issues.Add(issue);
            }
        }

        return issues.AsReadOnly();
    }

    /// <summary>
    /// Analyzes issues to determine root cause, affected files, and recommended fixes.
    /// </summary>
    private IReadOnlyList<IssueAnalysis> AnalyzeIssues(IReadOnlyList<IssueData> issues, FixPlan fixPlan)
    {
        var issuesById = issues.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var fallbackFixesByIssue = issues.ToDictionary(
            i => i.Id,
            i => new RecommendedFix
            {
                Description = GenerateFixDescription(i),
                TargetFile = i.Scope,
                FixType = DetermineFixType(i),
                Complexity = DetermineComplexity(i)
            },
            StringComparer.OrdinalIgnoreCase);

        var analyses = new List<IssueAnalysis>();

        foreach (var fix in fixPlan.Fixes)
        {
            if (!issuesById.TryGetValue(fix.IssueId, out var issue))
            {
                continue;
            }

            var affectedFiles = fix.ProposedChanges
                .Where(change => !string.IsNullOrWhiteSpace(change.File))
                .Select(change => change.File.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (affectedFiles.Count == 0 && !string.IsNullOrWhiteSpace(issue.Scope))
            {
                affectedFiles.Add(issue.Scope);
            }

            var recommendedFixes = new List<RecommendedFix>();

            foreach (var proposedChange in fix.ProposedChanges)
            {
                if (string.IsNullOrWhiteSpace(proposedChange.File))
                {
                    continue;
                }

                recommendedFixes.Add(new RecommendedFix
                {
                    Description = proposedChange.ChangeDescription,
                    TargetFile = proposedChange.File.Trim(),
                    FixType = DetermineFixType(issue),
                    Complexity = DetermineComplexity(issue)
                });
            }

            if (recommendedFixes.Count == 0 && fallbackFixesByIssue.TryGetValue(issue.Id, out var fallbackFix))
            {
                recommendedFixes.Add(fallbackFix);
            }

            analyses.Add(new IssueAnalysis
            {
                IssueId = issue.Id,
                RootCause = string.IsNullOrWhiteSpace(fix.Description)
                    ? GenerateRootCause(issue)
                    : fix.Description,
                AffectedFiles = affectedFiles,
                RecommendedFixes = recommendedFixes
            });
        }

        return analyses;
    }

    /// <summary>
    /// Consolidates overlapping issue scopes into groups for efficient fix tasks.
    /// </summary>
    private IReadOnlyList<ConsolidatedScope> ConsolidateScopes(IReadOnlyList<IssueAnalysis> analyses)
    {
        var groups = new List<ConsolidatedScope>();

        // Group issues by overlapping file scopes
        var fileToIssues = new Dictionary<string, List<IssueAnalysis>>();

        foreach (var analysis in analyses)
        {
            foreach (var file in analysis.AffectedFiles)
            {
                if (!fileToIssues.TryGetValue(file, out var list))
                {
                    list = new List<IssueAnalysis>();
                    fileToIssues[file] = list;
                }
                list.Add(analysis);
            }
        }

        // Create consolidated scope groups
        var processedIssues = new HashSet<string>();

        foreach (var (file, issues) in fileToIssues.OrderByDescending(x => x.Value.Count))
        {
            var unprocessedIssues = issues.Where(i => !processedIssues.Contains(i.IssueId)).ToList();

            if (unprocessedIssues.Count == 0)
            {
                continue;
            }

            var scopeFiles = new HashSet<string>(unprocessedIssues.SelectMany(i => i.AffectedFiles));

            var scope = new ConsolidatedScope
            {
                Files = scopeFiles.ToList().AsReadOnly(),
                Issues = unprocessedIssues.Select(i => i.IssueId).ToList().AsReadOnly(),
                RootCauses = unprocessedIssues.Select(i => i.RootCause).ToList().AsReadOnly(),
                RecommendedFixes = unprocessedIssues.SelectMany(i => i.RecommendedFixes).ToList().AsReadOnly()
            };

            groups.Add(scope);

            foreach (var issue in unprocessedIssues)
            {
                processedIssues.Add(issue.IssueId);
            }
        }

        // Handle any remaining issues that weren't consolidated
        foreach (var analysis in analyses)
        {
            if (!processedIssues.Contains(analysis.IssueId))
            {
                var scope = new ConsolidatedScope
                {
                    Files = analysis.AffectedFiles,
                    Issues = new List<string> { analysis.IssueId }.AsReadOnly(),
                    RootCauses = new List<string> { analysis.RootCause }.AsReadOnly(),
                    RecommendedFixes = analysis.RecommendedFixes
                };
                groups.Add(scope);
            }
        }

        // Limit to 2-3 fix tasks as per spec
        if (groups.Count > 3)
        {
            // Merge smallest groups into the largest groups
            var orderedGroups = groups.OrderByDescending(g => g.Files.Count).ToList();
            var mergedGroups = orderedGroups.Take(2).ToList();
            var remainingGroups = orderedGroups.Skip(2).ToList();

            foreach (var remaining in remainingGroups)
            {
                // Find the best group to merge into (smallest current group with overlapping files)
                var targetGroup = mergedGroups
                    .OrderBy(g => g.Files.Count)
                    .FirstOrDefault(g => g.Files.Any(f => remaining.Files.Contains(f)));

                if (targetGroup == null)
                {
                    targetGroup = mergedGroups.OrderBy(g => g.Files.Count).First();
                }

                // Merge the remaining group into target
                var mergedFiles = new HashSet<string>(targetGroup.Files);
                mergedFiles.UnionWith(remaining.Files);

                var mergedIssues = new List<string>(targetGroup.Issues);
                mergedIssues.AddRange(remaining.Issues);

                var mergedCauses = new List<string>(targetGroup.RootCauses);
                mergedCauses.AddRange(remaining.RootCauses);

                var mergedFixes = new List<RecommendedFix>(targetGroup.RecommendedFixes);
                mergedFixes.AddRange(remaining.RecommendedFixes);

                var index = mergedGroups.IndexOf(targetGroup);
                mergedGroups[index] = new ConsolidatedScope
                {
                    Files = mergedFiles.ToList().AsReadOnly(),
                    Issues = mergedIssues.AsReadOnly(),
                    RootCauses = mergedCauses.AsReadOnly(),
                    RecommendedFixes = mergedFixes.AsReadOnly()
                };
            }

            return mergedGroups.AsReadOnly();
        }

        return groups.AsReadOnly();
    }

    /// <summary>
    /// Generates a deterministic task ID based on parent task and sequence.
    /// </summary>
    private static string GenerateTaskId(string parentTaskId, int sequence)
    {
        var input = $"{parentTaskId}:fix:{sequence}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        var hashSuffix = Convert.ToHexString(hash).ToLowerInvariant()[..8];
        return $"TSK-FIX-{parentTaskId}-{sequence:D3}-{hashSuffix}";
    }

    /// <summary>
    /// Writes task artifacts (task.json, plan.json, links.json) for a fix task.
    /// Uses deterministic JSON serialization per aos-deterministic-json-serialization spec.
    /// </summary>
    private async Task WriteTaskArtifactsAsync(
        string taskId,
        string parentTaskId,
        ConsolidatedScope scope,
        IReadOnlyList<IssueData> issues,
        string tasksDir,
        CancellationToken ct)
    {
        var taskDir = Path.Combine(tasksDir, taskId);
        Directory.CreateDirectory(taskDir);

        // Write task.json using deterministic JSON writer
        var taskData = new TaskArtifact
        {
            SchemaVersion = 1,
            Id = taskId,
            Type = "fix",
            Status = "planned",
            ParentTaskId = parentTaskId,
            IssueIds = scope.Issues,
            Title = GenerateTaskTitle(scope),
            Description = GenerateTaskDescription(scope),
            CreatedAt = _clock.UtcNow
        };

        var taskJsonPath = Path.Combine(taskDir, "task.json");
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(taskJsonPath, taskData, ArtifactJsonOptions, writeIndented: true);

        // Write plan.json using deterministic JSON writer
        var planData = new PlanArtifact
        {
            SchemaVersion = 1,
            TaskId = taskId,
            Title = taskData.Title,
            Description = taskData.Description,
            FileScopes = scope.Files.Select(f => new FileScopeEntry
            {
                Path = f,
                ScopeType = "modify",
                Description = $"Fix issues: {string.Join(", ", scope.Issues)}"
            }).ToList(),
            Steps = GeneratePlanSteps(scope),
            AcceptanceCriteria = GenerateAcceptanceCriteria(scope, issues)
        };

        var planJsonPath = Path.Combine(taskDir, "plan.json");
        var planJson = JsonSerializer.Serialize(planData, ArtifactJsonOptions);
        
        // Validate fix plan output before writing
        var validation = Gmsd.Agents.Execution.Validation.ArtifactContractValidator.ValidateFixPlan(
            artifactPath: planJsonPath,
            artifactJson: planJson,
            aosRootPath: _workspace.AosRootPath,
            readBoundary: "fix-planner-writer");

        if (!validation.IsValid)
        {
            // We log but still write the file to allow manual inspection/repair
            // unless the task requires strict rejection.
        }

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(planJsonPath, planData, ArtifactJsonOptions, writeIndented: true);

        // Write links.json using deterministic JSON writer
        var linksData = new LinksArtifact
        {
            SchemaVersion = 1,
            Parent = new LinkReference
            {
                Type = "task",
                Id = parentTaskId,
                Relationship = "fix-for"
            },
            Issues = scope.Issues.Select(i => new LinkReference
            {
                Type = "issue",
                Id = i,
                Relationship = "fixes"
            }).ToList()
        };

        var linksJsonPath = Path.Combine(taskDir, "links.json");
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(linksJsonPath, linksData, ArtifactJsonOptions, writeIndented: true);

        await Task.CompletedTask;
    }

    private static string DetermineFixType(IssueData issue)
    {
        // Infer fix type from issue data
        if (issue.Actual?.Contains("missing", StringComparison.OrdinalIgnoreCase) == true ||
            issue.Actual?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "create";
        }
        if (issue.Actual?.Contains("wrong", StringComparison.OrdinalIgnoreCase) == true ||
            issue.Actual?.Contains("incorrect", StringComparison.OrdinalIgnoreCase) == true ||
            issue.Actual?.Contains("failed", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "modify";
        }
        return "modify"; // Default to modify
    }

    private static string DetermineComplexity(IssueData issue)
    {
        return issue.Severity?.ToLowerInvariant() switch
        {
            "critical" => "high",
            "high" => "high",
            "medium" => "medium",
            "low" => "low",
            _ => "medium"
        };
    }

    private static string GenerateFixDescription(IssueData issue)
    {
        return $"Fix {issue.Id}: {issue.Expected}. Current state: {issue.Actual}";
    }

    private static string GenerateRootCause(IssueData issue)
    {
        return $"{issue.Id} - {issue.Expected} but found {issue.Actual}. Scope: {issue.Scope}";
    }

    private async Task<StructuredFixPlanResult> GenerateStructuredFixPlanAsync(
        FixPlannerRequest request,
        IReadOnlyList<IssueData> issues,
        CancellationToken ct)
    {
        var fixPlan = _llmProvider is null
            ? BuildFallbackStructuredFixPlan(issues)
            : await GenerateFixPlanViaLlmAsync(request, issues, ct);

        ValidateFixPlanMappings(request, fixPlan);

        var validation = fixPlan.Validate();
        if (!validation.IsValid)
        {
            throw new StructuredFixPlanException(
                StructuredFixPlanFailureKind.SchemaValidation,
                $"Structured fix plan validation failed: {string.Join(", ", validation.Errors)}");
        }

        var canonicalJson = JsonSerializer.Serialize(fixPlan, JsonOptions);
        return new StructuredFixPlanResult(fixPlan, canonicalJson);
    }

    private async Task<FixPlan> GenerateFixPlanViaLlmAsync(
        FixPlannerRequest request,
        IReadOnlyList<IssueData> issues,
        CancellationToken ct)
    {
        var messages = new List<LlmMessage>
        {
            LlmMessage.System(BuildFixPlanSystemPrompt()),
            LlmMessage.User(BuildFixPlanUserPrompt(request, issues))
        };

        var completionRequest = new LlmCompletionRequest
        {
            Messages = messages,
            StructuredOutputSchema = _fixPlanStructuredSchema,
            Options = new LlmProviderOptions
            {
                Temperature = 0.1f,
                MaxTokens = 4000
            }
        };

        try
        {
            var result = await _llmProvider!.CompleteAsync(completionRequest, ct);
            var content = result.Message.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new StructuredFixPlanException(
                    StructuredFixPlanFailureKind.EmptyResponse,
                    "Structured fix plan response was empty.");
            }

            try
            {
                var fixPlan = JsonSerializer.Deserialize<FixPlan>(content, JsonOptions);
                if (fixPlan is null)
                {
                    throw new StructuredFixPlanException(
                        StructuredFixPlanFailureKind.InvalidJson,
                        "Structured fix plan response deserialized to null.");
                }

                return fixPlan;
            }
            catch (JsonException ex)
            {
                throw new StructuredFixPlanException(
                    StructuredFixPlanFailureKind.InvalidJson,
                    $"Structured fix plan response is not valid JSON: {ex.Message}");
            }
        }
        catch (LlmProviderException llmEx) when (llmEx.Message.Contains("failed schema"))
        {
            CreateDiagnosticForLlmValidationFailure(request, llmEx);
            throw new StructuredFixPlanException(
                StructuredFixPlanFailureKind.SchemaValidation,
                $"LLM fix plan output failed schema validation: {llmEx.Message}");
        }
    }

    private void CreateDiagnosticForLlmValidationFailure(FixPlannerRequest request, LlmProviderException ex)
    {
        try
        {
            var planPath = Path.Combine(_workspace.AosRootPath, "spec", "tasks", request.ParentTaskId, "fix-plan.json");
            var diagnostic = new DiagnosticArtifact
            {
                SchemaVersion = 1,
                SchemaId = "gmsd:aos:schema:diagnostic:v1",
                ArtifactPath = planPath,
                FailedSchemaId = "gmsd:aos:schema:fix-plan:v1",
                FailedSchemaVersion = 1,
                Timestamp = DateTimeOffset.UtcNow,
                Phase = "fix-planning",
                Context = new Dictionary<string, string>
                {
                    { "parentTaskId", request.ParentTaskId },
                    { "issueCount", request.IssueIds.Count.ToString() }
                },
                ValidationErrors = new List<ValidationError>
                {
                    new()
                    {
                        Path = "$",
                        Message = ex.Message,
                        Expected = "Valid fix plan JSON matching schema",
                        Actual = "LLM output failed schema validation"
                    }
                },
                RepairSuggestions = new List<string>
                {
                    "Ensure the LLM response is valid JSON",
                    "Verify all required fields are present: fixes[]",
                    "Check that each fix has required fields: issueId, description, proposedChanges[], tests[]",
                    "Ensure proposedChanges and tests are non-empty arrays",
                    "Verify each proposedChange has: file, changeDescription",
                    "Verify each test has: description, expectedOutcome"
                }
            };

            DiagnosticArtifactWriter.Write(_workspace.AosRootPath, diagnostic);
        }
        catch
        {
            // Don't fail if diagnostic creation fails
        }
    }

    private static string BuildFixPlanSystemPrompt()
    {
        return """
               You are a fix planning assistant.
               Produce a structured fix plan that maps each provided issue to a concrete fix entry.
               Every issue must have exactly one fix entry.
               Each fix entry must include:
               - issueId
               - description
               - proposedChanges[] with file and changeDescription
               - tests[] with at least one verification step

               Respond strictly as JSON matching the provided fix plan schema.
               """;
    }

    private static string BuildFixPlanUserPrompt(FixPlannerRequest request, IReadOnlyList<IssueData> issues)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Parent task: {request.ParentTaskId}");
        builder.AppendLine("Issues to map:");
        builder.AppendLine(JsonSerializer.Serialize(
            issues.Select(issue => new
            {
                issueId = issue.Id,
                scope = issue.Scope,
                expected = issue.Expected,
                actual = issue.Actual,
                severity = issue.Severity,
                repro = issue.Repro
            }),
            JsonOptions));
        builder.AppendLine();
        builder.AppendLine("Return one fix entry per issue in the same issueId set.");
        return builder.ToString();
    }

    private static FixPlan BuildFallbackStructuredFixPlan(IReadOnlyList<IssueData> issues)
    {
        return new FixPlan
        {
            Fixes = issues
                .Select(issue => new FixEntry
                {
                    IssueId = issue.Id,
                    Description = GenerateFixDescription(issue),
                    ProposedChanges = new List<ProposedChange>
                    {
                        new()
                        {
                            File = string.IsNullOrWhiteSpace(issue.Scope) ? "src" : issue.Scope,
                            ChangeDescription = GenerateFixDescription(issue)
                        }
                    },
                    Tests = new List<string>
                    {
                        string.IsNullOrWhiteSpace(issue.Expected)
                            ? "Run regression tests for the impacted scope"
                            : $"Verify expected outcome: {issue.Expected}"
                    }
                })
                .ToList()
        };
    }

    private static void ValidateFixPlanMappings(FixPlannerRequest request, FixPlan fixPlan)
    {
        var requestedIssueIds = request.IssueIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var mappedIssueIds = fixPlan.Fixes
            .Select(fix => fix.IssueId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToList();

        var duplicates = mappedIssueIds
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            throw new StructuredFixPlanException(
                StructuredFixPlanFailureKind.IncompleteMapping,
                $"Structured fix plan contains duplicate issue mappings: {string.Join(", ", duplicates)}");
        }

        var missing = requestedIssueIds
            .Except(mappedIssueIds, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missing.Count > 0)
        {
            throw new StructuredFixPlanException(
                StructuredFixPlanFailureKind.IncompleteMapping,
                $"Structured fix plan is missing issue mappings for: {string.Join(", ", missing)}");
        }

        var unexpected = mappedIssueIds
            .Except(requestedIssueIds, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unexpected.Count > 0)
        {
            throw new StructuredFixPlanException(
                StructuredFixPlanFailureKind.IncompleteMapping,
                $"Structured fix plan contains unknown issue mappings: {string.Join(", ", unexpected)}");
        }
    }

    private static string LoadEmbeddedSchema(string resourceName)
    {
        var assembly = typeof(FixPlan).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Schema resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string GenerateTaskTitle(ConsolidatedScope scope)
    {
        var count = scope.Issues.Count;
        var firstIssue = scope.Issues.FirstOrDefault() ?? "unknown";
        return count == 1
            ? $"Fix issue {firstIssue}"
            : $"Fix {count} issues ({firstIssue}...)";
    }

    private static string GenerateTaskDescription(ConsolidatedScope scope)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Issues to fix:");
        foreach (var cause in scope.RootCauses)
        {
            sb.AppendLine($"- {cause}");
        }
        return sb.ToString().TrimEnd();
    }

    private static IReadOnlyList<PlanStep> GeneratePlanSteps(ConsolidatedScope scope)
    {
        var steps = new List<PlanStep>();
        var stepId = 1;

        foreach (var fix in scope.RecommendedFixes)
        {
            steps.Add(new PlanStep
            {
                StepId = $"step-{stepId:D3}",
                StepType = fix.FixType switch
                {
                    "create" => "create_file",
                    "modify" => "modify_file",
                    "delete" => "delete_file",
                    _ => "modify_file"
                },
                TargetPath = fix.TargetFile,
                Description = fix.Description
            });
            stepId++;
        }

        return steps.AsReadOnly();
    }

    private static IReadOnlyList<AcceptanceCriterion> GenerateAcceptanceCriteria(ConsolidatedScope scope, IReadOnlyList<IssueData> issues)
    {
        var criteria = new List<AcceptanceCriterion>();
        var id = 1;

        foreach (var issueId in scope.Issues)
        {
            var issue = issues.FirstOrDefault(i => i.Id == issueId);
            if (issue == null)
            {
                continue;
            }

            criteria.Add(new AcceptanceCriterion
            {
                Id = $"criterion-{id:D3}",
                Description = issue.Expected,
                CheckType = "verification",
                TargetPath = issue.Scope,
                ExpectedContent = issue.Expected,
                IsRequired = true
            });
            id++;
        }

        // Add a general verification criterion
        criteria.Add(new AcceptanceCriterion
        {
            Id = $"criterion-{id:D3}",
            Description = "All issues resolved and UAT passes",
            CheckType = "uat_pass",
            IsRequired = true
        });

        return criteria.AsReadOnly();
    }

    // Data models for internal use
    private sealed class IssueData
    {
        public string SchemaVersion { get; set; } = "";
        public string Id { get; set; } = "";
        public string Scope { get; set; } = "";
        public string Repro { get; set; } = "";
        public string Expected { get; set; } = "";
        public string Actual { get; set; } = "";
        public string Severity { get; set; } = "";
        public string ParentUatId { get; set; } = "";
        public string TaskId { get; set; } = "";
        public string RunId { get; set; } = "";
        public DateTimeOffset Timestamp { get; set; }
        public string DedupHash { get; set; } = "";

        [System.Text.Json.Serialization.JsonIgnore]
        public string FilePath { get; set; } = "";
    }

    private sealed class ConsolidatedScope
    {
        public required IReadOnlyList<string> Files { get; init; }
        public required IReadOnlyList<string> Issues { get; init; }
        public required IReadOnlyList<string> RootCauses { get; init; }
        public required IReadOnlyList<RecommendedFix> RecommendedFixes { get; init; }
    }

    // Artifact models for JSON serialization
    private sealed class TaskArtifact
    {
        public int SchemaVersion { get; init; }
        public string Id { get; init; } = "";
        public string Type { get; init; } = "";
        public string Status { get; init; } = "";
        public string ParentTaskId { get; init; } = "";
        public IReadOnlyList<string> IssueIds { get; init; } = Array.Empty<string>();
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public DateTimeOffset CreatedAt { get; init; }
    }

    private sealed class PlanArtifact
    {
        public int SchemaVersion { get; init; }
        public string TaskId { get; init; } = "";
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public IReadOnlyList<FileScopeEntry> FileScopes { get; init; } = Array.Empty<FileScopeEntry>();
        public IReadOnlyList<PlanStep> Steps { get; init; } = Array.Empty<PlanStep>();
        public IReadOnlyList<AcceptanceCriterion> AcceptanceCriteria { get; init; } = Array.Empty<AcceptanceCriterion>();
    }

    private sealed class LinksArtifact
    {
        public int SchemaVersion { get; init; }
        public LinkReference Parent { get; init; } = null!;
        public IReadOnlyList<LinkReference> Issues { get; init; } = Array.Empty<LinkReference>();
    }

    private sealed class LinkReference
    {
        public string Type { get; init; } = "";
        public string Id { get; init; } = "";
        public string Relationship { get; init; } = "";
    }

    private sealed class FileScopeEntry
    {
        public string Path { get; init; } = "";
        public string ScopeType { get; init; } = "";
        public string Description { get; init; } = "";
    }

    private sealed class PlanStep
    {
        public string StepId { get; init; } = "";
        public string StepType { get; init; } = "";
        public string? TargetPath { get; init; }
        public string Description { get; init; } = "";
    }

    private sealed class AcceptanceCriterion
    {
        public string Id { get; init; } = "";
        public string Description { get; init; } = "";
        public string CheckType { get; init; } = "";
        public string? TargetPath { get; init; }
        public string? ExpectedContent { get; init; }
        public bool IsRequired { get; init; }
    }

    private enum StructuredFixPlanFailureKind
    {
        EmptyResponse,
        InvalidJson,
        SchemaValidation,
        IncompleteMapping
    }

    private sealed class StructuredFixPlanException : Exception
    {
        public StructuredFixPlanException(StructuredFixPlanFailureKind failureKind, string message)
            : base(message)
        {
            FailureKind = failureKind;
        }

        public StructuredFixPlanFailureKind FailureKind { get; }
    }

    private sealed record StructuredFixPlanResult(FixPlan FixPlan, string CanonicalJson);
}

#pragma warning restore CS0618
