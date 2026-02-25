/**
 * @fileoverview Command Proposal Event Renderers
 * @description Renderers for command suggestion events
 * Displays AI-suggested commands with accept/reject controls
 */

/**
 * @typedef {import('./ievent-renderer.js').EventRendererBase} EventRendererBase
 * @typedef {import('./event-types.js').StreamingEvent} StreamingEvent
 */

/**
 * Renders command.suggested events
 * Shows a command proposal card with accept/reject controls
 * @extends EventRendererBase
 */
class CommandProposalRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: 'command.suggested',
            name: 'CommandProposalRenderer',
            description: 'Renders command proposals with accept/reject controls',
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
        const payload = event.payload;
        const elementId = this.generateElementId(payload.confirmationRequestId, 'command-proposal');

        const html = `
            <div class="command-proposal-card command-proposal-card--pending" id="${elementId}" data-confirmation-id="${this.escapeHtml(payload.confirmationRequestId)}" data-status="pending">
                <div class="command-proposal-card__header">
                    <span class="command-proposal-card__icon">💡</span>
                    <span class="command-proposal-card__title">Suggested Command</span>
                    <span class="command-proposal-card__confidence">Confidence: ${(payload.confidence * 100).toFixed(0)}%</span>
                </div>
                <div class="command-proposal-card__body">
                    <div class="command-proposal-card__command">
                        <span class="command-proposal-card__command-label">Command:</span>
                        <code class="command-proposal-card__command-text">${this.escapeHtml(payload.formattedCommand)}</code>
                    </div>
                    <div class="command-proposal-card__reasoning">
                        <span class="command-proposal-card__reasoning-label">Why:</span>
                        <p class="command-proposal-card__reasoning-text">${this.escapeHtml(payload.reasoning)}</p>
                    </div>
                    ${payload.expectedOutcome ? `
                    <div class="command-proposal-card__outcome">
                        <span class="command-proposal-card__outcome-label">Expected Outcome:</span>
                        <p class="command-proposal-card__outcome-text">${this.escapeHtml(payload.expectedOutcome)}</p>
                    </div>
                    ` : ''}
                </div>
                <div class="command-proposal-card__controls">
                    <button type="button" class="command-proposal-card__btn command-proposal-card__btn--accept" data-action="accept" data-confirmation-id="${this.escapeHtml(payload.confirmationRequestId)}">
                        <span class="command-proposal-card__btn-icon">✓</span>
                        Accept
                    </button>
                    <button type="button" class="command-proposal-card__btn command-proposal-card__btn--reject" data-action="reject" data-confirmation-id="${this.escapeHtml(payload.confirmationRequestId)}">
                        <span class="command-proposal-card__btn-icon">✕</span>
                        Reject
                    </button>
                </div>
                <div class="command-proposal-card__footer">
                    <time class="command-proposal-card__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                    <span class="command-proposal-card__id">ID: ${this.escapeHtml(payload.confirmationRequestId.substring(0, 8))}...</span>
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
        return false;
    }
}

/**
 * Renders command.confirmed events
 * Updates pending proposal cards to show acceptance
 * @extends EventRendererBase
 */
class CommandConfirmedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: 'command.confirmed',
            name: 'CommandConfirmedRenderer',
            description: 'Renders command confirmation status',
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
        const payload = event.payload;
        const pendingId = this.generateElementId(payload.confirmationRequestId, 'command-proposal');
        const pendingElement = document.getElementById(pendingId);

        if (pendingElement) {
            this._updatePendingCard(pendingElement, payload, event);
            return this.createRenderResult('', {
                elementId: pendingId,
                append: false,
                targetSelector: `#${pendingId}`
            });
        }

        return this._renderStandaloneConfirmed(event, payload);
    }

    /**
     * Updates a pending proposal card to show confirmed status
     * @private
     * @param {HTMLElement} element
     * @param {Object} payload
     * @param {StreamingEvent} event
     */
    _updatePendingCard(element, payload, event) {
        const controls = element.querySelector('.command-proposal-card__controls');
        if (controls) {
            controls.remove();
        }

        const header = element.querySelector('.command-proposal-card__header');
        if (header) {
            header.innerHTML = `
                <span class="command-proposal-card__icon">✅</span>
                <span class="command-proposal-card__title">Command Accepted</span>
                <span class="command-proposal-card__status-badge status--confirmed">Confirmed</span>
            `;
        }

        const body = element.querySelector('.command-proposal-card__body');
        if (body) {
            const confirmedInfo = document.createElement('div');
            confirmedInfo.className = 'command-proposal-card__confirmed-info';
            confirmedInfo.innerHTML = `<span class="command-proposal-card__confirmed-at">Confirmed at: ${this.formatTimestamp(payload.confirmedAt)}</span>`;
            body.appendChild(confirmedInfo);
        }

        element.classList.remove('command-proposal-card--pending');
        element.classList.add('command-proposal-card--confirmed');
        element.dataset.status = 'confirmed';
    }

    /**
     * Renders a standalone confirmed proposal card
     * @private
     * @param {StreamingEvent} event
     * @param {Object} payload
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    _renderStandaloneConfirmed(event, payload) {
        const elementId = this.generateElementId(event.id, 'command-confirmed');

        const html = `
            <div class="command-proposal-card command-proposal-card--confirmed" id="${elementId}" data-confirmation-id="${this.escapeHtml(payload.confirmationRequestId)}" data-status="confirmed">
                <div class="command-proposal-card__header">
                    <span class="command-proposal-card__icon">✅</span>
                    <span class="command-proposal-card__title">Command Accepted</span>
                    <span class="command-proposal-card__status-badge status--confirmed">Confirmed</span>
                </div>
                <div class="command-proposal-card__body">
                    <p class="command-proposal-card__message">Command ${this.escapeHtml(payload.confirmationRequestId.substring(0, 8))}... was accepted and is executing.</p>
                    <span class="command-proposal-card__confirmed-at">Confirmed at: ${this.formatTimestamp(payload.confirmedAt)}</span>
                </div>
                <div class="command-proposal-card__footer">
                    <time class="command-proposal-card__timestamp">${this.formatTimestamp(event.timestamp)}</time>
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
 * Renders command.rejected events
 * Updates pending proposal cards to show rejection
 * @extends EventRendererBase
 */
class CommandRejectedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: 'command.rejected',
            name: 'CommandRejectedRenderer',
            description: 'Renders command rejection status',
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
        const payload = event.payload;
        const pendingId = this.generateElementId(payload.confirmationRequestId, 'command-proposal');
        const pendingElement = document.getElementById(pendingId);

        if (pendingElement) {
            this._updatePendingCard(pendingElement, payload, event);
            return this.createRenderResult('', {
                elementId: pendingId,
                append: false,
                targetSelector: `#${pendingId}`
            });
        }

        return this._renderStandaloneRejected(event, payload);
    }

    /**
     * Updates a pending proposal card to show rejected status
     * @private
     * @param {HTMLElement} element
     * @param {Object} payload
     * @param {StreamingEvent} event
     */
    _updatePendingCard(element, payload, event) {
        const controls = element.querySelector('.command-proposal-card__controls');
        if (controls) {
            controls.remove();
        }

        const header = element.querySelector('.command-proposal-card__header');
        if (header) {
            header.innerHTML = `
                <span class="command-proposal-card__icon">❌</span>
                <span class="command-proposal-card__title">Command Rejected</span>
                <span class="command-proposal-card__status-badge status--rejected">Rejected</span>
            `;
        }

        const body = element.querySelector('.command-proposal-card__body');
        if (body) {
            const rejectedInfo = document.createElement('div');
            rejectedInfo.className = 'command-proposal-card__rejected-info';

            let messageHtml = `<span class="command-proposal-card__rejected-at">Rejected at: ${this.formatTimestamp(payload.rejectedAt)}</span>`;
            if (payload.userMessage) {
                messageHtml += `<div class="command-proposal-card__user-message"><span class="command-proposal-card__user-message-label">Reason:</span><p class="command-proposal-card__user-message-text">${this.escapeHtml(payload.userMessage)}</p></div>`;
            }

            rejectedInfo.innerHTML = messageHtml;
            body.appendChild(rejectedInfo);
        }

        element.classList.remove('command-proposal-card--pending');
        element.classList.add('command-proposal-card--rejected');
        element.dataset.status = 'rejected';
    }

    /**
     * Renders a standalone rejected proposal card
     * @private
     * @param {StreamingEvent} event
     * @param {Object} payload
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    _renderStandaloneRejected(event, payload) {
        const elementId = this.generateElementId(event.id, 'command-rejected');

        const userMessageHtml = payload.userMessage
            ? `<div class="command-proposal-card__user-message"><span class="command-proposal-card__user-message-label">Reason:</span><p class="command-proposal-card__user-message-text">${this.escapeHtml(payload.userMessage)}</p></div>`
            : '';

        const html = `
            <div class="command-proposal-card command-proposal-card--rejected" id="${elementId}" data-confirmation-id="${this.escapeHtml(payload.confirmationRequestId)}" data-status="rejected">
                <div class="command-proposal-card__header">
                    <span class="command-proposal-card__icon">❌</span>
                    <span class="command-proposal-card__title">Command Rejected</span>
                    <span class="command-proposal-card__status-badge status--rejected">Rejected</span>
                </div>
                <div class="command-proposal-card__body">
                    <p class="command-proposal-card__message">Command ${this.escapeHtml(payload.confirmationRequestId.substring(0, 8))}... was rejected.</p>
                    <span class="command-proposal-card__rejected-at">Rejected at: ${this.formatTimestamp(payload.rejectedAt)}</span>
                    ${userMessageHtml}
                </div>
                <div class="command-proposal-card__footer">
                    <time class="command-proposal-card__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                </div>
            </div>
        `;

        return this.createRenderResult(html, {
            elementId,
            append: true
        });
    }
}
