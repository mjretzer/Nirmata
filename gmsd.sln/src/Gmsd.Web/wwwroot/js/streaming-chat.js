/**
 * StreamingChat - Server-Sent Events (SSE) integration for chat streaming
 * Handles progressive message rendering, cursor indicators, and cancel functionality
 */

(function() {
    'use strict';

    // Active stream tracking
    const activeStreams = new Map();

    // Streaming configuration
    const STREAMING_CONFIG = {
        retryDelay: 3000,
        maxRetries: 3,
        heartbeatInterval: 30000
    };

    /**
     * StreamingChatController - Manages SSE connections for chat streaming
     */
    class StreamingChatController {
        constructor(options = {}) {
            this.options = {
                threadId: 'default',
                onMessageStart: null,
                onContentChunk: null,
                onMessageComplete: null,
                onError: null,
                onCancel: null,
                ...options
            };

            this.currentStreamId = null;
            this.currentMessageId = null;
            this.eventSource = null;
            this.isStreaming = false;
            this.accumulatedContent = '';
            this.retryCount = 0;
        }

        /**
         * Start a streaming chat request
         * @param {string} command - The command/message to send
         * @returns {Promise<void>}
         */
        async streamCommand(command) {
            if (this.isStreaming) {
                console.warn('Already streaming, cancelling previous stream');
                this.cancel();
            }

            this.isStreaming = true;
            this.currentStreamId = this.generateStreamId();
            this.accumulatedContent = '';
            this.retryCount = 0;

            // Store reference for global access
            activeStreams.set(this.currentStreamId, this);

            try {
                // Add user message to UI immediately
                this.addUserMessage(command);

                // Show streaming indicator
                this.showStreamingIndicator();

                // Disable input during streaming
                this.setInputEnabled(false);

                // Start SSE connection
                await this.startSSE(command);

            } catch (error) {
                console.error('Streaming error:', error);
                this.handleError(error);
            }
        }

        /**
         * Start Server-Sent Events connection
         */
        async startSSE(command) {
            // Use fetch with ReadableStream for better control than EventSource
            const formData = new FormData();
            formData.append('command', command);
            formData.append('threadId', this.options.threadId);
            formData.append('streamId', this.currentStreamId);

            const response = await fetch('/api/chat/stream', {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const reader = response.body.getReader();
            const decoder = new TextDecoder();

            while (this.isStreaming) {
                const { done, value } = await reader.read();
                
                if (done) {
                    break;
                }

                const chunk = decoder.decode(value, { stream: true });
                this.processSSEChunk(chunk);
            }
        }

        /**
         * Process SSE data chunks
         */
        processSSEChunk(chunk) {
            const lines = chunk.split('\n');
            
            for (const line of lines) {
                if (line.trim() === '') continue;
                
                // SSE format: data: {...}
                if (line.startsWith('data:')) {
                    const data = line.substring(5).trim();
                    this.handleSSEData(data);
                }
            }
        }

        /**
         * Handle parsed SSE data
         */
        handleSSEData(data) {
            try {
                const event = JSON.parse(data);
                
                switch (event.type) {
                    case 'message_start':
                        this.currentMessageId = event.messageId;
                        this.onMessageStart(event);
                        break;
                        
                    case 'content_chunk':
                        this.accumulatedContent += event.content;
                        this.onContentChunk(event);
                        break;
                        
                    case 'message_complete':
                        this.onMessageComplete(event);
                        this.cleanup();
                        break;
                        
                    case 'cancelled':
                        this.onCancel(event);
                        this.cleanup();
                        break;
                        
                    case 'error':
                        this.handleError(new Error(event.content));
                        this.cleanup();
                        break;
                }
            } catch (error) {
                console.error('Error parsing SSE data:', error, data);
            }
        }

        /**
         * Handle message start event
         */
        onMessageStart(event) {
            // Create streaming message element
            const messageHtml = this.createStreamingMessageElement(event.messageId);
            this.appendToChat(messageHtml);
            
            if (this.options.onMessageStart) {
                this.options.onMessageStart(event);
            }
        }

        /**
         * Handle content chunk event
         */
        onContentChunk(event) {
            const messageEl = document.getElementById(`message-${event.messageId}`);
            if (messageEl) {
                const contentEl = messageEl.querySelector('.message-streaming-content');
                if (contentEl) {
                    contentEl.textContent += event.content;
                }
            }
            
            // Auto-scroll to bottom
            this.scrollToBottom();
            
            if (this.options.onContentChunk) {
                this.options.onContentChunk(event);
            }
        }

        /**
         * Handle message complete event
         */
        onMessageComplete(event) {
            const messageEl = document.getElementById(`message-${event.messageId}`);
            if (messageEl) {
                // Remove streaming class
                messageEl.classList.remove('message-streaming');
                
                // Update content to final formatted version
                const contentEl = messageEl.querySelector('.message-streaming-content');
                if (contentEl) {
                    contentEl.classList.remove('message-streaming-content');
                    contentEl.classList.add('message-text');
                    // Convert markdown-like formatting
                    contentEl.innerHTML = this.formatContent(this.accumulatedContent);
                }
                
                // Add actions
                this.addMessageActions(messageEl, event.messageId);
            }
            
            this.hideStreamingIndicator();
            this.setInputEnabled(true);
            
            if (this.options.onMessageComplete) {
                this.options.onMessageComplete(event);
            }
        }

        /**
         * Handle cancel event
         */
        onCancel(event) {
            const messageEl = document.getElementById(`message-${event.messageId}`);
            if (messageEl) {
                messageEl.classList.add('message-cancelled');
                const contentEl = messageEl.querySelector('.message-streaming-content');
                if (contentEl) {
                    contentEl.innerHTML += '<span class="cancelled-indicator"> (cancelled)</span>';
                }
            }
            
            this.hideStreamingIndicator();
            this.setInputEnabled(true);
            
            if (this.options.onCancel) {
                this.options.onCancel(event);
            }
        }

        /**
         * Cancel the current streaming request
         */
        cancel() {
            if (!this.isStreaming || !this.currentStreamId) {
                return;
            }

            // Send cancel request to server
            fetch(`/api/chat/cancel/${this.currentStreamId}`, {
                method: 'POST'
            }).catch(error => {
                console.error('Error sending cancel request:', error);
            });

            // Clean up locally
            this.cleanup();
        }

        /**
         * Cleanup after streaming ends
         */
        cleanup() {
            this.isStreaming = false;
            
            if (this.currentStreamId) {
                activeStreams.delete(this.currentStreamId);
            }
            
            this.currentStreamId = null;
            this.currentMessageId = null;
            this.accumulatedContent = '';
            
            this.hideStreamingIndicator();
            this.setInputEnabled(true);
        }

        /**
         * Handle errors
         */
        handleError(error) {
            console.error('Streaming error:', error);
            
            // Add error message to chat
            const errorHtml = `
                <div class="chat-message message-error">
                    <div class="message-avatar avatar-system">⚠️</div>
                    <div class="message-wrapper">
                        <div class="message-header">
                            <span class="message-author">System</span>
                        </div>
                        <div class="message-body">
                            <div class="message-error-content">
                                <p>Error: ${this.escapeHtml(error.message)}</p>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            this.appendToChat(errorHtml);
            
            if (this.options.onError) {
                this.options.onError(error);
            }
            
            this.cleanup();
        }

        /**
         * Add user message to chat
         */
        addUserMessage(command) {
            const messageId = 'user-' + Date.now();
            const html = `
                <div class="chat-message message-user" id="message-${messageId}">
                    <div class="message-avatar avatar-user">👤</div>
                    <div class="message-wrapper">
                        <div class="message-header">
                            <span class="message-author">You</span>
                            <time class="message-time">${new Date().toLocaleTimeString()}</time>
                        </div>
                        <div class="message-body">
                            <div class="message-text">
                                <p>${this.escapeHtml(command)}</p>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            this.appendToChat(html);
        }

        /**
         * Create streaming message element
         */
        createStreamingMessageElement(messageId) {
            return `
                <div class="chat-message message-ai message-streaming" id="message-${messageId}">
                    <div class="message-avatar avatar-ai">🤖</div>
                    <div class="message-wrapper">
                        <div class="message-header">
                            <span class="message-author">GMSD</span>
                            <time class="message-time">${new Date().toLocaleTimeString()}</time>
                            <span class="streaming-badge">streaming</span>
                        </div>
                        <div class="message-body">
                            <div class="message-streaming-content"></div>
                            <span class="cursor-blink">▊</span>
                        </div>
                        <div class="message-actions">
                            <button class="message-action-btn cancel-stream-btn" title="Cancel response">
                                <span>⏹</span>
                                <span class="action-label">Stop</span>
                            </button>
                        </div>
                    </div>
                </div>
            `;
        }

        /**
         * Add message actions after streaming completes
         */
        addMessageActions(messageEl, messageId) {
            const actionsEl = messageEl.querySelector('.message-actions');
            if (actionsEl) {
                actionsEl.innerHTML = `
                    <button class="message-action-btn" data-action="copy" data-message-id="${messageId}" 
                            title="Copy to clipboard">
                        <span>📋</span>
                        <span class="action-label">Copy</span>
                    </button>
                    <button class="message-action-btn" data-action="feedback-positive" data-message-id="${messageId}" 
                            title="This was helpful">
                        <span>👍</span>
                    </button>
                    <button class="message-action-btn" data-action="feedback-negative" data-message-id="${messageId}" 
                            title="This was not helpful">
                        <span>👎</span>
                    </button>
                `;
            }
        }

        /**
         * Show streaming indicator in UI
         */
        showStreamingIndicator() {
            // Hide any existing indicators first
            this.hideStreamingIndicator();
            
            const chatContainer = document.querySelector('.chat-thread-content, .message-thread');
            if (chatContainer) {
                const indicator = document.createElement('div');
                indicator.className = 'streaming-indicator active';
                indicator.id = 'streaming-indicator';
                indicator.innerHTML = `
                    <div class="typing-dots">
                        <span></span>
                        <span></span>
                        <span></span>
                    </div>
                    <span class="streaming-text">Thinking...</span>
                    <button class="cancel-stream-btn-global" title="Cancel">✕</button>
                `;
                chatContainer.appendChild(indicator);
                
                // Bind cancel button
                const cancelBtn = indicator.querySelector('.cancel-stream-btn-global');
                if (cancelBtn) {
                    cancelBtn.addEventListener('click', () => this.cancel());
                }
                
                this.scrollToBottom();
            }
        }

        /**
         * Hide streaming indicator
         */
        hideStreamingIndicator() {
            const indicator = document.getElementById('streaming-indicator');
            if (indicator) {
                indicator.remove();
            }
        }

        /**
         * Enable/disable input during streaming
         */
        setInputEnabled(enabled) {
            const input = document.getElementById('chat-input');
            const sendBtn = document.querySelector('.send-btn');
            
            if (input) {
                input.disabled = !enabled;
            }
            if (sendBtn) {
                sendBtn.disabled = !enabled;
            }
        }

        /**
         * Append HTML to chat container
         */
        appendToChat(html) {
            const container = document.querySelector('.messages-list, .message-thread');
            if (container) {
                container.insertAdjacentHTML('beforeend', html);
                this.scrollToBottom();
            }
        }

        /**
         * Scroll chat to bottom
         */
        scrollToBottom() {
            const viewport = document.querySelector('.chat-thread-viewport, .chat-container');
            if (viewport) {
                viewport.scrollTop = viewport.scrollHeight;
            }
        }

        /**
         * Format content with basic markdown
         */
        formatContent(content) {
            // Basic markdown formatting
            let html = this.escapeHtml(content);
            
            // Bold
            html = html.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
            // Italic
            html = html.replace(/\*(.+?)\*/g, '<em>$1</em>');
            // Code
            html = html.replace(/`(.+?)`/g, '<code>$1</code>');
            // Line breaks
            html = html.replace(/\n/g, '<br>');
            
            return html;
        }

        /**
         * Escape HTML entities
         */
        escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        /**
         * Generate unique stream ID
         */
        generateStreamId() {
            return 'stream-' + Date.now() + '-' + Math.random().toString(36).substr(2, 9);
        }
    }

    // ============================================
    // Public API
    // ============================================
    window.StreamingChat = {
        controller: null,

        /**
         * Initialize streaming chat
         * @param {Object} options - Configuration options
         */
        init(options = {}) {
            this.controller = new StreamingChatController(options);
            this.bindEvents();
            return this.controller;
        },

        /**
         * Bind event listeners
         */
        bindEvents() {
            // Override form submission to use streaming
            const form = document.getElementById('chat-form');
            if (form && this.controller) {
                form.addEventListener('submit', (e) => {
                    e.preventDefault();
                    const input = document.getElementById('chat-input');
                    if (input && input.value.trim()) {
                        this.controller.streamCommand(input.value.trim());
                        input.value = '';
                    }
                });
            }

            // Delegate click handler for cancel buttons
            document.addEventListener('click', (e) => {
                const cancelBtn = e.target.closest('.cancel-stream-btn');
                if (cancelBtn && this.controller) {
                    this.controller.cancel();
                }
            });
        },

        /**
         * Stream a command
         * @param {string} command - Command to stream
         */
        stream(command) {
            if (this.controller) {
                this.controller.streamCommand(command);
            }
        },

        /**
         * Cancel current stream
         */
        cancel() {
            if (this.controller) {
                this.controller.cancel();
            }
        },

        /**
         * Check if currently streaming
         * @returns {boolean}
         */
        isStreaming() {
            return this.controller ? this.controller.isStreaming : false;
        }
    };

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            if (document.querySelector('#chat-form')) {
                window.StreamingChat.init();
            }
        });
    } else {
        if (document.querySelector('#chat-form')) {
            window.StreamingChat.init();
        }
    }
})();
