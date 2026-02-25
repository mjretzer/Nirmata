# redesign-ui-chat-forward — Design Document

## Architecture Overview

The chat-box forward redesign transforms the GMSD web interface from a traditional page-based navigation model to a **conversation-centric architecture**. The chat interface becomes the primary interaction surface, with all other functionality accessible through or supplementary to the conversational flow.

## Core Principles

1. **Chat as Primary Interface**: The chat input is always available and is the primary way users accomplish tasks
2. **Contextual Panels**: Side panels display relevant information based on conversation context, not static navigation
3. **Inline Rich Content**: Complex data (tables, forms, visualizations) renders inline within the chat thread
4. **Progressive Disclosure**: Simple chat UI by default, rich interactions when needed
5. **Consistent Visual Language**: Unified design system with cross-cutting components

## Layout Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Header (minimal)                                    [User] │
├──────────┬────────────────────────────────────────┬──────────┤
│          │                                        │          │
│ Context  │      Chat Thread (main area)         │  Detail  │
│ Sidebar  │                                        │  Panel   │
│ (collap- │   ┌────────────────────────────┐    │ (collap- │
│  sible)  │   │ Welcome / Messages         │    │  sible)  │
│          │   │ ┌──┐ Hi! How can I help?   │    │          │
│ Workspace│   │ │🤖│                        │    │ Run      │
│  Status  │   │ └──┘                        │    │ Details  │
│          │   ├────────────────────────────┤    │          │
│ Recent   │   │ ┌──┐ Show my projects      │    │ Spec     │
│  Runs    │   │ │👤│                        │    │ View     │
│          │   │ └──┘                        │    │          │
│ Quick    │   │ ┌──┐ [Project Table]      │    │ File     │
│ Actions  │   │ │🤖│ ┌──┬──┬──┐           │    │ Browser  │
│          │   │ └──┘ │  │  │  │           │    │          │
│          │   │      └──┴──┴──┘           │    │          │
│          │   └────────────────────────────┘    │          │
│          │                                        │          │
├──────────┴────────────────────────────────────────┴──────────┤
│  [Context-aware Chat Input with Command Hints]      [Send]  │
└─────────────────────────────────────────────────────────────┘
```

## Component Structure

### 1. MainLayout (New)
Replaces the existing `_Layout.cshtml` with a flexible three-panel layout:
- **Left Panel**: Context sidebar (collapsible, 250px default)
- **Center Panel**: Chat thread (flexible, minimum 600px)
- **Right Panel**: Detail/inspector panel (collapsible, 350px default)
- **Bottom Bar**: Persistent chat input area (fixed height, 80px)

### 2. ChatThread Component
- Message history with virtual scrolling
- Support for message types: text, rich content, tables, forms, code blocks
- Streaming message display for AI responses
- Message actions (copy, retry, provide feedback)

### 3. ContextSidebar Component
- Dynamic content based on current conversation context
- Sections: Workspace Status, Recent Runs, Quick Actions, Navigation History
- Collapsible sections with persistence

### 4. DetailPanel Component
- Displays detailed information about selected entities
- Tabs for: Properties, Evidence, Related Items, Raw Data
- Auto-populates based on conversation mentions

### 5. ChatInput Component
- Persistent input at bottom of screen
- Slash command autocomplete with descriptions
- Context-aware hints ("You mentioned Run #123...")
- File attachment support (drag & drop)

## State Management

### Conversation State
- Stored in memory with periodic localStorage backup
- Includes: message history, current context, user preferences
- Survives page refreshes but not browser sessions

### UI State
- Panel visibility (left/right collapsed states)
- Sidebar section expansion states
- Input history (last 100 commands)

### Context State
- Current workspace path
- Active run/task/phase references from conversation
- Recently viewed entities

## Integration Points

### WorkflowClassifier
- All chat commands route through `WorkflowClassifier.ExecuteAsync()`
- Streaming responses rendered progressively in chat
- Structured results displayed as rich content cards

### Existing Pages
- Current pages transition to "detail views" accessible via:
  - Chat command: `/view projects`
  - Clicking entity references in chat
  - Deep links (backward compatible)
- Pages render in right panel when contextually relevant

### Navigation Backward Compatibility
- All existing URLs continue to work
- Direct page access opens page in full view with chat minimized
- Chat state preserved when navigating

## Accessibility Considerations

1. **Keyboard Navigation**: Full keyboard support (Tab, Enter, Escape, arrow keys)
2. **Screen Reader Support**: ARIA labels for messages, live regions for updates
3. **Focus Management**: Visible focus indicators, focus trapped in modals
4. **Color Contrast**: WCAG 2.1 AA compliant (4.5:1 minimum)
5. **Motion**: Respect `prefers-reduced-motion` for animations

## Technical Implementation

### HTMX + Razor Pages
- Leverage existing HTMX infrastructure
- Chat messages rendered as partial views
- Streaming via HTMX SSE (Server-Sent Events)

### CSS Grid Layout
```css
.main-layout {
  display: grid;
  grid-template-columns: auto 1fr auto;
  grid-template-rows: 1fr auto;
  grid-template-areas:
    "sidebar chat detail"
    "input   input input";
}
```

### Responsive Behavior (Phase 1: Desktop Only)
- Minimum viewport: 1280x768
- Panels collapsible to 60px icons-only mode
- Chat area always visible, minimum 500px

## Migration Strategy

1. **Phase 1**: Build new layout alongside existing pages
2. **Phase 2**: Create chat-first versions of key pages (Projects, Runs, Specs)
3. **Phase 3**: Make chat interface the default, pages accessible via chat
4. **Phase 4**: Deprecate standalone page navigation (optional)

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| User resistance to chat interface | Keep pages accessible, make chat optional initially |
| Performance with long chat history | Virtual scrolling, message pagination |
| Accessibility challenges | WCAG audit, keyboard testing, screen reader validation |
| Mobile usability | Explicitly out of scope for this change |
