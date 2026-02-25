using Gmsd.Agents.Execution.Preflight;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Preflight;

public class ConfirmationGateTests
{
    private readonly ConfirmationGate _gate;
    private readonly ConfirmationGateOptions _defaultOptions;

    public ConfirmationGateTests()
    {
        _gate = new ConfirmationGate();
        _defaultOptions = new ConfirmationGateOptions();
    }

    [Fact]
    public void Evaluate_ChatIntent_AllowsWithoutConfirmation()
    {
        var classification = CreateClassification(SideEffect.None, 0.9, false);

        var result = _gate.Evaluate(classification, _defaultOptions);

        Assert.True(result.CanProceed);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public void Evaluate_ReadOnlyIntent_AllowsWithoutConfirmation()
    {
        var classification = CreateClassification(SideEffect.ReadOnly, 1.0, false);

        var result = _gate.Evaluate(classification, _defaultOptions);

        Assert.True(result.CanProceed);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public void Evaluate_WriteIntentHighConfidence_AllowsWithoutConfirmation()
    {
        var classification = CreateClassification(SideEffect.Write, 0.95, false);

        var result = _gate.Evaluate(classification, _defaultOptions);

        Assert.True(result.CanProceed);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public void Evaluate_WriteIntentLowConfidence_RequiresConfirmation()
    {
        var classification = CreateClassification(SideEffect.Write, 0.7, true);

        var result = _gate.Evaluate(classification, _defaultOptions);

        Assert.False(result.CanProceed);
        Assert.True(result.RequiresConfirmation);
        Assert.NotNull(result.Request);
    }

    [Fact]
    public void Evaluate_AlwaysConfirmWritesEnabled_RequiresConfirmation()
    {
        var options = new ConfirmationGateOptions { AlwaysConfirmWrites = true };
        var classification = CreateClassification(SideEffect.Write, 1.0, false);

        var result = _gate.Evaluate(classification, options);

        Assert.False(result.CanProceed);
        Assert.True(result.RequiresConfirmation);
    }

    [Fact]
    public void Evaluate_NoConfirmationCommand_DoesNotRequireConfirmation()
    {
        var classification = CreateClassificationWithCommand("help", SideEffect.ReadOnly, 1.0, false);

        var result = _gate.Evaluate(classification, _defaultOptions);

        Assert.True(result.CanProceed);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public void Evaluate_RequestContainsCorrectDetails()
    {
        var classification = CreateClassification(SideEffect.Write, 0.7, true);

        var result = _gate.Evaluate(classification, _defaultOptions);

        Assert.NotNull(result.Request);
        Assert.NotNull(result.Request!.Id);
        Assert.NotEmpty(result.Request.Id);
        Assert.Equal(0.7, result.Request.Confidence);
        Assert.Equal(0.9, result.Request.Threshold);
        Assert.NotNull(result.Request.ActionDescription);
        Assert.NotNull(result.Request.ClassifiedIntent);
    }

    [Fact]
    public void ProcessResponse_ValidConfirmation_ReturnsTrue()
    {
        var classification = CreateClassification(SideEffect.Write, 0.7, true);
        var evaluation = _gate.Evaluate(classification, _defaultOptions);
        var requestId = evaluation.Request!.Id;

        var response = new ConfirmationResponse
        {
            RequestId = requestId,
            Confirmed = true
        };

        var result = _gate.ProcessResponse(response);

        Assert.True(result);
    }

    [Fact]
    public void ProcessResponse_DeniedConfirmation_ReturnsFalse()
    {
        var classification = CreateClassification(SideEffect.Write, 0.7, true);
        var evaluation = _gate.Evaluate(classification, _defaultOptions);
        var requestId = evaluation.Request!.Id;

        var response = new ConfirmationResponse
        {
            RequestId = requestId,
            Confirmed = false
        };

        var result = _gate.ProcessResponse(response);

        Assert.False(result);
    }

    [Fact]
    public void ProcessResponse_InvalidRequestId_ReturnsFalse()
    {
        var response = new ConfirmationResponse
        {
            RequestId = "non-existent-id",
            Confirmed = true
        };

        var result = _gate.ProcessResponse(response);

        Assert.False(result);
    }

    [Fact]
    public void ProcessResponse_TimedOutRequest_ReturnsFalse()
    {
        var classification = CreateClassification(SideEffect.Write, 0.7, true);
        var options = new ConfirmationGateOptions { Timeout = TimeSpan.FromMilliseconds(1) };
        var evaluation = _gate.Evaluate(classification, options);
        var requestId = evaluation.Request!.Id;

        // Wait for timeout
        Thread.Sleep(10);

        var response = new ConfirmationResponse
        {
            RequestId = requestId,
            Confirmed = true
        };

        var result = _gate.ProcessResponse(response);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_CustomThreshold_UsesCustomValue()
    {
        var options = new ConfirmationGateOptions { ConfirmationThreshold = 0.8 };
        var classification = CreateClassification(SideEffect.Write, 0.85, false);

        var result = _gate.Evaluate(classification, options);

        Assert.True(result.CanProceed);
    }

    [Fact]
    public void GetPendingConfirmation_ExistingRequest_ReturnsRequest()
    {
        var classification = CreateClassification(SideEffect.Write, 0.7, true);
        var evaluation = _gate.Evaluate(classification, _defaultOptions);
        var requestId = evaluation.Request!.Id;

        var pending = _gate.GetPendingConfirmation(requestId);

        Assert.NotNull(pending);
        Assert.Equal(requestId, pending!.Id);
    }

    [Fact]
    public void GetPendingConfirmation_NonExistent_ReturnsNull()
    {
        var pending = _gate.GetPendingConfirmation("non-existent-id");

        Assert.Null(pending);
    }

    private static IntentClassificationResult CreateClassification(SideEffect sideEffect, double confidence, bool requiresConfirmation)
    {
        return new IntentClassificationResult
        {
            Intent = new Intent
            {
                Kind = sideEffect == SideEffect.Write ? IntentKind.WorkflowCommand : IntentKind.Unknown,
                SideEffect = sideEffect,
                Confidence = confidence,
                Reasoning = "Test classification"
            },
            RequiresConfirmation = requiresConfirmation,
            ConfirmationThreshold = 0.9
        };
    }

    private static IntentClassificationResult CreateClassificationWithCommand(string commandName, SideEffect sideEffect, double confidence, bool requiresConfirmation)
    {
        return new IntentClassificationResult
        {
            Intent = new Intent
            {
                Kind = sideEffect == SideEffect.Write ? IntentKind.WorkflowCommand : IntentKind.Help,
                SideEffect = sideEffect,
                Confidence = confidence,
                Reasoning = "Test classification",
                Command = commandName
            },
            ParsedCommand = new ParsedCommand
            {
                RawInput = $"/{commandName}",
                CommandName = commandName,
                SideEffect = sideEffect,
                Confidence = confidence,
                IsKnownCommand = true
            },
            RequiresConfirmation = requiresConfirmation,
            ConfirmationThreshold = 0.9
        };
    }
}
