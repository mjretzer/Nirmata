/**
 * ChatThread - Virtual Scrolling and Message Interaction Controller
 * Handles virtual scrolling, message actions, and performance optimization
 */

(function() {
    'use strict';

    // Virtual scrolling configuration
    const VIRTUAL_SCROLL_CONFIG = {
        itemHeight: 100,      // Estimated average message height
        overscan: 5,          // Number of items to render outside viewport
        maxBufferSize: 100,  // Maximum items to keep in DOM
        scrollThreshold: 100  // Pixels from bottom to show scroll button
    };

    class ChatThreadController {
        constructor(containerId, options = {}) {
            this.containerId = containerId;
            this.container = document.getElementById(containerId);
            if (!this.container) {
                console.warn(`ChatThread container not found: ${containerId}`);
                return;
            }

            this.viewport = this.container.querySelector('.chat-thread-viewport');
            this.content = this.container.querySelector('.chat-thread-content');
            this.scrollBtn = this.container.querySelector('.scroll-to-bottom');
            
            this.options = { ...VIRTUAL_SCROLL_CONFIG, ...options };
            this.messages = [];
            this.visibleRange = { start: 0, end: 0 };
            this.isNearBottom = true;
            this.resizeObserver = null;
            this.intersectionObserver = null;

            this.init();
        }

        init() {
            this.bindEvents();
            this.setupObservers();
            this.scrollToBottom();
        }

        bindEvents() {
            // Viewport scroll handling
            if (this.viewport) {
                this.viewport.addEventListener('scroll', this.handleScroll.bind(this), { passive: true });
            }

            // Scroll to bottom button
            if (this.scrollBtn) {
                this.scrollBtn.addEventListener('click', () => this.scrollToBottom());
            }

            // Message action buttons (delegated)
            this.container.addEventListener('click', (e) => {
                const actionBtn = e.target.closest('.message-action-btn');
                if (actionBtn) {
                    this.handleMessageAction(actionBtn);
                }

                const metadataToggle = e.target.closest('.metadata-toggle');
                if (metadataToggle) {
                    this.toggleMetadata(metadataToggle);
                }

                const suggestedAction = e.target.closest('.suggested-action');
                if (suggestedAction) {
                    this.handleSuggestedAction(suggestedAction);
                }
            });

            // Copy action with keyboard shortcut
            document.addEventListener('keydown', (e) => {
                if ((e.metaKey || e.ctrlKey) && e.key === 'c' && window.getSelection().toString() === '') {
                    const focusedMessage = document.activeElement?.closest('.chat-message');
                    if (focusedMessage) {
                        this.copyMessageContent(focusedMessage.dataset.messageId);
                    }
                }
            });
        }

        setupObservers() {
            // Resize observer for virtual scrolling calculations
            if ('ResizeObserver' in window && this.viewport) {
                this.resizeObserver = new ResizeObserver((entries) => {
                    for (const entry of entries) {
                        this.updateVisibleRange();
                    }
                });
                this.resizeObserver.observe(this.viewport);
            }

            // Intersection observer for lazy loading/mounting
            this.intersectionObserver = new IntersectionObserver(
                (entries) => this.handleIntersection(entries),
                { root: this.viewport, threshold: 0.1 }
            );

            // Observe all messages
            this.container.querySelectorAll('.chat-message').forEach(msg => {
                this.intersectionObserver.observe(msg);
            });
        }

        handleScroll() {
            if (!this.viewport) return;

            const { scrollTop, scrollHeight, clientHeight } = this.viewport;
            const distanceFromBottom = scrollHeight - scrollTop - clientHeight;
            
            this.isNearBottom = distanceFromBottom < this.options.scrollThreshold;

            // Show/hide scroll to bottom button
            if (this.scrollBtn) {
                this.scrollBtn.classList.toggle('visible', !this.isNearBottom);
            }

            // Update visible range for virtual scrolling
            this.updateVisibleRange();

            // Load more messages when near top (pagination)
            if (scrollTop < 100) {
                this.loadMoreMessages();
            }
        }

        updateVisibleRange() {
            if (!this.viewport || !this.content) return;

            const messages = this.content.querySelectorAll('.chat-message');
            if (messages.length === 0) return;

            const viewportRect = this.viewport.getBoundingClientRect();
            const viewportTop = viewportRect.top;
            const viewportBottom = viewportRect.bottom;

            let startIndex = 0;
            let endIndex = messages.length - 1;

            // Find visible range
            for (let i = 0; i < messages.length; i++) {
                const msgRect = messages[i].getBoundingClientRect();
                if (msgRect.bottom >= viewportTop && msgRect.top <= viewportBottom) {
                    startIndex = Math.max(0, i - this.options.overscan);
                    break;
                }
            }

            for (let i = messages.length - 1; i >= 0; i--) {
                const msgRect = messages[i].getBoundingClientRect();
                if (msgRect.top <= viewportBottom && msgRect.bottom >= viewportTop) {
                    endIndex = Math.min(messages.length - 1, i + this.options.overscan);
                    break;
                }
            }

            // Update visibility of messages outside range (simple virtualization)
            messages.forEach((msg, index) => {
                const shouldRender = index >= startIndex && index <= endIndex;
                msg.style.display = shouldRender ? '' : 'none';
                
                if (shouldRender && !msg.dataset.mounted) {
                    msg.dataset.mounted = 'true';
                    // Trigger any mount animations
                    msg.classList.add('mounted');
                }
            });

            this.visibleRange = { start: startIndex, end: endIndex };
        }

        handleIntersection(entries) {
            entries.forEach(entry => {
                const message = entry.target;
                if (entry.isIntersecting) {
                    message.classList.add('in-viewport');
                    // Lazy load images or other heavy content here
                } else {
                    message.classList.remove('in-viewport');
                }
            });
        }

        scrollToBottom() {
            if (this.viewport) {
                this.viewport.scrollTo({
                    top: this.viewport.scrollHeight,
                    behavior: 'smooth'
                });
            }
        }

        handleMessageAction(button) {
            const action = button.dataset.action;
            const messageId = button.dataset.messageId;

            switch (action) {
                case 'copy':
                    this.copyMessageContent(messageId);
                    break;
                case 'feedback-positive':
                    this.submitFeedback(messageId, 'positive');
                    break;
                case 'feedback-negative':
                    this.submitFeedback(messageId, 'negative');
                    break;
                case 'retry':
                    this.retryMessage(messageId);
                    break;
            }
        }

        async copyMessageContent(messageId) {
            const messageEl = document.getElementById(`message-${messageId}`);
            if (!messageEl) return;

            const contentEl = messageEl.querySelector('.message-body');
            if (!contentEl) return;

            try {
                // Get text content, preserving formatting
                let text = '';
                const codeBlock = contentEl.querySelector('pre code');
                if (codeBlock) {
                    text = codeBlock.textContent;
                } else {
                    text = contentEl.textContent;
                }

                await navigator.clipboard.writeText(text);
                this.showToast('Message copied to clipboard', 'success');
            } catch (err) {
                console.error('Failed to copy:', err);
                this.showToast('Failed to copy message', 'error');
            }
        }

        async submitFeedback(messageId, type) {
            try {
                // Update UI immediately
                const messageEl = document.getElementById(`message-${messageId}`);
                if (messageEl) {
                    const buttons = messageEl.querySelectorAll('[data-action^="feedback-"]');
                    buttons.forEach(btn => {
                        btn.disabled = true;
                        if (btn.dataset.action === `feedback-${type}`) {
                            btn.classList.add('active');
                        }
                    });
                }

                // Send feedback to server
                await fetch('/api/chat/feedback', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ messageId, type })
                });

                this.showToast('Thank you for your feedback!', 'success');
            } catch (err) {
                console.error('Failed to submit feedback:', err);
                this.showToast('Failed to submit feedback', 'error');
            }
        }

        async retryMessage(messageId) {
            try {
                // Show loading state
                const messageEl = document.getElementById(`message-${messageId}`);
                if (messageEl) {
                    messageEl.classList.add('retrying');
                }

                // Request retry from server
                const response = await fetch(`/api/chat/retry/${messageId}`, {
                    method: 'POST'
                });

                if (response.ok) {
                    // Remove the message and trigger regeneration
                    if (messageEl) {
                        messageEl.remove();
                    }
                    this.showToast('Regenerating response...', 'info');
                } else {
                    throw new Error('Retry failed');
                }
            } catch (err) {
                console.error('Failed to retry:', err);
                this.showToast('Failed to retry message', 'error');
            }
        }

        toggleMetadata(button) {
            const targetId = button.dataset.target;
            const metadataEl = document.getElementById(targetId);
            if (metadataEl) {
                metadataEl.classList.toggle('collapsed');
                button.setAttribute('aria-expanded', !metadataEl.classList.contains('collapsed'));
            }
        }

        handleSuggestedAction(button) {
            const command = button.dataset.command;
            if (command && window.mainLayout) {
                window.mainLayout.insertCommand(command);
            }
        }

        loadMoreMessages() {
            const loadMoreBtn = this.container.querySelector('.load-more-btn');
            if (loadMoreBtn && !loadMoreBtn.classList.contains('htmx-request')) {
                loadMoreBtn.click();
            }
        }

        appendMessage(html) {
            const messagesList = this.content?.querySelector('.messages-list');
            if (!messagesList) return;

            // Check if we should auto-scroll
            const shouldScroll = this.isNearBottom;

            // Append the message
            messagesList.insertAdjacentHTML('beforeend', html);

            // Observe new message
            const newMessage = messagesList.lastElementChild;
            if (newMessage && this.intersectionObserver) {
                this.intersectionObserver.observe(newMessage);
            }

            // Scroll to bottom if we were already near bottom
            if (shouldScroll) {
                this.scrollToBottom();
            }

            // Update visible range
            this.updateVisibleRange();
        }

        updateStreamingMessage(messageId, content) {
            const messageEl = document.getElementById(`message-${messageId}`);
            if (!messageEl) return;

            const contentEl = messageEl.querySelector('.message-body');
            if (contentEl) {
                contentEl.innerHTML = content;
            }

            // Auto-scroll if near bottom
            if (this.isNearBottom) {
                this.scrollToBottom();
            }
        }

        removeMessage(messageId) {
            const messageEl = document.getElementById(`message-${messageId}`);
            if (messageEl) {
                if (this.intersectionObserver) {
                    this.intersectionObserver.unobserve(messageEl);
                }
                messageEl.remove();
                this.updateVisibleRange();
            }
        }

        clearMessages() {
            const messagesList = this.content?.querySelector('.messages-list');
            if (messagesList) {
                const messages = messagesList.querySelectorAll('.chat-message');
                messages.forEach(msg => {
                    if (this.intersectionObserver) {
                        this.intersectionObserver.unobserve(msg);
                    }
                });
                messagesList.innerHTML = '';
            }
        }

        showToast(message, type = 'info') {
            // Use global toast system if available
            if (window.showToast) {
                window.showToast(message, type);
            } else {
                console.log(`[${type}] ${message}`);
            }
        }

        destroy() {
            if (this.resizeObserver) {
                this.resizeObserver.disconnect();
            }
            if (this.intersectionObserver) {
                this.intersectionObserver.disconnect();
            }
        }

        // Performance monitoring
        getPerformanceMetrics() {
            const messages = this.content?.querySelectorAll('.chat-message') || [];
            return {
                totalMessages: messages.length,
                visibleMessages: this.visibleRange.end - this.visibleRange.start + 1,
                visibleRange: this.visibleRange,
                isNearBottom: this.isNearBottom,
                containerId: this.containerId
            };
        }
    }

    // Global ChatThread Manager
    window.ChatThread = {
        controllers: new Map(),

        init(containerId, options) {
            const controller = new ChatThreadController(containerId, options);
            this.controllers.set(containerId, controller);
            return controller;
        },

        get(containerId) {
            return this.controllers.get(containerId);
        },

        destroy(containerId) {
            const controller = this.controllers.get(containerId);
            if (controller) {
                controller.destroy();
                this.controllers.delete(containerId);
            }
        },

        destroyAll() {
            this.controllers.forEach(controller => controller.destroy());
            this.controllers.clear();
        },

        // Helper for HTMX integration
        onNewContent(containerId) {
            const controller = this.controllers.get(containerId);
            if (controller) {
                // Re-observe new messages
                controller.content?.querySelectorAll('.chat-message:not([data-mounted])').forEach(msg => {
                    if (controller.intersectionObserver) {
                        controller.intersectionObserver.observe(msg);
                    }
                });
                controller.updateVisibleRange();
            }
        }
    };

    // Auto-initialize chat threads on page load
    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('.chat-thread-container[data-thread-id]').forEach(container => {
            const threadId = container.dataset.threadId;
            const containerId = container.id;
            if (threadId && containerId) {
                window.ChatThread.init(containerId, {
                    itemHeight: 100,
                    overscan: 5
                });
            }
        });
    });

    // Cleanup on page unload
    window.addEventListener('beforeunload', () => {
        window.ChatThread.destroyAll();
    });

    // HTMX integration
    document.addEventListener('htmx:afterSwap', (event) => {
        const target = event.detail.target;
        if (target?.closest('.chat-thread-content')) {
            const container = target.closest('.chat-thread-container');
            if (container) {
                window.ChatThread.onNewContent(container.id);
            }
        }
    });

})();
