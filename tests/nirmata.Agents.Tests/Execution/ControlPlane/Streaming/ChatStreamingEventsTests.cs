using Xunit;
using nirmata.Agents.Execution.ControlPlane.Streaming;
using System.Text.Json;

namespace nirmata.Agents.Tests.Execution.ControlPlane.Streaming;

public class ChatStreamingEventsTests
{
    [Fact]
    public void ChatMessageStartEvent_HasCorrectEventType()
    {
        var evt = new ChatMessageStartEvent();

        Assert.Equal(ChatStreamingEventType.MessageStart, evt.EventType);
    }

    [Fact]
    public void ChatMessageStartEvent_HasUniqueId()
    {
        var evt1 = new ChatMessageStartEvent();
        var evt2 = new ChatMessageStartEvent();

        Assert.NotEqual(evt1.Id, evt2.Id);
    }

    [Fact]
    public void ChatMessageStartEvent_HasUniqueMessageId()
    {
        var evt1 = new ChatMessageStartEvent();
        var evt2 = new ChatMessageStartEvent();

        Assert.NotEqual(evt1.MessageId, evt2.MessageId);
    }

    [Fact]
    public void ChatMessageStartEvent_DefaultRoleIsAssistant()
    {
        var evt = new ChatMessageStartEvent();

        Assert.Equal("assistant", evt.Role);
    }

    [Fact]
    public void ChatDeltaEvent_HasCorrectEventType()
    {
        var evt = new ChatDeltaEvent
        {
            MessageId = "msg-123",
            Content = "Hello"
        };

        Assert.Equal(ChatStreamingEventType.ContentDelta, evt.EventType);
        Assert.Equal("msg-123", evt.MessageId);
        Assert.Equal("Hello", evt.Content);
    }

    [Fact]
    public void ChatCompleteEvent_HasCorrectEventType()
    {
        var evt = new ChatCompleteEvent
        {
            MessageId = "msg-123",
            FullContent = "Hello world"
        };

        Assert.Equal(ChatStreamingEventType.MessageComplete, evt.EventType);
        Assert.Equal("msg-123", evt.MessageId);
        Assert.Equal("Hello world", evt.FullContent);
    }

    [Fact]
    public void ChatCompleteEvent_DefaultFinishReasonIsStop()
    {
        var evt = new ChatCompleteEvent { MessageId = "msg-123" };

        Assert.Equal("stop", evt.FinishReason);
    }

    [Fact]
    public void ChatErrorEvent_HasCorrectEventType()
    {
        var evt = new ChatErrorEvent
        {
            Code = "ERR_001",
            Message = "Something went wrong"
        };

        Assert.Equal(ChatStreamingEventType.Error, evt.EventType);
        Assert.Equal("ERR_001", evt.Code);
        Assert.Equal("Something went wrong", evt.Message);
    }

    [Fact]
    public void ChatErrorEvent_DefaultSeverityIsError()
    {
        var evt = new ChatErrorEvent { Code = "ERR", Message = "Test" };

        Assert.Equal("error", evt.Severity);
    }

    [Fact]
    public void ChatSseSerializer_MessageStart_ProducesCorrectFormat()
    {
        var sse = ChatSseSerializer.CreateMessageStart("msg-123", "corr-456", "gpt-4");

        Assert.StartsWith("data: ", sse);
        Assert.EndsWith("\n\n", sse);
        Assert.Contains("\"messageId\":\"msg-123\"", sse);
        Assert.Contains("\"eventType\":0", sse); // 0 = MessageStart enum value
        Assert.Contains("\"role\":\"assistant\"", sse);
    }

    [Fact]
    public void ChatSseSerializer_ContentDelta_ProducesCorrectFormat()
    {
        var sse = ChatSseSerializer.CreateContentDelta("msg-123", "Hello", 0, true);

        Assert.StartsWith("data: ", sse);
        Assert.EndsWith("\n\n", sse);
        Assert.Contains("\"messageId\":\"msg-123\"", sse);
        Assert.Contains("\"content\":\"Hello\"", sse);
        Assert.Contains("\"index\":0", sse);
        Assert.Contains("\"isCompleteToken\":true", sse);
        Assert.Contains("\"eventType\":1", sse); // 1 = ContentDelta enum value
    }

    [Fact]
    public void ChatSseSerializer_MessageComplete_ProducesCorrectFormat()
    {
        var sse = ChatSseSerializer.CreateMessageComplete("msg-123", "Full content", 42, 1500, "stop");

        Assert.StartsWith("data: ", sse);
        Assert.EndsWith("\n\n", sse);
        Assert.Contains("\"messageId\":\"msg-123\"", sse);
        Assert.Contains("\"fullContent\":\"Full content\"", sse);
        Assert.Contains("\"tokenCount\":42", sse);
        Assert.Contains("\"durationMs\":1500", sse);
        Assert.Contains("\"finishReason\":\"stop\"", sse);
        Assert.Contains("\"eventType\":2", sse); // 2 = MessageComplete enum value
    }

    [Fact]
    public void ChatSseSerializer_Error_ProducesCorrectFormat()
    {
        var sse = ChatSseSerializer.CreateError("TIMEOUT", "Request timed out", true);

        Assert.StartsWith("data: ", sse);
        Assert.EndsWith("\n\n", sse);
        Assert.Contains("\"code\":\"TIMEOUT\"", sse);
        Assert.Contains("\"message\":\"Request timed out\"", sse);
        Assert.Contains("\"recoverable\":true", sse);
        Assert.Contains("\"eventType\":3", sse); // 3 = Error enum value
    }

    [Fact]
    public void ChatSseSerializer_ContentDelta_SerializesSpecialCharacters()
    {
        var sse = ChatSseSerializer.CreateContentDelta("msg-123", "Hello\nWorld\"Quote\"", 0);

        // Check that newlines and quotes are properly escaped in JSON
        Assert.Contains("Hello", sse);
        Assert.Contains("World", sse);
        Assert.Contains("Quote", sse);
        Assert.DoesNotContain("\nWorld", sse); // Should be escaped
    }

    [Fact]
    public void ChatDeltaEvent_WithIndex_TracksPosition()
    {
        var evt = new ChatDeltaEvent
        {
            MessageId = "msg-123",
            Content = "token",
            Index = 5
        };

        Assert.Equal(5, evt.Index);
    }

    [Fact]
    public void ChatCompleteEvent_HasTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var evt = new ChatCompleteEvent { MessageId = "msg-123" };
        var after = DateTimeOffset.UtcNow;

        Assert.True(evt.Timestamp >= before);
        Assert.True(evt.Timestamp <= after);
    }
}
