/**
 * @fileoverview Assistant Event Renderers
 * @description Renderers for assistant dialogue events (assistant.delta, assistant.final)
 * Provides streaming message display and rich content rendering
 */

/**
 * @typedef {import('./ievent-renderer.js').EventRendererBase} EventRendererBase
 * @typedef {import('./event-types.js').StreamingEvent} StreamingEvent
 * @typedef {import('./event-types.js').AssistantDeltaPayload} AssistantDeltaPayload
 * @typedef {import('./event-types.js').AssistantFinalPayload} AssistantFinalPayload
 */

/**
 * Manages streaming message state by messageId
 * Tracks accumulated content and DOM elements for delta events
 */
class StreamingMessageManager {
    constructor() {
        /** @private @type {Map<string, {content: string, elementId: string, element: HTMLElement|null}>} */
        this._messages = new Map();
    }

    /**
     * Gets or creates a message entry
     * @param {string} messageId
     * @returns {{content: string, elementId: string, element: HTMLElement|null}}
     */
    getOrCreate(messageId) {
        if (!this._messages.has(messageId)) {
            const elementId = `assistant-message-${messageId.replace(/[^a-zA-Z0-9_-]/g, '')}`;
            this._messages.set(messageId, {
                content: '',
                elementId,
                element: null
            });
        }
        return this._messages.get(messageId);
    }

    /**
     * Gets an existing message entry
     * @param {string} messageId
     * @returns {{content: string, elementId: string, element: HTMLElement|null}|undefined}
     */
    get(messageId) {
        return this._messages.get(messageId);
    }

    /**
     * Appends content to a message
     * @param {string} messageId
     * @param {string} delta
     * @returns {string} Updated full content
     */
    appendContent(messageId, delta) {
        const entry = this.getOrCreate(messageId);
        entry.content += delta;
        return entry.content;
    }

    /**
     * Sets the DOM element for a message
     * @param {string} messageId
     * @param {HTMLElement} element
     */
    setElement(messageId, element) {
        const entry = this._messages.get(messageId);
        if (entry) {
            entry.element = element;
        }
    }

    /**
     * Gets the DOM element for a message
     * @param {string} messageId
     * @returns {HTMLElement|null}
     */
    getElement(messageId) {
        const entry = this._messages.get(messageId);
        return entry ? entry.element : null;
    }

    /**
     * Marks a message as complete and returns final content
     * @param {string} messageId
     * @returns {string|null} Final content or null if not found
     */
    finalize(messageId) {
        const entry = this._messages.get(messageId);
        if (entry) {
            const content = entry.content;
            this._messages.delete(messageId);
            return content;
        }
        return null;
    }

    /**
     * Clears all tracked messages
     */
    clear() {
        this._messages.clear();
    }

    /**
     * Gets count of active streaming messages
     * @returns {number}
     */
    getActiveCount() {
        return this._messages.size;
    }
}

// Global streaming message manager instance
const streamingMessageManager = new StreamingMessageManager();

/**
 * Detects and parses rich content types from text
 * Provides utilities for identifying code blocks, tables, etc.
 */
class RichContentDetector {
    /**
     * Checks if content contains code blocks
     * @param {string} content
     * @returns {boolean}
     */
    static hasCodeBlocks(content) {
        return /```[\s\S]*?```/.test(content);
    }

    /**
     * Checks if content contains markdown tables
     * @param {string} content
     * @returns {boolean}
     */
    static hasTables(content) {
        return /\|[^\n]+\|[^\n]+\n\|[-:\s|]+\|/.test(content);
    }

    /**
     * Checks if content contains structured data (JSON, etc.)
     * @param {string} content
     * @returns {boolean}
     */
    static hasStructuredData(content) {
        try {
            const trimmed = content.trim();
            return (trimmed.startsWith('{') && trimmed.endsWith('}')) ||
                   (trimmed.startsWith('[') && trimmed.endsWith(']'));
        } catch {
            return false;
        }
    }

    /**
     * Detects the primary content type
     * @param {string} content
     * @returns {'text'|'code'|'table'|'structured'}
     */
    static detectContentType(content) {
        if (this.hasCodeBlocks(content)) return 'code';
        if (this.hasTables(content)) return 'table';
        if (this.hasStructuredData(content)) return 'structured';
        return 'text';
    }
}

/**
 * Parses markdown content into rich HTML
 * Supports code blocks, tables, and inline formatting
 */
class MarkdownParser {
    /**
     * Parses markdown text to HTML
     * @param {string} markdown
     * @returns {string}
     */
    static parse(markdown) {
        if (!markdown) return '';

        let html = this.escapeHtml(markdown);

        // Code blocks (must be first to avoid escaping content inside)
        html = html.replace(/```(\w+)?\n([\s\S]*?)```/g, (match, lang, code) => {
            const language = lang || 'text';
            const displayLang = this._getDisplayLanguage(language);
            return `<div class="code-block" data-language="${language}">
                <div class="code-block__header">
                    <span class="code-block__language">${displayLang}</span>
                    <button type="button" class="code-block__copy" title="Copy to clipboard">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                            <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                        </svg>
                    </button>
                </div>
                <pre class="code-block__pre"><code class="code-block__code">${code}</code></pre>
            </div>`;
        });

        // Inline code
        html = html.replace(/`([^`]+)`/g, '<code class="inline-code">$1</code>');

        // Tables
        html = html.replace(/\|([^\n]+)\|[^\n]+\n\|([-:\s|]+)\|((?:\n\|[^\n]+\|[^\n]+)*)/g, (match, header, separator, rows) => {
            const headers = header.split('|').map(h => h.trim()).filter(h => h);
            const rowData = rows.trim().split('\n').map(row => {
                const cells = row.split('|').map(c => c.trim()).filter(c => c);
                return `<tr>${cells.map(c => `<td>${c}</td>`).join('')}</tr>`;
            }).join('');

            return `<table class="rich-table">
                <thead>
                    <tr>${headers.map(h => `<th>${h}</th>`).join('')}</tr>
                </thead>
                <tbody>${rowData}</tbody>
            </table>`;
        });

        // Headers
        html = html.replace(/^### (.+)$/gm, '<h3 class="assistant-message__heading">$1</h3>');
        html = html.replace(/^## (.+)$/gm, '<h2 class="assistant-message__heading">$2</h1>');
        html = html.replace(/^# (.+)$/gm, '<h1 class="assistant-message__heading">$1</h1>');

        // Bold and italic
        html = html.replace(/\*\*\*(.+?)\*\*\*/g, '<strong><em>$1</em></strong>');
        html = html.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
        html = html.replace(/\*(.+?)\*/g, '<em>$1</em>');
        html = html.replace(/__(.+?)__/g, '<strong>$1</strong>');
        html = html.replace(/_(.+?)_/g, '<em>$1</em>');

        // Links
        html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" class="assistant-message__link" target="_blank" rel="noopener">$1</a>');

        // Lists
        html = html.replace(/^\s*[-*] (.+)$/gm, '<li class="assistant-message__list-item">$1</li>');
        html = html.replace(/(<li[^>]*>[\s\S]*?<\/li>)/g, '<ul class="assistant-message__list">$1</ul>');

        // Blockquotes
        html = html.replace(/^> (.+)$/gm, '<blockquote class="assistant-message__quote">$1</blockquote>');

        // Line breaks (preserve paragraphs)
        html = html.replace(/\n\n/g, '</p><p class="assistant-message__paragraph">');
        html = html.replace(/\n/g, '<br>');

        // Wrap in paragraph if not already wrapped
        if (!html.startsWith('<')) {
            html = `<p class="assistant-message__paragraph">${html}</p>`;
        }

        return html;
    }

    /**
     * Escapes HTML entities
     * @private
     * @param {string} text
     * @returns {string}
     */
    static escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Gets display name for language
     * @private
     * @param {string} lang
     * @returns {string}
     */
    static _getDisplayLanguage(lang) {
        const displayNames = {
            'js': 'JavaScript',
            'ts': 'TypeScript',
            'py': 'Python',
            'cs': 'C#',
            'json': 'JSON',
            'xml': 'XML',
            'html': 'HTML',
            'css': 'CSS',
            'sql': 'SQL',
            'sh': 'Shell',
            'bash': 'Bash',
            'ps1': 'PowerShell',
            'md': 'Markdown',
            'yaml': 'YAML',
            'yml': 'YAML',
            'text': 'Plain Text'
        };
        return displayNames[lang.toLowerCase()] || lang.toUpperCase();
    }
}

/**
 * Renders assistant.delta events with streaming text
 * Accumulates chunks and updates the DOM in real-time
 * @extends EventRendererBase
 */
class AssistantDeltaRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.AssistantDelta,
            name: 'AssistantDeltaRenderer',
            description: 'Renders streaming assistant message deltas with real-time updates',
            version: '1.0.0',
            priority: 10
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @param {HTMLElement} [context.container]
     * @param {string} [context.threadId]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        /** @type {AssistantDeltaPayload} */
        const payload = event.payload;
        const messageId = payload.messageId;
        const delta = payload.content || '';

        // Get or create message entry
        const entry = streamingMessageManager.getOrCreate(messageId);

        // Append new content
        const fullContent = streamingMessageManager.appendContent(messageId, delta);

        // Check if this is the first chunk for this message
        const isFirstChunk = fullContent.length === delta.length;

        if (isFirstChunk) {
            // Create new message container
            const html = this._createMessageContainer(entry.elementId, messageId, fullContent);
            return this.createRenderResult(html, {
                elementId: entry.elementId,
                append: true,
                targetSelector: context?.container ? null : '.assistant-messages'
            });
        } else {
            // Update existing message - return instruction to update
            const html = this._renderContent(fullContent);
            return this.createRenderResult(html, {
                elementId: entry.elementId,
                append: false
            });
        }
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {HTMLElement} element
     * @param {Object} [context]
     * @returns {boolean}
     */
    update(event, element, context) {
        /** @type {AssistantDeltaPayload} */
        const payload = event.payload;
        const messageId = payload.messageId;
        const delta = payload.content || '';

        // Update the content
        const fullContent = streamingMessageManager.appendContent(messageId, delta);

        // Find the content container
        const contentElement = element.querySelector('.assistant-message__content');
        if (contentElement) {
            contentElement.innerHTML = this._renderContent(fullContent);

            // Store reference to element
            streamingMessageManager.setElement(messageId, element);

            // Scroll to bottom if needed
            this._autoScroll(element);

            return true;
        }

        return false;
    }

    /**
     * Creates the initial message container HTML
     * @private
     * @param {string} elementId
     * @param {string} messageId
     * @param {string} content
     * @returns {string}
     */
    _createMessageContainer(elementId, messageId, content) {
        return `
            <div class="assistant-message assistant-message--streaming" id="${elementId}" data-message-id="${this.escapeHtml(messageId)}">
                <div class="assistant-message__avatar">
                    <span class="assistant-message__avatar-icon">🤖</span>
                </div>
                <div class="assistant-message__body">
                    <div class="assistant-message__header">
                        <span class="assistant-message__sender">Assistant</span>
                        <span class="assistant-message__indicator">
                            <span class="typing-indicator">
                                <span class="typing-indicator__dot"></span>
                                <span class="typing-indicator__dot"></span>
                                <span class="typing-indicator__dot"></span>
                            </span>
                        </span>
                    </div>
                    <div class="assistant-message__content">
                        ${this._renderContent(content)}
                    </div>
                    <div class="assistant-message__footer">
                        <time class="assistant-message__timestamp">${new Date().toLocaleTimeString()}</time>
                    </div>
                </div>
            </div>
        `;
    }

    /**
     * Renders content with markdown parsing
     * @private
     * @param {string} content
     * @returns {string}
     */
    _renderContent(content) {
        return MarkdownParser.parse(content);
    }

    /**
     * Auto-scrolls the message into view if near bottom
     * @private
     * @param {HTMLElement} element
     */
    _autoScroll(element) {
        const container = element.closest('.messages-container') || element.parentElement;
        if (container) {
            const isNearBottom = container.scrollHeight - container.scrollTop - container.clientHeight < 100;
            if (isNearBottom) {
                container.scrollTop = container.scrollHeight;
            }
        }
    }
}

/**
 * Renders assistant.final events with structured data
 * Finalizes streaming messages and displays complete content
 * @extends EventRendererBase
 */
class AssistantFinalRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.AssistantFinal,
            name: 'AssistantFinalRenderer',
            description: 'Renders final assistant messages with structured data support',
            version: '1.0.0',
            priority: 10
        });
    }

    /**
     * @override
     * @param {StreamingEvent} event
     * @param {Object} [context]
     * @param {HTMLElement} [context.container]
     * @returns {import('./ievent-renderer.js').RenderResult}
     */
    render(event, context) {
        /** @type {AssistantFinalPayload} */
        const payload = event.payload;
        const messageId = payload.messageId;
        const content = payload.content || '';
        const structuredData = payload.structuredData;

        // Check if we have a streaming message to finalize
        const existingEntry = streamingMessageManager.get(messageId);
        const existingElement = existingEntry?.element;

        if (existingElement) {
            // Update existing streaming message to final state
            this._finalizeExistingMessage(existingElement, content, structuredData);

            // Clean up streaming manager
            streamingMessageManager.finalize(messageId);

            return this.createRenderResult('', {
                elementId: existingEntry.elementId,
                append: false
            });
        } else {
            // Create new final message (for non-streaming responses)
            const elementId = `assistant-message-${messageId.replace(/[^a-zA-Z0-9_-]/g, '')}`;
            const html = this._createFinalMessage(elementId, messageId, content, structuredData);

            return this.createRenderResult(html, {
                elementId,
                append: true,
                targetSelector: context?.container ? null : '.assistant-messages'
            });
        }
    }

    /**
     * Finalizes an existing streaming message element
     * @private
     * @param {HTMLElement} element
     * @param {string} content
     * @param {*} structuredData
     */
    _finalizeExistingMessage(element, content, structuredData) {
        // Remove streaming indicator
        const indicator = element.querySelector('.assistant-message__indicator');
        if (indicator) {
            indicator.remove();
        }

        // Update content with final parsed version
        const contentElement = element.querySelector('.assistant-message__content');
        if (contentElement) {
            contentElement.innerHTML = this._renderFinalContent(content, structuredData);
        }

        // Update timestamp
        const timestampElement = element.querySelector('.assistant-message__timestamp');
        if (timestampElement) {
            timestampElement.textContent = new Date().toLocaleTimeString();
        }

        // Add structured data if present
        if (structuredData) {
            const body = element.querySelector('.assistant-message__body');
            if (body) {
                const structuredHtml = this._renderStructuredData(structuredData);
                body.insertAdjacentHTML('beforeend', structuredHtml);
            }
        }

        // Remove streaming class and add completed class
        element.classList.remove('assistant-message--streaming');
        element.classList.add('assistant-message--completed');

        // Initialize interactive elements
        this._initializeInteractiveElements(element);
    }

    /**
     * Creates a final message container HTML
     * @private
     * @param {string} elementId
     * @param {string} messageId
     * @param {string} content
     * @param {*} structuredData
     * @returns {string}
     */
    _createFinalMessage(elementId, messageId, content, structuredData) {
        const structuredHtml = structuredData ? this._renderStructuredData(structuredData) : '';

        return `
            <div class="assistant-message assistant-message--completed" id="${elementId}" data-message-id="${this.escapeHtml(messageId)}">
                <div class="assistant-message__avatar">
                    <span class="assistant-message__avatar-icon">🤖</span>
                </div>
                <div class="assistant-message__body">
                    <div class="assistant-message__header">
                        <span class="assistant-message__sender">Assistant</span>
                    </div>
                    <div class="assistant-message__content">
                        ${this._renderFinalContent(content, structuredData)}
                    </div>
                    ${structuredHtml}
                    <div class="assistant-message__footer">
                        <time class="assistant-message__timestamp">${new Date().toLocaleTimeString()}</time>
                    </div>
                </div>
            </div>
        `;
    }

    /**
     * Renders final content with full markdown parsing
     * @private
     * @param {string} content
     * @param {*} structuredData
     * @returns {string}
     */
    _renderFinalContent(content, structuredData) {
        return MarkdownParser.parse(content);
    }

    /**
     * Renders structured data as a card
     * @private
     * @param {*} data
     * @returns {string}
     */
    _renderStructuredData(data) {
        const json = JSON.stringify(data, null, 2);

        return `
            <div class="assistant-message__structured-data">
                <div class="structured-data-card">
                    <div class="structured-data-card__header">
                        <span class="structured-data-card__icon">📊</span>
                        <span class="structured-data-card__title">Structured Data</span>
                        <button type="button" class="structured-data-card__toggle" aria-expanded="false">
                            <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M4 6L8 10L12 6" stroke-linecap="round" stroke-linejoin="round"/>
                            </svg>
                        </button>
                    </div>
                    <div class="structured-data-card__content" style="display: none;">
                        <pre class="structured-data-card__code"><code>${this.escapeHtml(json)}</code></pre>
                    </div>
                </div>
            </div>
        `;
    }

    /**
     * Initializes interactive elements in the message
     * @private
     * @param {HTMLElement} element
     */
    _initializeInteractiveElements(element) {
        // Initialize code block copy buttons
        const copyButtons = element.querySelectorAll('.code-block__copy');
        copyButtons.forEach(btn => {
            btn.addEventListener('click', (e) => {
                const codeBlock = e.currentTarget.closest('.code-block');
                const code = codeBlock?.querySelector('.code-block__code')?.textContent;
                if (code) {
                    navigator.clipboard.writeText(code).then(() => {
                        btn.classList.add('code-block__copy--copied');
                        setTimeout(() => btn.classList.remove('code-block__copy--copied'), 2000);
                    });
                }
            });
        });

        // Initialize structured data toggles
        const toggles = element.querySelectorAll('.structured-data-card__toggle');
        toggles.forEach(toggle => {
            toggle.addEventListener('click', (e) => {
                const card = e.currentTarget.closest('.structured-data-card');
                const content = card?.querySelector('.structured-data-card__content');
                const isExpanded = toggle.getAttribute('aria-expanded') === 'true';
                
                toggle.setAttribute('aria-expanded', !isExpanded);
                if (content) {
                    content.style.display = isExpanded ? 'none' : 'block';
                }
            });
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
        // Final messages don't typically get updated
        return false;
    }
}

/**
 * Creates all assistant event renderers
 * @returns {Array<{eventType: string, renderer: IEventRenderer, priority: number}>}
 */
function createAssistantEventRenderers() {
    return [
        {
            eventType: StreamingEventType.AssistantDelta,
            renderer: new AssistantDeltaRenderer(),
            priority: 10
        },
        {
            eventType: StreamingEventType.AssistantFinal,
            renderer: new AssistantFinalRenderer(),
            priority: 10
        }
    ];
}

/**
 * Initializes assistant message behaviors
 * Should be called after DOM updates
 * @param {HTMLElement} [container] - Container to search for messages (defaults to document)
 */
function initializeAssistantMessages(container = document) {
    // Initialize code block copy buttons
    const copyButtons = container.querySelectorAll('.code-block__copy');
    copyButtons.forEach(btn => {
        btn.removeEventListener('click', _handleCodeCopy);
        btn.addEventListener('click', _handleCodeCopy);
    });

    // Initialize structured data toggles
    const toggles = container.querySelectorAll('.structured-data-card__toggle');
    toggles.forEach(toggle => {
        toggle.removeEventListener('click', _handleStructuredDataToggle);
        toggle.addEventListener('click', _handleStructuredDataToggle);
    });
}

/**
 * @private
 * @param {MouseEvent} event
 */
function _handleCodeCopy(event) {
    const button = event.currentTarget;
    const codeBlock = button.closest('.code-block');
    const code = codeBlock?.querySelector('.code-block__code')?.textContent;
    if (code) {
        navigator.clipboard.writeText(code).then(() => {
            button.classList.add('code-block__copy--copied');
            setTimeout(() => button.classList.remove('code-block__copy--copied'), 2000);
        });
    }
}

/**
 * @private
 * @param {MouseEvent} event
 */
function _handleStructuredDataToggle(event) {
    const toggle = event.currentTarget;
    const card = toggle.closest('.structured-data-card');
    const content = card?.querySelector('.structured-data-card__content');
    const isExpanded = toggle.getAttribute('aria-expanded') === 'true';

    toggle.setAttribute('aria-expanded', !isExpanded);
    if (content) {
        content.style.display = isExpanded ? 'none' : 'block';
    }
}

/**
 * Gets the streaming message manager instance
 * Useful for testing and external access
 * @returns {StreamingMessageManager}
 */
function getStreamingMessageManager() {
    return streamingMessageManager;
}

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        AssistantDeltaRenderer,
        AssistantFinalRenderer,
        StreamingMessageManager,
        RichContentDetector,
        MarkdownParser,
        createAssistantEventRenderers,
        initializeAssistantMessages,
        getStreamingMessageManager,
        streamingMessageManager
    };
}
