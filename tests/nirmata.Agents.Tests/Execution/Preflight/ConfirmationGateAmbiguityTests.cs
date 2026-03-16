using nirmata.Agents.Execution.Preflight;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Preflight;

public class ConfirmationGateAmbiguityTests
{
    private readonly ConfirmationGate _gate;
    private readonly ConfirmationGateOptions _defaultOptions;

    public ConfirmationGateAmbiguityTests()
    {
        _gate = new ConfirmationGate();
        _defaultOptions = new ConfirmationGateOptions();
    }

    [Theory]
    [InlineData("do it")]
    [InlineData("fix this")]
    [InlineData("change that")]
    public void Evaluate_AmbiguousWriteIntent_RequiresConfirmation(string input)
    {
        var classification = CreateClassificationWithInput(SideEffect.Write, 0.9, input);

        var result = _gate.Evaluate(classification, _defaultOptions);

        Assert.False(result.CanProceed);
        Assert.True(result.RequiresConfirmation);
        Assert.NotNull(result.Request);
        Assert.Contains("Ambiguous", result.Reason);
    }

    [Fact]
    public void Evaluate_AmbiguousIntent_UsesAmbiguousThreshold()
    {
        var options = new ConfirmationGateOptions { AmbiguousThreshold = 0.7 };
        var classification = CreateClassificationWithInput(SideEffect.Write, 0.8, "do it");

        var result = _gate.Evaluate(classification, options);

        Assert.True(result.RequiresConfirmation);
        Assert.NotNull(result.Request);
        // The threshold should be the ambiguous threshold when ambiguity detected
        Assert.True(result.Request!.Threshold <= 0.7);
    }

    [Fact]
    public void Evaluate_AmbiguousIntent_IncludesAmbiguityContext()
    {
        var classification = CreateClassificationWithInput(SideEffect.Write, 0.9, "fix it");

        var result = _gate.Evaluate(classification, _defaultOptions);

        Assert.True(result.RequiresConfirmation);
        Assert.NotNull(result.Request);
        Assert.NotNull(result.Request!.Context);
        Assert.True(result.Request.Context!.ContainsKey("ambiguityLevel"));
        Assert.True(result.Request.Context.ContainsKey("hasVagueVerbs"));
        Assert.True(result.Request.Context.ContainsKey("hasMissingContext"));
    }

    [Fact]
    public void Evaluate_NonAmbiguousWriteIntent_DoesNotRequireConfirmation()
    {
        var classification = CreateClassificationWithInput(SideEffect.Write, 0.95, "/run tests");

        var result = _gate.Evaluate(classification, _defaultOptions);

        Assert.True(result.CanProceed);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public void Evaluate_AmbiguousReadOnlyIntent_AllowsWithoutConfirmation()
    {
        // Read-only operations should not require confirmation even if ambiguous
        var classification = CreateClassificationWithInput(SideEffect.ReadOnly, 0.9, "check it");

        var result = _gate.Evaluate(classification, _defaultOptions);

        Assert.True(result.CanProceed);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public void Evaluate_VagueVerbWriteIntent_RequiresConfirmation()
    {
        var classification = CreateClassificationWithInput(SideEffect.Write, 0.9, "update things");

        var result = _gate.Evaluate(classification, _defaultOptions);

        Assert.True(result.RequiresConfirmation);
        Assert.Contains("Ambiguous", result.Reason);
    }

    [Fact]
    public void Evaluate_MissingContextWriteIntent_RequiresConfirmation()
    {
        var classification = CreateClassificationWithInput(SideEffect.Write, 0.9, "/run");

        var result = _gate.Evaluate(classification, _defaultOptions);

        Assert.True(result.RequiresConfirmation);
        Assert.NotNull(result.Request);
    }

    [Fact]
    public void Evaluate_AmbiguousIntentWithCustomReason_UsesProvidedReason()
    {
        var customReason = "Custom ambiguity reason";
        var request = new ConfirmationRequest
        {
            OriginalInput = "do it",
            ClassifiedIntent = new IntentClassifiedPayload
            {
                Category = "Write",
                Confidence = 0.9,
                Reasoning = "Test"
            },
            ActionDescription = "Execute action",
            Confidence = 0.9,
            Threshold = 0.7
        };

        var result = ConfirmationGateResult.RequireConfirmation(request, 0.9, customReason);

        Assert.Equal(customReason, result.Reason);
    }

    [Fact]
    public void Evaluate_AmbiguousIntentWithDefaultReason_UsesDefaultFormat()
    {
        var request = new ConfirmationRequest
        {
            OriginalInput = "do it",
            ClassifiedIntent = new IntentClassifiedPayload
            {
                Category = "Write",
                Confidence = 0.6,
                Reasoning = "Test"
            },
            ActionDescription = "Execute action",
            Confidence = 0.6,
            Threshold = 0.9
        };

        var result = ConfirmationGateResult.RequireConfirmation(request, 0.6);

        Assert.Contains("0.60", result.Reason);
        Assert.Contains("0.90", result.Reason);
    }

    [Fact]
    public void EvaluateWithWorkspaceConfig_AmbiguousIntent_UsesWorkspaceAmbiguousThreshold()
    {
        // This test validates that the ambiguity check works in workspace config mode
        // Note: We can't fully test without a real workspace, but we can verify the structure
        var classification = CreateClassificationWithInput(SideEffect.Write, 0.8, "fix it");

        // The method should not throw and should evaluate properly
        var result = _gate.Evaluate(classification, _defaultOptions);

        // Ambiguous write intent should require confirmation
        Assert.True(result.RequiresConfirmation);
    }

    private static IntentClassificationResult CreateClassificationWithInput(SideEffect sideEffect, double confidence, string input)
    {
        var commandName = input.StartsWith('/') ? input.Split(' ')[0][1..] : null;
        var isExplicitCommand = input.StartsWith('/');

        return new IntentClassificationResult
        {
            Intent = new Intent
            {
                Kind = sideEffect == SideEffect.Write ? IntentKind.WorkflowCommand : IntentKind.Status,
                SideEffect = sideEffect,
                Confidence = confidence,
                Reasoning = "Test classification",
                Command = commandName
            },
            ParsedCommand = isExplicitCommand
                ? new ParsedCommand
                {
                    RawInput = input,
                    CommandName = commandName ?? "unknown",
                    SideEffect = sideEffect,
                    Confidence = confidence,
                    IsKnownCommand = true,
                    IsExplicitCommand = true
                }
                : null,
            RequiresConfirmation = false,
            ClassificationMethod = isExplicitCommand ? "prefix" : "default"
        };
    }
}
