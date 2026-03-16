namespace nirmata.Agents.Tests.E2E.ControlLoop;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Helper class for building deterministic E2E test scenarios.
/// </summary>
public sealed class TestScenarioBuilder
{
    private readonly string _repoRoot;
    private readonly List<TestPhase> _phases = new();
    private readonly List<PlanOutput> _planOutputs = new();

    public TestScenarioBuilder(string repoRoot)
    {
        _repoRoot = repoRoot;
    }

    /// <summary>
    /// Adds a phase with tasks to the scenario.
    /// </summary>
    public TestScenarioBuilder WithPhase(string phaseId, params string[] taskIds)
    {
        _phases.Add(new TestPhase(phaseId, new List<string>(taskIds)));
        return this;
    }

    /// <summary>
    /// Adds a plan output file to be created during execution.
    /// </summary>
    public TestScenarioBuilder WithPlanOutput(string relativePath, string contentsUtf8)
    {
        _planOutputs.Add(new PlanOutput(relativePath, contentsUtf8));
        return this;
    }

    /// <summary>
    /// Builds the scenario files in the .aos/spec/ directory.
    /// </summary>
    public void Build()
    {
        var specDir = Path.Combine(_repoRoot, ".aos", "spec");
        Directory.CreateDirectory(specDir);

        // Create roadmap.json with phases and tasks
        var roadmap = new
        {
            phases = _phases.Select(p => new
            {
                id = p.PhaseId,
                tasks = p.TaskIds.Select(t => new { id = t }).ToList()
            }).ToList()
        };

        var roadmapPath = Path.Combine(specDir, "roadmap.json");
        File.WriteAllText(roadmapPath, JsonSerializer.Serialize(roadmap, new JsonSerializerOptions { WriteIndented = true }));

        // Create plan.json if outputs are defined
        if (_planOutputs.Count > 0)
        {
            var plan = new
            {
                schemaVersion = 1,
                outputs = _planOutputs.Select(o => new
                {
                    relativePath = o.RelativePath,
                    contentsUtf8 = o.ContentsUtf8
                }).ToList()
            };

            var planPath = Path.Combine(specDir, "plan.json");
            File.WriteAllText(planPath, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    /// <summary>
    /// Creates an execute-plan compatible plan file at the specified path.
    /// </summary>
    public string CreatePlanFile(string fileName, IEnumerable<(string relativePath, string contentsUtf8)> outputs)
    {
        var specDir = Path.Combine(_repoRoot, ".aos", "spec");
        Directory.CreateDirectory(specDir);

        var plan = new
        {
            schemaVersion = 1,
            outputs = outputs.Select(o => new
            {
                relativePath = o.relativePath,
                contentsUtf8 = o.contentsUtf8
            }).ToList()
        };

        var planPath = Path.Combine(specDir, fileName);
        File.WriteAllText(planPath, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));

        return planPath;
    }

    private sealed record TestPhase(string PhaseId, List<string> TaskIds);
    private sealed record PlanOutput(string RelativePath, string ContentsUtf8);
}
