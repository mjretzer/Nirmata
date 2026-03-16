# UI Renderer Development Guide

## Overview

This guide explains how to create custom renderers for streaming dialogue events. Renderers convert event payloads into HTML/DOM elements that are displayed in the chat interface.

## Architecture

### Renderer Lifecycle

```
1. Event received from SSE stream
2. EventRendererRegistry.resolveRenderer(event)
3. Renderer.canRender(event) check
4. Renderer.render(event, context) → HTML/DOM
5. HTMX swaps content into the DOM
6. (Optional) Renderer.update(event, element) for streaming content
```

### Core Interfaces

#### IEventRenderer

```javascript
/**
 * @interface IEventRenderer
 * Contract for event renderers
 */
class IEventRenderer {
    /**
     * Returns metadata about this renderer
     * @returns {RendererMetadata}
     */
    getMetadata() { throw new Error('Must implement'); }

    /**
     * Determines if this renderer can handle the given event
     * @param {StreamingEvent} event
     * @returns {boolean}
     */
    canRender(event) { throw new Error('Must implement'); }

    /**
     * Renders the event into HTML/DOM
     * @param {StreamingEvent} event
     * @param {Object} context - Rendering context
     * @returns {RenderResult|string}
     */
    render(event, context) { throw new Error('Must implement'); }

    /**
     * Updates an existing rendered element (optional)
     * @param {StreamingEvent} event
     * @param {HTMLElement} element
     * @returns {boolean}
     */
    update(event, element, context) { return false; }
}
```

#### RendererMetadata

```javascript
/**
 * @typedef {Object} RendererMetadata
 * @property {StreamingEventType} eventType - Event type this renderer handles
 * @property {string} name - Human-readable name
 * @property {string} [description] - Renderer description
 * @property {string} [version='1.0.0'] - Renderer version
 * @property {number} [priority=0] - Priority for conflict resolution
 */
```

#### RenderResult

```javascript
/**
 * @typedef {Object} RenderResult
 * @property {string} html - Rendered HTML string
 * @property {string} [elementId] - Optional element ID for updates
 * @property {boolean} [append=true] - Whether to append or replace
 * @property {string} [targetSelector] - CSS selector for insertion
 */
```

## Creating a Custom Renderer

### Step 1: Extend EventRendererBase

```javascript
/**
 * @fileoverview MyCustomEventRenderer
 * @extends EventRendererBase
 */
class MyCustomEventRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.MyCustomEvent,
            name: 'My Custom Event Renderer',
            description: 'Renders custom events with special formatting',
            version: '1.0.0',
            priority: 10
        });
    }

    /**
     * @override
     */
    canRender(event) {
        return event?.type === StreamingEventType.MyCustomEvent;
    }

    /**
     * @override
     */
    render(event, context) {
        const { payload } = event;
        
        const html = `
            <div class="my-custom-event" id="${this.generateElementId(event.id)}">
                <div class="header">
                    <span class="icon">🎨</span>
                    <span class="title">${this.escapeHtml(payload.title)}</span>
                </div>
                <div class="content">
                    ${this.escapeHtml(payload.content)}
                </div>
                <div class="footer">
                    <time>${this.formatTimestamp(event.timestamp)}</time>
                </div>
            </div>
        `;

        return this.createRenderResult(html, {
            elementId: this.generateElementId(event.id),
            append: true,
            targetSelector: '#chat-thread'
        });
    }
}
```

### Step 2: Register the Renderer

```javascript
// In your initialization code
import { EventRendererRegistry } from './event-renderer-registry.js';
import { MyCustomEventRenderer } from './my-custom-event-renderer.js';

// Create and register
const registry = new EventRendererRegistry();
const myRenderer = new MyCustomEventRenderer();
registry.register(myRenderer);

// Or register with options
registry.register(myRenderer, {
    overrideExisting: false,  // Don't replace existing renderers
    priority: 'high'          // High priority in renderer list
});
```

### Step 3: CSS Styling

```css
/* my-custom-event.css */
.my-custom-event {
    border: 1px solid #e0e0e0;
    border-radius: 8px;
    padding: 16px;
    margin: 8px 0;
    background: #f8f9fa;
}

.my-custom-event .header {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 12px;
}

.my-custom-event .icon {
    font-size: 20px;
}

.my-custom-event .title {
    font-weight: 600;
    color: #333;
}

.my-custom-event .content {
    color: #555;
    line-height: 1.5;
}

.my-custom-event .footer {
    margin-top: 12px;
    font-size: 12px;
    color: #888;
}
```

## Renderer Patterns

### Pattern 1: Simple Static Renderer

For events that render once and don't update:

```javascript
class StaticEventRenderer extends EventRendererBase {
    render(event, context) {
        const html = `
            <div class="static-event">
                <p>${this.escapeHtml(event.payload.message)}</p>
            </div>
        `;
        return this.createRenderResult(html);
    }
}
```

### Pattern 2: Streaming/Updating Renderer

For events that receive updates (like `AssistantDelta`):

```javascript
class StreamingMessageRenderer extends EventRendererBase {
    constructor() {
        super({ eventType: StreamingEventType.AssistantDelta });
        this.messageBuffers = new Map();
    }

    render(event, context) {
        const { messageId, content } = event.payload;
        
        // Accumulate content
        if (!this.messageBuffers.has(messageId)) {
            this.messageBuffers.set(messageId, '');
        }
        this.messageBuffers.set(messageId, 
            this.messageBuffers.get(messageId) + content);

        const fullContent = this.messageBuffers.get(messageId);
        const elementId = `message-${messageId}`;

        const html = `
            <div class="assistant-message streaming" id="${elementId}">
                <span class="content">${this.escapeHtml(fullContent)}</span>
                <span class="cursor">|</span>
            </div>
        `;

        return this.createRenderResult(html, {
            elementId,
            append: false,  // Replace existing content
            targetSelector: `#${elementId}`
        });
    }

    update(event, element, context) {
        // Update existing element instead of full re-render
        const contentSpan = element.querySelector('.content');
        if (contentSpan) {
            const { messageId, content } = event.payload;
            this.messageBuffers.set(messageId, 
                (this.messageBuffers.get(messageId) || '') + content);
            contentSpan.textContent = this.messageBuffers.get(messageId);
            return true;
        }
        return false;
    }
}
```

### Pattern 3: Correlated Event Renderer

For events that update existing elements (like `ToolResult` updating `ToolCall`):

```javascript
class ToolResultRenderer extends EventRendererBase {
    constructor() {
        super({ eventType: StreamingEventType.ToolResult });
    }

    render(event, context) {
        const { callId, success, result, durationMs } = event.payload;
        const elementId = `tool-call-${callId}`;
        
        // Find existing tool call element
        const existingElement = document.getElementById(elementId);
        
        if (existingElement) {
            // Update existing card
            const statusClass = success ? 'success' : 'error';
            const icon = success ? '✅' : '❌';
            
            existingElement.classList.remove('pending');
            existingElement.classList.add(statusClass);
            existingElement.querySelector('.status').innerHTML = 
                `${icon} Completed in ${durationMs}ms`;
            
            // Add result section if not present
            if (!existingElement.querySelector('.result')) {
                const resultDiv = document.createElement('div');
                resultDiv.className = 'result';
                resultDiv.innerHTML = `
                    <details>
                        <summary>Result</summary>
                        <pre>${this.escapeHtml(JSON.stringify(result, null, 2))}</pre>
                    </details>
                `;
                existingElement.appendChild(resultDiv);
            }
            
            return this.createRenderResult('', {
                elementId,
                append: false
            });
        }
        
        // Orphaned result - render standalone
        return this.createRenderResult(`
            <div class="tool-result orphan" id="${elementId}">
                <span class="warning">Orphaned result for ${callId}</span>
            </div>
        `);
    }
}
```

### Pattern 4: Collapsible Reasoning Block

For reasoning events that should be collapsible:

```javascript
class IntentClassifiedRenderer extends EventRendererBase {
    constructor() {
        super({ eventType: StreamingEventType.IntentClassified });
    }

    render(event, context) {
        const { classification, confidence, reasoning } = event.payload;
        const elementId = this.generateElementId(event.id);
        
        const confidencePercent = Math.round(confidence * 100);
        const confidenceColor = confidence > 0.8 ? 'high' : 
                               confidence > 0.6 ? 'medium' : 'low';
        
        const html = `
            <div class="reasoning-block intent-classified" id="${elementId}">
                <details class="collapsible">
                    <summary>
                        <span class="badge ${classification.toLowerCase()}">
                            ${classification}
                        </span>
                        <span class="confidence-bar ${confidenceColor}" 
                              style="width: ${confidencePercent}%"></span>
                        <span class="confidence-text">${confidencePercent}%</span>
                    </summary>
                    <div class="reasoning-content">
                        <p>${this.escapeHtml(reasoning)}</p>
                    </div>
                </details>
            </div>
        `;

        return this.createRenderResult(html, { elementId });
    }
}
```

### Pattern 5: Confirmation Dialog Renderer

For events requiring user confirmation:

```javascript
class GateSelectedRenderer extends EventRendererBase {
    constructor() {
        super({ eventType: StreamingEventType.GateSelected });
    }

    render(event, context) {
        const { targetPhase, reasoning, requiresConfirmation, proposedAction } = event.payload;
        const elementId = this.generateElementId(event.id);

        let confirmationHtml = '';
        if (requiresConfirmation) {
            confirmationHtml = `
                <div class="confirmation-section">
                    <div class="proposed-action">
                        <strong>${this.escapeHtml(proposedAction.name)}</strong>
                        <p>${this.escapeHtml(proposedAction.description)}</p>
                    </div>
                    <div class="actions">
                        <button class="btn-confirm" 
                                data-action="confirm" 
                                data-event-id="${event.id}">
                            Confirm
                        </button>
                        <button class="btn-cancel" 
                                data-action="cancel" 
                                data-event-id="${event.id}">
                            Cancel
                        </button>
                    </div>
                </div>
            `;
        }

        const html = `
            <div class="decision-card gate-selected" id="${elementId}">
                <div class="phase-badge">${targetPhase}</div>
                <div class="reasoning">${this.escapeHtml(reasoning)}</div>
                ${confirmationHtml}
            </div>
        `;

        // Attach event listeners after rendering
        setTimeout(() => {
            const element = document.getElementById(elementId);
            if (element) {
                element.querySelector('.btn-confirm')?.addEventListener('click', 
                    () => this.handleConfirm(event.id));
                element.querySelector('.btn-cancel')?.addEventListener('click', 
                    () => this.handleCancel(event.id));
            }
        }, 0);

        return this.createRenderResult(html, { elementId });
    }

    handleConfirm(eventId) {
        // Emit confirmation response
        fetch('/api/chat/confirm', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ eventId, action: 'confirm' })
        });
    }

    handleCancel(eventId) {
        // Emit cancellation
        fetch('/api/chat/confirm', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ eventId, action: 'cancel' })
        });
    }
}
```

## HTMX Integration

### SSE Event Handling

```javascript
// HTMX SSE extension integration
document.body.addEventListener('htmx:sseMessage', function(event) {
    const sseEvent = event.detail;
    const streamingEvent = JSON.parse(sseEvent.data);
    
    // Resolve renderer
    const renderer = registry.resolveRenderer(streamingEvent);
    
    // Render the event
    const result = renderer.render(streamingEvent, {
        container: document.getElementById('chat-thread'),
        threadId: streamingEvent.correlationId
    });
    
    // Handle render result
    if (result.append) {
        // Append to target
        const target = document.querySelector(result.targetSelector);
        if (target) {
            target.insertAdjacentHTML('beforeend', result.html);
        }
    } else {
        // Replace existing element
        const existing = document.getElementById(result.elementId);
        if (existing) {
            existing.outerHTML = result.html;
        }
    }
});
```

### Event Sequencing

For events that may arrive out of order:

```javascript
class EventSequencer {
    constructor() {
        this.buffer = new Map();
        this.lastSequence = 0;
        this.bufferTimeout = 100; // ms
    }

    processEvent(event, renderer) {
        const seq = event.sequenceNumber || 0;
        
        // Buffer events that arrive too early
        if (seq > this.lastSequence + 1) {
            this.buffer.set(seq, { event, renderer });
            this.scheduleBufferFlush();
            return;
        }
        
        // Render immediately
        this.renderEvent(event, renderer);
        this.lastSequence = seq;
        
        // Check for buffered events that are now in sequence
        this.flushBuffer();
    }
    
    flushBuffer() {
        while (this.buffer.has(this.lastSequence + 1)) {
            const { event, renderer } = this.buffer.get(this.lastSequence + 1);
            this.renderEvent(event, renderer);
            this.buffer.delete(this.lastSequence + 1);
            this.lastSequence++;
        }
    }
}
```

## Best Practices

### 1. Always Escape Content

```javascript
// Use the built-in escapeHtml helper
this.escapeHtml(userProvidedContent);

// Or implement your own
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
```

### 2. Use Semantic HTML

```javascript
// Good: Semantic elements
const html = `
    <article class="event-card">
        <header><h3>${title}</h3></header>
        <section>${content}</section>
        <footer><time>${timestamp}</time></footer>
    </article>
`;

// Avoid: Div soup
const html = `
    <div class="event-card">
        <div class="header"><div class="title">${title}</div></div>
        <div class="content">${content}</div>
    </div>
`;
```

### 3. Implement Update Methods for Streaming Content

```javascript
// For streaming events, implement update to avoid full re-renders
update(event, element, context) {
    // Update specific parts instead of full replacement
    const contentEl = element.querySelector('.content');
    if (contentEl) {
        contentEl.textContent += event.payload.content;
        return true; // Update handled
    }
    return false; // Fall back to re-render
}
```

### 4. Clean Up Resources

```javascript
class MyRenderer extends EventRendererBase {
    constructor() {
        super({ ... });
        this.intervals = new Set();
        this.listeners = new Set();
    }

    startAnimation(element) {
        const interval = setInterval(() => this.animate(element), 100);
        this.intervals.add(interval);
    }

    addListener(element, event, handler) {
        element.addEventListener(event, handler);
        this.listeners.push({ element, event, handler });
    }

    destroy() {
        // Clean up intervals
        this.intervals.forEach(clearInterval);
        this.intervals.clear();
        
        // Clean up listeners
        this.listeners.forEach(({ element, event, handler }) => {
            element.removeEventListener(event, handler);
        });
        this.listeners.clear();
    }
}
```

### 5. Handle Missing Data Gracefully

```javascript
render(event, context) {
    const { payload } = event;
    
    // Provide defaults for missing data
    const title = payload.title || 'Untitled';
    const content = payload.content || '';
    const timestamp = event.timestamp || new Date().toISOString();
    
    // Handle optional fields
    const tags = payload.tags?.length 
        ? payload.tags.map(t => `<span class="tag">${t}</span>`).join('')
        : '';
    
    return this.createRenderResult(/* ... */);
}
```

## Testing Renderers

### Unit Test Example (Jest)

```javascript
// my-renderer.test.js
import { MyCustomEventRenderer } from './my-custom-event-renderer.js';

describe('MyCustomEventRenderer', () => {
    let renderer;
    
    beforeEach(() => {
        renderer = new MyCustomEventRenderer();
    });
    
    test('canRender returns true for matching event type', () => {
        const event = { type: 'MyCustomEvent' };
        expect(renderer.canRender(event)).toBe(true);
    });
    
    test('render returns valid HTML', () => {
        const event = {
            id: 'evt-001',
            type: 'MyCustomEvent',
            timestamp: '2026-02-11T14:30:00Z',
            payload: { title: 'Test', content: 'Hello' }
        };
        
        const result = renderer.render(event, {});
        
        expect(result.html).toContain('Test');
        expect(result.html).toContain('Hello');
        expect(result.elementId).toBeDefined();
    });
    
    test('escapes HTML in content', () => {
        const event = {
            type: 'MyCustomEvent',
            payload: { content: '<script>alert("xss")</script>' }
        };
        
        const result = renderer.render(event, {});
        
        expect(result.html).not.toContain('<script>');
        expect(result.html).toContain('&lt;script&gt;');
    });
});
```

### Visual Testing

```javascript
// Manual test page
const testEvents = [
    {
        type: 'IntentClassified',
        payload: { classification: 'Write', confidence: 0.92, reasoning: 'Test' }
    },
    {
        type: 'ToolCall',
        payload: { toolName: 'test_tool', arguments: {}, callId: 'call-001' }
    }
];

testEvents.forEach(event => {
    const renderer = registry.resolveRenderer(event);
    const result = renderer.render(event, {});
    document.body.insertAdjacentHTML('beforeend', result.html);
});
```

## Performance Tips

### 1. Debounce Rapid Events

```javascript
class DebouncedRenderer extends EventRendererBase {
    constructor() {
        super({ ... });
        this.debounceMap = new Map();
    }
    
    render(event, context) {
        const key = event.payload.messageId;
        
        // Clear pending debounce
        if (this.debounceMap.has(key)) {
            clearTimeout(this.debounceMap.get(key));
        }
        
        // Schedule render
        const timeout = setTimeout(() => {
            this.actualRender(event, context);
            this.debounceMap.delete(key);
        }, 16); // 1 frame
        
        this.debounceMap.set(key, timeout);
        
        // Return empty result for now
        return { html: '', append: true };
    }
}
```

### 2. Use Document Fragments for Batches

```javascript
renderBatch(events, context) {
    const fragment = document.createDocumentFragment();
    
    events.forEach(event => {
        const renderer = registry.resolveRenderer(event);
        const result = renderer.render(event, context);
        
        const temp = document.createElement('div');
        temp.innerHTML = result.html;
        fragment.appendChild(temp.firstElementChild);
    });
    
    context.container.appendChild(fragment);
}
```

### 3. Virtual Scrolling for Long Conversations

```javascript
class VirtualChatContainer {
    constructor(container, itemHeight = 100) {
        this.container = container;
        this.itemHeight = itemHeight;
        this.events = [];
        this.visibleRange = { start: 0, end: 20 };
    }
    
    addEvent(event) {
        this.events.push(event);
        this.renderVisible();
    }
    
    renderVisible() {
        const { start, end } = this.visibleRange;
        const visible = this.events.slice(start, end);
        
        // Only render visible events
        this.container.innerHTML = visible
            .map(e => this.renderEvent(e))
            .join('');
    }
}
```

## Example: Complete Custom Renderer

```javascript
/**
 * @fileoverview CodeBlockRenderer
 * Renders code blocks with syntax highlighting and copy button
 */
class CodeBlockRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.AssistantFinal,
            name: 'Code Block Renderer',
            description: 'Renders code blocks with syntax highlighting',
            version: '1.0.0',
            priority: 5  // Higher than default assistant renderer
        });
        
        // Regex to detect code blocks in markdown
        this.codeBlockRegex = /```(\w+)?\n([\s\S]*?)```/g;
    }

    canRender(event) {
        if (event.type !== StreamingEventType.AssistantFinal) {
            return false;
        }
        
        // Check if content contains code blocks
        const content = event.payload?.content || '';
        return this.codeBlockRegex.test(content);
    }

    render(event, context) {
        const content = event.payload.content;
        const messageId = event.payload.messageId;
        
        // Process markdown to HTML
        const html = this.processMarkdown(content, messageId);
        
        // Attach copy button handlers after render
        setTimeout(() => this.attachCopyHandlers(messageId), 0);
        
        return this.createRenderResult(html, {
            elementId: `message-${messageId}`,
            append: false,
            targetSelector: `#message-${messageId}`
        });
    }

    processMarkdown(content, messageId) {
        let html = this.escapeHtml(content);
        
        // Replace code blocks with styled versions
        html = html.replace(this.codeBlockRegex, (match, lang, code) => {
            const language = lang || 'text';
            const blockId = `code-${messageId}-${Math.random().toString(36).substr(2, 9)}`;
            
            return `
                <div class="code-block" data-language="${language}">
                    <div class="code-header">
                        <span class="lang">${language}</span>
                        <button class="copy-btn" data-target="${blockId}">
                            Copy
                        </button>
                    </div>
                    <pre><code id="${blockId}" class="language-${language}">
                        ${this.escapeHtml(code.trim())}
                    </code></pre>
                </div>
            `;
        });
        
        return `<div class="assistant-message" id="message-${messageId}">${html}</div>`;
    }

    attachCopyHandlers(messageId) {
        const messageEl = document.getElementById(`message-${messageId}`);
        if (!messageEl) return;
        
        messageEl.querySelectorAll('.copy-btn').forEach(btn => {
            btn.addEventListener('click', async () => {
                const targetId = btn.dataset.target;
                const code = document.getElementById(targetId)?.textContent || '';
                
                try {
                    await navigator.clipboard.writeText(code);
                    btn.textContent = 'Copied!';
                    setTimeout(() => btn.textContent = 'Copy', 2000);
                } catch (err) {
                    console.error('Copy failed:', err);
                }
            });
        });
    }
}

// Register the renderer
const registry = new EventRendererRegistry();
registry.register(new CodeBlockRenderer());
```

## See Also

- [API Documentation](./API_DOCUMENTATION.md)
- [Event Type Reference Guide](./EVENT_TYPE_REFERENCE.md)
- [Migration Guide](./MIGRATION_GUIDE.md)
