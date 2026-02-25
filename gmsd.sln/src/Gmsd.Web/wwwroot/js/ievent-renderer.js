/**
 * @fileoverview IEventRenderer Interface
 * @description Defines the contract for UI event renderers
 * Renderers convert streaming events into HTML/DOM elements
 */

/**
 * @typedef {import('./event-types.js').StreamingEvent} StreamingEvent
 * @typedef {import('./event-types.js').StreamingEventType} StreamingEventType
 */

/**
 * Render result containing the rendered output and metadata
 * @typedef {Object} RenderResult
 * @property {string} html - Rendered HTML string
 * @property {string} [elementId] - Optional element ID for updates
 * @property {boolean} [append=true] - Whether to append or replace content
 * @property {string} [targetSelector] - CSS selector for insertion target
 */

/**
 * Renderer metadata for registration and discovery
 * @typedef {Object} RendererMetadata
 * @property {StreamingEventType} eventType - Event type this renderer handles
 * @property {string} name - Human-readable renderer name
 * @property {string} [description] - Renderer description
 * @property {string} [version='1.0.0'] - Renderer version
 * @property {number} [priority=0] - Priority for conflict resolution (higher = preferred)
 */

/**
 * @interface IEventRenderer
 * @description Contract for event renderers that convert streaming events into UI elements
 */
class IEventRenderer {
    /**
     * Returns metadata about this renderer
     * @returns {RendererMetadata}
     */
    getMetadata() {
        throw new Error('IEventRenderer.getMetadata() must be implemented by subclass');
    }

    /**
     * Determines if this renderer can handle the given event
     * @param {StreamingEvent} event - The event to check
     * @returns {boolean}
     */
    canRender(event) {
        throw new Error('IEventRenderer.canRender() must be implemented by subclass');
    }

    /**
     * Renders the event into HTML/DOM representation
     * @param {StreamingEvent} event - The event to render
     * @param {Object} [context] - Rendering context
     * @param {HTMLElement} [context.container] - Container element
     * @param {string} [context.threadId] - Thread identifier
     * @param {number} [context.index] - Event index in sequence
     * @returns {RenderResult|string} Render result or HTML string
     */
    render(event, context) {
        throw new Error('IEventRenderer.render() must be implemented by subclass');
    }

    /**
     * Updates an existing rendered element with new event data
     * Optional - only required for renderers that support updates
     * @param {StreamingEvent} event - The event with updated data
     * @param {HTMLElement} element - The existing rendered element
     * @param {Object} [context] - Update context
     * @returns {boolean} Whether the update was handled
     */
    update(event, element, context) {
        // Default implementation - subclasses can override
        return false;
    }

    /**
     * Cleans up any resources when renderer is destroyed
     * Optional lifecycle method
     */
    destroy() {
        // Default implementation - no cleanup needed
    }
}

/**
 * Base class for event renderers providing common functionality
 * @abstract
 * @extends IEventRenderer
 */
class EventRendererBase extends IEventRenderer {
    /**
     * @param {RendererMetadata} metadata
     */
    constructor(metadata) {
        super();
        this._metadata = metadata;
    }

    /**
     * @override
     * @returns {RendererMetadata}
     */
    getMetadata() {
        return this._metadata;
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @returns {boolean}
     */
    canRender(event) {
        return event?.type === this._metadata.eventType;
    }

    /**
     * Escapes HTML entities to prevent XSS
     * @protected
     * @param {string} text
     * @returns {string}
     */
    escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Formats a timestamp for display
     * @protected
     * @param {string} timestamp - ISO 8601 timestamp
     * @returns {string}
     */
    formatTimestamp(timestamp) {
        if (!timestamp) return '';
        const date = new Date(timestamp);
        return date.toLocaleTimeString();
    }

    /**
     * Generates a unique element ID for event correlation
     * @protected
     * @param {string} eventId
     * @param {string} [suffix]
     * @returns {string}
     */
    generateElementId(eventId, suffix) {
        const cleanId = eventId.replace(/[^a-zA-Z0-9_-]/g, '');
        return suffix ? `event-${cleanId}-${suffix}` : `event-${cleanId}`;
    }

    /**
     * Creates a standard RenderResult object
     * @protected
     * @param {string} html
     * @param {Object} [options]
     * @param {string} [options.elementId]
     * @param {boolean} [options.append=true]
     * @param {string} [options.targetSelector]
     * @returns {RenderResult}
     */
    createRenderResult(html, options = {}) {
        return {
            html,
            append: true,
            ...options
        };
    }
}

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { IEventRenderer, EventRendererBase };
}
