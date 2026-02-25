/**
 * @fileoverview Event Sequencing Buffer
 * @description Client-side event buffer for reordering events that arrive out-of-order.
 * Implements timestamp-based sorting, sequence number validation, and gap detection.
 */

/**
 * @typedef {import('./event-types.js').StreamingEvent} StreamingEvent
 */

/**
 * Configuration options for EventSequencer
 * @typedef {Object} SequencerOptions
 * @property {number} bufferSize - Maximum number of events to buffer (default: 100)
 * @property {number} maxWaitMs - Maximum time to wait for missing events (default: 500)
 * @property {boolean} validateSequence - Whether to validate sequence numbers (default: true)
 * @property {boolean} useTimestamps - Whether to use timestamps for sorting (default: true)
 * @property {number} gapThreshold - Sequence gap threshold for warnings (default: 5)
 */

/**
 * Represents a gap in the event sequence
 * @typedef {Object} SequenceGap
 * @property {number} expected - The expected sequence number
 * @property {number} actual - The actual sequence number received
 * @property {number} size - Size of the gap (actual - expected)
 */

/**
 * Statistics about sequencing
 * @typedef {Object} SequencerStats
 * @property {number} totalBuffered - Total events currently buffered
 * @property {number} totalReleased - Total events released in order
 * @property {number} outOfOrderCount - Number of events received out of order
 * @property {number} gapCount - Number of sequence gaps detected
 * @property {number} maxGapSize - Maximum gap size detected
 * @property {number} avgWaitMs - Average wait time for out-of-order events
 */

/**
 * Event Sequencer - Buffers and reorders events for in-order delivery
 * Handles out-of-order arrival, sequence gaps, and timestamp-based sorting
 */
class EventSequencer {
    /**
     * @param {SequencerOptions} [options]
     */
    constructor(options = {}) {
        this.options = {
            bufferSize: 100,
            maxWaitMs: 500,
            validateSequence: true,
            useTimestamps: true,
            gapThreshold: 5,
            ...options
        };

        /** @private @type {Map<number, StreamingEvent>} */
        this._buffer = new Map();

        /** @private @type {number} */
        this._lastReleasedSequence = 0;

        /** @private @type {number} */
        this._expectedSequence = 1;

        /** @private @type {boolean} */
        this._isProcessing = false;

        /** @private @type {SequencerStats} */
        this._stats = {
            totalBuffered: 0,
            totalReleased: 0,
            outOfOrderCount: 0,
            gapCount: 0,
            maxGapSize: 0,
            avgWaitMs: 0
        };

        /** @private @type {number} */
        this._totalWaitMs = 0;

        /** @private @type {Function|null} */
        this._onReleaseCallback = null;

        /** @private @type {Function|null} */
        this._onGapCallback = null;

        /** @private @type {number|null} */
        this._flushTimer = null;
    }

    /**
     * Add an event to the buffer
     * @param {StreamingEvent} event
     * @returns {boolean} True if event was accepted
     */
    add(event) {
        if (!event || !event.sequenceNumber) {
            // Events without sequence numbers pass through immediately
            this._releaseEvent(event);
            return true;
        }

        const seq = event.sequenceNumber;

        // Check if we've already released this sequence
        if (seq <= this._lastReleasedSequence) {
            console.warn(`Duplicate or late event received: sequence ${seq}`);
            return false;
        }

        // Check buffer size
        if (this._buffer.size >= this.options.bufferSize) {
            this._flushBuffer(true); // Force flush on buffer full
        }

        // Calculate gap if any
        if (this.options.validateSequence && seq > this._expectedSequence) {
            const gapSize = seq - this._expectedSequence;
            this._stats.gapCount++;
            this._stats.maxGapSize = Math.max(this._stats.maxGapSize, gapSize);
            this._stats.outOfOrderCount++;

            if (gapSize >= this.options.gapThreshold) {
                console.warn(`Large sequence gap detected: expected ${this._expectedSequence}, got ${seq} (gap: ${gapSize})`);
            }

            if (this._onGapCallback) {
                this._onGapCallback({
                    expected: this._expectedSequence,
                    actual: seq,
                    size: gapSize
                });
            }
        }

        // Add to buffer
        this._buffer.set(seq, {
            ...event,
            _receivedAt: Date.now()
        });
        this._stats.totalBuffered++;

        // Try to release events in order
        this._tryRelease();

        // Set flush timer for max wait
        this._scheduleFlush();

        return true;
    }

    /**
     * Try to release events in sequence order
     * @private
     */
    _tryRelease() {
        if (this._isProcessing) return;
        this._isProcessing = true;

        try {
            const sortedSequences = Array.from(this._buffer.keys()).sort((a, b) => a - b);
            const toRelease = [];

            // Find contiguous sequence starting from expected
            let expected = this._expectedSequence;
            for (const seq of sortedSequences) {
                if (seq === expected) {
                    toRelease.push(seq);
                    expected++;
                } else if (seq > expected) {
                    // Gap found, stop here
                    break;
                }
            }

            // Release events
            for (const seq of toRelease) {
                const event = this._buffer.get(seq);
                if (event) {
                    this._buffer.delete(seq);

                    // Calculate wait time for stats
                    if (event._receivedAt) {
                        const waitMs = Date.now() - event._receivedAt;
                        this._totalWaitMs += waitMs;
                    }

                    // Release the event
                    this._releaseEvent(event);
                    this._lastReleasedSequence = seq;
                    this._expectedSequence = seq + 1;
                    this._stats.totalReleased++;
                }
            }

            // Update average wait time
            if (this._stats.outOfOrderCount > 0) {
                this._stats.avgWaitMs = this._totalWaitMs / this._stats.outOfOrderCount;
            }
        } finally {
            this._isProcessing = false;
        }
    }

    /**
     * Schedule a buffer flush after max wait time
     * @private
     */
    _scheduleFlush() {
        if (this._flushTimer) {
            clearTimeout(this._flushTimer);
        }

        this._flushTimer = setTimeout(() => {
            this._flushBuffer(false);
        }, this.options.maxWaitMs);
    }

    /**
     * Flush buffer, optionally forcing release of all events
     * @private
     * @param {boolean} [force] - Release all events regardless of sequence gaps
     */
    _flushBuffer(force = false) {
        if (this._flushTimer) {
            clearTimeout(this._flushTimer);
            this._flushTimer = null;
        }

        if (force) {
            // Sort by sequence number, then timestamp if available
            const entries = Array.from(this._buffer.entries());
            entries.sort((a, b) => {
                const seqDiff = a[0] - b[0];
                if (seqDiff !== 0) return seqDiff;

                // Fallback to timestamp sorting
                if (this.options.useTimestamps && a[1].timestamp && b[1].timestamp) {
                    return new Date(a[1].timestamp) - new Date(b[1].timestamp);
                }
                return 0;
            });

            // Release all events
            for (const [seq, event] of entries) {
                this._buffer.delete(seq);
                this._releaseEvent(event);
                this._stats.totalReleased++;
            }

            this._expectedSequence = this._lastReleasedSequence + 1;
        } else {
            // Try normal release
            this._tryRelease();

            // If buffer still has events and we're past max wait, force release
            if (this._buffer.size > 0) {
                const oldestEvent = this._getOldestEvent();
                if (oldestEvent && oldestEvent._receivedAt) {
                    const age = Date.now() - oldestEvent._receivedAt;
                    if (age >= this.options.maxWaitMs) {
                        this._flushBuffer(true);
                    }
                }
            }
        }
    }

    /**
     * Get the oldest event in the buffer
     * @private
     * @returns {StreamingEvent|null}
     */
    _getOldestEvent() {
        let oldest = null;
        let oldestSeq = Infinity;

        for (const [seq, event] of this._buffer) {
            if (seq < oldestSeq) {
                oldestSeq = seq;
                oldest = event;
            }
        }

        return oldest;
    }

    /**
     * Release an event through the callback
     * @private
     * @param {StreamingEvent} event
     */
    _releaseEvent(event) {
        // Clean up internal tracking
        const cleanEvent = { ...event };
        delete cleanEvent._receivedAt;

        if (this._onReleaseCallback) {
            this._onReleaseCallback(cleanEvent);
        }
    }

    /**
     * Set callback for event release
     * @param {Function} callback
     */
    onRelease(callback) {
        this._onReleaseCallback = callback;
    }

    /**
     * Set callback for gap detection
     * @param {Function} callback
     */
    onGap(callback) {
        this._onGapCallback = callback;
    }

    /**
     * Get current statistics
     * @returns {SequencerStats}
     */
    getStats() {
        return { ...this._stats, totalBuffered: this._buffer.size };
    }

    /**
     * Get current buffer state
     * @returns {Object}
     */
    getState() {
        return {
            bufferedCount: this._buffer.size,
            lastReleasedSequence: this._lastReleasedSequence,
            expectedSequence: this._expectedSequence,
            bufferedSequences: Array.from(this._buffer.keys()).sort((a, b) => a - b)
        };
    }

    /**
     * Clear the buffer and reset state
     */
    clear() {
        if (this._flushTimer) {
            clearTimeout(this._flushTimer);
            this._flushTimer = null;
        }
        this._buffer.clear();
        this._lastReleasedSequence = 0;
        this._expectedSequence = 1;
        this._isProcessing = false;
    }

    /**
     * Reset statistics
     */
    resetStats() {
        this._stats = {
            totalBuffered: this._buffer.size,
            totalReleased: 0,
            outOfOrderCount: 0,
            gapCount: 0,
            maxGapSize: 0,
            avgWaitMs: 0
        };
        this._totalWaitMs = 0;
    }

    /**
     * Dispose of the sequencer
     */
    dispose() {
        this.clear();
        this._onReleaseCallback = null;
        this._onGapCallback = null;
    }
}

/**
 * Event Sequencer Manager - Manages multiple sequencer instances
 */
const EventSequencerManager = {
    /** @private @type {Map<string, EventSequencer>} */
    _sequencers: new Map(),

    /**
     * Create a new sequencer
     * @param {string} id
     * @param {SequencerOptions} [options]
     * @returns {EventSequencer}
     */
    create(id, options = {}) {
        if (this._sequencers.has(id)) {
            console.warn(`Sequencer ${id} already exists, disposing old instance`);
            this.dispose(id);
        }

        const sequencer = new EventSequencer(options);
        this._sequencers.set(id, sequencer);
        return sequencer;
    },

    /**
     * Get a sequencer by ID
     * @param {string} id
     * @returns {EventSequencer|null}
     */
    get(id) {
        return this._sequencers.get(id) || null;
    },

    /**
     * Dispose a sequencer
     * @param {string} id
     */
    dispose(id) {
        const sequencer = this._sequencers.get(id);
        if (sequencer) {
            sequencer.dispose();
            this._sequencers.delete(id);
        }
    },

    /**
     * Dispose all sequencers
     */
    disposeAll() {
        for (const [id, sequencer] of this._sequencers) {
            sequencer.dispose();
        }
        this._sequencers.clear();
    },

    /**
     * Get all active sequencer IDs
     * @returns {string[]}
     */
    getActiveIds() {
        return Array.from(this._sequencers.keys());
    }
};

/**
 * Integrate EventSequencer with HtmxSseIntegration
 * @param {HtmxSseIntegration} integration
 * @param {string} [sequencerId]
 */
function integrateWithHtmxSse(integration, sequencerId = 'default') {
    // Create or get sequencer
    let sequencer = EventSequencerManager.get(sequencerId);
    if (!sequencer) {
        sequencer = EventSequencerManager.create(sequencerId, {
            bufferSize: 50,
            maxWaitMs: 200,
            validateSequence: true,
            useTimestamps: true
        });
    }

    // Hook into the integration's event processing
    const originalRenderEvent = integration.renderEvent.bind(integration);

    // Replace with sequencer-buffered version
    integration.renderEvent = function(event) {
        sequencer.add(event);
    };

    // Set up release callback to actually render
    sequencer.onRelease((orderedEvent) => {
        originalRenderEvent(orderedEvent);
    });

    // Set up gap callback for monitoring
    sequencer.onGap((gap) => {
        console.warn(`Event sequence gap detected: expected ${gap.expected}, got ${gap.actual}`);
        document.dispatchEvent(new CustomEvent('event-sequencer:gap', {
            detail: gap,
            bubbles: true
        }));
    });

    return sequencer;
}

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        EventSequencer,
        EventSequencerManager,
        integrateWithHtmxSse
    };
}
