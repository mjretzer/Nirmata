using System.Text.Json;
using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Contracts.State;
using Json.Schema;
using Xunit;

namespace nirmata.Aos.Tests;

public class SchemaContractComplianceTests
{
    private readonly JsonSchema _phasePlanSchema;
    private readonly JsonSchema _fixPlanSchema;
    private readonly JsonSchema _commandProposalSchema;

    public SchemaContractComplianceTests()
    {
        var assembly = typeof(PhasePlan).Assembly;
        
        _phasePlanSchema = LoadSchema(assembly, "nirmata.Aos.Resources.Schemas.phase-plan.schema.json");
        _fixPlanSchema = LoadSchema(assembly, "nirmata.Aos.Resources.Schemas.fix-plan.schema.json");
        _commandProposalSchema = LoadSchema(assembly, "nirmata.Aos.Resources.Schemas.command-proposal.schema.json");
    }

    private static readonly object SchemaRegistrationLock = new();

    private JsonSchema LoadSchema(System.Reflection.Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource {resourceName} not found. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
        using var reader = new System.IO.StreamReader(stream);
        var json = reader.ReadToEnd();

        string? schemaId = null;
        using (var doc = JsonDocument.Parse(json))
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("$id", out var idProp) &&
                idProp.ValueKind == JsonValueKind.String)
            {
                schemaId = idProp.GetString();
            }
        }

        if (string.IsNullOrWhiteSpace(schemaId))
        {
            return JsonSchema.FromText(json);
        }

        var uri = new Uri(schemaId, UriKind.Absolute);
        lock (SchemaRegistrationLock)
        {
            var existing = SchemaRegistry.Global.Get(uri);
            if (existing is JsonSchema existingSchema)
            {
                return existingSchema;
            }

            var parsed = JsonSchema.FromText(json);
            if (SchemaRegistry.Global.Get(uri) is null)
            {
                SchemaRegistry.Global.Register(uri, parsed);
            }

            return parsed;
        }
    }

    [Fact]
    public void PhasePlan_Contract_MatchesSchema()
    {
        var plan = new PhasePlan
        {
            PlanId = "plan-1",
            PhaseId = "phase-1",
            Tasks = new List<PhaseTask>
            {
                new PhaseTask
                {
                    Id = "TSK-001",
                    Title = "Task 1",
                    Description = "Description for task 1 that is long enough",
                    FileScopes = new List<PhaseFileScope> { new() { Path = "file1.cs" } },
                    VerificationSteps = new List<string> { "Verify it works" }
                }
            }
        };

        var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var result = _phasePlanSchema.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });

        Assert.True(result.IsValid, $"Schema validation failed: {JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })}");
    }

    [Fact]
    public void FixPlan_Contract_MatchesSchema()
    {
        var plan = new FixPlan
        {
            Fixes = new List<FixEntry>
            {
                new FixEntry
                {
                    IssueId = "ISS-001",
                    Description = "Fixing the issue with valid description",
                    ProposedChanges = new List<ProposedChange>
                    {
                        new ProposedChange { File = "file.cs", ChangeDescription = "Fix bug" }
                    },
                    Tests = new List<string> { "Run TestA" }
                }
            }
        };

        var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var result = _fixPlanSchema.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });

        Assert.True(result.IsValid, $"Schema validation failed: {JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })}");
    }

    [Fact]
    public void CommandProposal_Contract_MatchesSchema()
    {
        var proposal = new CommandIntentProposal
        {
            SchemaVersion = 1,
            Intent = new CommandIntent { Goal = "Run tests" },
            Command = "/run-tests",
            Group = "run",
            Rationale = "Needed to verify changes",
            ExpectedOutcome = "Tests pass successfully"
        };

        var json = JsonSerializer.Serialize(proposal, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var result = _commandProposalSchema.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });

        Assert.True(result.IsValid, $"Schema validation failed: {JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })}");
    }
}
