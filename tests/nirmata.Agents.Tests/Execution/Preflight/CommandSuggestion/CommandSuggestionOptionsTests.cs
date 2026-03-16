using nirmata.Agents.Execution.Preflight.CommandSuggestion;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Preflight.CommandSuggestion;

public class CommandSuggestionOptionsTests
{
    [Fact]
    public void Defaults_ConfidenceThreshold_Is07()
    {
        var options = new CommandSuggestionOptions();

        Assert.Equal(0.7, options.ConfidenceThreshold);
    }

    [Fact]
    public void Defaults_MaxInputLength_Is1000()
    {
        var options = new CommandSuggestionOptions();

        Assert.Equal(1000, options.MaxInputLength);
    }

    [Fact]
    public void Defaults_IncludeExamples_IsTrue()
    {
        var options = new CommandSuggestionOptions();

        Assert.True(options.IncludeExamples);
    }

    [Fact]
    public void ConfidenceThreshold_CanBeOverridden()
    {
        var options = new CommandSuggestionOptions
        {
            ConfidenceThreshold = 0.5
        };

        Assert.Equal(0.5, options.ConfidenceThreshold);
    }

    [Fact]
    public void MaxInputLength_CanBeOverridden()
    {
        var options = new CommandSuggestionOptions
        {
            MaxInputLength = 500
        };

        Assert.Equal(500, options.MaxInputLength);
    }

    [Fact]
    public void IncludeExamples_CanBeDisabled()
    {
        var options = new CommandSuggestionOptions
        {
            IncludeExamples = false
        };

        Assert.False(options.IncludeExamples);
    }
}
