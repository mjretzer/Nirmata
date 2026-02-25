using Gmsd.Agents.Execution.Preflight.CommandSuggestion;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Preflight.CommandSuggestion;

public class CommandProposalTests
{
    [Fact]
    public void IsValid_WithValidProperties_ReturnsTrue()
    {
        var proposal = new CommandProposal
        {
            CommandName = "run",
            Arguments = new[] { "--verbose", "test-suite" },
            Confidence = 0.85,
            Reasoning = "User wants to execute tests",
            FormattedCommand = "/run --verbose test-suite"
        };

        Assert.True(proposal.IsValid());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyCommandName_ReturnsFalse(string? commandName)
    {
        var proposal = new CommandProposal
        {
            CommandName = commandName!,
            Confidence = 0.85
        };

        Assert.False(proposal.IsValid());
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    [InlineData(-1.0)]
    public void IsValid_WithInvalidConfidence_ReturnsFalse(double confidence)
    {
        var proposal = new CommandProposal
        {
            CommandName = "plan",
            Confidence = confidence
        };

        Assert.False(proposal.IsValid());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void IsValid_WithBoundaryConfidenceValues_ReturnsTrue(double confidence)
    {
        var proposal = new CommandProposal
        {
            CommandName = "status",
            Confidence = confidence
        };

        Assert.True(proposal.IsValid());
    }

    [Fact]
    public void GetValidationErrors_WithValidProperties_ReturnsEmpty()
    {
        var proposal = new CommandProposal
        {
            CommandName = "fix",
            Confidence = 0.9
        };

        var errors = proposal.GetValidationErrors();

        Assert.Empty(errors);
    }

    [Fact]
    public void GetValidationErrors_WithEmptyCommandName_ReturnsError()
    {
        var proposal = new CommandProposal
        {
            CommandName = "",
            Confidence = 0.9
        };

        var errors = proposal.GetValidationErrors();

        Assert.Single(errors);
        Assert.Contains("CommandName must be non-empty", errors[0]);
    }

    [Fact]
    public void GetValidationErrors_WithNegativeConfidence_ReturnsError()
    {
        var proposal = new CommandProposal
        {
            CommandName = "run",
            Confidence = -0.5
        };

        var errors = proposal.GetValidationErrors();

        Assert.Single(errors);
        Assert.Contains("Confidence must be between 0.0 and 1.0", errors[0]);
    }

    [Fact]
    public void GetValidationErrors_WithHighConfidence_ReturnsError()
    {
        var proposal = new CommandProposal
        {
            CommandName = "run",
            Confidence = 1.5
        };

        var errors = proposal.GetValidationErrors();

        Assert.Single(errors);
        Assert.Contains("Confidence must be between 0.0 and 1.0", errors[0]);
    }

    [Fact]
    public void GetValidationErrors_WithMultipleErrors_ReturnsAllErrors()
    {
        var proposal = new CommandProposal
        {
            CommandName = "",
            Confidence = 2.0
        };

        var errors = proposal.GetValidationErrors();

        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Arguments_DefaultsToEmptyArray()
    {
        var proposal = new CommandProposal
        {
            CommandName = "help",
            Confidence = 0.8
        };

        Assert.NotNull(proposal.Arguments);
        Assert.Empty(proposal.Arguments);
    }

    [Fact]
    public void OptionalProperties_CanBeNull()
    {
        var proposal = new CommandProposal
        {
            CommandName = "status",
            Confidence = 0.75,
            Reasoning = null,
            FormattedCommand = null
        };

        Assert.Null(proposal.Reasoning);
        Assert.Null(proposal.FormattedCommand);
        Assert.True(proposal.IsValid());
    }
}
