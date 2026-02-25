# ui-event-renderer Specification

## Purpose

Defines the durable contract for $capabilityId in the GMSD platform.

- **Lives in:** See repo projects and `.aos/**` artifacts as applicable
- **Owns:** Capability-level contract and scenarios
- **Does not own:** Unrelated domain concerns outside this capability
## Requirements
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

