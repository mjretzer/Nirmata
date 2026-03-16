using Xunit;
using nirmata.Agents.Execution.ControlPlane.Chat;

namespace nirmata.Agents.Tests.Execution.ControlPlane.Chat;

public class ContextSummarizerTests
{
    [Fact]
    public void Summarize_WithNullContext_ThrowsArgumentNullException()
    {
        var summarizer = new ContextSummarizer();

        Assert.Throws<ArgumentNullException>(() => summarizer.Summarize(null!));
    }

    [Fact]
    public void Summarize_WithEmptyContext_ReturnsEmptyContext()
    {
        var summarizer = new ContextSummarizer();
        var context = new ChatContext
        {
            State = new StateContext(),
            AvailableCommands = Array.Empty<CommandContext>(),
            RecentRuns = Array.Empty<RunHistoryContext>()
        };

        var result = summarizer.Summarize(context);

        Assert.NotNull(result);
        Assert.Null(result.Project);
        Assert.Null(result.Roadmap);
        Assert.Empty(result.AvailableCommands);
        Assert.Empty(result.RecentRuns);
    }

    [Fact]
    public void Summarize_TruncatesLongProjectDescription()
    {
        var summarizer = new ContextSummarizer(maxTokens: 100);
        var longDescription = new string('a', 500);
        var context = new ChatContext
        {
            Project = new ProjectContext
            {
                Name = "TestProject",
                Description = longDescription
            },
            State = new StateContext(),
            AvailableCommands = Array.Empty<CommandContext>()
        };

        var result = summarizer.Summarize(context);

        Assert.NotNull(result.Project);
        Assert.True(result.Project.Description!.Length < longDescription.Length);
        Assert.EndsWith("...", result.Project.Description);
    }

    [Fact]
    public void Summarize_LimitsPhasesInRoadmap()
    {
        var summarizer = new ContextSummarizer();
        var manyPhases = Enumerable.Range(1, 20)
            .Select(i => $"Phase {i}: This is a very long phase name that describes many things")
            .ToList();
        var context = new ChatContext
        {
            Roadmap = new RoadmapContext
            {
                PhaseCount = 20,
                Phases = manyPhases,
                CurrentPhase = "Phase 1"
            },
            State = new StateContext(),
            AvailableCommands = Array.Empty<CommandContext>()
        };

        var result = summarizer.Summarize(context);

        Assert.NotNull(result.Roadmap);
        Assert.True(result.Roadmap.Phases.Count <= 5, "Should limit phases to 5");
    }

    [Fact]
    public void Summarize_TruncatesLongPhaseNames()
    {
        var summarizer = new ContextSummarizer();
        var longPhaseName = new string('x', 2000); // Long enough to trigger truncation
        var context = new ChatContext
        {
            Roadmap = new RoadmapContext
            {
                PhaseCount = 1,
                Phases = new List<string> { longPhaseName },
                CurrentPhase = longPhaseName
            },
            State = new StateContext(),
            AvailableCommands = Array.Empty<CommandContext>()
        };

        var result = summarizer.Summarize(context);

        Assert.NotNull(result.Roadmap);
        Assert.True(result.Roadmap.Phases[0].Length < longPhaseName.Length);
        Assert.EndsWith("...", result.Roadmap.Phases[0]);
    }

    [Fact]
    public void Summarize_LimitsAvailableCommands()
    {
        var summarizer = new ContextSummarizer();
        var manyCommands = Enumerable.Range(1, 20)
            .Select(i => new CommandContext
            {
                Name = $"cmd{i}",
                Syntax = $"/cmd{i}",
                Description = "Command description"
            })
            .ToList();
        var context = new ChatContext
        {
            State = new StateContext(),
            AvailableCommands = manyCommands
        };

        var result = summarizer.Summarize(context);

        Assert.True(result.AvailableCommands.Count <= 8, "Should limit commands to 8");
    }

    [Fact]
    public void Summarize_TruncatesLongCommandDescriptions()
    {
        var summarizer = new ContextSummarizer();
        var longDescription = new string('y', 200);
        var context = new ChatContext
        {
            State = new StateContext(),
            AvailableCommands = new List<CommandContext>
            {
                new()
                {
                    Name = "test",
                    Syntax = "/test",
                    Description = longDescription
                }
            }
        };

        var result = summarizer.Summarize(context);

        Assert.True(result.AvailableCommands[0].Description.Length < longDescription.Length);
        Assert.EndsWith("...", result.AvailableCommands[0].Description);
    }

    [Fact]
    public void Summarize_LimitsRecentRuns()
    {
        var summarizer = new ContextSummarizer();
        var manyRuns = Enumerable.Range(1, 10)
            .Select(i => new RunHistoryContext
            {
                RunId = $"run-{i}",
                Status = "completed",
                Timestamp = DateTime.UtcNow.AddHours(-i)
            })
            .ToList();
        var context = new ChatContext
        {
            State = new StateContext(),
            AvailableCommands = Array.Empty<CommandContext>(),
            RecentRuns = manyRuns
        };

        var result = summarizer.Summarize(context);

        Assert.True(result.RecentRuns.Count <= 3, "Should limit runs to 3");
    }

    [Fact]
    public void Summarize_PreservesStateInformation()
    {
        var summarizer = new ContextSummarizer();
        var context = new ChatContext
        {
            State = new StateContext
            {
                Cursor = "PH-0001",
                CurrentPhaseId = "PH-0001",
                CurrentTaskId = "T-001",
                LastRunStatus = "success"
            },
            AvailableCommands = Array.Empty<CommandContext>()
        };

        var result = summarizer.Summarize(context);

        Assert.Equal("PH-0001", result.State.Cursor);
        Assert.Equal("PH-0001", result.State.CurrentPhaseId);
        Assert.Equal("T-001", result.State.CurrentTaskId);
        Assert.Equal("success", result.State.LastRunStatus);
    }

    [Fact]
    public void Summarize_PreservesIsSuccessAndErrorMessage()
    {
        var summarizer = new ContextSummarizer();
        var context = new ChatContext
        {
            State = new StateContext(),
            AvailableCommands = Array.Empty<CommandContext>(),
            IsSuccess = false,
            ErrorMessage = "Something went wrong"
        };

        var result = summarizer.Summarize(context);

        Assert.False(result.IsSuccess);
        Assert.Equal("Something went wrong", result.ErrorMessage);
    }

    [Fact]
    public void Summarize_LimitsProjectGoals()
    {
        var summarizer = new ContextSummarizer();
        var manyGoals = Enumerable.Range(1, 10)
            .Select(i => $"Goal {i}")
            .ToList();
        var context = new ChatContext
        {
            Project = new ProjectContext
            {
                Name = "Test",
                Goals = manyGoals
            },
            State = new StateContext(),
            AvailableCommands = Array.Empty<CommandContext>()
        };

        var result = summarizer.Summarize(context);

        Assert.True(result.Project!.Goals.Count <= 3, "Should limit goals to 3");
    }

    [Fact]
    public void EstimateTokens_EmptyString_ReturnsZero()
    {
        var summarizer = new ContextSummarizer();

        Assert.Equal(0, summarizer.EstimateTokens(""));
        Assert.Equal(0, summarizer.EstimateTokens(null));
    }

    [Fact]
    public void EstimateTokens_ShortText_ReturnsAppropriateEstimate()
    {
        var summarizer = new ContextSummarizer();
        var text = "Hello world"; // 11 chars

        var tokens = summarizer.EstimateTokens(text);

        Assert.Equal(3, tokens); // 11/4 = 2.75, ceiling = 3
    }

    [Fact]
    public void TruncateToTokens_TextWithinBudget_ReturnsUnchanged()
    {
        var summarizer = new ContextSummarizer();
        var text = "Short text";

        var result = summarizer.TruncateToTokens(text, 10);

        Assert.Equal(text, result);
    }

    [Fact]
    public void TruncateToTokens_LongText_TruncatesWithEllipsis()
    {
        var summarizer = new ContextSummarizer();
        var text = new string('x', 100); // 100 chars

        var result = summarizer.TruncateToTokens(text, 10); // 10 tokens = ~40 chars

        Assert.EndsWith("...", result);
        Assert.True(result.Length < text.Length);
    }

    [Fact]
    public void TruncateToTokens_EmptyOrNull_ReturnsEmpty()
    {
        var summarizer = new ContextSummarizer();

        Assert.Equal("", summarizer.TruncateToTokens("", 10));
        Assert.Equal("", summarizer.TruncateToTokens(null, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidMaxTokens_UsesDefault(int invalidTokens)
    {
        var summarizer = new ContextSummarizer(invalidTokens);

        // Should not throw - uses default value internally
        var context = new ChatContext
        {
            State = new StateContext(),
            AvailableCommands = Array.Empty<CommandContext>()
        };
        var result = summarizer.Summarize(context);

        Assert.NotNull(result);
    }

    [Fact]
    public void Summarize_RespectsCustomTokenBudget()
    {
        // Very small budget should still produce valid output
        var summarizer = new ContextSummarizer(maxTokens: 100);
        var veryLongDescription = new string('z', 10000);
        var context = new ChatContext
        {
            Project = new ProjectContext
            {
                Name = "Test",
                Description = veryLongDescription
            },
            State = new StateContext(),
            AvailableCommands = Enumerable.Range(1, 100)
                .Select(i => new CommandContext
                {
                    Name = $"cmd{i}",
                    Syntax = $"/cmd{i}",
                    Description = veryLongDescription
                })
                .ToList()
        };

        var result = summarizer.Summarize(context);

        // Should still produce valid, truncated output
        Assert.NotNull(result.Project);
        Assert.NotNull(result.Project.Description);
        Assert.True(result.AvailableCommands.Count <= 8);
    }
}
