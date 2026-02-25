/**
 * @fileoverview Command Suggestion Telemetry Tracker
 * @description Tracks user interactions with command suggestions for analytics
 * Monitors acceptance rates, rejection reasons, and suggestion effectiveness
 */

/**
 * Tracks telemetry for command suggestion interactions
 */
class CommandSuggestionTelemetry {
    constructor() {
        this.suggestions = new Map();
        this.metrics = {
            totalSuggestions: 0,
            totalAccepted: 0,
            totalRejected: 0,
            byCommand: new Map(),
            byConfidence: new Map()
        };
    }

    /**
     * Records a new command suggestion
     * @param {string} confirmationRequestId - Unique suggestion ID
     * @param {Object} suggestionData - Suggestion details
     * @param {string} suggestionData.commandName - Name of suggested command
     * @param {string} suggestionData.formattedCommand - Full command string
     * @param {number} suggestionData.confidence - Confidence score (0-1)
     * @param {string} suggestionData.reasoning - Why the command was suggested
     * @param {string} [suggestionData.originalInput] - User's original input
     */
    recordSuggestion(confirmationRequestId, suggestionData) {
        const timestamp = new Date().toISOString();
        
        this.suggestions.set(confirmationRequestId, {
            ...suggestionData,
            timestamp,
            status: 'pending',
            statusChangedAt: timestamp,
            userMessage: null
        });

        this.metrics.totalSuggestions++;
        
        const commandName = suggestionData.commandName;
        if (!this.metrics.byCommand.has(commandName)) {
            this.metrics.byCommand.set(commandName, {
                suggested: 0,
                accepted: 0,
                rejected: 0,
                acceptanceRate: 0
            });
        }
        const cmdMetrics = this.metrics.byCommand.get(commandName);
        cmdMetrics.suggested++;

        const confidenceBucket = Math.floor(suggestionData.confidence * 10) * 10;
        const bucketKey = `${confidenceBucket}-${confidenceBucket + 10}`;
        if (!this.metrics.byConfidence.has(bucketKey)) {
            this.metrics.byConfidence.set(bucketKey, {
                suggested: 0,
                accepted: 0,
                rejected: 0,
                acceptanceRate: 0
            });
        }
        const confMetrics = this.metrics.byConfidence.get(bucketKey);
        confMetrics.suggested++;

        this._emitTelemetryEvent('suggestion_recorded', {
            confirmationRequestId,
            ...suggestionData,
            timestamp
        });
    }

    /**
     * Records acceptance of a suggestion
     * @param {string} confirmationRequestId - Suggestion ID
     */
    recordAcceptance(confirmationRequestId) {
        const suggestion = this.suggestions.get(confirmationRequestId);
        if (!suggestion) {
            console.warn(`Suggestion ${confirmationRequestId} not found for acceptance tracking`);
            return;
        }

        const timestamp = new Date().toISOString();
        suggestion.status = 'accepted';
        suggestion.statusChangedAt = timestamp;

        this.metrics.totalAccepted++;

        const cmdMetrics = this.metrics.byCommand.get(suggestion.commandName);
        if (cmdMetrics) {
            cmdMetrics.accepted++;
            cmdMetrics.acceptanceRate = (cmdMetrics.accepted / cmdMetrics.suggested * 100).toFixed(1);
        }

        const confidenceBucket = Math.floor(suggestion.confidence * 10) * 10;
        const bucketKey = `${confidenceBucket}-${confidenceBucket + 10}`;
        const confMetrics = this.metrics.byConfidence.get(bucketKey);
        if (confMetrics) {
            confMetrics.accepted++;
            confMetrics.acceptanceRate = (confMetrics.accepted / confMetrics.suggested * 100).toFixed(1);
        }

        this._emitTelemetryEvent('suggestion_accepted', {
            confirmationRequestId,
            commandName: suggestion.commandName,
            confidence: suggestion.confidence,
            timeToAcceptanceMs: new Date(timestamp) - new Date(suggestion.timestamp)
        });
    }

    /**
     * Records rejection of a suggestion
     * @param {string} confirmationRequestId - Suggestion ID
     * @param {string} [userMessage] - Optional reason for rejection
     */
    recordRejection(confirmationRequestId, userMessage = null) {
        const suggestion = this.suggestions.get(confirmationRequestId);
        if (!suggestion) {
            console.warn(`Suggestion ${confirmationRequestId} not found for rejection tracking`);
            return;
        }

        const timestamp = new Date().toISOString();
        suggestion.status = 'rejected';
        suggestion.statusChangedAt = timestamp;
        suggestion.userMessage = userMessage;

        this.metrics.totalRejected++;

        const cmdMetrics = this.metrics.byCommand.get(suggestion.commandName);
        if (cmdMetrics) {
            cmdMetrics.rejected++;
            cmdMetrics.acceptanceRate = (cmdMetrics.accepted / cmdMetrics.suggested * 100).toFixed(1);
        }

        const confidenceBucket = Math.floor(suggestion.confidence * 10) * 10;
        const bucketKey = `${confidenceBucket}-${confidenceBucket + 10}`;
        const confMetrics = this.metrics.byConfidence.get(bucketKey);
        if (confMetrics) {
            confMetrics.rejected++;
            confMetrics.acceptanceRate = (confMetrics.accepted / confMetrics.suggested * 100).toFixed(1);
        }

        this._emitTelemetryEvent('suggestion_rejected', {
            confirmationRequestId,
            commandName: suggestion.commandName,
            confidence: suggestion.confidence,
            userMessage,
            timeToRejectionMs: new Date(timestamp) - new Date(suggestion.timestamp)
        });
    }

    /**
     * Gets current metrics
     * @returns {Object} Current telemetry metrics
     */
    getMetrics() {
        return {
            ...this.metrics,
            overallAcceptanceRate: this.metrics.totalSuggestions > 0
                ? (this.metrics.totalAccepted / this.metrics.totalSuggestions * 100).toFixed(1)
                : 0,
            byCommand: Object.fromEntries(this.metrics.byCommand),
            byConfidence: Object.fromEntries(this.metrics.byConfidence)
        };
    }

    /**
     * Gets metrics for a specific command
     * @param {string} commandName - Command name
     * @returns {Object|null} Command-specific metrics or null if not found
     */
    getCommandMetrics(commandName) {
        return this.metrics.byCommand.get(commandName) || null;
    }

    /**
     * Gets metrics for a confidence range
     * @param {number} minConfidence - Minimum confidence (0-1)
     * @param {number} maxConfidence - Maximum confidence (0-1)
     * @returns {Object|null} Confidence-range metrics or null if not found
     */
    getConfidenceMetrics(minConfidence, maxConfidence) {
        const minBucket = Math.floor(minConfidence * 10) * 10;
        const maxBucket = Math.floor(maxConfidence * 10) * 10;
        const bucketKey = `${minBucket}-${maxBucket + 10}`;
        return this.metrics.byConfidence.get(bucketKey) || null;
    }

    /**
     * Emits a telemetry event
     * @private
     * @param {string} eventName - Event name
     * @param {Object} eventData - Event data
     */
    _emitTelemetryEvent(eventName, eventData) {
        const event = new CustomEvent('commandSuggestionTelemetry', {
            detail: {
                eventName,
                eventData,
                timestamp: new Date().toISOString()
            }
        });
        document.dispatchEvent(event);

        // Also log to console in development
        if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
            console.log(`[Telemetry] ${eventName}:`, eventData);
        }
    }

    /**
     * Exports metrics as JSON
     * @returns {string} JSON string of metrics
     */
    exportMetrics() {
        return JSON.stringify(this.getMetrics(), null, 2);
    }

    /**
     * Resets all metrics
     */
    reset() {
        this.suggestions.clear();
        this.metrics = {
            totalSuggestions: 0,
            totalAccepted: 0,
            totalRejected: 0,
            byCommand: new Map(),
            byConfidence: new Map()
        };
    }
}

// Global instance
window.commandSuggestionTelemetry = new CommandSuggestionTelemetry();
