namespace Gmsd.Web.Models;

/// <summary>
/// Represents the type of content in a chat message
/// </summary>
public enum MessageContentType
{
    Text,
    RichText,
    Table,
    Form,
    Code,
    Json,
    Html,
    Image,
    Error
}

/// <summary>
/// Represents the sender of a chat message
/// </summary>
public enum MessageSender
{
    User,
    Ai,
    System
}

/// <summary>
/// Represents a single chat message in the thread
/// </summary>
public class ChatMessageModel
{
    /// <summary>
    /// Unique identifier for the message
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The sender of the message (User, AI, or System)
    /// </summary>
    public MessageSender Sender { get; set; } = MessageSender.User;

    /// <summary>
    /// The type of content in the message
    /// </summary>
    public MessageContentType ContentType { get; set; } = MessageContentType.Text;

    /// <summary>
    /// The main text content of the message
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// HTML content for rich messages (if ContentType is RichText or Html)
    /// </summary>
    public string? HtmlContent { get; set; }

    /// <summary>
    /// Timestamp when the message was sent
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the message is currently being streamed/streaming
    /// </summary>
    public bool IsStreaming { get; set; } = false;

    /// <summary>
    /// Additional metadata for the message (e.g., tokens used, model name)
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// For table content: column definitions
    /// </summary>
    public List<TableColumnModel>? TableColumns { get; set; }

    /// <summary>
    /// For table content: row data
    /// </summary>
    public List<Dictionary<string, object>>? TableRows { get; set; }

    /// <summary>
    /// For code content: language identifier
    /// </summary>
    public string? CodeLanguage { get; set; }

    /// <summary>
    /// For form content: form fields
    /// </summary>
    public List<FormFieldModel>? FormFields { get; set; }

    /// <summary>
    /// For form content: form action/submit URL
    /// </summary>
    public string? FormAction { get; set; }

    /// <summary>
    /// Error message (if ContentType is Error)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the display name for the sender
    /// </summary>
    public string SenderDisplayName => Sender switch
    {
        MessageSender.User => "You",
        MessageSender.Ai => "GMSD",
        MessageSender.System => "System",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the avatar character for the sender
    /// </summary>
    public string SenderAvatar => Sender switch
    {
        MessageSender.User => "👤",
        MessageSender.Ai => "🤖",
        MessageSender.System => "⚙️",
        _ => "❓"
    };

    /// <summary>
    /// Gets the CSS class for the sender's avatar
    /// </summary>
    public string SenderAvatarClass => Sender switch
    {
        MessageSender.User => "avatar-user",
        MessageSender.Ai => "avatar-ai",
        MessageSender.System => "avatar-system",
        _ => "avatar-default"
    };

    /// <summary>
    /// Formatted timestamp for display
    /// </summary>
    public string FormattedTimestamp => Timestamp.ToLocalTime().ToString("g");

    /// <summary>
    /// Relative time display (e.g., "just now", "5 min ago")
    /// </summary>
    public string RelativeTime
    {
        get
        {
            var diff = DateTime.UtcNow - Timestamp;
            return diff.TotalMinutes switch
            {
                < 1 => "just now",
                < 60 => $"{diff.TotalMinutes:0} min ago",
                < 1440 => $"{diff.TotalHours:0} hr ago",
                _ => Timestamp.ToLocalTime().ToString("MMM d")
            };
        }
    }
}

/// <summary>
/// Represents a column definition for table content
/// </summary>
public class TableColumnModel
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string? Width { get; set; }
    public bool IsSortable { get; set; } = false;
    public string? Format { get; set; }
}

/// <summary>
/// Represents a form field for form content
/// </summary>
public class FormFieldModel
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text"; // text, textarea, select, checkbox, etc.
    public bool Required { get; set; } = false;
    public string? Placeholder { get; set; }
    public string? Value { get; set; }
    public List<SelectOptionModel>? Options { get; set; }
    public string? HelpText { get; set; }
}

/// <summary>
/// Represents a select option for dropdown fields
/// </summary>
public class SelectOptionModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Represents the chat thread model containing all messages
/// </summary>
public class ChatThreadModel
{
    /// <summary>
    /// Unique identifier for the chat thread
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// List of messages in the thread
    /// </summary>
    public List<ChatMessageModel> Messages { get; set; } = new();

    /// <summary>
    /// Whether there are more messages to load (for pagination/virtual scrolling)
    /// </summary>
    public bool HasMoreMessages { get; set; } = false;

    /// <summary>
    /// Total message count (for virtual scrolling calculations)
    /// </summary>
    public int TotalMessageCount { get; set; } = 0;

    /// <summary>
    /// Current streaming message ID (if any)
    /// </summary>
    public string? StreamingMessageId { get; set; }

    /// <summary>
    /// Whether the chat is currently processing a request
    /// </summary>
    public bool IsProcessing { get; set; } = false;

    /// <summary>
    /// Creates a simple text message
    /// </summary>
    public static ChatMessageModel CreateTextMessage(string content, MessageSender sender = MessageSender.User)
    {
        return new ChatMessageModel
        {
            Content = content,
            Sender = sender,
            ContentType = MessageContentType.Text
        };
    }

    /// <summary>
    /// Creates a code message with syntax highlighting
    /// </summary>
    public static ChatMessageModel CreateCodeMessage(string code, string language, MessageSender sender = MessageSender.Ai)
    {
        return new ChatMessageModel
        {
            Content = code,
            Sender = sender,
            ContentType = MessageContentType.Code,
            CodeLanguage = language
        };
    }

    /// <summary>
    /// Creates a table message
    /// </summary>
    public static ChatMessageModel CreateTableMessage(
        List<TableColumnModel> columns,
        List<Dictionary<string, object>> rows,
        string? caption = null,
        MessageSender sender = MessageSender.Ai)
    {
        return new ChatMessageModel
        {
            Content = caption ?? string.Empty,
            Sender = sender,
            ContentType = MessageContentType.Table,
            TableColumns = columns,
            TableRows = rows
        };
    }

    /// <summary>
    /// Creates a welcome message for new conversations
    /// </summary>
    public static ChatMessageModel CreateWelcomeMessage()
    {
        return new ChatMessageModel
        {
            Content = "Welcome to GMSD! I'm here to help you manage your game development projects.\n\nYou can:\n• Ask about your projects, runs, and tasks\n• Execute commands like /status or /list projects\n• Get help with any questions\n\nWhat would you like to do today?",
            Sender = MessageSender.Ai,
            ContentType = MessageContentType.Text
        };
    }
}
