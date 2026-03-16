using System.Text.Json;
using FluentAssertions;
using nirmata.Agents.Execution.ControlPlane.Llm.Filters;
using nirmata.Aos.Engine.Evidence;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution.ControlPlane.Llm.Filters;

/// <summary>
/// Unit tests for <see cref="AosEvidenceFunctionFilter"/>.
/// </summary>
public sealed class AosEvidenceFunctionFilterTests
{
    private readonly Mock<ILlmEvidenceWriter> _evidenceWriterMock;
    private const string TestRunId = "RUN-20240205-001";
    private const string TestProvider = "openai";
    private const string TestModel = "gpt-4";

    public AosEvidenceFunctionFilterTests()
    {
        _evidenceWriterMock = new Mock<ILlmEvidenceWriter>();
    }

    [Fact]
    public void Constructor_WhenEvidenceWriterIsNull_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new AosEvidenceFunctionFilter(
            null!,
            TestRunId,
            TestProvider,
            TestModel);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("evidenceWriter");
    }

    [Fact]
    public void Constructor_WhenRunIdIsNull_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new AosEvidenceFunctionFilter(
            _evidenceWriterMock.Object,
            null!,
            TestProvider,
            TestModel);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("runId");
    }

    [Fact]
    public void Constructor_WhenProviderIsNull_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new AosEvidenceFunctionFilter(
            _evidenceWriterMock.Object,
            TestRunId,
            null!,
            TestModel);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("provider");
    }

    [Fact]
    public async Task OnFunctionInvocationAsync_WhenInvocationSucceeds_WritesSuccessEnvelope()
    {
        // Arrange
        var filter = CreateFilter();
        var context = CreateFunctionInvocationContext();
        var capturedEnvelope = CaptureWrittenEnvelope();

        // Act
        await filter.OnFunctionInvocationAsync(context, async ctx =>
        {
            // Simulate successful completion by setting result
            ctx.Result = CreateMockFunctionResult("Test response content");
            await Task.CompletedTask;
        });

        // Assert
        _evidenceWriterMock.Verify(x => x.Write(It.IsAny<LlmCallEnvelope>()), Times.Once);
        capturedEnvelope.Should().NotBeNull();
        capturedEnvelope!.Status.Should().Be("success");
        capturedEnvelope.RunId.Should().Be(TestRunId);
        capturedEnvelope.Provider.Should().Be(TestProvider);
        capturedEnvelope.Model.Should().Be(TestModel);
        capturedEnvelope.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        capturedEnvelope.CallId.Should().NotBeNullOrEmpty();
        capturedEnvelope.Request.Should().NotBeNull();
        capturedEnvelope.Response.Should().NotBeNull();
        capturedEnvelope.Error.Should().BeNull();
    }

    [Fact]
    public async Task OnFunctionInvocationAsync_WhenInvocationThrows_WritesErrorEnvelope()
    {
        // Arrange
        var filter = CreateFilter();
        var context = CreateFunctionInvocationContext();
        var capturedEnvelope = CaptureWrittenEnvelope();
        var expectedException = new InvalidOperationException("Test failure");

        // Act
        Func<Task> act = async () => await filter.OnFunctionInvocationAsync(context, async _ =>
        {
            await Task.CompletedTask;
            throw expectedException;
        });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _evidenceWriterMock.Verify(x => x.Write(It.IsAny<LlmCallEnvelope>()), Times.Once);
        capturedEnvelope.Should().NotBeNull();
        capturedEnvelope!.Status.Should().Be("error");
        capturedEnvelope.RunId.Should().Be(TestRunId);
        capturedEnvelope.Provider.Should().Be(TestProvider);
        capturedEnvelope.Model.Should().Be(TestModel);
        capturedEnvelope.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        capturedEnvelope.Error.Should().NotBeNull();
        capturedEnvelope.Response.Should().BeNull();
    }

    [Fact]
    public async Task OnFunctionInvocationAsync_WhenResultIsNull_WritesEnvelopeWithNoResultStatus()
    {
        // Arrange
        var filter = CreateFilter();
        var context = CreateFunctionInvocationContext();
        var capturedEnvelope = CaptureWrittenEnvelope();

        // Act
        await filter.OnFunctionInvocationAsync(context, async ctx =>
        {
            ctx.Result = null!;
            await Task.CompletedTask;
        });

        // Assert
        _evidenceWriterMock.Verify(x => x.Write(It.IsAny<LlmCallEnvelope>()), Times.Once);
        capturedEnvelope.Should().NotBeNull();
        capturedEnvelope!.Status.Should().Be("success");
        capturedEnvelope.Response.Should().NotBeNull();
    }

    [Fact]
    public async Task OnFunctionInvocationAsync_WritesCorrectRequestData()
    {
        // Arrange
        var filter = CreateFilter();
        var kernelFunction = CreateMockKernelFunction("TestFunction", "Test plugin function", "TestPlugin");
        var arguments = new KernelArguments
        {
            ["param1"] = "string value",
            ["param2"] = 42,
            ["param3"] = true,
            ["param4"] = 3.14,
            ["param5"] = new { Complex = "object" } // Should be type-only
        };
        var context = CreateFunctionInvocationContext(kernelFunction, arguments);
        var capturedEnvelope = CaptureWrittenEnvelope();

        // Act
        await filter.OnFunctionInvocationAsync(context, async ctx =>
        {
            ctx.Result = CreateMockFunctionResult("Response");
            await Task.CompletedTask;
        });

        // Assert
        capturedEnvelope.Should().NotBeNull();
        capturedEnvelope!.Request.Should().NotBeNull();
        
        var requestJson = JsonSerializer.Serialize(capturedEnvelope.Request);
        using var doc = JsonDocument.Parse(requestJson);
        var root = doc.RootElement;
        
        root.GetProperty("functionName").GetString().Should().Be("TestFunction");
        root.GetProperty("functionDescription").GetString().Should().Be("Test plugin function");
        root.GetProperty("pluginName").GetString().Should().Be("TestPlugin");
        
        var args = root.GetProperty("arguments");
        args.GetProperty("param1").GetString().Should().Be("string value");
        args.GetProperty("param2").GetInt32().Should().Be(42);
        args.GetProperty("param3").GetBoolean().Should().BeTrue();
        args.GetProperty("param4").GetDouble().Should().Be(3.14);
        args.GetProperty("param5").GetString().Should().Be("[<>f__AnonymousType0`2]"); // Type name for complex object
    }

    [Fact]
    public async Task OnFunctionInvocationAsync_GeneratesCallId_WithCorrectFormat()
    {
        // Arrange
        var filter = CreateFilter();
        var kernelFunction = CreateMockKernelFunction("MyTestFunction", null, null);
        var context = CreateFunctionInvocationContext(kernelFunction);
        var capturedEnvelope = CaptureWrittenEnvelope();

        // Act
        await filter.OnFunctionInvocationAsync(context, async ctx =>
        {
            ctx.Result = CreateMockFunctionResult("Response");
            await Task.CompletedTask;
        });

        // Assert
        capturedEnvelope.Should().NotBeNull();
        capturedEnvelope!.CallId.Should().NotBeNullOrEmpty();
        // Format: {functionName}-{timestamp}-{random}
        capturedEnvelope.CallId.Should().StartWith("MyTestFunction-");
        var parts = capturedEnvelope.CallId.Split('-');
        parts.Length.Should().BeGreaterThanOrEqualTo(3);
        // Should contain timestamp (numeric) and random suffix
        long.TryParse(parts[1], out _).Should().BeTrue("second part should be a timestamp");
    }

    [Fact]
    public async Task OnFunctionInvocationAsync_WhenModelIsNull_WritesEnvelopeWithNullModel()
    {
        // Arrange
        var filter = new AosEvidenceFunctionFilter(
            _evidenceWriterMock.Object,
            TestRunId,
            TestProvider,
            model: null);
        var context = CreateFunctionInvocationContext();
        var capturedEnvelope = CaptureWrittenEnvelope();

        // Act
        await filter.OnFunctionInvocationAsync(context, async ctx =>
        {
            ctx.Result = CreateMockFunctionResult("Response");
            await Task.CompletedTask;
        });

        // Assert
        capturedEnvelope.Should().NotBeNull();
        capturedEnvelope!.Model.Should().BeNull();
    }

    [Fact]
    public async Task OnFunctionInvocationAsync_WhenFunctionIsNull_UsesUnknownInCallId()
    {
        // Arrange
        var filter = CreateFilter();
        var context = CreateFunctionInvocationContext(function: null);
        var capturedEnvelope = CaptureWrittenEnvelope();

        // Act
        await filter.OnFunctionInvocationAsync(context, async ctx =>
        {
            ctx.Result = CreateMockFunctionResult("Response");
            await Task.CompletedTask;
        });

        // Assert
        capturedEnvelope.Should().NotBeNull();
        capturedEnvelope!.CallId.Should().StartWith("unknown-");
    }

    private AosEvidenceFunctionFilter CreateFilter()
    {
        return new AosEvidenceFunctionFilter(
            _evidenceWriterMock.Object,
            TestRunId,
            TestProvider,
            TestModel);
    }

    private LlmCallEnvelope? CaptureWrittenEnvelope()
    {
        LlmCallEnvelope? captured = null;
        _evidenceWriterMock
            .Setup(x => x.Write(It.IsAny<LlmCallEnvelope>()))
            .Callback<LlmCallEnvelope>(envelope => captured = envelope);
        return captured;
    }

    private FunctionInvocationContext CreateFunctionInvocationContext(
        KernelFunction? function = null,
        KernelArguments? arguments = null)
    {
        // Create a mock Kernel
        var kernelMock = new Mock<Kernel>();
        
        // Create the context using the public constructor or factory method
        // Since FunctionInvocationContext may not have a public constructor,
        // we use Moq to create a mock with settable properties
        var contextMock = new Mock<FunctionInvocationContext>(
            kernelMock.Object,
            function ?? CreateMockKernelFunction("TestFunction", "Test description", "TestPlugin"),
            arguments ?? new KernelArguments(),
            CancellationToken.None);

        // Setup properties to be settable
        contextMock.SetupProperty(x => x.Result);
        
        return contextMock.Object;
    }

    private KernelFunction CreateMockKernelFunction(string name, string? description, string? pluginName)
    {
        var mock = new Mock<KernelFunction>();
        mock.Setup(x => x.Name).Returns(name);
        mock.Setup(x => x.Description).Returns(description ?? string.Empty);
        mock.Setup(x => x.PluginName).Returns(pluginName ?? string.Empty);
        return mock.Object;
    }

    private FunctionResult CreateMockFunctionResult(string content)
    {
        // Create a mock FunctionResult with a simple value
        var mock = new Mock<FunctionResult>(
            MockBehavior.Loose,
            CreateMockKernelFunction("Test", string.Empty, string.Empty),
            new Kernel(),
            content,
            null!);
        
        // Setup the ToString to return the content
        mock.Setup(x => x.ToString()).Returns(content);
        
        return mock.Object;
    }
}
