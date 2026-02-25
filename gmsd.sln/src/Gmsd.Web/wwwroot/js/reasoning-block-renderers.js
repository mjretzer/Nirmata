/**
 * @fileoverview Reasoning Block Renderers
 * @description Renderers for reasoning events (intent.classified, gate.selected)
 * Provides visual representation of the orchestrator's decision process
 */

/**
 * @typedef {import('./ievent-renderer.js').EventRendererBase} EventRendererBase
 * @typedef {import('./event-types.js').StreamingEvent} StreamingEvent
 * @typedef {import('./event-types.js').IntentClassifiedPayload} IntentClassifiedPayload
 * @typedef {import('./event-types.js').GateSelectedPayload} GateSelectedPayload
 * @typedef {import('./event-types.js').ProposedAction} ProposedAction
 */

/**
 * Renders intent.classified events with confidence visualization
 * Shows the orchestrator's classification decision with visual confidence indicator
 * @extends EventRendererBase
 */
class IntentClassifiedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.IntentClassified,
            name: 'IntentClassifiedRenderer',
            description: 'Renders intent classification with confidence visualization',
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
        /** @type {IntentClassifiedPayload} */
        const payload = event.payload;
        const elementId = this.generateElementId(event.id, 'intent');

        const confidencePercent = Math.round((payload.confidence || 0) * 100);
        const confidenceLevel = this._getConfidenceLevel(confidencePercent);
        const classificationIcon = this._getClassificationIcon(payload.classification);
        const classificationLabel = this._getClassificationLabel(payload.classification);

        const html = `
            <div class="reasoning-block reasoning-block--intent" id="${elementId}" data-event-id="${this.escapeHtml(event.id)}">
                <div class="reasoning-block__header">
                    <button type="button" class="reasoning-block__toggle" aria-expanded="true" aria-controls="${elementId}-content">
                        <span class="reasoning-block__icon">${classificationIcon}</span>
                        <span class="reasoning-block__title">Intent: ${classificationLabel}</span>
                        <span class="reasoning-block__chevron">
                            <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                                <path d="M4 6L8 10L12 6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                            </svg>
                        </span>
                    </button>
                    <div class="confidence-bar confidence-bar--${confidenceLevel}" title="Confidence: ${confidencePercent}%">
                        <div class="confidence-bar__track">
                            <div class="confidence-bar__fill" style="width: ${confidencePercent}%"></div>
                        </div>
                        <span class="confidence-bar__label">${confidencePercent}%</span>
                    </div>
                </div>
                <div class="reasoning-block__content" id="${elementId}-content">
                    <div class="reasoning-block__reasoning">
                        <p class="reasoning-block__reasoning-text">${this.escapeHtml(payload.reasoning || 'No reasoning provided')}</p>
                    </div>
                    <div class="reasoning-block__meta">
                        <time class="reasoning-block__timestamp">${this.formatTimestamp(event.timestamp)}</time>
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
     * Gets confidence level classification
     * @private
     * @param {number} percent
     * @returns {'high'|'medium'|'low'}
     */
    _getConfidenceLevel(percent) {
        if (percent >= 80) return 'high';
        if (percent >= 50) return 'medium';
        return 'low';
    }

    /**
     * Gets icon for classification type
     * @private
     * @param {string} classification
     * @returns {string}
     */
    _getClassificationIcon(classification) {
        const icons = {
            'Chat': '💬',
            'ReadOnly': '👁️',
            'Write': '✏️'
        };
        return icons[classification] || '❓';
    }

    /**
     * Gets human-readable label for classification
     * @private
     * @param {string} classification
     * @returns {string}
     */
    _getClassificationLabel(classification) {
        const labels = {
            'Chat': 'Conversation',
            'ReadOnly': 'Read-Only Query',
            'Write': 'Write Operation'
        };
        return labels[classification] || classification;
    }
}

/**
 * Renders gate.selected events with decision card
 * Shows phase selection with reasoning and optional confirmation
 * @extends EventRendererBase
 */
class GateSelectedRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.GateSelected,
            name: 'GateSelectedRenderer',
            description: 'Renders gate selection with decision card and confirmation',
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
        /** @type {GateSelectedPayload} */
        const payload = event.payload;
        const elementId = this.generateElementId(event.id, 'gate');

        const phaseIcon = this._getPhaseIcon(payload.phase);
        const requiresConfirmation = payload.requiresConfirmation;

        let confirmationHtml = '';
        if (requiresConfirmation && payload.proposedAction) {
            confirmationHtml = this._renderConfirmationSection(payload.proposedAction, event.id);
        }

        const html = `
            <div class="reasoning-block reasoning-block--gate" id="${elementId}" data-event-id="${this.escapeHtml(event.id)}">
                <div class="reasoning-block__header">
                    <button type="button" class="reasoning-block__toggle" aria-expanded="true" aria-controls="${elementId}-content">
                        <span class="reasoning-block__icon">${phaseIcon}</span>
                        <span class="reasoning-block__title">Selected: ${this.escapeHtml(payload.phase)}</span>
                        <span class="reasoning-block__chevron">
                            <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                                <path d="M4 6L8 10L12 6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                            </svg>
                        </span>
                    </button>
                    ${requiresConfirmation ? '<span class="reasoning-block__badge reasoning-block__badge--confirm">Needs Confirmation</span>' : ''}
                </div>
                <div class="reasoning-block__content" id="${elementId}-content">
                    <div class="reasoning-block__reasoning">
                        <h4 class="reasoning-block__subtitle">Decision Reasoning</h4>
                        <p class="reasoning-block__reasoning-text">${this.escapeHtml(payload.reasoning || 'No reasoning provided')}</p>
                    </div>
                    ${confirmationHtml}
                    <div class="reasoning-block__meta">
                        <time class="reasoning-block__timestamp">${this.formatTimestamp(event.timestamp)}</time>
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
     * Renders confirmation section for actions requiring user approval
     * @private
     * @param {ProposedAction} action
     * @param {string} eventId
     * @returns {string}
     */
    _renderConfirmationSection(action, eventId) {
        const paramsHtml = action.parameters 
            ? `<pre class="confirmation-card__params"><code>${this.escapeHtml(JSON.stringify(action.parameters, null, 2))}</code></pre>`
            : '';

        return `
            <div class="confirmation-card" data-event-id="${this.escapeHtml(eventId)}">
                <div class="confirmation-card__header">
                    <span class="confirmation-card__icon">⚠️</span>
                    <span class="confirmation-card__title">Action Requires Confirmation</span>
                </div>
                <div class="confirmation-card__body">
                    <p class="confirmation-card__description">${this.escapeHtml(action.description || action.name)}</p>
                    ${paramsHtml}
                </div>
                <div class="confirmation-card__actions">
                    <button type="button" class="confirmation-card__btn confirmation-card__btn--confirm" data-action="confirm" data-event-id="${this.escapeHtml(eventId)}">
                        <span class="confirmation-card__btn-icon">✓</span>
                        Confirm
                    </button>
                    <button type="button" class="confirmation-card__btn confirmation-card__btn--cancel" data-action="cancel" data-event-id="${this.escapeHtml(eventId)}">
                        <span class="confirmation-card__btn-icon">✕</span>
                        Cancel
                    </button>
                </div>
            </div>
        `;
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
            'GitCommitter': '📦'
        };
        return icons[phase] || '🔄';
    }
}

/**
 * Creates all reasoning block renderers
 * @returns {Array<{eventType: string, renderer: IEventRenderer, priority: number}>}
 */
function createReasoningBlockRenderers() {
    return [
        {
            eventType: StreamingEventType.IntentClassified,
            renderer: new IntentClassifiedRenderer(),
            priority: 10
        },
        {
            eventType: StreamingEventType.GateSelected,
            renderer: new GateSelectedRenderer(),
            priority: 10
        }
    ];
}

/**
 * Initializes collapsible behavior for reasoning blocks
 * Should be called after DOM updates
 * @param {HTMLElement} [container] - Container to search for blocks (defaults to document)
 */
function initializeReasoningBlocks(container = document) {
    // Initialize collapsible toggles
    const toggles = container.querySelectorAll('.reasoning-block__toggle');
    toggles.forEach(toggle => {
        // Remove existing listener to prevent duplicates
        toggle.removeEventListener('click', _handleToggleClick);
        toggle.addEventListener('click', _handleToggleClick);
    });

    // Initialize confirmation buttons
    const confirmButtons = container.querySelectorAll('.confirmation-card__btn--confirm');
    const cancelButtons = container.querySelectorAll('.confirmation-card__btn--cancel');

    confirmButtons.forEach(btn => {
        btn.removeEventListener('click', _handleConfirmClick);
        btn.addEventListener('click', _handleConfirmClick);
    });

    cancelButtons.forEach(btn => {
        btn.removeEventListener('click', _handleCancelClick);
        btn.addEventListener('click', _handleCancelClick);
    });
}

/**
 * @private
 * @param {MouseEvent} event
 */
function _handleToggleClick(event) {
    const toggle = event.currentTarget;
    const block = toggle.closest('.reasoning-block');
    const content = block?.querySelector('.reasoning-block__content');
    const isExpanded = toggle.getAttribute('aria-expanded') === 'true';

    toggle.setAttribute('aria-expanded', !isExpanded);
    block?.classList.toggle('reasoning-block--collapsed', isExpanded);

    if (content) {
        content.style.display = isExpanded ? 'none' : 'block';
    }
}

/**
 * @private
 * @param {MouseEvent} event
 */
function _handleConfirmClick(event) {
    const button = event.currentTarget;
    const eventId = button.getAttribute('data-event-id');
    const card = button.closest('.confirmation-card');

    // Dispatch custom event for the application to handle
    const confirmEvent = new CustomEvent('reasoning:confirm', {
        detail: { eventId, action: 'confirm' },
        bubbles: true,
        cancelable: true
    });

    card?.dispatchEvent(confirmEvent);

    // Visual feedback
    if (card) {
        card.classList.add('confirmation-card--confirmed');
        card.innerHTML = '<div class="confirmation-card__success">✓ Action confirmed</div>';
    }
}

/**
 * @private
 * @param {MouseEvent} event
 */
function _handleCancelClick(event) {
    const button = event.currentTarget;
    const eventId = button.getAttribute('data-event-id');
    const card = button.closest('.confirmation-card');

    // Dispatch custom event for the application to handle
    const cancelEvent = new CustomEvent('reasoning:cancel', {
        detail: { eventId, action: 'cancel' },
        bubbles: true,
        cancelable: true
    });

    card?.dispatchEvent(cancelEvent);

    // Visual feedback
    if (card) {
        card.classList.add('confirmation-card--cancelled');
        card.innerHTML = '<div class="confirmation-card__cancelled">✕ Action cancelled</div>';
    }
}

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        IntentClassifiedRenderer,
        GateSelectedRenderer,
        createReasoningBlockRenderers,
        initializeReasoningBlocks
    };
}
