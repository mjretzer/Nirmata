/**
 * ContextAwarePanels - Smart panel content based on conversation
 * Parses chat messages for entity mentions and auto-updates panels
 */

(function() {
    'use strict';

    const STORAGE_KEY = 'gmsd_mentioned_entities';
    const CONTEXT_STORAGE_KEY = 'gmsd_conversation_context';

    // Entity patterns for parsing mentions
    const ENTITY_PATTERNS = {
        run: {
            patterns: [
                /(?:run|#)\s*[:#]?\s*([a-zA-Z0-9_-]+)/gi,
                /run[:\s]+([a-zA-Z0-9_-]{8,})/gi,
                /run\s+([a-zA-Z0-9_-]+)/gi
            ],
            priority: 1
        },
        project: {
            patterns: [
                /(?:project|proj)\s*[:#]?\s*([a-zA-Z0-9_-]+)/gi,
                /project[:\s]+([a-zA-Z0-9_-]+)/gi
            ],
            priority: 2
        },
        task: {
            patterns: [
                /(?:task|#)\s*[:#]?\s*(\d+)/gi,
                /task[:\s]+(\d+)/gi
            ],
            priority: 3
        },
        spec: {
            patterns: [
                /(?:spec|change)\s*[:#]?\s*([a-zA-Z0-9_-]+)/gi,
                /spec[:\s]+([a-zA-Z0-9_-]+)/gi
            ],
            priority: 4
        },
        issue: {
            patterns: [
                /(?:issue|bug|🐛)\s*[:#]?\s*(\d+)/gi,
                /issue[:\s]+(\d+)/gi
            ],
            priority: 5
        },
        milestone: {
            patterns: [
                /(?:milestone|m)\s*[:#]?\s*(\d+)/gi,
                /milestone[:\s]+(\d+)/gi
            ],
            priority: 6
        },
        phase: {
            patterns: [
                /(?:phase|p)\s*[:#]?\s*(\d+)/gi,
                /phase[:\s]+(\d+)/gi
            ],
            priority: 7
        },
        checkpoint: {
            patterns: [
                /(?:checkpoint|cp)\s*[:#]?\s*([a-zA-Z0-9_-]+)/gi,
                /checkpoint[:\s]+([a-zA-Z0-9_-]+)/gi
            ],
            priority: 8
        }
    };

    // Direct intent patterns (e.g., "show me run 123")
    const INTENT_PATTERNS = [
        {
            regex: /(?:show|view|open|display|get)\s+(?:me\s+)?(?:the\s+)?(?:run|project|task|spec|issue|milestone|phase|checkpoint)\s+[:#]?\s*([a-zA-Z0-9_-]+)/i,
            action: 'showEntity'
        },
        {
            regex: /(?:show|view|open|display|get)\s+(?:me\s+)?(?:the\s+)?(?:details?|info)\s+(?:of|for|about)\s+(?:the\s+)?(?:run|project|task|spec|issue|milestone|phase|checkpoint)?\s*[:#]?\s*([a-zA-Z0-9_-]+)/i,
            action: 'showEntity'
        }
    ];

    class ContextAwareController {
        constructor() {
            this.mentionedEntities = new Map();
            this.recentContext = [];
            this.sidebarHighlightTimer = null;
            this.autoUpdateEnabled = true;
            this.loadStoredContext();
        }

        /**
         * Initialize the context-aware system
         */
        init() {
            this.observeChatMessages();
            this.bindIntentHandlers();
            console.log('[ContextAwarePanels] Initialized');
        }

        /**
         * Parse text for entity mentions
         */
        parseMentions(text) {
            const mentions = [];

            for (const [entityType, config] of Object.entries(ENTITY_PATTERNS)) {
                for (const pattern of config.patterns) {
                    let match;
                    while ((match = pattern.exec(text)) !== null) {
                        const entityId = match[1];
                        const mentionText = match[0];
                        const index = match.index;

                        // Validate ID format based on entity type
                        if (this.isValidEntityId(entityType, entityId)) {
                            mentions.push({
                                type: entityType,
                                id: entityId,
                                text: mentionText,
                                index: index,
                                priority: config.priority
                            });
                        }
                    }
                    // Reset lastIndex for next pattern
                    pattern.lastIndex = 0;
                }
            }

            // Sort by priority (lower number = higher priority)
            mentions.sort((a, b) => a.priority - b.priority);

            // Remove duplicates by keeping first occurrence
            const seen = new Set();
            return mentions.filter(m => {
                const key = `${m.type}:${m.id}`;
                if (seen.has(key)) return false;
                seen.add(key);
                return true;
            });
        }

        /**
         * Validate entity ID format
         */
        isValidEntityId(type, id) {
            if (!id || id.length < 1) return false;

            switch (type) {
                case 'run':
                    return /^[a-zA-Z0-9_-]{4,}$/.test(id);
                case 'project':
                    return /^[a-zA-Z0-9_-]+$/.test(id);
                case 'task':
                case 'issue':
                case 'milestone':
                    return /^\d+$/.test(id);
                case 'spec':
                    return /^[a-zA-Z0-9_-]+$/.test(id);
                case 'phase':
                    return /^\d+$/.test(id);
                case 'checkpoint':
                    return /^[a-zA-Z0-9_-]+$/.test(id);
                default:
                    return true;
            }
        }

        /**
         * Observe chat messages for new entity mentions
         */
        observeChatMessages() {
            // Use MutationObserver to detect new messages
            const chatThread = document.getElementById('chat-thread');
            if (!chatThread) {
                console.warn('[ContextAwarePanels] Chat thread not found');
                return;
            }

            this.messageObserver = new MutationObserver((mutations) => {
                mutations.forEach((mutation) => {
                    mutation.addedNodes.forEach((node) => {
                        if (node.nodeType === Node.ELEMENT_NODE && node.classList.contains('chat-message')) {
                            this.processMessage(node);
                        }
                    });
                });
            });

            this.messageObserver.observe(chatThread, {
                childList: true,
                subtree: true
            });

            // Process existing messages
            chatThread.querySelectorAll('.chat-message').forEach(msg => {
                this.processMessage(msg);
            });
        }

        /**
         * Process a chat message for entity mentions
         */
        processMessage(messageEl) {
            const messageBody = messageEl.querySelector('.message-body');
            if (!messageBody) return;

            const text = messageBody.textContent || '';
            const mentions = this.parseMentions(text);

            if (mentions.length === 0) return;

            // Check for direct intent patterns
            const intent = this.parseIntent(text);

            // Record mentions
            mentions.forEach(mention => {
                this.recordMention(mention);
            });

            // Auto-update if direct intent detected
            if (intent && this.autoUpdateEnabled) {
                this.handleIntent(intent, mentions);
            }

            // Highlight mentions in the message
            this.highlightMentions(messageEl, mentions);

            // Update sidebar highlights
            this.highlightSidebarItems(mentions);

            // Update conversation context
            this.updateConversationContext(mentions);
        }

        /**
         * Parse user intent from message text
         */
        parseIntent(text) {
            for (const pattern of INTENT_PATTERNS) {
                const match = pattern.regex.exec(text);
                if (match) {
                    return {
                        action: pattern.action,
                        entityId: match[1],
                        fullMatch: match[0]
                    };
                }
            }
            return null;
        }

        /**
         * Handle detected user intent
         */
        handleIntent(intent, mentions) {
            if (intent.action === 'showEntity' && mentions.length > 0) {
                // Find the matching mention
                const primaryMention = mentions.find(m => m.id === intent.entityId) || mentions[0];

                // Auto-open detail panel after a short delay
                setTimeout(() => {
                    if (window.DetailPanel) {
                        window.DetailPanel.showEntity(primaryMention.type, primaryMention.id);
                    }
                }, 500);
            }
        }

        /**
         * Record an entity mention for context tracking
         */
        recordMention(mention) {
            const key = `${mention.type}:${mention.id}`;
            const existing = this.mentionedEntities.get(key);

            if (existing) {
                existing.mentionCount++;
                existing.lastMentionedAt = new Date().toISOString();
            } else {
                this.mentionedEntities.set(key, {
                    ...mention,
                    mentionCount: 1,
                    firstMentionedAt: new Date().toISOString(),
                    lastMentionedAt: new Date().toISOString()
                });
            }

            this.saveContext();
        }

        /**
         * Highlight entity mentions within a message
         */
        highlightMentions(messageEl, mentions) {
            // This would add clickable links to mentions in the message
            // For now, we add a data attribute for styling
            messageEl.dataset.hasMentions = 'true';
            messageEl.dataset.mentions = mentions.map(m => `${m.type}:${m.id}`).join(',');
        }

        /**
         * Highlight relevant items in the context sidebar
         */
        highlightSidebarItems(mentions) {
            const sidebar = document.getElementById('context-sidebar');
            if (!sidebar) return;

            // Clear previous highlights
            sidebar.querySelectorAll('[data-mention-highlight]').forEach(el => {
                el.removeAttribute('data-mention-highlight');
                el.classList.remove('mention-highlight');
            });

            // Highlight runs
            mentions.filter(m => m.type === 'run').forEach(mention => {
                const runItem = sidebar.querySelector(`.run-item[data-run-id*="${mention.id}"]`);
                if (runItem) {
                    runItem.setAttribute('data-mention-highlight', 'true');
                    runItem.classList.add('mention-highlight');

                    // Scroll into view if not visible
                    runItem.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                }
            });

            // Clear highlights after delay
            if (this.sidebarHighlightTimer) {
                clearTimeout(this.sidebarHighlightTimer);
            }
            this.sidebarHighlightTimer = setTimeout(() => {
                sidebar.querySelectorAll('.mention-highlight').forEach(el => {
                    el.classList.remove('mention-highlight');
                });
            }, 5000);
        }

        /**
         * Update conversation context with new mentions
         */
        updateConversationContext(mentions) {
            // Add to recent context (keep last 20)
            this.recentContext.unshift({
                timestamp: new Date().toISOString(),
                mentions: mentions
            });

            if (this.recentContext.length > 20) {
                this.recentContext = this.recentContext.slice(0, 20);
            }

            this.saveContext();

            // Dispatch event for other components
            document.dispatchEvent(new CustomEvent('conversation-context-updated', {
                detail: {
                    mentions: mentions,
                    recentContext: this.recentContext,
                    allMentions: Array.from(this.mentionedEntities.values())
                }
            }));
        }

        /**
         * Get cross-references for an entity
         */
        getCrossReferences(entityType, entityId) {
            const key = `${entityType}:${entityId}`;
            const entity = this.mentionedEntities.get(key);

            if (!entity) return [];

            // Find related entities that were mentioned together
            const related = [];
            for (const [otherKey, otherEntity] of this.mentionedEntities) {
                if (otherKey === key) continue;

                // Check if mentioned in same message
                const sameContext = this.recentContext.some(ctx =>
                    ctx.mentions.some(m => `${m.type}:${m.id}` === key) &&
                    ctx.mentions.some(m => `${m.type}:${m.id}` === otherKey)
                );

                if (sameContext) {
                    related.push({
                        ...otherEntity,
                        relationship: 'mentioned-together'
                    });
                }
            }

            return related;
        }

        /**
         * Get most frequently mentioned entities
         */
        getTopMentions(limit = 5) {
            const sorted = Array.from(this.mentionedEntities.values())
                .sort((a, b) => b.mentionCount - a.mentionCount);
            return sorted.slice(0, limit);
        }

        /**
         * Get recently mentioned entities
         */
        getRecentMentions(limit = 5) {
            const sorted = Array.from(this.mentionedEntities.values())
                .sort((a, b) => new Date(b.lastMentionedAt) - new Date(a.lastMentionedAt));
            return sorted.slice(0, limit);
        }

        /**
         * Enable/disable auto-update
         */
        setAutoUpdate(enabled) {
            this.autoUpdateEnabled = enabled;
        }

        /**
         * Manually show an entity in detail panel
         */
        showEntity(entityType, entityId) {
            if (window.DetailPanel) {
                window.DetailPanel.showEntity(entityType, entityId);
            }
        }

        /**
         * Save context to localStorage
         */
        saveContext() {
            try {
                const data = {
                    mentionedEntities: Array.from(this.mentionedEntities.entries()),
                    recentContext: this.recentContext,
                    savedAt: new Date().toISOString()
                };
                localStorage.setItem(CONTEXT_STORAGE_KEY, JSON.stringify(data));
            } catch (e) {
                console.warn('[ContextAwarePanels] Failed to save context:', e);
            }
        }

        /**
         * Load stored context from localStorage
         */
        loadStoredContext() {
            try {
                const stored = localStorage.getItem(CONTEXT_STORAGE_KEY);
                if (stored) {
                    const data = JSON.parse(stored);

                    if (data.mentionedEntities) {
                        this.mentionedEntities = new Map(data.mentionedEntities);
                    }

                    if (data.recentContext) {
                        this.recentContext = data.recentContext;
                    }
                }
            } catch (e) {
                console.warn('[ContextAwarePanels] Failed to load stored context:', e);
            }
        }

        /**
         * Clear all stored context
         */
        clearContext() {
            this.mentionedEntities.clear();
            this.recentContext = [];
            localStorage.removeItem(CONTEXT_STORAGE_KEY);
        }

        /**
         * Bind intent handlers (e.g., for command buttons)
         */
        bindIntentHandlers() {
            document.addEventListener('click', (e) => {
                const entityLink = e.target.closest('[data-entity-type][data-entity-id]');
                if (entityLink) {
                    e.preventDefault();
                    const type = entityLink.dataset.entityType;
                    const id = entityLink.dataset.entityId;
                    this.showEntity(type, id);
                }
            });
        }

        /**
         * Get context-aware suggestions for chat input
         */
        getSuggestions() {
            const suggestions = [];

            // Suggest most mentioned entities
            const topMentions = this.getTopMentions(3);
            topMentions.forEach(entity => {
                suggestions.push({
                    type: 'recent-mention',
                    label: `${entity.type}: ${entity.id}`,
                    command: `/show ${entity.type} ${entity.id}`,
                    description: `Mentioned ${entity.mentionCount} times`
                });
            });

            return suggestions;
        }
    }

    // ============================================
    // Public API
    // ============================================
    window.ContextAwarePanels = {
        controller: null,

        /**
         * Initialize context-aware panels
         */
        init() {
            this.controller = new ContextAwareController();
            this.controller.init();
            return this.controller;
        },

        /**
         * Parse text for entity mentions
         */
        parseMentions(text) {
            return this.controller?.parseMentions(text) || [];
        },

        /**
         * Show an entity in the detail panel
         */
        showEntity(entityType, entityId) {
            return this.controller?.showEntity(entityType, entityId);
        },

        /**
         * Get most mentioned entities
         */
        getTopMentions(limit) {
            return this.controller?.getTopMentions(limit) || [];
        },

        /**
         * Get recent mentions
         */
        getRecentMentions(limit) {
            return this.controller?.getRecentMentions(limit) || [];
        },

        /**
         * Get cross-references for an entity
         */
        getCrossReferences(entityType, entityId) {
            return this.controller?.getCrossReferences(entityType, entityId) || [];
        },

        /**
         * Enable/disable auto-update
         */
        setAutoUpdate(enabled) {
            this.controller?.setAutoUpdate(enabled);
        },

        /**
         * Get suggestions for chat input
         */
        getSuggestions() {
            return this.controller?.getSuggestions() || [];
        },

        /**
         * Clear all context
         */
        clearContext() {
            this.controller?.clearContext();
        },

        /**
         * Destroy the controller
         */
        destroy() {
            if (this.controller) {
                if (this.controller.messageObserver) {
                    this.controller.messageObserver.disconnect();
                }
                if (this.controller.sidebarHighlightTimer) {
                    clearTimeout(this.controller.sidebarHighlightTimer);
                }
                this.controller = null;
            }
        }
    };

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            window.ContextAwarePanels.init();
        });
    } else {
        window.ContextAwarePanels.init();
    }
})();
