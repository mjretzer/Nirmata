/**
 * @fileoverview Confirmation Event Renderers
 * @description Renderers for confirmation lifecycle events (requested, accepted, rejected, timeout)
 * Provides visual confirmation prompts with action details and accept/reject controls
 */

/**
 * @typedef {import('./ievent-renderer.js').EventRendererBase} EventRendererBase
 * @typedef {import('./event-types.js').StreamingEvent} StreamingEvent
 * @typedef {import('./event-types.js').ConfirmationRequestedPayload} ConfirmationRequestedPayload
 * @typedef {import('./event-types.js').ConfirmationAcceptedPayload} ConfirmationAcceptedPayload
 * @typedef {import('./event-types.js').ConfirmationRejectedPayload} ConfirmationRejectedPayload
 * @typedef {import('./event-types.js').ConfirmationTimeoutPayload} ConfirmationTimeoutPayload
 */

/**
 * Maps risk levels to display styles and icons
 * @param {string} riskLevel
 * @returns {{icon: string, class: string, label: string}}
 */
function getRiskLevelDisplay(riskLevel) {
    const displays = {
        'Read': { icon: '👁️', class: 'risk--read', label: 'Read Only' },
        'WriteSafe': { icon: '✏️', class: 'risk--write-safe', label: 'Safe Write' },
        'WriteDestructive': { icon: '⚠️', class: 'risk--destructive', label: 'Destructive' },
        'WriteDestructiveGit': { icon: '📦', class: 'risk--destructive-git', label: 'Git Operation' },
        'WorkspaceDestructive': { icon: '🔥', class: 'risk--workspace-destructive', label: 'Workspace Risk' }
    };
    return displays[riskLevel] || { icon: '❓', class: 'risk--unknown', label: riskLevel || 'Unknown' };
}

/**
 * Renders confirmation.requested events
 * Shows a confirmation prompt with action details and accept/reject controls
 * @extends EventRendererBase
 */
class ConfirmationRequestedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ConfirmationRequested,
            name: 'ConfirmationRequestedRenderer',
            description: 'Renders confirmation prompts with action details and controls',
            version: '1.0.0',
            priority: 100
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        /** @type {ConfirmationRequestedPayload} */
        const payload = event.payload;
        const elementId = this.generateElementId(payload.confirmationId, 'confirmation');
        const riskDisplay = getRiskLevelDisplay(payload.riskLevel);

        const affectedResourcesHtml = this._renderAffectedResources(payload.action.affectedResources);
        const timeoutHtml = this._renderTimeout(payload.timeout);
        const metadataHtml = this._renderMetadata(payload.action.metadata);

        const html = `
            <div class="confirmation-card confirmation-card--pending" id="${elementId}" data-confirmation-id="${this.escapeHtml(payload.confirmationId)}" data-status="pending">
                <div class="confirmation-card__header">
                    <span class="confirmation-card__icon">🛡️</span>
                    <span class="confirmation-card__title">Confirmation Required</span>
                    <span class="confirmation-card__risk-badge ${riskDisplay.class}">${riskDisplay.icon} ${riskDisplay.label}</span>
                </div>
                <div class="confirmation-card__body">
                    <div class="confirmation-card__action">
                        <div class="confirmation-card__phase">
                            <span class="confirmation-card__phase-label">Phase:</span>
                            <span class="confirmation-card__phase-value">${this.escapeHtml(payload.action.phase)}</span>
                        </div>
                        <div class="confirmation-card__description">
                            <span class="confirmation-card__description-label">Action:</span>
                            <p class="confirmation-card__description-text">${this.escapeHtml(payload.action.description)}</p>
                        </div>
                        ${affectedResourcesHtml}
                        ${metadataHtml}
                    </div>
                    <div class="confirmation-card__reason">
                        <span class="confirmation-card__reason-label">Why confirmation is needed:</span>
                        <p class="confirmation-card__reason-text">${this.escapeHtml(payload.reason)}</p>
                    </div>
                    <div class="confirmation-card__details">
                        <span class="confirmation-card__confidence">Confidence: ${(payload.confidence * 100).toFixed(1)}%</span>
                        ${payload.threshold ? `<span class="confirmation-card__threshold">Threshold: ${(payload.threshold * 100).toFixed(1)}%</span>` : ''}
                    </div>
                    ${timeoutHtml}
                </div>
                <div class="confirmation-card__controls">
                    <button type="button" class="confirmation-card__btn confirmation-card__btn--accept" data-action="accept" data-confirmation-id="${this.escapeHtml(payload.confirmationId)}">
                        <span class="confirmation-card__btn-icon">✓</span>
                        Accept
                    </button>
                    <button type="button" class="confirmation-card__btn confirmation-card__btn--reject" data-action="reject" data-confirmation-id="${this.escapeHtml(payload.confirmationId)}">
                        <span class="confirmation-card__btn-icon">✕</span>
                        Reject
                    </button>
                </div>
                <div class="confirmation-card__footer">
                    <time class="confirmation-card__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                    <span class="confirmation-card__id">ID: ${this.escapeHtml(payload.confirmationId.substring(0, 8))}...</span>
                </div>
            </div>
        `;

        return this.createRenderResult(html, {
            elementId,
            append: true
        });
    }

    /**
     * Renders affected resources list
     * @private
     * @param {string[]} resources
     * @returns {string}
     */
    _renderAffectedResources(resources) {
        if (!resources || resources.length === 0) {
            return '';
        }

        const resourceList = resources
            .map(r => `<li class="confirmation-card__resource-item">${this.escapeHtml(r)}</li>`)
            .join('');

        return `
            <div class="confirmation-card__resources">
                <span class="confirmation-card__resources-label">📁 Affected Resources:</span>
                <ul class="confirmation-card__resources-list">${resourceList}</ul>
            </div>
        `;
    }

    /**
     * Renders timeout indicator
     * @private
     * @param {string} [timeout]
     * @returns {string}
     */
    _renderTimeout(timeout) {
        if (!timeout) {
            return '<div class="confirmation-card__timeout confirmation-card__timeout--none">No timeout (waits indefinitely)</div>';
        }

        // Parse ISO 8601 duration
        const match = timeout.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?/);
        if (!match) {
            return `<div class="confirmation-card__timeout">Timeout: ${this.escapeHtml(timeout)}</div>`;
        }

        const hours = parseInt(match[1] || '0', 10);
        const minutes = parseInt(match[2] || '0', 10);
        const seconds = parseInt(match[3] || '0', 10);

        const parts = [];
        if (hours > 0) parts.push(`${hours}h`);
        if (minutes > 0) parts.push(`${minutes}m`);
        if (seconds > 0 || parts.length === 0) parts.push(`${seconds}s`);

        return `
            <div class="confirmation-card__timeout confirmation-card__timeout--limited">
                <span class="confirmation-card__timeout-icon">⏱️</span>
                <span class="confirmation-card__timeout-label">Timeout:</span>
                <span class="confirmation-card__timeout-value">${parts.join(' ')}</span>
                <div class="confirmation-card__timeout-bar">
                    <div class="confirmation-card__timeout-progress" data-timeout="${timeout}"></div>
                </div>
            </div>
        `;
    }

    /**
     * Renders action metadata
     * @private
     * @param {Object} [metadata]
     * @returns {string}
     */
    _renderMetadata(metadata) {
        if (!metadata || Object.keys(metadata).length === 0) {
            return '';
        }

        const metadataList = Object.entries(metadata)
            .map(([key, value]) => {
                const displayValue = typeof value === 'object' ? JSON.stringify(value) : String(value);
                return `<li class="confirmation-card__metadata-item"><strong>${this.escapeHtml(key)}:</strong> ${this.escapeHtml(displayValue)}</li>`;
            })
            .join('');

        return `
            <div class="confirmation-card__metadata">
                <span class="confirmation-card__metadata-label">Additional Info:</span>
                <ul class="confirmation-card__metadata-list">${metadataList}</ul>
            </div>
        `;
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {HTMLElement} element
     * @param {Object} [context]
     * @returns {boolean}
     */
    update(event, element, context) {
        // This renderer doesn't update - it waits for user interaction
        // Updates are handled by the accepted/rejected renderers or timeout handler
        return false;
    }
}

/**
 * Renders confirmation.accepted events
 * Updates pending confirmation cards to show acceptance
 * @extends EventRendererBase
 */
class ConfirmationAcceptedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ConfirmationAccepted,
            name: 'ConfirmationAcceptedRenderer',
            description: 'Renders confirmation acceptance status',
            version: '1.0.0',
            priority: 100
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        /** @type {ConfirmationAcceptedPayload} */
        const payload = event.payload;
        const pendingId = this.generateElementId(payload.confirmationId, 'confirmation');
        const pendingElement = document.getElementById(pendingId);

        // If there's a pending card, update it
        if (pendingElement) {
            this._updatePendingCard(pendingElement, payload, event);
            return this.createRenderResult('', {
                elementId: pendingId,
                append: false,
                targetSelector: `#${pendingId}`
            });
        }

        // Otherwise render a standalone accepted card
        return this._renderStandaloneAccepted(event, payload);
    }

    /**
     * Updates a pending confirmation card to show accepted status
     * @private
     * @param {HTMLElement} element
     * @param {ConfirmationAcceptedPayload} payload
     * @param {StreamingEvent} event
     */
    _updatePendingCard(element, payload, event) {
        // Remove controls
        const controls = element.querySelector('.confirmation-card__controls');
        if (controls) {
            controls.remove();
        }

        // Update header
        const header = element.querySelector('.confirmation-card__header');
        if (header) {
            header.innerHTML = `
                <span class="confirmation-card__icon">✅</span>
                <span class="confirmation-card__title">Confirmation Accepted</span>
                <span class="confirmation-card__status-badge status--accepted">Accepted</span>
            `;
        }

        // Add accepted timestamp
        const body = element.querySelector('.confirmation-card__body');
        if (body) {
            const acceptedInfo = document.createElement('div');
            acceptedInfo.className = 'confirmation-card__accepted-info';
            acceptedInfo.innerHTML = `<span class="confirmation-card__accepted-at">Accepted at: ${this.formatTimestamp(payload.acceptedAt)}</span>`;
            body.appendChild(acceptedInfo);
        }

        // Update styling
        element.classList.remove('confirmation-card--pending');
        element.classList.add('confirmation-card--accepted');
        element.dataset.status = 'accepted';

        // Stop timeout animation if any
        const timeoutProgress = element.querySelector('.confirmation-card__timeout-progress');
        if (timeoutProgress) {
            timeoutProgress.style.animationPlayState = 'paused';
        }
    }

    /**
     * Renders a standalone accepted confirmation card
     * @private
     * @param {StreamingEvent} event
     * @param {ConfirmationAcceptedPayload} payload
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    _renderStandaloneAccepted(event, payload) {
        const elementId = this.generateElementId(event.id, 'confirmation-accepted');

        const html = `
            <div class="confirmation-card confirmation-card--accepted" id="${elementId}" data-confirmation-id="${this.escapeHtml(payload.confirmationId)}" data-status="accepted">
                <div class="confirmation-card__header">
                    <span class="confirmation-card__icon">✅</span>
                    <span class="confirmation-card__title">Confirmation Accepted</span>
                    <span class="confirmation-card__status-badge status--accepted">Accepted</span>
                </div>
                <div class="confirmation-card__body">
                    <p class="confirmation-card__message">Confirmation ${this.escapeHtml(payload.confirmationId.substring(0, 8))}... was accepted.</p>
                    <span class="confirmation-card__accepted-at">Accepted at: ${this.formatTimestamp(payload.acceptedAt)}</span>
                </div>
                <div class="confirmation-card__footer">
                    <time class="confirmation-card__timestamp">${this.formatTimestamp(event.timestamp)}</time>
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
 * Renders confirmation.rejected events
 * Updates pending confirmation cards to show rejection
 * @extends EventRendererBase
 */
class ConfirmationRejectedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ConfirmationRejected,
            name: 'ConfirmationRejectedRenderer',
            description: 'Renders confirmation rejection status with optional user message',
            version: '1.0.0',
            priority: 100
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        /** @type {ConfirmationRejectedPayload} */
        const payload = event.payload;
        const pendingId = this.generateElementId(payload.confirmationId, 'confirmation');
        const pendingElement = document.getElementById(pendingId);

        // If there's a pending card, update it
        if (pendingElement) {
            this._updatePendingCard(pendingElement, payload, event);
            return this.createRenderResult('', {
                elementId: pendingId,
                append: false,
                targetSelector: `#${pendingId}`
            });
        }

        // Otherwise render a standalone rejected card
        return this._renderStandaloneRejected(event, payload);
    }

    /**
     * Updates a pending confirmation card to show rejected status
     * @private
     * @param {HTMLElement} element
     * @param {ConfirmationRejectedPayload} payload
     * @param {StreamingEvent} event
     */
    _updatePendingCard(element, payload, event) {
        // Remove controls
        const controls = element.querySelector('.confirmation-card__controls');
        if (controls) {
            controls.remove();
        }

        // Update header
        const header = element.querySelector('.confirmation-card__header');
        if (header) {
            header.innerHTML = `
                <span class="confirmation-card__icon">❌</span>
                <span class="confirmation-card__title">Confirmation Rejected</span>
                <span class="confirmation-card__status-badge status--rejected">Rejected</span>
            `;
        }

        // Add rejection info
        const body = element.querySelector('.confirmation-card__body');
        if (body) {
            const rejectedInfo = document.createElement('div');
            rejectedInfo.className = 'confirmation-card__rejected-info';

            let messageHtml = `<span class="confirmation-card__rejected-at">Rejected at: ${this.formatTimestamp(payload.rejectedAt)}</span>`;
            if (payload.userMessage) {
                messageHtml += `<div class="confirmation-card__user-message"><span class="confirmation-card__user-message-label">User message:</span><p class="confirmation-card__user-message-text">${this.escapeHtml(payload.userMessage)}</p></div>`;
            }

            rejectedInfo.innerHTML = messageHtml;
            body.appendChild(rejectedInfo);
        }

        // Update styling
        element.classList.remove('confirmation-card--pending');
        element.classList.add('confirmation-card--rejected');
        element.dataset.status = 'rejected';

        // Stop timeout animation if any
        const timeoutProgress = element.querySelector('.confirmation-card__timeout-progress');
        if (timeoutProgress) {
            timeoutProgress.style.animationPlayState = 'paused';
        }
    }

    /**
     * Renders a standalone rejected confirmation card
     * @private
     * @param {StreamingEvent} event
     * @param {ConfirmationRejectedPayload} payload
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    _renderStandaloneRejected(event, payload) {
        const elementId = this.generateElementId(event.id, 'confirmation-rejected');

        const userMessageHtml = payload.userMessage
            ? `<div class="confirmation-card__user-message"><span class="confirmation-card__user-message-label">User message:</span><p class="confirmation-card__user-message-text">${this.escapeHtml(payload.userMessage)}</p></div>`
            : '';

        const html = `
            <div class="confirmation-card confirmation-card--rejected" id="${elementId}" data-confirmation-id="${this.escapeHtml(payload.confirmationId)}" data-status="rejected">
                <div class="confirmation-card__header">
                    <span class="confirmation-card__icon">❌</span>
                    <span class="confirmation-card__title">Confirmation Rejected</span>
                    <span class="confirmation-card__status-badge status--rejected">Rejected</span>
                </div>
                <div class="confirmation-card__body">
                    <p class="confirmation-card__message">Confirmation ${this.escapeHtml(payload.confirmationId.substring(0, 8))}... was rejected.</p>
                    <span class="confirmation-card__rejected-at">Rejected at: ${this.formatTimestamp(payload.rejectedAt)}</span>
                    ${userMessageHtml}
                </div>
                <div class="confirmation-card__footer">
                    <time class="confirmation-card__timestamp">${this.formatTimestamp(event.timestamp)}</time>
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
 * Renders confirmation.timeout events
 * Updates pending confirmation cards to show timeout
 * @extends EventRendererBase
 */
class ConfirmationTimeoutRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ConfirmationTimeout,
            name: 'ConfirmationTimeoutRenderer',
            description: 'Renders confirmation timeout status',
            version: '1.0.0',
            priority: 100
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        /** @type {ConfirmationTimeoutPayload} */
        const payload = event.payload;
        const pendingId = this.generateElementId(payload.confirmationId, 'confirmation');
        const pendingElement = document.getElementById(pendingId);

        // If there's a pending card, update it
        if (pendingElement) {
            this._updatePendingCard(pendingElement, payload, event);
            return this.createRenderResult('', {
                elementId: pendingId,
                append: false,
                targetSelector: `#${pendingId}`
            });
        }

        // Otherwise render a standalone timeout card
        return this._renderStandaloneTimeout(event, payload);
    }

    /**
     * Updates a pending confirmation card to show timeout status
     * @private
     * @param {HTMLElement} element
     * @param {ConfirmationTimeoutPayload} payload
     * @param {StreamingEvent} event
     */
    _updatePendingCard(element, payload, event) {
        // Remove controls
        const controls = element.querySelector('.confirmation-card__controls');
        if (controls) {
            controls.remove();
        }

        // Update header
        const header = element.querySelector('.confirmation-card__header');
        if (header) {
            header.innerHTML = `
                <span class="confirmation-card__icon">⏱️</span>
                <span class="confirmation-card__title">Confirmation Timed Out</span>
                <span class="confirmation-card__status-badge status--timeout">Timeout</span>
            `;
        }

        // Add timeout info
        const body = element.querySelector('.confirmation-card__body');
        if (body) {
            const timeoutInfo = document.createElement('div');
            timeoutInfo.className = 'confirmation-card__timeout-info';

            let messageHtml = `<span class="confirmation-card__timeout-message">${this.escapeHtml(payload.message)}</span>`;
            if (payload.cancellationReason && payload.cancellationReason !== 'timeout') {
                messageHtml += `<span class="confirmation-card__cancellation-reason">Reason: ${this.escapeHtml(payload.cancellationReason)}</span>`;
            }

            timeoutInfo.innerHTML = messageHtml;
            body.appendChild(timeoutInfo);
        }

        // Update styling
        element.classList.remove('confirmation-card--pending');
        element.classList.add('confirmation-card--timeout');
        element.dataset.status = 'timeout';

        // Stop timeout animation
        const timeoutProgress = element.querySelector('.confirmation-card__timeout-progress');
        if (timeoutProgress) {
            timeoutProgress.style.animationPlayState = 'paused';
        }
    }

    /**
     * Renders a standalone timeout confirmation card
     * @private
     * @param {StreamingEvent} event
     * @param {ConfirmationTimeoutPayload} payload
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    _renderStandaloneTimeout(event, payload) {
        const elementId = this.generateElementId(event.id, 'confirmation-timeout');

        const reasonHtml = payload.cancellationReason && payload.cancellationReason !== 'timeout'
            ? `<span class="confirmation-card__cancellation-reason">Reason: ${this.escapeHtml(payload.cancellationReason)}</span>`
            : '';

        const html = `
            <div class="confirmation-card confirmation-card--timeout" id="${elementId}" data-confirmation-id="${this.escapeHtml(payload.confirmationId)}" data-status="timeout">
                <div class="confirmation-card__header">
                    <span class="confirmation-card__icon">⏱️</span>
                    <span class="confirmation-card__title">Confirmation Timed Out</span>
                    <span class="confirmation-card__status-badge status--timeout">Timeout</span>
                </div>
                <div class="confirmation-card__body">
                    <p class="confirmation-card__message">Confirmation ${this.escapeHtml(payload.confirmationId.substring(0, 8))}... timed out after ${this.escapeHtml(payload.timeout)}.</p>
                    <span class="confirmation-card__timeout-message">${this.escapeHtml(payload.message)}</span>
                    ${reasonHtml}
                    <span class="confirmation-card__requested-at">Originally requested: ${this.formatTimestamp(payload.requestedAt)}</span>
                </div>
                <div class="confirmation-card__footer">
                    <time class="confirmation-card__timestamp">${this.formatTimestamp(event.timestamp)}</time>
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
 * Creates all confirmation event renderers
 * @returns {Array<{eventType: string, renderer: IEventRenderer, priority: number}>}
 */
function createConfirmationEventRenderers() {
    return [
        {
            eventType: StreamingEventType.ConfirmationRequested,
            renderer: new ConfirmationRequestedRenderer(),
            priority: 100
        },
        {
            eventType: StreamingEventType.ConfirmationAccepted,
            renderer: new ConfirmationAcceptedRenderer(),
            priority: 100
        },
        {
            eventType: StreamingEventType.ConfirmationRejected,
            renderer: new ConfirmationRejectedRenderer(),
            priority: 100
        },
        {
            eventType: StreamingEventType.ConfirmationTimeout,
            renderer: new ConfirmationTimeoutRenderer(),
            priority: 100
        }
    ];
}

/**
 * Initializes confirmation card behaviors including button handlers
 * Should be called after DOM updates
 * @param {HTMLElement} [container] - Container to search for cards (defaults to document)
 */
function initializeConfirmationCards(container = document) {
    // Handle accept button clicks
    container.querySelectorAll('.confirmation-card__btn--accept').forEach(btn => {
        if (!btn.dataset.initialized) {
            btn.dataset.initialized = 'true';
            btn.addEventListener('click', async (e) => {
                e.preventDefault();
                const confirmationId = btn.dataset.confirmationId;
                if (!confirmationId) return;

                btn.disabled = true;
                btn.innerHTML = '<span class="confirmation-card__btn-icon">⏳</span> Processing...';

                try {
                    // Emit accept event via SSE or API
                    await emitConfirmationResponse(confirmationId, true);
                } catch (error) {
                    console.error('Failed to accept confirmation:', error);
                    btn.disabled = false;
                    btn.innerHTML = '<span class="confirmation-card__btn-icon">✓</span> Accept';
                    alert('Failed to accept confirmation. Please try again.');
                }
            });
        }
    });

    // Handle reject button clicks
    container.querySelectorAll('.confirmation-card__btn--reject').forEach(btn => {
        if (!btn.dataset.initialized) {
            btn.dataset.initialized = 'true';
            btn.addEventListener('click', async (e) => {
                e.preventDefault();
                const confirmationId = btn.dataset.confirmationId;
                if (!confirmationId) return;

                // Prompt for rejection reason
                const userMessage = prompt('Why are you rejecting this action? (optional)');
                if (userMessage === null) return; // User cancelled

                btn.disabled = true;
                btn.innerHTML = '<span class="confirmation-card__btn-icon">⏳</span> Processing...';

                try {
                    // Emit reject event via SSE or API
                    await emitConfirmationResponse(confirmationId, false, userMessage);
                } catch (error) {
                    console.error('Failed to reject confirmation:', error);
                    btn.disabled = false;
                    btn.innerHTML = '<span class="confirmation-card__btn-icon">✕</span> Reject';
                    alert('Failed to reject confirmation. Please try again.');
                }
            });
        }
    });

    // Initialize timeout progress bars
    container.querySelectorAll('.confirmation-card__timeout-progress').forEach(progress => {
        const timeout = progress.dataset.timeout;
        if (timeout && !progress.dataset.initialized) {
            progress.dataset.initialized = 'true';

            // Parse duration and set CSS animation
            const match = timeout.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?/);
            if (match) {
                const hours = parseInt(match[1] || '0', 10);
                const minutes = parseInt(match[2] || '0', 10);
                const seconds = parseInt(match[3] || '0', 10);
                const totalSeconds = hours * 3600 + minutes * 60 + seconds;

                if (totalSeconds > 0) {
                    progress.style.animationDuration = `${totalSeconds}s`;
                    progress.classList.add('confirmation-card__timeout-progress--animating');
                }
            }
        }
    });
}

/**
 * Emits a confirmation response to the server
 * @param {string} confirmationId
 * @param {boolean} accepted
 * @param {string} [userMessage]
 * @returns {Promise<void>}
 */
async function emitConfirmationResponse(confirmationId, accepted, userMessage = null) {
    // This would typically call an API endpoint or emit via SSE
    // Implementation depends on the specific server setup
    const response = await fetch('/api/confirmation/respond', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            confirmationId,
            accepted,
            userMessage,
            respondedAt: new Date().toISOString()
        })
    });

    if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
    }
}

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        ConfirmationRequestedRenderer,
        ConfirmationAcceptedRenderer,
        ConfirmationRejectedRenderer,
        ConfirmationTimeoutRenderer,
        createConfirmationEventRenderers,
        initializeConfirmationCards,
        emitConfirmationResponse,
        getRiskLevelDisplay
    };
}
