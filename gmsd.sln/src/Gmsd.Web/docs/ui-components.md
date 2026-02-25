# Chat-Forward UI Component Documentation

Developer guide for the GMSD chat-forward interface components.

## Table of Contents

1. [Component Usage Guide](#component-usage-guide)
2. [Chat Command Development Guide](#chat-command-development-guide)
3. [Rich Content Renderer API](#rich-content-renderer-api)
4. [Styling and Theming](#styling-and-theming)

---

## Component Usage Guide

### MainLayout (`_MainLayout.cshtml`)

The three-panel layout that serves as the foundation for all chat-forward pages.

**Location**: `Pages/Shared/_MainLayout.cshtml`

**Structure**:
```
┌─────────────────────────────────────────────────────────────┐
│  Header                                                    │
├──────────┬────────────────────────────────────────┬──────────┤
│ Context  │           Chat Thread                   │ Detail  │
│ Sidebar  │           (main area)                   │ Panel   │
│ (left)   │                                       │ (right) │
├──────────┴────────────────────────────────────────┴──────────┤
│  Chat Input Bar                                            │
└─────────────────────────────────────────────────────────────┘
```

**Usage in a Page**:
```csharp
@{
    Layout = "_MainLayout";
    ViewData["Title"] = "Page Title";
}

<!-- Page content renders in the chat-area -->
<partial name="_ChatThread" model="Model.ChatThread" />
```

**Key Features**:
- Collapsible sidebar and detail panels (Cmd+1, Cmd+2, Cmd+3)
- Persistent chat input at bottom
- Skip links for accessibility
- ARIA landmarks for screen readers

---

### ChatThread (`_ChatThread.cshtml`)

Renders a scrollable message history with virtual scrolling support.

**Location**: `Pages/Shared/_ChatThread.cshtml`

**Model**: `ChatThreadModel`

**Basic Usage**:
```csharp
@model ChatThreadModel
<partial name="_ChatThread" model="Model" />
```

**Model Properties**:
| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Unique thread identifier |
| `Messages` | `List<ChatMessageModel>` | Message history |
| `HasMoreMessages` | `bool` | Enable pagination |
| `IsProcessing` | `bool` | Show streaming indicator |

**JavaScript API**:
```javascript
// Access thread controller
const thread = window.chatThreads[threadId];

// Scroll to bottom
thread.scrollToBottom();

// Append message HTML
thread.appendMessage(htmlString);
```

---

### ChatMessage (`_ChatMessage.cshtml`)

Individual message renderer supporting multiple content types.

**Location**: `Pages/Shared/_ChatMessage.cshtml`

**Model**: `ChatMessageModel`

**Content Types**:

| Type | Usage | Example |
|------|-------|---------|
| `Text` | Plain text messages | Status updates |
| `RichText` | HTML content | Formatted responses |
| `Table` | Data tables | Project lists |
| `Form` | Interactive forms | Quick inputs |
| `Code` | Syntax highlighted | Code snippets |
| `Json` | JSON viewer | API responses |
| `Error` | Error display | Failure messages |

**Creating Messages**:
```csharp
// Simple text message
var msg = ChatThreadModel.CreateTextMessage("Hello", MessageSender.Ai);

// Code with syntax highlighting
var code = ChatThreadModel.CreateCodeMessage(
    "var x = 1;", 
    "csharp", 
    MessageSender.Ai
);

// Data table
var table = ChatThreadModel.CreateTableMessage(
    columns: new List<TableColumnModel> {
        new() { Key = "name", Header = "Name" },
        new() { Key = "status", Header = "Status" }
    },
    rows: projectData,
    caption: "Active Projects"
);
```

---

### ContextSidebar (`_ContextSidebar.cshtml`)

Left panel displaying workspace status, recent runs, and quick actions.

**Location**: `Pages/Shared/_ContextSidebar.cshtml`

**Model**: `ContextSidebarViewModel`

**Sections**:
1. **Workspace** - Current workspace status, stats, cursor
2. **Recent Runs** - Last 5 runs with status
3. **Quick Actions** - Configurable command buttons
4. **Settings** - Links to configuration pages

**Usage**:
```csharp
var viewModel = new ContextSidebarViewModel
{
    Workspace = new WorkspaceStatusModel { ... },
    RecentRuns = await _runService.GetRecentAsync(5),
    QuickActions = new List<QuickActionModel>
    {
        new() { Command = "/status", Label = "Status", Icon = "📊" },
        new() { Command = "/help", Label = "Help", Icon = "❓" }
    }
};
```

**Auto-refresh**: The sidebar refreshes every 30 seconds via HTMX:
```html
<div hx-get="/api/sidebar/context" hx-trigger="every 30s">
```

---

### DetailPanel (`_DetailPanel.cshtml`)

Right panel for entity details with tabbed interface.

**Location**: `Pages/Shared/_DetailPanel.cshtml`

**Model**: `DetailPanelViewModel`

**Tabs**:
- **Properties** - Key-value entity attributes
- **Evidence** - Attached artifacts, logs, files
- **Raw** - JSON representation

**Showing an Entity**:
```csharp
var viewModel = new DetailPanelViewModel
{
    Entity = new EntityDetailModel
    {
        Id = "project-123",
        Type = "project",
        Name = "My Project",
        Status = "active",
        Properties = new Dictionary<string, PropertyValueModel>
        {
            ["Path"] = new() { Value = "/projects/my-project" },
            ["Created"] = new() { Value = "2024-01-15", Format = "date" }
        },
        Evidence = new List<EvidenceItemModel>(),
        RawData = jsonString
    }
};
```

**JavaScript API**:
```javascript
// Show entity in detail panel
window.DetailPanel.showEntity('project', 'project-123');

// Refresh current entity
window.DetailPanel.refresh();
```

---

## Chat Command Development Guide

### Overview

Chat commands integrate with the `WorkflowClassifier` to execute agent workflows. Commands can be triggered via:
- Direct input in the chat box
- Quick action buttons
- Entity reference clicks

### Creating a New Command

**Step 1: Define the Command Pattern**

Commands follow a slash-command pattern:
```
/command [subcommand] [arguments]
```

Examples:
- `/status` - Check workspace status
- `/list projects` - List all projects
- `/view project:123` - View specific project

**Step 2: Implement the Handler**

Create a handler in your controller or page model:

```csharp
public class ChatController : Controller
{
    private readonly WorkflowClassifier _classifier;
    
    [HttpPost("/api/chat/execute")]
    public async Task<IActionResult> ExecuteCommand([FromBody] ChatCommandRequest request)
    {
        // Parse command
        var (command, args) = ParseCommand(request.Text);
        
        // Route to appropriate handler
        var result = command switch
        {
            "status" => await HandleStatusCommand(),
            "list" => await HandleListCommand(args),
            "view" => await HandleViewCommand(args),
            _ => await _classifier.ExecuteAsync(request.Text)
        };
        
        // Return chat message response
        return PartialView("_ChatMessage", CreateResponseMessage(result));
    }
}
```

**Step 3: Return Rich Responses**

```csharp
private ChatMessageModel CreateTableResponse<T>(List<T> items, string title)
{
    var columns = ExtractColumns<T>();
    var rows = items.Select(i => ExtractRow(i)).ToList();
    
    return new ChatMessageModel
    {
        Content = title,
        ContentType = MessageContentType.Table,
        TableColumns = columns,
        TableRows = rows,
        Sender = MessageSender.Ai
    };
}
```

### Streaming Responses

For long-running operations, use SSE streaming:

```csharp
[HttpGet("/api/chat/stream")]
public async IAsyncEnumerable<string> StreamResponse(string command)
{
    await foreach (var chunk in _classifier.StreamExecuteAsync(command))
    {
        yield return chunk;
    }
}
```

The frontend handles streaming via HTMX SSE:
```html
<div hx-sse="connect:/api/chat/stream?command={cmd}">
    <div sse:swap="message"></div>
</div>
```

### Command Autocomplete

Register commands for autocomplete in `chat-input.js`:

```javascript
const COMMANDS = [
    { command: '/status', description: 'Check workspace status' },
    { command: '/list projects', description: 'List all projects' },
    { command: '/list runs', description: 'List recent runs' },
    { command: '/help', description: 'Show available commands' }
];
```

---

## Rich Content Renderer API

### Content Type Reference

#### Text Content
```csharp
new ChatMessageModel
{
    Content = "Plain text message",
    ContentType = MessageContentType.Text,
    Sender = MessageSender.Ai
}
```

#### Rich Text / HTML
```csharp
new ChatMessageModel
{
    Content = "Fallback text",
    HtmlContent = "<strong>Formatted</strong> content",
    ContentType = MessageContentType.RichText,
    Sender = MessageSender.Ai
}
```

#### Table Content
```csharp
new ChatMessageModel
{
    ContentType = MessageContentType.Table,
    TableColumns = new List<TableColumnModel>
    {
        new() { Key = "id", Header = "ID", Width = "100px" },
        new() { Key = "name", Header = "Name", IsSortable = true },
        new() { Key = "status", Header = "Status", Format = "badge" }
    },
    TableRows = new List<Dictionary<string, object>>
    {
        new() { ["id"] = "1", ["name"] = "Project A", ["status"] = "active" },
        new() { ["id"] = "2", ["name"] = "Project B", ["status"] = "pending" }
    }
}
```

#### Code Content
```csharp
new ChatMessageModel
{
    Content = "Console.WriteLine(\"Hello\");",
    CodeLanguage = "csharp",  // Used for syntax highlighting
    ContentType = MessageContentType.Code,
    Sender = MessageSender.Ai
}
```

Supported languages: `csharp`, `javascript`, `json`, `html`, `css`, `sql`, `text`

#### Form Content
```csharp
new ChatMessageModel
{
    Content = "Enter project details:",
    ContentType = MessageContentType.Form,
    FormAction = "/api/projects/create",
    FormFields = new List<FormFieldModel>
    {
        new() { 
            Name = "name", 
            Label = "Project Name", 
            Type = "text", 
            Required = true,
            Placeholder = "Enter name..."
        },
        new() { 
            Name = "type", 
            Label = "Type", 
            Type = "select",
            Options = new List<SelectOptionModel>
            {
                new() { Value = "game", Label = "Game" },
                new() { Value = "tool", Label = "Tool" }
            }
        },
        new() { 
            Name = "description", 
            Label = "Description", 
            Type = "textarea",
            HelpText = "Brief project description"
        }
    }
}
```

Field types: `text`, `textarea`, `select`, `checkbox`, `number`, `email`, `url`, `date`

#### JSON Content
```csharp
new ChatMessageModel
{
    Content = JsonSerializer.Serialize(data, new JsonSerializerOptions 
    { 
        WriteIndented = true 
    }),
    ContentType = MessageContentType.Json,
    Sender = MessageSender.Ai
}
```

Renders with syntax highlighting in a scrollable container.

#### Error Content
```csharp
new ChatMessageModel
{
    ErrorMessage = "Failed to load projects",
    Content = "Connection timeout after 30 seconds",  // Details
    ContentType = MessageContentType.Error,
    Sender = MessageSender.System
}
```

### Entity References

Auto-detect entity patterns in text to create clickable links:

```csharp
// In message content, these patterns auto-link:
// - project:abc-123
// - run:def-456
// - task:ghi-789
// - issue:jkl-012

// The _ChatMessage component detects these and renders:
// <a href="#" data-entity-type="project" data-entity-id="abc-123">project:abc-123</a>
```

---

## Styling and Theming

### CSS Architecture

| File | Purpose |
|------|---------|
| `site.css` | Base styles, layout grid, components |
| `streaming-chat.css` | Chat thread, messages, input |
| `context-sidebar.css` | Sidebar layout and components |
| `detail-panel.css` | Detail panel, tabs, entity display |
| `keyboard-shortcuts.css` | Keyboard hint overlays |
| `accessibility.css` | A11y utilities, focus styles, reduced motion |

### CSS Variables (Theme Tokens)

```css
:root {
    /* Colors */
    --color-primary: #4f46e5;
    --color-primary-hover: #4338ca;
    --color-background: #0f0f23;
    --color-surface: #1a1a2e;
    --color-surface-elevated: #252542;
    --color-text: #e2e8f0;
    --color-text-muted: #94a3b8;
    --color-border: #334155;
    
    /* Spacing */
    --space-xs: 0.25rem;
    --space-sm: 0.5rem;
    --space-md: 1rem;
    --space-lg: 1.5rem;
    --space-xl: 2rem;
    
    /* Typography */
    --font-sans: system-ui, -apple-system, sans-serif;
    --font-mono: ui-monospace, SFMono-Regular, monospace;
    --text-sm: 0.875rem;
    --text-base: 1rem;
    --text-lg: 1.125rem;
    
    /* Layout */
    --sidebar-width: 250px;
    --detail-width: 350px;
    --header-height: 48px;
    --input-height: 80px;
    
    /* Effects */
    --radius-sm: 4px;
    --radius-md: 8px;
    --radius-lg: 12px;
    --shadow-sm: 0 1px 2px rgba(0,0,0,0.3);
    --shadow-md: 0 4px 6px rgba(0,0,0,0.4);
}
```

### Component CSS Classes

#### Chat Message Classes
```css
.chat-message           /* Base message container */
.message-user           /* User message variant */
.message-ai             /* AI message variant */
.message-streaming      /* Streaming/active state */
.message-avatar         /* Avatar wrapper */
.message-wrapper        /* Content wrapper */
.message-header         /* Author + timestamp */
.message-body           /* Main content */
.message-actions        /* Copy, feedback buttons */
```

#### Layout Classes
```css
.main-layout            /* Three-panel grid */
.main-layout.collapsed  /* Both panels hidden */
.panel                  /* Sidebar/detail panel */
.panel.collapsed       /* Icon-only mode */
.chat-area              /* Center chat region */
.chat-input-bar         /* Bottom input area */
```

### Customizing Themes

Override CSS variables in a custom stylesheet:

```css
/* wwwroot/css/custom-theme.css */
:root {
    --color-primary: #your-brand-color;
    --color-background: #your-bg-color;
}
```

Add to `_MainLayout.cshtml`:
```html
<link rel="stylesheet" href="~/css/custom-theme.css" asp-append-version="true" />
```

### Accessibility Classes

From `accessibility.css`:

```css
.sr-only                /* Screen-reader only content */
.focus-visible          /* Visible focus indicator */
.skip-link              /* Skip navigation links */
.high-contrast          /* High contrast mode overrides */
.reduced-motion         /* Reduced motion preferences */
```

### Responsive Breakpoints

```css
/* Desktop-first (minimum 1280px for full layout) */
@media (max-width: 1279px) {
    .main-layout {
        /* Collapse to single column */
    }
}
```

Note: Mobile responsive design is explicitly out of scope for this phase.

---

## JavaScript Component APIs

### MainLayout
```javascript
window.mainLayout = {
    insertCommand(text),      // Insert text into chat input
    togglePanel(panel),       // Toggle sidebar/detail
    collapseAll(),            // Collapse both panels
    expandAll()               // Expand both panels
};
```

### ChatThreads
```javascript
window.chatThreads = {
    [threadId]: {
        containerId: string,
        scrollToBottom(),
        appendMessage(html)
    }
};
```

### DetailPanel
```javascript
window.DetailPanel = {
    showEntity(type, id),     // Load and display entity
    refresh(),               // Reload current entity
    setTab(tabName),         // Switch active tab
    close()                  // Clear and collapse
};
```

---

## Testing Components

### Unit Testing ViewModels

```csharp
[Fact]
public void ChatMessageModel_FormatsRelativeTime()
{
    var message = new ChatMessageModel
    {
        Timestamp = DateTime.UtcNow.AddMinutes(-5)
    };
    
    Assert.Equal("5 min ago", message.RelativeTime);
}
```

### Integration Testing

Test component rendering with `HtmlRenderer`:

```csharp
[Fact]
public async Task ChatThread_RendersMessages()
{
    var model = new ChatThreadModel
    {
        Messages = new List<ChatMessageModel>
        {
            ChatThreadModel.CreateWelcomeMessage()
        }
    };
    
    var html = await RenderPartialAsync("_ChatThread", model);
    
    Assert.Contains("Welcome to GMSD", html);
}
```

---

## Best Practices

1. **Always set ARIA attributes** - Use `aria-label`, `aria-expanded`, etc.
2. **Use CSS variables** - Don't hardcode colors or sizes
3. **Handle streaming states** - Show indicators for in-progress operations
4. **Validate HTML content** - Sanitize any user-provided HTML in rich content
5. **Test keyboard navigation** - Ensure Tab, Enter, Escape work correctly
6. **Respect reduced motion** - Wrap animations in `prefers-reduced-motion` checks

---

## Related Documentation

- `docs/streaming-events.md` - SSE streaming protocol
- `docs/tool-calling-protocol.md` - Agent tool integration
- `docs/ui-contract-command-suggestion.md` - Command suggestion API
