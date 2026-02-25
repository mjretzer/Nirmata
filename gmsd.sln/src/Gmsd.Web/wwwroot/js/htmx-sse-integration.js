/**
 * @fileoverview HTMX SSE Integration for v2 Streaming Endpoint
 * @description Configures HTMX SSE extension to work with the v2 streaming endpoint,
 * mapping typed events to renderer invocations with sequencing and debouncing.
 */

/**
 * @typedef {import('./event-types.js').StreamingEvent} StreamingEvent
 * @typedef {import('./event-renderer-registry.js').EventRendererRegistry} EventRendererRegistry
 * @typedef {import('./event-sequencer.js').EventSequencer} EventSequencer
 */

/**
 * HTMX SSE Integration for v2 streaming endpoint
 * Manages SSE connection, event routing, sequencing, and debouncing
 */
class HtmxSseIntegration {
    constructor(options = {}) {
        this.options = {
            endpoint: '/api/chat/stream-v2',
            threadId: 'default',
            chatOnly: false,
            debounceMs: 50,
            sequenceBufferSize: 100,
            ...options
        };

        this.eventSource = null;
        this.isConnected = false;
        this.eventBuffer = new Map();
        this.lastSequenceNumber = 0;
        this.pendingDeltas = new Map();
        this.debounceTimers = new Map();
        this.accumulatedContent = new Map();
        this.currentMessageId = null;

        /** @private @type {EventSequencer|null} */
        this._sequencer = null;
    }

    /**
     * Initialize SSE connection to v2 endpoint
     * @returns {Promise<void>}
     */
    async connect() {
        if (this.isConnected) {
            console.warn('SSE connection already active');
            return;
        }

        // Build endpoint URL with query parameters
        const params = new URLSearchParams();
        params.append('threadId', this.options.threadId);
        if (this.options.chatOnly) {
            params.append('chatOnly', 'true');
        }

        const url = `${this.options.endpoint}?${params.toString()}`;

        try {
            this.eventSource = new EventSource(url, { withCredentials: true });
            this.setupEventHandlers();
            this.isConnected = true;
            console.log('SSE connection established to v2 endpoint');
        } catch (error) {
            console.error('Failed to establish SSE connection:', error);
            throw error;
        }
    }

    /**
     * Setup SSE event handlers
     * @private
     */
    setupEventHandlers() {
        // Handle connection open
        this.eventSource.onopen = () => {
            console.log('SSE connection opened');
            this.dispatchCustomEvent('sse:open', {});
        };

        // Handle incoming messages
        this.eventSource.onmessage = (event) => {
            this.handleSseMessage(event.data);
        };

        // Handle errors
        this.eventSource.onerror = (error) => {
            console.error('SSE error:', error);
            this.dispatchCustomEvent('sse:error', { error });
            this.reconnect();
        };
    }

    /**
     * Handle incoming SSE message
     * @private
     * @param {string} data
     */
    handleSseMessage(data) {
        try {
            const event = JSON.parse(data);
            this.processEvent(event);
        } catch (error) {
            console.error('Error parsing SSE data:', error, data);
        }
    }

    /**
     * Process a streaming event
     * @private
     * @param {StreamingEvent} event
     */
    processEvent(event) {
        // Update sequence tracking
        if (event.sequenceNumber !== undefined && event.sequenceNumber !== null) {
            this.lastSequenceNumber = Math.max(this.lastSequenceNumber, event.sequenceNumber);
        }

        // Handle event based on type
        switch (event.type) {
            case StreamingEventType.AssistantDelta:
                this.handleAssistantDelta(event);
                break;
            case StreamingEventType.AssistantFinal:
                this.handleAssistantFinal(event);
                break;
            default:
                this.renderEvent(event);
                break;
        }
    }

    /**
     * Handle assistant.delta events with debouncing
     * @private
     * @param {StreamingEvent} event
     */
    handleAssistantDelta(event) {
        const messageId = event.payload?.messageId;
        if (!messageId) return;

        // Accumulate content
        if (!this.accumulatedContent.has(messageId)) {
            this.accumulatedContent.set(messageId, '');
        }
        this.accumulatedContent.set(messageId,
            this.accumulatedContent.get(messageId) + (event.payload?.content || ''));

        // Debounce the render
        this.debounceRender(messageId, event);
    }

    /**
     * Handle assistant.final events
     * @private
     * @param {StreamingEvent} event
     */
    handleAssistantFinal(event) {
        const messageId = event.payload?.messageId;
        if (!messageId) return;

        // Clear any pending debounce
        if (this.debounceTimers.has(messageId)) {
            clearTimeout(this.debounceTimers.get(messageId));
            this.debounceTimers.delete(messageId);
        }

        // Use final content if available, otherwise use accumulated
        const finalContent = event.payload?.content || this.accumulatedContent.get(messageId) || '';

        // Render final message
        this.renderEvent({
            ...event,
            payload: { ...event.payload, content: finalContent }
        });

        // Cleanup
        this.accumulatedContent.delete(messageId);
        this.pendingDeltas.delete(messageId);
    }

    /**
     * Debounce rendering for rapid delta events
     * @private
     * @param {string} messageId
     * @param {StreamingEvent} event
     */
    debounceRender(messageId, event) {
        // Clear existing timer
        if (this.debounceTimers.has(messageId)) {
            clearTimeout(this.debounceTimers.get(messageId));
        }

        // Store pending event
        this.pendingDeltas.set(messageId, event);

        // Set new timer
        const timer = setTimeout(() => {
            const pendingEvent = this.pendingDeltas.get(messageId);
            if (pendingEvent) {
                // Create a synthetic event with accumulated content
                const accumulatedEvent = {
                    ...pendingEvent,
                    payload: {
                        ...pendingEvent.payload,
                        content: this.accumulatedContent.get(messageId) || ''
                    }
                };
                this.renderEvent(accumulatedEvent);
                this.pendingDeltas.delete(messageId);
            }
            this.debounceTimers.delete(messageId);
        }, this.options.debounceMs);

        this.debounceTimers.set(messageId, timer);
    }

    /**
     * Render event using appropriate renderer
     * @private
     * @param {StreamingEvent} event
     */
    renderEvent(event) {
        try {
            const registry = getEventRendererRegistry();
            const renderer = registry.resolve(event);

            if (!renderer) {
                console.warn('No renderer found for event type:', event.type);
                return;
            }

            const result = renderer.render(event, {
                threadId: this.options.threadId,
                sequenceNumber: event.sequenceNumber
            });

            // Dispatch render result
            this.dispatchCustomEvent('event:rendered', {
                event,
                result,
                renderer: renderer.getInfo()
            });

        } catch (error) {
            console.error('Error rendering event:', error, event);
            this.dispatchCustomEvent('event:renderError', { event, error });
        }
    }

    /**
     * Dispatch custom event for UI integration
     * @private
     * @param {string} name
     * @param {Object} detail
     */
    dispatchCustomEvent(name, detail) {
        const event = new CustomEvent(`htmx-sse:${name}`, {
            detail,
            bubbles: true,
            cancelable: true
        });
        document.dispatchEvent(event);
    }

    /**
     * Reconnect SSE connection with exponential backoff
     * @private
     */
    reconnect() {
        if (this.reconnectAttempts === undefined) {
            this.reconnectAttempts = 0;
        }

        const maxAttempts = 5;
        const baseDelay = 1000;

        if (this.reconnectAttempts >= maxAttempts) {
            console.error('Max reconnection attempts reached');
            this.dispatchCustomEvent('sse:maxReconnectReached', {});
            return;
        }

        const delay = baseDelay * Math.pow(2, this.reconnectAttempts);
        this.reconnectAttempts++;

        console.log(`Reconnecting in ${delay}ms (attempt ${this.reconnectAttempts})`);

        setTimeout(() => {
            this.disconnect();
            this.connect().catch(err => {
                console.error('Reconnection failed:', err);
            });
        }, delay);
    }

    /**
     * Disconnect SSE connection
     */
    disconnect() {
        if (this.eventSource) {
            this.eventSource.close();
            this.eventSource = null;
        }

        // Clear all debounce timers
        for (const timer of this.debounceTimers.values()) {
            clearTimeout(timer);
        }
        this.debounceTimers.clear();

        this.isConnected = false;
        this.reconnectAttempts = 0;

        console.log('SSE connection closed');
        this.dispatchCustomEvent('sse:close', {});
    }

    /**
     * Get current connection state
     * @returns {Object}
     */
    getState() {
        return {
            isConnected: this.isConnected,
            lastSequenceNumber: this.lastSequenceNumber,
            pendingDeltaCount: this.pendingDeltas.size,
            bufferedEventCount: this.eventBuffer.size,
            hasSequencer: this._sequencer !== null
        };
    }

    /**
     * Enable event sequencing with the EventSequencer
     * @param {Object} [options]
     * @param {number} [options.bufferSize=50] - Sequencer buffer size
     * @param {number} [options.maxWaitMs=200] - Max wait time for out-of-order events
     * @returns {EventSequencer|null}
     */
    enableSequencing(options = {}) {
        if (typeof EventSequencer === 'undefined') {
            console.warn('EventSequencer not available, sequencing disabled');
            return null;
        }

        // Create sequencer
        this._sequencer = new EventSequencer({
            bufferSize: options.bufferSize || 50,
            maxWaitMs: options.maxWaitMs || 200,
            validateSequence: true,
            useTimestamps: true
        });

        // Store original render method
        const originalRenderEvent = this.renderEvent.bind(this);

        // Override renderEvent to use sequencer
        this.renderEvent = (event) => {
            if (this._sequencer) {
                this._sequencer.add(event);
            } else {
                originalRenderEvent(event);
            }
        };

        // Set up release callback
        this._sequencer.onRelease((orderedEvent) => {
            originalRenderEvent(orderedEvent);
        });

        // Set up gap callback for monitoring
        this._sequencer.onGap((gap) => {
            console.warn(`Event sequence gap: expected ${gap.expected}, got ${gap.actual}`);
            this.dispatchCustomEvent('sequencer:gap', gap);
        });

        console.log('Event sequencing enabled');
        return this._sequencer;
    }

    /**
     * Get sequencer stats if sequencing is enabled
     * @returns {Object|null}
     */
    getSequencerStats() {
        return this._sequencer ? this._sequencer.getStats() : null;
    }
}

/**
 * HTMX SSE Extension for v2 streaming
 * Registers HTMX extension to enable hx-sse attributes with v2 endpoint
 */
function registerHtmxSseExtension() {
    if (typeof htmx === 'undefined') {
        console.warn('HTMX not loaded, skipping SSE extension registration');
        return;
    }

    htmx.defineExtension('sse-v2', {
        init: function(api) {
            // Track active SSE connections per element
            const connections = new Map();

            /**
             * Handle sse-v2 attribute: hx-ext="sse-v2" sse-connect="..." sse-swap="..."
             */
            api.onLoad(function(evt) {
                const elt = evt.detail.elt;
                const sseConnect = api.getAttributeValue(elt, 'sse-connect');
                const sseSwap = api.getAttributeValue(elt, 'sse-swap');

                if (!sseConnect) return;

                // Parse options from attributes
                const threadId = api.getAttributeValue(elt, 'sse-thread') || 'default';
                const chatOnly = api.getAttributeValue(elt, 'sse-chat-only') === 'true';
                const debounceMs = parseInt(api.getAttributeValue(elt, 'sse-debounce') || '50', 10);

                // Create integration instance
                const integration = new HtmxSseIntegration({
                    endpoint: sseConnect,
                    threadId,
                    chatOnly,
                    debounceMs
                });

                // Store connection reference
                connections.set(elt, integration);

                // Setup event rendering
                document.addEventListener('htmx-sse:event:rendered', function(e) {
                    if (sseSwap && e.detail && e.detail.result) {
                        const target = document.querySelector(sseSwap);
                        if (target) {
                            // Append rendered HTML
                            target.insertAdjacentHTML('beforeend', e.detail.result.html);

                            // Apply HTMX processing to new content
                            api.process(target.lastElementChild);
                        }
                    }
                });

                // Connect
                integration.connect().catch(err => {
                    console.error('SSE connection failed:', err);
                });
            });

            /**
             * Cleanup on element removal
             */
            api.onUnload(function(evt) {
                const elt = evt.detail.elt;
                const connection = connections.get(elt);
                if (connection) {
                    connection.disconnect();
                    connections.delete(elt);
                }
            });
        }
    });

    console.log('HTMX SSE-v2 extension registered');
}

/**
 * Global HtmxSseIntegration instance manager
 */
const HtmxSseManager = {
    integrations: new Map(),

    /**
     * Create and connect a new integration
     * @param {string} id
     * @param {Object} options
     * @returns {HtmxSseIntegration}
     */
    create(id, options = {}) {
        if (this.integrations.has(id)) {
            console.warn(`Integration ${id} already exists, disconnecting old instance`);
            this.disconnect(id);
        }

        const integration = new HtmxSseIntegration(options);
        this.integrations.set(id, integration);
        return integration;
    },

    /**
     * Connect an integration
     * @param {string} id
     */
    async connect(id) {
        const integration = this.integrations.get(id);
        if (integration) {
            await integration.connect();
        }
    },

    /**
     * Disconnect an integration
     * @param {string} id
     */
    disconnect(id) {
        const integration = this.integrations.get(id);
        if (integration) {
            integration.disconnect();
            this.integrations.delete(id);
        }
    },

    /**
     * Get integration state
     * @param {string} id
     * @returns {Object|null}
     */
    getState(id) {
        const integration = this.integrations.get(id);
        return integration ? integration.getState() : null;
    },

    /**
     * Disconnect all integrations
     */
    disconnectAll() {
        for (const [id, integration] of this.integrations) {
            integration.disconnect();
        }
        this.integrations.clear();
    }
};

// Auto-register extension when HTMX is available
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        registerHtmxSseExtension();
    });
} else {
    registerHtmxSseExtension();
}

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { HtmxSseIntegration, HtmxSseManager, registerHtmxSseExtension };
}
