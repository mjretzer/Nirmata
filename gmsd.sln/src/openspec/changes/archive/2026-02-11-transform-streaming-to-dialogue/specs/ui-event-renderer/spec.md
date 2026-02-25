# ui-event-renderer — Specification

## Overview

Specifies the UI components and rendering logic for displaying streaming dialogue events in the GMSD web interface. This complements the `redesign-ui-chat-forward` layout components with behavioral rendering for the new event types.

## ADDED Requirements

### Requirement: Event Renderer Registry

The UI MUST maintain a registry of renderers for each event type.

#### Scenario: Renderer Registration
**Given** a new event type to support
**When** the renderer is implemented
**Then** it is registered in the event renderer registry
**And** the registry maps event types to renderer functions

#### Scenario: Unknown Event Handling
**Given** an event with an unknown type
**When** the UI receives the event
**Then** a fallback renderer displays the event as generic JSON
**And** a console warning is logged

### Requirement: Intent Classification Renderer

The UI MUST render `intent.classified` events as reasoning blocks.

#### Scenario: Classification Display
**Given** an `intent.classified` event is received
**When** the event is rendered
**Then** a collapsible reasoning block displays:
- Classification badge (Chat, ReadOnly, Write)
- Confidence indicator (progress bar or percentage)
- Reasoning explanation text

#### Scenario: Confidence Visualization
**Given** an event with confidence score
**When** rendered
**Then** scores >= 0.8 show green indicator
**And** scores 0.5-0.8 show yellow indicator
**And** scores < 0.5 show red indicator

### Requirement: Gate Selection Renderer

The UI MUST render `gate.selected` events as decision cards.

#### Scenario: Gate Selection Display
**Given** a `gate.selected` event is received
**When** the event is rendered
**Then** a decision card displays:
- Target phase badge
- Selection reasoning
- Proposed action summary (if present)

#### Scenario: Confirmation Request Display
**Given** an event with `requiresConfirmation: true`
**When** the event is rendered
**Then** Confirm and Cancel buttons are displayed
**And** Confirm button is visually emphasized
**And** subsequent events are paused until user response

### Requirement: Run Lifecycle Renderers

The UI MUST render run lifecycle events as status indicators.

#### Scenario: Run Started Display
**Given** a `run.started` event is received
**When** the event is rendered
**Then** a status indicator shows:
- Run identifier
- Spinner indicating active execution
- "Run started" message

#### Scenario: Run Finished Display
**Given** a `run.finished` event is received
**When** the event is rendered
**Then** a completion indicator shows:
- Success checkmark or error icon
- Duration information
- Link to run details

### Requirement: Phase Lifecycle Renderers

The UI MUST render phase lifecycle events as progress steps.

#### Scenario: Phase Started Display
**Given** a `phase.started` event is received
**When** the event is rendered
**Then** a progress step indicator shows:
- Phase name
- Active state (animated)
- Expandable details section

#### Scenario: Phase Completed Display
**Given** a `phase.completed` event is received
**When** the event is rendered
**Then** the step indicator updates to:
- Completed state (checkmark)
- Success or failure styling
- Artifacts list (if present)

#### Scenario: Phase Sequence
**Given** multiple phase lifecycle events
**When** rendered sequentially
**Then** steps are displayed in a vertical timeline
**And** active step is visually highlighted

### Requirement: Tool Call Renderers

The UI MUST render tool call events as interactive cards.

#### Scenario: Tool Call Display
**Given** a `tool.call` event is received
**When** the event is rendered
**Then** a tool card displays:
- Tool name with icon
- Arguments as formatted JSON or key-value pairs
- Pending indicator

#### Scenario: Tool Result Display
**Given** a `tool.result` event is received
**When** the event is rendered
**Then** the corresponding tool call card updates to show:
- Success or failure status
- Result data (truncated with expand option)
- Execution duration

#### Scenario: Tool Call Correlation
**Given** tool call and result events
**When** results are received
**Then** they match to call cards by `callId`
**And** unmatched results show as orphaned entries

### Requirement: Assistant Message Renderers

The UI MUST render assistant dialogue events as streaming messages.

#### Scenario: Delta Streaming
**Given** `assistant.delta` events with the same `messageId`
**When** events are received
**Then** content is appended to a growing message bubble
**And** the message streams in real-time

#### Scenario: Final Message Display
**Given** an `assistant.final` event
**When** the event is received
**Then** the streaming message is finalized
**And** any structured data is rendered as rich content (tables, cards)

#### Scenario: Structured Data Rendering
**Given** an `assistant.final` event with `structuredData`
**When** the event is rendered
**Then** structured data is displayed as:
- Tables for list data
- Cards for entity data
- Tree views for hierarchical data
- Code blocks for code/data

### Requirement: Error Renderer

The UI MUST render error events as alert messages.

#### Scenario: Error Display
**Given** an `error` event is received
**When** the event is rendered
**Then** an alert banner displays:
- Error message
- Severity indicator (recoverable vs fatal)
- Retry option (if recoverable)
- Context information (which phase failed)

### Requirement: Event Sequencing Visualization

The UI MUST visually group related events into conversation turns.

#### Scenario: Turn Grouping
**Given** a sequence of events from a single user message
**When** events are rendered
**Then** they are visually grouped under the originating user message
**And** a subtle separator indicates turn boundaries

#### Scenario: Event Ordering
**Given** events arriving out of order
**When** rendering
**Then** events are reordered by timestamp
**And** sequence numbers (if available) are used for secondary sorting

### Requirement: Backward Compatibility Rendering

The UI MUST support rendering legacy event types.

#### Scenario: Legacy Event Support
**Given** legacy `content_chunk` events
**When** the UI receives them
**Then** they are rendered as streaming text
**And** legacy `message_complete` triggers message finalization

#### Scenario: Mixed Event Handling
**Given** a mix of new typed events and legacy events
**When** rendering
**Then** new events take precedence
**And** legacy events are handled gracefully

## MODIFIED Requirements

None.

## REMOVED Requirements

None.

---

## Visual Design Guidelines

### Reasoning Block Styling

```css
.reasoning-block {
  background: var(--bg-tertiary);
  border-left: 3px solid var(--accent-info);
  padding: 0.75rem 1rem;
  font-size: 0.875rem;
  color: var(--text-secondary);
}

.reasoning-block.collapsible {
  cursor: pointer;
}

.reasoning-block.collapsed .reasoning-content {
  display: none;
}
```

### Decision Card Styling

```css
.decision-card {
  background: var(--bg-secondary);
  border: 1px solid var(--border-default);
  border-radius: 0.5rem;
  padding: 1rem;
  margin: 0.5rem 0;
}

.decision-card.requires-confirmation {
  border-color: var(--accent-warning);
  background: var(--bg-warning-subtle);
}
```

### Tool Card Styling

```css
.tool-card {
  background: var(--bg-secondary);
  border: 1px solid var(--border-default);
  border-radius: 0.5rem;
  font-family: monospace;
  font-size: 0.875rem;
}

.tool-card.pending {
  opacity: 0.7;
}

.tool-card.success {
  border-left: 3px solid var(--accent-success);
}

.tool-card.error {
  border-left: 3px solid var(--accent-error);
}
```

### Phase Timeline Styling

```css
.phase-timeline {
  position: relative;
  padding-left: 1.5rem;
}

.phase-timeline::before {
  content: '';
  position: absolute;
  left: 0.5rem;
  top: 0;
  bottom: 0;
  width: 2px;
  background: var(--border-default);
}

.phase-step {
  position: relative;
  padding: 0.5rem 0;
}

.phase-step::before {
  content: '';
  position: absolute;
  left: -1.25rem;
  top: 0.75rem;
  width: 0.5rem;
  height: 0.5rem;
  border-radius: 50%;
  background: var(--bg-default);
  border: 2px solid var(--border-default);
}

.phase-step.active::before {
  background: var(--accent-info);
  border-color: var(--accent-info);
  animation: pulse 2s infinite;
}

.phase-step.completed::before {
  background: var(--accent-success);
  border-color: var(--accent-success);
}
```

## HTMX Integration

### SSE Event Mapping

```html
<div id="chat-thread"
     hx-sse="connect:/api/chat/stream-v2">
  <!-- Events are swapped into this container -->
</div>
```

### Event Handler Registration

```javascript
// Register renderers for each event type
eventRendererRegistry.register('intent.classified', renderIntentClassified);
eventRendererRegistry.register('gate.selected', renderGateSelected);
eventRendererRegistry.register('tool.call', renderToolCall);
eventRendererRegistry.register('tool.result', renderToolResult);
eventRendererRegistry.register('assistant.delta', renderAssistantDelta);
eventRendererRegistry.register('assistant.final', renderAssistantFinal);
```

## Accessibility Requirements

- Reasoning blocks: `aria-expanded` for collapsible sections
- Tool cards: `aria-live="polite"` for status updates
- Phase timeline: `role="list"` and `role="listitem"`
- Confirmation buttons: Clear focus indicators and keyboard navigation
- Streaming content: `aria-live="polite"` with `aria-atomic="false"`
