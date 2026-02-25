/**
 * @fileoverview Event Renderer Registry
 * @description Singleton registry for managing event renderers
 * Provides renderer resolution by event type with priority-based selection
 */

/**
 * @typedef {import('./ievent-renderer.js').IEventRenderer} IEventRenderer
 * @typedef {import('./event-types.js').StreamingEvent} StreamingEvent
 * @typedef {import('./event-types.js').StreamingEventType} StreamingEventType
 */

/**
 * Registry entry containing renderer and metadata
 * @typedef {Object} RegistryEntry
 * @property {IEventRenderer} renderer - The renderer instance
 * @property {number} priority - Priority for conflict resolution
 * @property {Date} registeredAt - Registration timestamp
 */

/**
 * Singleton registry for event renderers
 * Manages renderer registration, resolution, and lifecycle
 */
class EventRendererRegistry {
    constructor() {
        /** @private @type {Map<StreamingEventType, RegistryEntry[]>} */
        this._renderers = new Map();
        
        /** @private @type {IEventRenderer|null} */
        this._defaultRenderer = null;
        
        /** @private @type {boolean} */
        this._frozen = false;
        
        /** @type {EventRendererRegistry|null} */
        EventRendererRegistry._instance = this;
    }

    /**
     * Gets the singleton instance
     * @static
     * @returns {EventRendererRegistry}
     */
    static getInstance() {
        if (!EventRendererRegistry._instance) {
            EventRendererRegistry._instance = new EventRendererRegistry();
        }
        return EventRendererRegistry._instance;
    }

    /**
     * Resets the singleton instance (useful for testing)
     * @static
     */
    static reset() {
        if (EventRendererRegistry._instance) {
            EventRendererRegistry._instance.clear();
        }
        EventRendererRegistry._instance = null;
    }

    /**
     * Registers a renderer for a specific event type
     * @param {StreamingEventType} eventType - Event type to handle
     * @param {IEventRenderer} renderer - Renderer instance
     * @param {Object} [options] - Registration options
     * @param {number} [options.priority=0] - Priority for conflict resolution
     * @returns {EventRendererRegistry} This registry for chaining
     * @throws {Error} If registry is frozen
     */
    register(eventType, renderer, options = {}) {
        if (this._frozen) {
            throw new Error('Cannot register renderer: registry is frozen');
        }

        if (!eventType) {
            throw new Error('Event type is required for registration');
        }

        if (!renderer) {
            throw new Error('Renderer is required for registration');
        }

        const priority = options.priority ?? 0;
        
        if (!this._renderers.has(eventType)) {
            this._renderers.set(eventType, []);
        }

        const entry = {
            renderer,
            priority,
            registeredAt: new Date()
        };

        const entries = this._renderers.get(eventType);
        entries.push(entry);
        
        // Sort by priority (higher first), then by registration time
        entries.sort((a, b) => {
            if (b.priority !== a.priority) {
                return b.priority - a.priority;
            }
            return a.registeredAt - b.registeredAt;
        });

        return this;
    }

    /**
     * Registers multiple renderers at once
     * @param {Array<{eventType: StreamingEventType, renderer: IEventRenderer, priority?: number}>} registrations
     * @returns {EventRendererRegistry}
     */
    registerAll(registrations) {
        for (const reg of registrations) {
            this.register(reg.eventType, reg.renderer, { priority: reg.priority });
        }
        return this;
    }

    /**
     * Unregisters a renderer for an event type
     * @param {StreamingEventType} eventType
     * @param {IEventRenderer} renderer
     * @returns {boolean} Whether a renderer was removed
     * @throws {Error} If registry is frozen
     */
    unregister(eventType, renderer) {
        if (this._frozen) {
            throw new Error('Cannot unregister renderer: registry is frozen');
        }

        const entries = this._renderers.get(eventType);
        if (!entries) return false;

        const index = entries.findIndex(e => e.renderer === renderer);
        if (index >= 0) {
            entries.splice(index, 1);
            if (entries.length === 0) {
                this._renderers.delete(eventType);
            }
            return true;
        }
        return false;
    }

    /**
     * Sets the default/fallback renderer for unknown event types
     * @param {IEventRenderer} renderer
     * @returns {EventRendererRegistry}
     * @throws {Error} If registry is frozen
     */
    setDefaultRenderer(renderer) {
        if (this._frozen) {
            throw new Error('Cannot set default renderer: registry is frozen');
        }
        this._defaultRenderer = renderer;
        return this;
    }

    /**
     * Gets the default/fallback renderer
     * @returns {IEventRenderer|null}
     */
    getDefaultRenderer() {
        return this._defaultRenderer;
    }

    /**
     * Resolves the best renderer for a given event
     * @param {StreamingEvent} event
     * @returns {IEventRenderer|null} The resolved renderer or null
     */
    resolve(event) {
        if (!event || !event.type) {
            return this._defaultRenderer;
        }

        const entries = this._renderers.get(event.type);
        if (!entries || entries.length === 0) {
            return this._defaultRenderer;
        }

        // Find first renderer that can handle this specific event
        for (const entry of entries) {
            if (entry.renderer.canRender(event)) {
                return entry.renderer;
            }
        }

        // Fall back to highest priority renderer for this type
        return entries[0].renderer;
    }

    /**
     * Resolves renderer by event type (without requiring full event object)
     * @param {StreamingEventType} eventType
     * @returns {IEventRenderer|null}
     */
    resolveByType(eventType) {
        const entries = this._renderers.get(eventType);
        if (entries && entries.length > 0) {
            return entries[0].renderer;
        }
        return this._defaultRenderer;
    }

    /**
     * Gets all renderers registered for an event type
     * @param {StreamingEventType} eventType
     * @returns {IEventRenderer[]}
     */
    getRenderersForType(eventType) {
        const entries = this._renderers.get(eventType);
        return entries ? entries.map(e => e.renderer) : [];
    }

    /**
     * Gets all registered event types
     * @returns {StreamingEventType[]}
     */
    getRegisteredTypes() {
        return Array.from(this._renderers.keys());
    }

    /**
     * Checks if a renderer is registered for an event type
     * @param {StreamingEventType} eventType
     * @returns {boolean}
     */
    hasRenderer(eventType) {
        const entries = this._renderers.get(eventType);
        return entries && entries.length > 0;
    }

    /**
     * Gets the count of renderers for an event type
     * @param {StreamingEventType} eventType
     * @returns {number}
     */
    getRendererCount(eventType) {
        const entries = this._renderers.get(eventType);
        return entries ? entries.length : 0;
    }

    /**
     * Clears all registrations
     * @returns {EventRendererRegistry}
     */
    clear() {
        this._renderers.clear();
        this._defaultRenderer = null;
        this._frozen = false;
        return this;
    }

    /**
     * Freezes the registry to prevent further modifications
     * Useful after initial setup is complete
     * @returns {EventRendererRegistry}
     */
    freeze() {
        this._frozen = true;
        return this;
    }

    /**
     * Unfreezes the registry to allow modifications
     * @returns {EventRendererRegistry}
     */
    unfreeze() {
        this._frozen = false;
        return this;
    }

    /**
     * Checks if the registry is frozen
     * @returns {boolean}
     */
    isFrozen() {
        return this._frozen;
    }

    /**
     * Gets registration statistics
     * @returns {Object}
     * @property {number} totalTypes - Number of event types with renderers
     * @property {number} totalRenderers - Total number of renderer registrations
     * @property {boolean} hasDefault - Whether a default renderer is set
     * @property {boolean} isFrozen - Whether registry is frozen
     */
    getStats() {
        let totalRenderers = 0;
        for (const entries of this._renderers.values()) {
            totalRenderers += entries.length;
        }

        return {
            totalTypes: this._renderers.size,
            totalRenderers,
            hasDefault: this._defaultRenderer !== null,
            isFrozen: this._frozen
        };
    }
}

/**
 * Convenience function to get the global registry instance
 * @returns {EventRendererRegistry}
 */
function getEventRendererRegistry() {
    return EventRendererRegistry.getInstance();
}

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { EventRendererRegistry, getEventRendererRegistry };
}
