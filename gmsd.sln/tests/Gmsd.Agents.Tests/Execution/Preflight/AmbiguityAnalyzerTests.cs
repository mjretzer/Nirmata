using Gmsd.Agents.Execution.Preflight;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Preflight;

public class AmbiguityAnalyzerTests
{
    private readonly AmbiguityAnalyzer _analyzer;

    public AmbiguityAnalyzerTests()
    {
        _analyzer = new AmbiguityAnalyzer();
    }

    [Fact]
    public void Analyze_ClearIntent_NoAmbiguityDetected()
    {
        var input = "/run test-suite --verbose";
        var intent = CreateIntent(SideEffect.Write, 0.95, "run", new[] { "test-suite" });
        var classification = CreateClassification(intent, input);

        var signals = _analyzer.Analyze(input, classification);

        Assert.False(signals.IsAmbiguous);
        Assert.Equal(AmbiguityLevel.None, signals.Level);
    }

    [Fact]
    public void Analyze_LowConfidence_DetectsAmbiguity()
    {
        var input = "/run something";
        var intent = CreateIntent(SideEffect.Write, 0.6, "run", new[] { "something" });
        var classification = CreateClassification(intent, input);

        var signals = _analyzer.Analyze(input, classification, threshold: 0.9);

        Assert.True(signals.IsAmbiguous);
        Assert.True(signals.HasLowConfidence);
        Assert.Equal(0.6, signals.ConfidenceScore);
        Assert.Contains("0.60", signals.Reasoning);
    }

    [Theory]
    [InlineData("do it")]
    [InlineData("fix this")]
    [InlineData("change that")]
    [InlineData("update it")]
    [InlineData("handle this")]
    public void Analyze_VagueVerbs_DetectsAmbiguity(string input)
    {
        var intent = CreateIntent(SideEffect.Write, 0.9, null, null);
        var classification = CreateClassification(intent, input);

        var signals = _analyzer.Analyze(input, classification);

        Assert.True(signals.IsAmbiguous);
        Assert.True(signals.HasVagueVerbs);
        Assert.NotEmpty(signals.DetectedVagueVerbs);
    }

    [Fact]
    public void Analyze_WriteWithoutTarget_DetectsMissingContext()
    {
        var input = "/run";
        var intent = CreateIntent(SideEffect.Write, 0.9, "run", Array.Empty<string>());
        var classification = CreateClassification(intent, input);

        var signals = _analyzer.Analyze(input, classification);

        Assert.True(signals.IsAmbiguous);
        Assert.True(signals.HasMissingContext);
        Assert.Contains("target", signals.MissingContextDescription ?? "");
    }

    [Fact]
    public void Analyze_EmptyInput_DetectsMissingContext()
    {
        var input = "";
        var intent = CreateIntent(SideEffect.Write, 0.0, null, null);
        var classification = CreateClassification(intent, input);

        var signals = _analyzer.Analyze(input, classification);

        Assert.True(signals.IsAmbiguous);
        Assert.True(signals.HasMissingContext);
    }

    [Fact]
    public void Analyze_MultipleSignals_DetectsHighAmbiguity()
    {
        var input = "do it";
        var intent = CreateIntent(SideEffect.Write, 0.5, null, null);
        var classification = CreateClassification(intent, input);

        var signals = _analyzer.Analyze(input, classification, threshold: 0.9);

        Assert.True(signals.IsAmbiguous);
        Assert.Equal(AmbiguityLevel.High, signals.Level);
        Assert.True(signals.HasLowConfidence);
        Assert.True(signals.HasVagueVerbs);
        Assert.True(signals.HasMissingContext);
    }

    [Fact]
    public void Analyze_ReadOnlyIntent_NoAmbiguityForLowConfidence()
    {
        var input = "/status";
        var intent = CreateIntent(SideEffect.ReadOnly, 0.6, "status", null);
        var classification = CreateClassification(intent, input);

        var signals = _analyzer.Analyze(input, classification, threshold: 0.9);

        // Read-only intents should not trigger missing context for write operations
        Assert.False(signals.HasMissingContext);
    }

    [Theory]
    [InlineData("update", "update")]
    [InlineData("fix", "fix")]
    [InlineData("change file.txt", "change")]
    public void Analyze_DetectsSpecificVagueVerb(string input, string expectedVerb)
    {
        var intent = CreateIntent(SideEffect.Write, 0.9, null, null);
        var classification = CreateClassification(intent, input);

        var signals = _analyzer.Analyze(input, classification);

        Assert.True(signals.HasVagueVerbs);
        Assert.Contains(signals.DetectedVagueVerbs, v => v.Equals(expectedVerb, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_ExistingAmbiguity_MergesSignals()
    {
        var input = "fix it";
        var intent = CreateIntent(SideEffect.Write, 0.5, null, null);
        var existingAmbiguity = new AmbiguitySignals
        {
            Level = AmbiguityLevel.Low,
            HasLowConfidence = true,
            ConfidenceScore = 0.5,
            Reasoning = "Already low confidence"
        };
        var classification = CreateClassification(intent, input, existingAmbiguity);

        var signals = _analyzer.Analyze(input, classification, threshold: 0.9);

        Assert.True(signals.IsAmbiguous);
        // Should keep the more severe level
        Assert.True(signals.Level >= AmbiguityLevel.Low);
    }

    [Fact]
    public void Analyze_ClearIntent_ReturnsCorrectProperties()
    {
        var input = "/run tests";
        var intent = CreateIntent(SideEffect.Write, 0.95, "run", new[] { "tests" });
        var classification = CreateClassification(intent, input);

        var signals = _analyzer.Analyze(input, classification);

        Assert.False(signals.IsAmbiguous);
        Assert.Equal(AmbiguityLevel.None, signals.Level);
        Assert.False(signals.HasLowConfidence);
        Assert.False(signals.HasVagueVerbs);
        Assert.False(signals.HasMissingContext);
        Assert.Empty(signals.DetectedVagueVerbs);
        Assert.Equal(0.95, signals.ConfidenceScore);
    }

    private static Intent CreateIntent(SideEffect sideEffect, double confidence, string? command, string[]? targets)
    {
        return new Intent
        {
            Kind = sideEffect == SideEffect.Write ? IntentKind.WorkflowCommand : IntentKind.Status,
            SideEffect = sideEffect,
            Confidence = confidence,
            Reasoning = "Test intent",
            Command = command,
            Targets = targets
        };
    }

    private static IntentClassificationResult CreateClassification(Intent intent, string rawInput, AmbiguitySignals? existingAmbiguity = null)
    {
        var command = intent.Command != null
            ? new ParsedCommand
            {
                RawInput = rawInput,
                CommandName = intent.Command,
                SideEffect = intent.SideEffect,
                Confidence = intent.Confidence,
                IsKnownCommand = true
            }
            : null;

        return new IntentClassificationResult
        {
            Intent = intent,
            ParsedCommand = command,
            ClassificationMethod = "test",
            RequiresConfirmation = false,
            Ambiguity = existingAmbiguity
        };
    }
}
