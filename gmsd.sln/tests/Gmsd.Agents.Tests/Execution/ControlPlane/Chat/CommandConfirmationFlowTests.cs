using Gmsd.Agents.Execution.ControlPlane.Chat;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ControlPlane.Chat;

public class CommandConfirmationFlowTests
{
    private readonly CommandConfirmationFlow _flow = new();

    [Fact]
    public void CreateConfirmation_CreatesValidRequest()
    {
        var request = _flow.CreateConfirmation("run", new Dictionary<string, string> { { "workflow", "test" } }, "Test message", 0.9);

        Assert.NotNull(request);
        Assert.NotEmpty(request.ConfirmationId);
        Assert.Equal("run", request.CommandName);
        Assert.Equal("Test message", request.Message);
        Assert.Equal(0.9, request.Confidence);
    }

    [Fact]
    public void GetPendingConfirmation_ReturnsCreatedRequest()
    {
        var created = _flow.CreateConfirmation("help", new Dictionary<string, string>(), "Help message", 0.95);
        var retrieved = _flow.GetPendingConfirmation(created.ConfirmationId);

        Assert.NotNull(retrieved);
        Assert.Equal(created.ConfirmationId, retrieved.ConfirmationId);
        Assert.Equal("help", retrieved.CommandName);
    }

    [Fact]
    public void GetPendingConfirmation_WithInvalidId_ReturnsNull()
    {
        var result = _flow.GetPendingConfirmation("invalid-id");

        Assert.Null(result);
    }

    [Fact]
    public void ProcessResponse_WithAcceptance_ReturnsResponse()
    {
        var request = _flow.CreateConfirmation("run", new Dictionary<string, string>(), "Test", 0.9);
        var response = _flow.ProcessResponse(request.ConfirmationId, true, "Looks good");

        Assert.NotNull(response);
        Assert.Equal(request.ConfirmationId, response.ConfirmationId);
        Assert.True(response.Accepted);
        Assert.Equal("Looks good", response.UserFeedback);
    }

    [Fact]
    public void ProcessResponse_WithRejection_ReturnsResponse()
    {
        var request = _flow.CreateConfirmation("run", new Dictionary<string, string>(), "Test", 0.9);
        var response = _flow.ProcessResponse(request.ConfirmationId, false, "Not what I wanted");

        Assert.NotNull(response);
        Assert.False(response.Accepted);
        Assert.Equal("Not what I wanted", response.UserFeedback);
    }

    [Fact]
    public void ProcessResponse_RemovesPendingConfirmation()
    {
        var request = _flow.CreateConfirmation("run", new Dictionary<string, string>(), "Test", 0.9);
        _flow.ProcessResponse(request.ConfirmationId, true);

        var retrieved = _flow.GetPendingConfirmation(request.ConfirmationId);
        Assert.Null(retrieved);
    }

    [Fact]
    public void ProcessResponse_WithInvalidId_ReturnsNull()
    {
        var response = _flow.ProcessResponse("invalid-id", true);

        Assert.Null(response);
    }

    [Fact]
    public void GetAllPendingConfirmations_ReturnsAllRequests()
    {
        var request1 = _flow.CreateConfirmation("run", new Dictionary<string, string>(), "Test 1", 0.9);
        var request2 = _flow.CreateConfirmation("plan", new Dictionary<string, string>(), "Test 2", 0.8);
        var request3 = _flow.CreateConfirmation("verify", new Dictionary<string, string>(), "Test 3", 0.85);

        var all = _flow.GetAllPendingConfirmations();

        Assert.Equal(3, all.Count);
        Assert.Contains(all, r => r.ConfirmationId == request1.ConfirmationId);
        Assert.Contains(all, r => r.ConfirmationId == request2.ConfirmationId);
        Assert.Contains(all, r => r.ConfirmationId == request3.ConfirmationId);
    }

    [Fact]
    public void FormatConfirmationMessage_WithArguments_IncludesArguments()
    {
        var request = new CommandConfirmationFlow.ConfirmationRequest
        {
            ConfirmationId = "test-id",
            CommandName = "run",
            Arguments = new Dictionary<string, string> { { "workflow", "build" } },
            Message = "This will build the project",
            Confidence = 0.9
        };

        var formatted = CommandConfirmationFlow.FormatConfirmationMessage(request);

        Assert.Contains("/run", formatted);
        Assert.Contains("workflow=build", formatted);
        Assert.Contains("This will build the project", formatted);
    }

    [Fact]
    public void FormatConfirmationMessage_WithoutArguments_OmitsArguments()
    {
        var request = new CommandConfirmationFlow.ConfirmationRequest
        {
            ConfirmationId = "test-id",
            CommandName = "help",
            Arguments = new Dictionary<string, string>(),
            Message = "Show available commands",
            Confidence = 0.95
        };

        var formatted = CommandConfirmationFlow.FormatConfirmationMessage(request);

        Assert.Contains("/help", formatted);
        Assert.DoesNotContain("with arguments", formatted);
    }

    [Fact]
    public void CleanupExpiredConfirmations_RemovesExpiredRequests()
    {
        // Create a request with a very short timeout
        var request = _flow.CreateConfirmation("run", new Dictionary<string, string>(), "Test", 0.9);
        
        // Wait for the request to expire (longer than the default 5 minute timeout)
        // Instead, let's test the cleanup logic by creating a request that should be cleaned up
        // by modifying the timeout to be very short via reflection
        
        // Use reflection to set a very short timeout on the existing request
        var field = typeof(CommandConfirmationFlow).GetField("_pendingConfirmations", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var pendingConfirmations = field?.GetValue(_flow) as System.Collections.Generic.Dictionary<string, CommandConfirmationFlow.ConfirmationRequest>;
        
        if (pendingConfirmations != null && pendingConfirmations.ContainsKey(request.ConfirmationId))
        {
            // Create a new request with the same properties but very short timeout
            var expiredRequest = new CommandConfirmationFlow.ConfirmationRequest
            {
                ConfirmationId = request.ConfirmationId,
                CommandName = request.CommandName,
                Arguments = request.Arguments,
                Message = request.Message,
                Confidence = request.Confidence,
                RequestedAt = DateTimeOffset.UtcNow.AddSeconds(-10), // 10 seconds ago
                Timeout = TimeSpan.FromSeconds(5) // 5 second timeout
            };
            
            // Replace the existing request
            pendingConfirmations[request.ConfirmationId] = expiredRequest;
        }

        // This test verifies the cleanup logic exists
        _flow.CleanupExpiredConfirmations();
        
        // The expired request should be removed
        var retrieved = _flow.GetPendingConfirmation(request.ConfirmationId);
        Assert.Null(retrieved);
    }

    [Fact]
    public void ConfirmationRequest_HasTimestamp()
    {
        var request = _flow.CreateConfirmation("run", new Dictionary<string, string>(), "Test", 0.9);

        Assert.NotEqual(default(DateTimeOffset), request.RequestedAt);
        Assert.True(request.RequestedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ConfirmationResponse_HasTimestamp()
    {
        var request = _flow.CreateConfirmation("run", new Dictionary<string, string>(), "Test", 0.9);
        var response = _flow.ProcessResponse(request.ConfirmationId, true);

        Assert.NotNull(response);
        Assert.NotEqual(default(DateTimeOffset), response.RespondedAt);
        Assert.True(response.RespondedAt >= request.RequestedAt);
    }

    [Fact]
    public void CreateConfirmation_GeneratesUniqueIds()
    {
        var request1 = _flow.CreateConfirmation("run", new Dictionary<string, string>(), "Test 1", 0.9);
        var request2 = _flow.CreateConfirmation("run", new Dictionary<string, string>(), "Test 2", 0.9);

        Assert.NotEqual(request1.ConfirmationId, request2.ConfirmationId);
    }
}
