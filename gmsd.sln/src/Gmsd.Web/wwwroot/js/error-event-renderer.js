/**
 * @fileoverview Error Event Renderer
 * @description Renderer for error events with alert styling and retry functionality
 * Displays error banners with severity-based visual indicators
 */

/**
 * @typedef {import('./ievent-renderer.js').EventRendererBase} EventRendererBase
 * @typedef {import('./event-types.js').StreamingEvent} StreamingEvent
 * @typedef {import('./event-types.js').ErrorPayload} ErrorPayload
 */

/**
 * Renders error events with alert styling and severity-based visual indicators
 * Supports retry functionality for recoverable errors and links errors to phase context
 * @extends EventRendererBase
 */
class ErrorEventRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.Error,
            name: 'ErrorEventRenderer',
            description: 'Renders error events with alert styling and retry functionality',
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
        /** @type {ErrorPayload} */
        const payload = event.payload;
        const elementId = this.generateElementId(event.id, 'error');

        const severity = payload.severity || 'error';
        const severityConfig = this._getSeverityConfig(severity);
        const phaseContext = payload.phase || payload.context;

        const html = `
            <div class="error-alert error-alert--${severity}" 
                 id="${elementId}" 
                 data-event-id="${this.escapeHtml(event.id)}"
                 data-severity="${severity}"
                 data-recoverable="${payload.recoverable || false}"
                 role="alert"
                 aria-live="assertive">
                <div class="error-alert__header">
                    <span class="error-alert__icon">${severityConfig.icon}</span>
                    <span class="error-alert__title">${this.escapeHtml(severityConfig.title)}</span>
                    ${payload.code ? `<span class="error-alert__code">${this.escapeHtml(payload.code)}</span>` : ''}
                    <button type="button" 
                            class="error-alert__close" 
                            aria-label="Dismiss error"
                            data-action="dismiss"
                            data-event-id="${this.escapeHtml(event.id)}">
                        <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M4 4L12 12M4 12L12 4" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                        </svg>
                    </button>
                </div>
                <div class="error-alert__body">
                    <p class="error-alert__message">${this.escapeHtml(payload.message || 'An error occurred')}</p>
                    ${phaseContext ? this._renderPhaseContext(phaseContext) : ''}
                </div>
                <div class="error-alert__footer">
                    <time class="error-alert__timestamp">${this.formatTimestamp(event.timestamp)}</time>
                    ${this._renderRetryButton(payload)}
                </div>
            </div>
        `;

        return this.createRenderResult(html, {
            elementId,
            append: true
        });
    }

    /**
     * Updates an existing error alert (e.g., after retry attempt)
     * @override
     * @param {StreamingEvent} event
     * @param {HTMLElement} element
     * @param {Object} [context]
     * @returns {boolean}
     */
    update(event, element, context) {
        /** @type {ErrorPayload} */
        const payload = event.payload;

        // Update message if changed
        const messageEl = element.querySelector('.error-alert__message');
        if (messageEl && payload.message) {
            messageEl.textContent = payload.message;
        }

        // Update recoverable state
        if (payload.recoverable !== undefined) {
            element.setAttribute('data-recoverable', payload.recoverable);
            const footer = element.querySelector('.error-alert__footer');
            const existingRetry = footer?.querySelector('.error-alert__retry');
            
            if (payload.recoverable && !existingRetry) {
                const retryHtml = this._renderRetryButton(payload);
                if (footer && retryHtml) {
                    footer.insertAdjacentHTML('beforeend', retryHtml);
                }
            } else if (!payload.recoverable && existingRetry) {
                existingRetry.remove();
            }
        }

        return true;
    }

    /**
     * Gets severity configuration (icon, title, styling class)
     * @private
     * @param {string} severity
     * @returns {{icon: string, title: string, className: string}}
     */
    _getSeverityConfig(severity) {
        const configs = {
            error: {
                icon: '⚠️',
                title: 'Error',
                className: 'error-alert--error'
            },
            warning: {
                icon: '⚡',
                title: 'Warning',
                className: 'error-alert--warning'
            },
            info: {
                icon: 'ℹ️',
                title: 'Information',
                className: 'error-alert--info'
            }
        };

        return configs[severity] || configs.error;
    }

    /**
     * Renders phase context information
     * @private
     * @param {string} phase
     * @returns {string}
     */
    _renderPhaseContext(phase) {
        return `
            <div class="error-alert__context">
                <span class="error-alert__context-label">Phase:</span>
                <span class="error-alert__context-value">${this.escapeHtml(phase)}</span>
            </div>
        `;
    }

    /**
     * Renders retry button for recoverable errors
     * @private
     * @param {ErrorPayload} payload
     * @returns {string}
     */
    _renderRetryButton(payload) {
        if (!payload.recoverable) {
            return '';
        }

        const retryLabel = payload.retryAction || 'Retry';

        return `
            <button type="button" 
                    class="error-alert__retry" 
                    data-action="retry"
                    data-event-id="${this.escapeHtml(payload.eventId || '')}"
                    aria-label="${this.escapeHtml(retryLabel)}">
                <span class="error-alert__retry-icon">🔄</span>
                <span class="error-alert__retry-label">${this.escapeHtml(retryLabel)}</span>
            </button>
        `;
    }
}

/**
 * Creates error event renderer registration
 * @returns {Array<{eventType: string, renderer: IEventRenderer, priority: number}>}
 */
function createErrorEventRenderers() {
    return [
        {
            eventType: StreamingEventType.Error,
            renderer: new ErrorEventRenderer(),
            priority: 10
        }
    ];
}

/**
 * Initializes error alert interactions (close buttons, retry buttons)
 * Should be called after DOM updates
 * @param {HTMLElement} [container] - Container to search for error alerts (defaults to document)
 */
function initializeErrorAlerts(container = document) {
    // Initialize close buttons
    const closeButtons = container.querySelectorAll('.error-alert__close');
    closeButtons.forEach(btn => {
        // Remove existing listener to prevent duplicates
        btn.removeEventListener('click', _handleCloseClick);
        btn.addEventListener('click', _handleCloseClick);
    });

    // Initialize retry buttons
    const retryButtons = container.querySelectorAll('.error-alert__retry');
    retryButtons.forEach(btn => {
        btn.removeEventListener('click', _handleRetryClick);
        btn.addEventListener('click', _handleRetryClick);
    });
}

/**
 * @private
 * @param {MouseEvent} event
 */
function _handleCloseClick(event) {
    const button = event.currentTarget;
    const alert = button.closest('.error-alert');
    
    if (alert) {
        // Add dismissal animation
        alert.classList.add('error-alert--dismissing');
        
        // Remove after animation completes
        setTimeout(() => {
            alert.remove();
        }, 300);

        // Dispatch custom event
        const dismissEvent = new CustomEvent('error:dismiss', {
            detail: { eventId: alert.getAttribute('data-event-id') },
            bubbles: true,
            cancelable: true
        });
        alert.dispatchEvent(dismissEvent);
    }
}

/**
 * @private
 * @param {MouseEvent} event
 */
function _handleRetryClick(event) {
    const button = event.currentTarget;
    const alert = button.closest('.error-alert');
    const eventId = button.getAttribute('data-event-id');

    // Show loading state
    button.disabled = true;
    button.classList.add('error-alert__retry--loading');
    button.innerHTML = '<span class="error-alert__retry-icon">⏳</span><span class="error-alert__retry-label">Retrying...</span>';

    // Dispatch custom event for the application to handle
    const retryEvent = new CustomEvent('error:retry', {
        detail: { 
            eventId: eventId || alert?.getAttribute('data-event-id'),
            alertElement: alert
        },
        bubbles: true,
        cancelable: true
    });

    alert?.dispatchEvent(retryEvent);

    // If event wasn't cancelled, reset button after delay
    if (!retryEvent.defaultPrevented) {
        setTimeout(() => {
            button.disabled = false;
            button.classList.remove('error-alert__retry--loading');
            button.innerHTML = '<span class="error-alert__retry-icon">🔄</span><span class="error-alert__retry-label">Retry</span>';
        }, 2000);
    }
}

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        ErrorEventRenderer,
        createErrorEventRenderers,
        initializeErrorAlerts
    };
}
