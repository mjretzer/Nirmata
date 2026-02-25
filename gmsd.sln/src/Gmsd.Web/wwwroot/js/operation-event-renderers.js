/**
 * @fileoverview Operation Event Renderers
 * @description Renderers for operation events (run lifecycle, phase lifecycle, tool calls)
 * Provides visual representation of system operations and workflow progress
 */

/**
 * @typedef {import('./ievent-renderer.js').EventRendererBase} EventRendererBase
 * @typedef {import('./event-types.js').StreamingEvent} StreamingEvent
 * @typedef {import('./event-types.js').RunLifecyclePayload} RunLifecyclePayload
 * @typedef {import('./event-types.js').PhaseLifecyclePayload} PhaseLifecyclePayload
 * @typedef {import('./event-types.js').ToolCallPayload} ToolCallPayload
 * @typedef {import('./event-types.js').ToolResultPayload} ToolResultPayload
 */

/**
 * Renders run.lifecycle events (run.started, run.finished)
 * Shows workflow execution status with visual indicators
 * @extends EventRendererBase
 */
class RunLifecycleRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.RunLifecycle,
            name: 'RunLifecycleRenderer',
            description: 'Renders run lifecycle events with status indicators',
            version: '1.0.0',
            priority: 10
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        /** @type {RunLifecyclePayload} */
        const payload = event.payload;
        const elementId = this.generateElementId(event.id, 'run');
        const isStarted = payload.status === 'started';

        const statusIcon = isStarted ? '▶️' : '✅';
        const statusClass = isStarted ? 'run--started' : 'run--finished';
        const statusLabel = isStarted ? 'Run Started' : 'Run Finished';

        const durationHtml = payload.duration && !isStarted
            ? `<span class="run-card__duration">⏱️ ${this._formatDuration(payload.duration)}</span>`
            : '';

        const artifactsHtml = payload.artifactReferences && !isStarted
            ? this._renderArtifacts(payload.artifactReferences)
            : '';

        const html = `
            <div class="run-card ${statusClass}" id="${elementId}" data-run-id="${this.escapeHtml(payload.runId)}" data-status="${payload.status}">
                <div class="run-card__header">
                    <span class="run-card__icon">${statusIcon}</span>
                    <span class="run-card__status">${statusLabel}</span>
                    ${durationHtml}
                </div>
                <div class="run-card__body">
                    <span class="run-card__id">Run ID: ${this.escapeHtml(payload.runId)}</span>
                    ${artifactsHtml}
                </div>
                <div class="run-card__footer">
                    <time class="run-card__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                </div>
            </div>
        `;

        return this.createRenderResult(html, {
            elementId,
            append: true
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {HTMLElement} element
     * @param {Object} [context]
     * @returns {boolean}
     */
    update(event, element, context) {
        /** @type {RunLifecyclePayload} */
        const payload = event.payload;

        // Only update if transitioning from started to finished
        if (payload.status === 'finished' && element.dataset.status === 'started') {
            const header = element.querySelector('.run-card__header');
            const body = element.querySelector('.run-card__body');

            if (header) {
                header.innerHTML = `
                    <span class="run-card__icon">✅</span>
                    <span class="run-card__status">Run Finished</span>
                    ${payload.duration ? `<span class="run-card__duration">⏱️ ${this._formatDuration(payload.duration)}</span>` : ''}
                `;
            }

            if (body && payload.artifactReferences) {
                const artifactsHtml = this._renderArtifacts(payload.artifactReferences);
                const existingArtifacts = body.querySelector('.run-card__artifacts');
                if (existingArtifacts) {
                    existingArtifacts.outerHTML = artifactsHtml;
                } else {
                    body.insertAdjacentHTML('beforeend', artifactsHtml);
                }
            }

            element.classList.remove('run--started');
            element.classList.add('run--finished');
            element.dataset.status = 'finished';

            return true;
        }

        return false;
    }

    /**
     * Formats ISO 8601 duration string for display
     * @private
     * @param {string} duration - ISO 8601 duration (e.g., PT1M30S)
     * @returns {string}
     */
    _formatDuration(duration) {
        if (!duration) return '';

        // Parse PT1H2M3S format
        const match = duration.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?/);
        if (!match) return duration;

        const hours = parseInt(match[1] || '0', 10);
        const minutes = parseInt(match[2] || '0', 10);
        const seconds = parseInt(match[3] || '0', 10);

        const parts = [];
        if (hours > 0) parts.push(`${hours}h`);
        if (minutes > 0) parts.push(`${minutes}m`);
        if (seconds > 0 || parts.length === 0) parts.push(`${seconds}s`);

        return parts.join(' ');
    }

    /**
     * Renders artifact references
     * @private
     * @param {Object} artifacts
     * @returns {string}
     */
    _renderArtifacts(artifacts) {
        const artifactList = Object.entries(artifacts)
            .map(([key, value]) => `<li class="run-card__artifact">${this.escapeHtml(key)}: ${this.escapeHtml(String(value))}</li>`)
            .join('');

        return `
            <div class="run-card__artifacts">
                <span class="run-card__artifacts-label">Artifacts:</span>
                <ul class="run-card__artifact-list">${artifactList}</ul>
            </div>
        `;
    }
}

/**
 * Renders phase.lifecycle events (phase.started, phase.completed)
 * Shows phase execution with timeline visualization
 * @extends EventRendererBase
 */
class PhaseLifecycleRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.PhaseLifecycle,
            name: 'PhaseLifecycleRenderer',
            description: 'Renders phase lifecycle events with timeline visualization',
            version: '1.0.0',
            priority: 10
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        /** @type {PhaseLifecyclePayload} */
        const payload = event.payload;
        const elementId = this.generateElementId(event.id, 'phase');
        const isStarted = payload.status === 'started';

        const phaseIcon = this._getPhaseIcon(payload.phase);
        const statusIcon = isStarted ? '⏳' : '✓';
        const statusClass = isStarted ? 'phase--started' : 'phase--completed';
        const statusLabel = isStarted ? 'Running' : 'Completed';

        const contextHtml = payload.context && isStarted
            ? this._renderContext(payload.context)
            : '';

        const artifactsHtml = payload.outputArtifacts && !isStarted
            ? this._renderOutputArtifacts(payload.outputArtifacts)
            : '';

        const runIdAttr = payload.runId ? `data-run-id="${this.escapeHtml(payload.runId)}"` : '';

        const html = `
            <div class="phase-card ${statusClass}" id="${elementId}" data-phase="${this.escapeHtml(payload.phase)}" data-status="${payload.status}" ${runIdAttr}>
                <div class="phase-card__timeline">
                    <div class="phase-card__timeline-node ${isStarted ? 'phase-card__timeline-node--active' : 'phase-card__timeline-node--complete'}"></div>
                    <div class="phase-card__timeline-line"></div>
                </div>
                <div class="phase-card__content">
                    <div class="phase-card__header">
                        <span class="phase-card__icon">${phaseIcon}</span>
                        <span class="phase-card__name">${this.escapeHtml(payload.phase)}</span>
                        <span class="phase-card__status-badge phase-card__status-badge--${payload.status}">${statusIcon} ${statusLabel}</span>
                    </div>
                    <div class="phase-card__body">
                        ${contextHtml}
                        ${artifactsHtml}
                    </div>
                    <div class="phase-card__footer">
                        <time class="phase-card__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                    </div>
                </div>
            </div>
        `;

        return this.createRenderResult(html, {
            elementId,
            append: true
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {HTMLElement} element
     * @param {Object} [context]
     * @returns {boolean}
     */
    update(event, element, context) {
        /** @type {PhaseLifecyclePayload} */
        const payload = event.payload;
        const currentStatus = element.dataset.status;

        // Update when phase completes
        if (payload.status === 'completed' && currentStatus === 'started') {
            const timelineNode = element.querySelector('.phase-card__timeline-node');
            const statusBadge = element.querySelector('.phase-card__status-badge');
            const body = element.querySelector('.phase-card__body');

            if (timelineNode) {
                timelineNode.classList.remove('phase-card__timeline-node--active');
                timelineNode.classList.add('phase-card__timeline-node--complete');
            }

            if (statusBadge) {
                statusBadge.className = 'phase-card__status-badge phase-card__status-badge--completed';
                statusBadge.innerHTML = '✓ Completed';
            }

            if (body && payload.outputArtifacts) {
                const artifactsHtml = this._renderOutputArtifacts(payload.outputArtifacts);
                const existingArtifacts = body.querySelector('.phase-card__artifacts');
                if (existingArtifacts) {
                    existingArtifacts.outerHTML = artifactsHtml;
                } else {
                    body.insertAdjacentHTML('beforeend', artifactsHtml);
                }
            }

            element.classList.remove('phase--started');
            element.classList.add('phase--completed');
            element.dataset.status = 'completed';

            return true;
        }

        return false;
    }

    /**
     * Gets icon for phase type
     * @private
     * @param {string} phase
     * @returns {string}
     */
    _getPhaseIcon(phase) {
        const icons = {
            'Chat': '💬',
            'Planner': '📋',
            'Coder': '💻',
            'Reviewer': '🔍',
            'Tester': '🧪',
            'Runner': '▶️',
            'GitCommitter': '📦',
            'Gating': '🚪',
            'Classification': '🏷️'
        };
        return icons[phase] || '🔄';
    }

    /**
     * Renders phase context data
     * @private
     * @param {Object} context
     * @returns {string}
     */
    _renderContext(context) {
        const contextList = Object.entries(context)
            .map(([key, value]) => `<li class="phase-card__context-item"><strong>${this.escapeHtml(key)}:</strong> ${this.escapeHtml(String(value))}</li>`)
            .join('');

        return `
            <div class="phase-card__context">
                <span class="phase-card__context-label">Context:</span>
                <ul class="phase-card__context-list">${contextList}</ul>
            </div>
        `;
    }

    /**
     * Renders output artifacts
     * @private
     * @param {Object} artifacts
     * @returns {string}
     */
    _renderOutputArtifacts(artifacts) {
        const artifactList = Object.entries(artifacts)
            .map(([key, value]) => {
                const displayValue = typeof value === 'object'
                    ? JSON.stringify(value, null, 2)
                    : String(value);
                return `<li class="phase-card__artifact"><strong>${this.escapeHtml(key)}:</strong> <pre><code>${this.escapeHtml(displayValue)}</code></pre></li>`;
            })
            .join('');

        return `
            <div class="phase-card__artifacts">
                <span class="phase-card__artifacts-label">Output:</span>
                <ul class="phase-card__artifact-list">${artifactList}</ul>
            </div>
        `;
    }
}

/**
 * Renders tool.call events with pending state
 * Shows tool invocation with animated loading indicator
 * @extends EventRendererBase
 */
class ToolCallRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ToolCall,
            name: 'ToolCallRenderer',
            description: 'Renders tool call events with pending state animation',
            version: '1.0.0',
            priority: 10
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        /** @type {ToolCallPayload} */
        const payload = event.payload;
        const elementId = this.generateElementId(payload.callId, 'tool-call');

        const argsHtml = this._renderArguments(payload.arguments);

        const html = `
            <div class="tool-card tool-card--pending" id="${elementId}" data-call-id="${this.escapeHtml(payload.callId)}" data-tool="${this.escapeHtml(payload.toolName)}">
                <div class="tool-card__header">
                    <span class="tool-card__icon">🔧</span>
                    <span class="tool-card__name">${this.escapeHtml(payload.toolName)}</span>
                    <span class="tool-card__pending-indicator">
                        <span class="tool-card__spinner"></span>
                        <span class="tool-card__pending-text">Calling...</span>
                    </span>
                </div>
                <div class="tool-card__body">
                    ${argsHtml}
                </div>
                <div class="tool-card__footer">
                    <span class="tool-card__call-id">Call ID: ${this.escapeHtml(payload.callId.substring(0, 8))}...</span>
                    <time class="tool-card__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                </div>
            </div>
        `;

        return this.createRenderResult(html, {
            elementId,
            append: true
        });
    }

    /**
     * Renders tool arguments
     * @private
     * @param {Object} args
     * @returns {string}
     */
    _renderArguments(args) {
        const argsList = Object.entries(args)
            .map(([key, value]) => {
                const displayValue = typeof value === 'object'
                    ? JSON.stringify(value, null, 2)
                    : String(value);
                return `
                    <div class="tool-card__arg">
                        <span class="tool-card__arg-name">${this.escapeHtml(key)}:</span>
                        <code class="tool-card__arg-value">${this.escapeHtml(displayValue)}</code>
                    </div>
                `;
            })
            .join('');

        return `
            <div class="tool-card__args">
                <span class="tool-card__args-label">Arguments:</span>
                <div class="tool-card__args-list">${argsList}</div>
            </div>
        `;
    }
}

/**
 * Renders tool.result events
 * Updates existing tool call cards with results
 * @extends EventRendererBase
 */
class ToolResultRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ToolResult,
            name: 'ToolResultRenderer',
            description: 'Renders tool results and updates call cards',
            version: '1.0.0',
            priority: 10
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        /** @type {ToolResultPayload} */
        const payload = event.payload;
        const elementId = this.generateElementId(payload.callId, 'tool-call');

        // Find existing tool call element to update
        const existingElement = document.getElementById(elementId);
        if (existingElement) {
            this._updateExistingCard(existingElement, payload, event);
            return this.createRenderResult('', {
                elementId,
                append: false,
                targetSelector: `#${elementId}`
            });
        }

        // If no existing element, render a standalone result card
        return this._renderStandaloneResult(event, payload, elementId);
    }

    /**
     * Updates an existing tool call card with result
     * @private
     * @param {HTMLElement} element
     * @param {ToolResultPayload} payload
     * @param {StreamingEvent} event
     */
    _updateExistingCard(element, payload, event) {
        const header = element.querySelector('.tool-card__header');
        const body = element.querySelector('.tool-card__body');
        const footer = element.querySelector('.tool-card__footer');

        // Update header to show completion status
        if (header) {
            const statusIcon = payload.success ? '✅' : '❌';
            const statusText = payload.success ? 'Success' : 'Failed';
            const toolName = element.dataset.tool || 'Unknown Tool';

            header.innerHTML = `
                <span class="tool-card__icon">🔧</span>
                <span class="tool-card__name">${this.escapeHtml(toolName)}</span>
                <span class="tool-card__status tool-card__status--${payload.success ? 'success' : 'error'}">${statusIcon} ${statusText}</span>
            `;
        }

        // Add result to body
        if (body) {
            const resultHtml = this._renderResult(payload);
            body.insertAdjacentHTML('beforeend', resultHtml);
        }

        // Update footer with duration
        if (footer) {
            const durationText = `⏱️ ${payload.durationMs}ms`;
            const existingCallId = footer.querySelector('.tool-card__call-id');
            const existingTimestamp = footer.querySelector('.tool-card__timestamp');

            footer.innerHTML = `
                ${existingCallId ? existingCallId.outerHTML : ''}
                <span class="tool-card__duration">${durationText}</span>
                ${existingTimestamp ? existingTimestamp.outerHTML : ''}
            `;
        }

        // Update card styling
        element.classList.remove('tool-card--pending');
        element.classList.add(payload.success ? 'tool-card--success' : 'tool-card--error');
    }

    /**
     * Renders a standalone result card (when no matching call exists)
     * @private
     * @param {StreamingEvent} event
     * @param {ToolResultPayload} payload
     * @param {string} elementId
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    _renderStandaloneResult(event, payload, elementId) {
        const statusIcon = payload.success ? '✅' : '❌';
        const statusClass = payload.success ? 'tool-card--success' : 'tool-card--error';
        const statusText = payload.success ? 'Success' : 'Failed';

        const resultHtml = this._renderResult(payload);

        const html = `
            <div class="tool-card ${statusClass}" id="${elementId}" data-call-id="${this.escapeHtml(payload.callId)}">
                <div class="tool-card__header">
                    <span class="tool-card__icon">🔧</span>
                    <span class="tool-card__name">Tool Result</span>
                    <span class="tool-card__status tool-card__status--${payload.success ? 'success' : 'error'}">${statusIcon} ${statusText}</span>
                </div>
                <div class="tool-card__body">
                    ${resultHtml}
                </div>
                <div class="tool-card__footer">
                    <span class="tool-card__call-id">Call ID: ${this.escapeHtml(payload.callId.substring(0, 8))}...</span>
                    <span class="tool-card__duration">⏱️ ${payload.durationMs}ms</span>
                    <time class="tool-card__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                </div>
            </div>
        `;

        return this.createRenderResult(html, {
            elementId,
            append: true
        });
    }

    /**
     * Renders the result content
     * @private
     * @param {ToolResultPayload} payload
     * @returns {string}
     */
    _renderResult(payload) {
        const resultDisplay = typeof payload.result === 'object'
            ? JSON.stringify(payload.result, null, 2)
            : String(payload.result);

        return `
            <div class="tool-card__result">
                <span class="tool-card__result-label">Result:</span>
                <pre class="tool-card__result-content"><code>${this.escapeHtml(resultDisplay)}</code></pre>
            </div>
        `;
    }
}

/**
 * Renders tool.call.detected events (Task 8.3)
 * Shows when LLM requests tool calls
 * @extends EventRendererBase
 */
class ToolCallDetectedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ToolCallDetected,
            name: 'ToolCallDetectedRenderer',
            description: 'Renders tool call detection events',
            version: '1.0.0',
            priority: 10
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        const payload = event.payload;
        const elementId = this.generateElementId(event.id, 'tool-detected');

        const toolCallsHtml = payload.toolCalls.map(tc => `
            <div class="tool-detected__call">
                <span class="tool-detected__name">${this.escapeHtml(tc.toolName)}</span>
                <code class="tool-detected__args">${this.escapeHtml(tc.argumentsJson.substring(0, 100))}${tc.argumentsJson.length > 100 ? '...' : ''}</code>
            </div>
        `).join('');

        const html = `
            <div class="tool-detected-card" id="${elementId}" data-iteration="${payload.iteration}">
                <div class="tool-detected__header">
                    <span class="tool-detected__icon">🤖</span>
                    <span class="tool-detected__title">AI Requested ${payload.toolCalls.length} Tool Call(s)</span>
                    <span class="tool-detected__iteration">Iteration ${payload.iteration}</span>
                </div>
                <div class="tool-detected__body">
                    ${toolCallsHtml}
                </div>
                <div class="tool-detected__footer">
                    <time class="tool-detected__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                </div>
            </div>
        `;

        return this.createRenderResult(html, { elementId, append: true });
    }
}

/**
 * Renders tool.call.started events (Task 8.4)
 * Shows when individual tool execution begins
 * @extends EventRendererBase
 */
class ToolCallStartedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ToolCallStarted,
            name: 'ToolCallStartedRenderer',
            description: 'Renders tool call execution start events',
            version: '1.0.0',
            priority: 10
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        const payload = event.payload;
        const elementId = this.generateElementId(payload.toolCallId, 'tool-started');

        const html = `
            <div class="tool-started-card" id="${elementId}" data-call-id="${this.escapeHtml(payload.toolCallId)}" data-iteration="${payload.iteration}">
                <div class="tool-started__header">
                    <span class="tool-started__icon">🔧</span>
                    <span class="tool-started__name">${this.escapeHtml(payload.toolName)}</span>
                    <span class="tool-started__status">
                        <span class="tool-started__spinner"></span>
                        Executing...
                    </span>
                </div>
                <div class="tool-started__body">
                    <code class="tool-started__args">${this.escapeHtml(payload.argumentsJson.substring(0, 200))}${payload.argumentsJson.length > 200 ? '...' : ''}</code>
                </div>
                <div class="tool-started__footer">
                    <span class="tool-started__call-id">Call: ${this.escapeHtml(payload.toolCallId.substring(0, 8))}...</span>
                    <time class="tool-started__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                </div>
            </div>
        `;

        return this.createRenderResult(html, { elementId, append: true });
    }
}

/**
 * Renders tool.call.completed events (Task 8.5)
 * Shows successful tool execution
 * @extends EventRendererBase
 */
class ToolCallCompletedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ToolCallCompleted,
            name: 'ToolCallCompletedRenderer',
            description: 'Renders successful tool execution events',
            version: '1.0.0',
            priority: 10
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        const payload = event.payload;
        const elementId = this.generateElementId(payload.toolCallId, 'tool-completed');

        const resultHtml = payload.hasResult && payload.resultSummary
            ? `<div class="tool-completed__result">
                <span class="tool-completed__result-label">Result:</span>
                <pre class="tool-completed__result-content"><code>${this.escapeHtml(payload.resultSummary)}</code></pre>
               </div>`
            : '';

        const html = `
            <div class="tool-completed-card" id="${elementId}" data-call-id="${this.escapeHtml(payload.toolCallId)}">
                <div class="tool-completed__header">
                    <span class="tool-completed__icon">✅</span>
                    <span class="tool-completed__name">${this.escapeHtml(payload.toolName)}</span>
                    <span class="tool-completed__status">Completed</span>
                </div>
                <div class="tool-completed__body">
                    ${resultHtml}
                </div>
                <div class="tool-completed__footer">
                    <span class="tool-completed__duration">⏱️ ${payload.durationMs}ms</span>
                    <time class="tool-completed__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                </div>
            </div>
        `;

        return this.createRenderResult(html, { elementId, append: true });
    }
}

/**
 * Renders tool.call.failed events (Task 8.5)
 * Shows failed tool execution
 * @extends EventRendererBase
 */
class ToolCallFailedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ToolCallFailed,
            name: 'ToolCallFailedRenderer',
            description: 'Renders failed tool execution events',
            version: '1.0.0',
            priority: 10
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        const payload = event.payload;
        const elementId = this.generateElementId(payload.toolCallId, 'tool-failed');

        const html = `
            <div class="tool-failed-card" id="${elementId}" data-call-id="${this.escapeHtml(payload.toolCallId)}">
                <div class="tool-failed__header">
                    <span class="tool-failed__icon">❌</span>
                    <span class="tool-failed__name">${this.escapeHtml(payload.toolName)}</span>
                    <span class="tool-failed__status">Failed</span>
                </div>
                <div class="tool-failed__body">
                    <div class="tool-failed__error">
                        <span class="tool-failed__error-code">${this.escapeHtml(payload.errorCode)}</span>
                        <span class="tool-failed__error-message">${this.escapeHtml(payload.errorMessage)}</span>
                    </div>
                </div>
                <div class="tool-failed__footer">
                    <span class="tool-failed__duration">⏱️ ${payload.durationMs}ms</span>
                    <time class="tool-failed__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                </div>
            </div>
        `;

        return this.createRenderResult(html, { elementId, append: true });
    }
}

/**
 * Renders tool.results.submitted events
 * Shows when tool results are sent back to LLM
 * @extends EventRendererBase
 */
class ToolResultsSubmittedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ToolResultsSubmitted,
            name: 'ToolResultsSubmittedRenderer',
            description: 'Renders tool results submission events',
            version: '1.0.0',
            priority: 5
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        const payload = event.payload;
        const elementId = this.generateElementId(event.id, 'tool-results-submitted');

        const successCount = payload.results.filter(r => r.isSuccess).length;
        const failCount = payload.results.length - successCount;

        const html = `
            <div class="tool-results-submitted-card" id="${elementId}" data-iteration="${payload.iteration}">
                <div class="tool-results-submitted__header">
                    <span class="tool-results-submitted__icon">📤</span>
                    <span class="tool-results-submitted__title">Results Submitted to AI</span>
                </div>
                <div class="tool-results-submitted__body">
                    <span class="tool-results-submitted__count">${payload.resultCount} result(s): ${successCount} success, ${failCount} failed</span>
                </div>
                <div class="tool-results-submitted__footer">
                    <time class="tool-results-submitted__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                </div>
            </div>
        `;

        return this.createRenderResult(html, { elementId, append: true });
    }
}

/**
 * Renders tool.loop.iteration.completed events
 * Shows iteration progress
 * @extends EventRendererBase
 */
class ToolLoopIterationCompletedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ToolLoopIterationCompleted,
            name: 'ToolLoopIterationCompletedRenderer',
            description: 'Renders tool loop iteration completion events',
            version: '1.0.0',
            priority: 5
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        const payload = event.payload;
        const elementId = this.generateElementId(event.id, 'tool-loop-iteration');

        const moreCallsHtml = payload.hasMoreToolCalls
            ? '<span class="tool-loop-iteration__more">🔄 More tool calls requested</span>'
            : '<span class="tool-loop-iteration__complete">✓ Iteration complete</span>';

        const html = `
            <div class="tool-loop-iteration-card" id="${elementId}" data-iteration="${payload.iteration}">
                <div class="tool-loop-iteration__header">
                    <span class="tool-loop-iteration__icon">🔄</span>
                    <span class="tool-loop-iteration__title">Iteration ${payload.iteration} Complete</span>
                </div>
                <div class="tool-loop-iteration__body">
                    <span class="tool-loop-iteration__calls">${payload.toolCallCount} tool call(s) executed</span>
                    ${moreCallsHtml}
                </div>
                <div class="tool-loop-iteration__footer">
                    <span class="tool-loop-iteration__duration">⏱️ ${payload.durationMs}ms</span>
                    <time class="tool-loop-iteration__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                </div>
            </div>
        `;

        return this.createRenderResult(html, { elementId, append: true });
    }
}

/**
 * Renders tool.loop.completed events
 * Shows final loop completion
 * @extends EventRendererBase
 */
class ToolLoopCompletedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ToolLoopCompleted,
            name: 'ToolLoopCompletedRenderer',
            description: 'Renders tool loop completion events',
            version: '1.0.0',
            priority: 5
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        const payload = event.payload;
        const elementId = this.generateElementId(event.id, 'tool-loop-completed');

        const html = `
            <div class="tool-loop-completed-card" id="${elementId}">
                <div class="tool-loop-completed__header">
                    <span class="tool-loop-completed__icon">🏁</span>
                    <span class="tool-loop-completed__title">Tool Calling Complete</span>
                </div>
                <div class="tool-loop-completed__body">
                    <div class="tool-loop-completed__stats">
                        <span class="tool-loop-completed__stat">${payload.totalIterations} iteration(s)</span>
                        <span class="tool-loop-completed__stat">${payload.totalToolCalls} tool call(s)</span>
                        <span class="tool-loop-completed__reason">${this.escapeHtml(payload.completionReason)}</span>
                    </div>
                </div>
                <div class="tool-loop-completed__footer">
                    <span class="tool-loop-completed__duration">⏱️ ${payload.totalDurationMs}ms total</span>
                    <time class="tool-loop-completed__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                </div>
            </div>
        `;

        return this.createRenderResult(html, { elementId, append: true });
    }
}

/**
 * Renders tool.loop.failed events
 * Shows loop failure
 * @extends EventRendererBase
 */
class ToolLoopFailedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ToolLoopFailed,
            name: 'ToolLoopFailedRenderer',
            description: 'Renders tool loop failure events',
            version: '1.0.0',
            priority: 10
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        const payload = event.payload;
        const elementId = this.generateElementId(event.id, 'tool-loop-failed');

        const html = `
            <div class="tool-loop-failed-card" id="${elementId}" data-iteration="${payload.iteration}">
                <div class="tool-loop-failed__header">
                    <span class="tool-loop-failed__icon">⚠️</span>
                    <span class="tool-loop-failed__title">Tool Calling Failed</span>
                </div>
                <div class="tool-loop-failed__body">
                    <div class="tool-loop-failed__error">
                        <span class="tool-loop-failed__error-code">${this.escapeHtml(payload.errorCode)}</span>
                        <span class="tool-loop-failed__error-message">${this.escapeHtml(payload.errorMessage)}</span>
                    </div>
                    <span class="tool-loop-failed__iteration">Failed at iteration ${payload.iteration}</span>
                </div>
                <div class="tool-loop-failed__footer">
                    <time class="tool-loop-failed__timestamp">${this.formatTimestamp(event.timestamp)}</time>
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
 * Renders ui.navigation events
 * Handles navigation to pages and entities in the detail panel
 * @extends EventRendererBase
 */
class UiNavigationRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.UiNavigation,
            name: 'UiNavigationRenderer',
            description: 'Renders UI navigation events for detail panel updates',
            version: '1.0.0',
            priority: 10
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        /** @type {import('./event-types.js').UiNavigationPayload} */
        const payload = event.payload;
        const elementId = this.generateElementId(event.id, 'nav');

        const actionLabel = this._getActionLabel(payload.action);
        const targetLabel = payload.title || payload.pageUrl || `${payload.entityType} ${payload.entityId}`;

        const html = `
            <div class="nav-card" id="${elementId}" data-action="${this.escapeHtml(payload.action)}">
                <div class="nav-card__header">
                    <span class="nav-card__icon">🧭</span>
                    <span class="nav-card__action">${this.escapeHtml(actionLabel)}</span>
                </div>
                <div class="nav-card__body">
                    <span class="nav-card__target">${this.escapeHtml(targetLabel)}</span>
                </div>
                <div class="nav-card__footer">
                    <time class="nav-card__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                </div>
            </div>
        `;

        // Trigger the actual navigation
        this._performNavigation(payload);

        return this.createRenderResult(html, {
            elementId,
            append: true
        });
    }

    /**
     * Gets a human-readable label for the action
     * @private
     * @param {string} action
     * @returns {string}
     */
    _getActionLabel(action) {
        const labels = {
            'showEntity': 'Opening Entity',
            'showPage': 'Opening Page',
            'navigate': 'Navigating'
        };
        return labels[action] || 'Navigating';
    }

    /**
     * Performs the actual navigation based on the payload
     * @private
     * @param {import('./event-types.js').UiNavigationPayload} payload
     */
    _performNavigation(payload) {
        if (typeof window === 'undefined' || !window.DetailPanel) return;

        switch (payload.action) {
            case 'showEntity':
                if (payload.entityType && payload.entityId) {
                    window.DetailPanel.showEntity(payload.entityType, payload.entityId);
                }
                break;
            case 'showPage':
                if (payload.pageUrl) {
                    window.DetailPanel.showPage(payload.pageUrl, payload.title);
                }
                break;
            case 'navigate':
                if (payload.pageUrl && !payload.useDetailPanel) {
                    window.location.href = payload.pageUrl;
                } else if (payload.pageUrl) {
                    window.DetailPanel.showPage(payload.pageUrl, payload.title);
                }
                break;
        }
    }
}

/**
 * Creates all operation event renderers
 * @returns {Array<{eventType: string, renderer: IEventRenderer, priority: number}>}
 */
function createOperationEventRenderers() {
    return [
        {
            eventType: StreamingEventType.RunLifecycle,
            renderer: new RunLifecycleRenderer(),
            priority: 10
        },
        {
            eventType: StreamingEventType.PhaseLifecycle,
            renderer: new PhaseLifecycleRenderer(),
            priority: 10
        },
        {
            eventType: StreamingEventType.ToolCall,
            renderer: new ToolCallRenderer(),
            priority: 10
        },
        {
            eventType: StreamingEventType.ToolResult,
            renderer: new ToolResultRenderer(),
            priority: 10
        },
        {
            eventType: StreamingEventType.ToolCallDetected,
            renderer: new ToolCallDetectedRenderer(),
            priority: 10
        },
        {
            eventType: StreamingEventType.ToolCallStarted,
            renderer: new ToolCallStartedRenderer(),
            priority: 10
        },
        {
            eventType: StreamingEventType.ToolCallCompleted,
            renderer: new ToolCallCompletedRenderer(),
            priority: 10
        },
        {
            eventType: StreamingEventType.ToolCallFailed,
            renderer: new ToolCallFailedRenderer(),
            priority: 10
        },
        {
            eventType: StreamingEventType.ToolResultsSubmitted,
            renderer: new ToolResultsSubmittedRenderer(),
            priority: 5
        },
        {
            eventType: StreamingEventType.ToolLoopIterationCompleted,
            renderer: new ToolLoopIterationCompletedRenderer(),
            priority: 5
        },
        {
            eventType: StreamingEventType.ToolLoopCompleted,
            renderer: new ToolLoopCompletedRenderer(),
            priority: 5
        },
        {
            eventType: StreamingEventType.ToolLoopFailed,
            renderer: new ToolLoopFailedRenderer(),
            priority: 10
        },
        {
            eventType: StreamingEventType.UiNavigation,
            renderer: new UiNavigationRenderer(),
            priority: 10
        }
    ];
}

/**
 * Initializes operation event card behaviors
 * Should be called after DOM updates
 * @param {HTMLElement} [container] - Container to search for cards (defaults to document)
 */
function initializeOperationCards(container = document) {
    // Initialize any interactive elements on operation cards
    // Currently a placeholder for future interactivity

    // Example: Add click-to-expand for result details
    const resultContents = container.querySelectorAll('.tool-card__result-content');
    resultContents.forEach(content => {
        content.addEventListener('click', () => {
            content.classList.toggle('tool-card__result-content--expanded');
        });
    });
}

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        RunLifecycleRenderer,
        PhaseLifecycleRenderer,
        ToolCallRenderer,
        ToolResultRenderer,
        UiNavigationRenderer,
        createOperationEventRenderers,
        initializeOperationCards
    };
}
