/**
 * @fileoverview Default Event Renderer
 * @description Fallback renderer for unknown or unregistered event types
 * Provides safe rendering with diagnostic information for debugging
 */

/**
 * @typedef {import('./ievent-renderer.js').EventRendererBase} EventRendererBase
 * @typedef {import('./event-types.js').StreamingEvent} StreamingEvent
 */

/**
 * Default renderer for unknown event types
 * Renders a diagnostic card showing raw event data
 * @extends EventRendererBase
 */
class DefaultEventRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: null, // Handles any event type
            name: 'DefaultEventRenderer',
            description: 'Fallback renderer for unknown event types',
            version: '1.0.0',
            priority: -1000 // Lowest priority, always last resort
        });
    }

    /**
     * Can render any event - this is the fallback renderer
     * @override
     * @param {StreamingEvent} event
     * @returns {boolean}
     */
    canRender(event) {
        // Always returns true - this is the catch-all renderer
        return true;
    }

    /**
     * Renders an unknown event as a diagnostic card
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        const elementId = this.generateElementId(event.id, 'unknown');
        
        const html = `
            <div class="event-card event-card--unknown" id="${elementId}" data-event-type="${this.escapeHtml(event.type)}">
                <div class="event-card__header">
                    <span class="event-card__badge">Unknown Event</span>
                    <span class="event-card__type">${this.escapeHtml(event.type)}</span>
                </div>
                <div class="event-card__body">
                    <details class="event-card__details">
                        <summary>Event Data</summary>
                        <pre class="event-card__json"><code>${this.escapeHtml(JSON.stringify(event, null, 2))}</code></pre>
                    </details>
                </div>
                <div class="event-card__footer">
                    <time class="event-card__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                    ${event.correlationId ? `<span class="event-card__correlation">ID: ${this.escapeHtml(event.correlationId.substring(0, 8))}...</span>` : ''}
                </div>
            </div>
        `;

        return this.createRenderResult(html, {
            elementId,
            append: true
        });
    }
}

/**
 * Creates a pre-configured default renderer instance
 * @returns {DefaultEventRenderer}
 */
function createDefaultEventRenderer() {
    return new DefaultEventRenderer();
}

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { DefaultEventRenderer, createDefaultEventRenderer };
}
