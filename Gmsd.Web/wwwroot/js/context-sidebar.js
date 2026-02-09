/**
 * ContextSidebar - Dynamic Sidebar Controller
 * Handles workspace status, recent runs, and section persistence
 */

(function() {
    'use strict';

    const STORAGE_KEY = 'gmsd_sidebar_sections';
    const POLLING_INTERVAL = 30000; // 30 seconds

    class ContextSidebarController {
        constructor() {
            this.container = document.getElementById('context-sidebar');
            this.pollingTimer = null;
            this.init();
        }

        init() {
            if (!this.container) {
                console.warn('Context sidebar container not found');
                return;
            }

            this.loadSectionStates();
            this.bindEvents();
            this.startPolling();
        }

        /**
         * Load saved section collapse states from localStorage
         */
        loadSectionStates() {
            try {
                const stored = localStorage.getItem(STORAGE_KEY);
                if (stored) {
                    const states = JSON.parse(stored);
                    this.applySectionStates(states);
                }
            } catch (e) {
                console.warn('Failed to load sidebar section states:', e);
            }
        }

        /**
         * Apply section states to DOM
         */
        applySectionStates(states) {
            Object.entries(states).forEach(([section, isCollapsed]) => {
                const sectionEl = this.container?.querySelector(`.sidebar-section[data-section="${section}"]`);
                if (sectionEl) {
                    sectionEl.classList.toggle('collapsed', isCollapsed);
                    const toggle = sectionEl.querySelector('.section-toggle');
                    if (toggle) {
                        toggle.setAttribute('aria-expanded', !isCollapsed);
                    }
                }
            });
        }

        /**
         * Save section states to localStorage
         */
        saveSectionState(section, isCollapsed) {
            try {
                const stored = localStorage.getItem(STORAGE_KEY);
                const states = stored ? JSON.parse(stored) : {};
                states[section] = isCollapsed;
                localStorage.setItem(STORAGE_KEY, JSON.stringify(states));
            } catch (e) {
                console.warn('Failed to save sidebar section state:', e);
            }
        }

        /**
         * Bind event handlers
         */
        bindEvents() {
            // Section toggle handlers
            this.container?.querySelectorAll('.section-toggle').forEach(toggle => {
                toggle.addEventListener('click', (e) => {
                    const section = e.currentTarget.closest('.sidebar-section');
                    const sectionName = section?.dataset.section;
                    if (!section || !sectionName) return;

                    const isCollapsed = section.classList.toggle('collapsed');
                    e.currentTarget.setAttribute('aria-expanded', !isCollapsed);
                    this.saveSectionState(sectionName, isCollapsed);
                });
            });

            // Quick action handlers
            this.container?.querySelectorAll('.quick-action-btn[data-command]').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    const command = e.currentTarget.dataset.command;
                    if (command) {
                        this.executeCommand(command);
                    }
                });
            });

            // Run item click handlers
            this.container?.querySelectorAll('.run-item').forEach(item => {
                item.addEventListener('click', () => {
                    const runId = item.dataset.runId;
                    if (runId) {
                        this.viewRun(runId);
                    }
                });
            });

            // Listen for workspace change events
            document.addEventListener('workspace-changed', () => {
                this.refresh();
            });

            // Listen for conversation context updates from context-aware panels
            document.addEventListener('conversation-context-updated', (e) => {
                this.handleContextUpdate(e.detail);
            });

            // Listen for HTMX events to update after refresh
            this.container?.addEventListener('htmx:afterSwap', () => {
                this.loadSectionStates();
                this.bindDynamicEvents();
                // Re-apply any active highlights after refresh
                this.reapplyHighlights();
            });
        }

        /**
         * Bind events to dynamically loaded content
         */
        bindDynamicEvents() {
            // Quick action handlers for dynamically loaded content
            this.container?.querySelectorAll('.quick-action-btn[data-command]').forEach(btn => {
                // Remove existing listeners to avoid duplicates
                const newBtn = btn.cloneNode(true);
                btn.parentNode?.replaceChild(newBtn, btn);
                
                newBtn.addEventListener('click', (e) => {
                    const command = e.currentTarget.dataset.command;
                    if (command) {
                        this.executeCommand(command);
                    }
                });
            });

            // Run item click handlers
            this.container?.querySelectorAll('.run-item').forEach(item => {
                const newItem = item.cloneNode(true);
                item.parentNode?.replaceChild(newItem, item);
                
                newItem.addEventListener('click', () => {
                    const runId = newItem.dataset.runId;
                    if (runId) {
                        this.viewRun(runId);
                    }
                });
            });
        }

        /**
         * Execute a command via chat input
         */
        executeCommand(command) {
            if (window.mainLayout && window.mainLayout.insertCommand) {
                window.mainLayout.insertCommand(command);
            } else {
                const chatInput = document.getElementById('chat-input');
                if (chatInput) {
                    chatInput.value = command;
                    chatInput.focus();
                    // Trigger auto-resize if available
                    if (window.mainLayout && window.mainLayout.autoResizeTextarea) {
                        window.mainLayout.autoResizeTextarea();
                    }
                }
            }
        }

        /**
         * Navigate to run details
         */
        viewRun(runId) {
            // Check if detail panel is available
            if (window.detailPanel) {
                window.detailPanel.showRun(runId);
            } else {
                // Fallback: navigate to runs page
                window.location.href = `/Runs/Details?id=${encodeURIComponent(runId)}`;
            }
        }

        /**
         * Start polling for updates
         */
        startPolling() {
            // HTMX handles polling via hx-trigger, but we can also do manual refresh
            this.pollingTimer = setInterval(() => {
                this.refresh();
            }, POLLING_INTERVAL);
        }

        /**
         * Stop polling
         */
        stopPolling() {
            if (this.pollingTimer) {
                clearInterval(this.pollingTimer);
                this.pollingTimer = null;
            }
        }

        /**
         * Refresh sidebar content
         */
        refresh() {
            // Preserve current scroll position
            const scrollPos = this.container?.scrollTop;
            
            // Trigger HTMX refresh if available
            if (window.htmx) {
                window.htmx.trigger(this.container, 'refresh');
            }
            
            // Restore scroll position after refresh
            if (scrollPos !== undefined && this.container) {
                setTimeout(() => {
                    this.container.scrollTop = scrollPos;
                }, 100);
            }
        }

        /**
         * Update workspace status display
         */
        updateWorkspaceStatus(workspace) {
            const statusEl = this.container?.querySelector('#workspace-section-content');
            if (!statusEl) return;

            if (!workspace) {
                statusEl.innerHTML = `
                    <div class="workspace-empty">
                        <span class="empty-icon">📂</span>
                        <p>No workspace selected</p>
                        <a href="/Workspace" class="btn btn-primary btn-sm">Select Workspace</a>
                    </div>
                `;
                return;
            }

            const cursorHtml = workspace.cursor ? `
                <div class="workspace-cursor">
                    <span class="cursor-label">Cursor:</span>
                    <span class="cursor-value">${this.escapeHtml(workspace.cursor)}</span>
                </div>
            ` : '';

            statusEl.innerHTML = `
                <div class="workspace-status-card ${this.escapeHtml(workspace.statusClass)}">
                    <div class="workspace-header">
                        <span class="workspace-name" title="${this.escapeHtml(workspace.path)}">${this.escapeHtml(workspace.name)}</span>
                        <span class="workspace-status-badge ${this.escapeHtml(workspace.statusClass)}">${this.escapeHtml(workspace.status)}</span>
                    </div>
                    
                    <div class="workspace-stats">
                        <div class="stat-item">
                            <span class="stat-icon">📁</span>
                            <span class="stat-value">${workspace.projectCount}</span>
                            <span class="stat-label">Projects</span>
                        </div>
                        <div class="stat-item">
                            <span class="stat-icon">🐛</span>
                            <span class="stat-value">${workspace.openIssueCount}</span>
                            <span class="stat-label">Issues</span>
                        </div>
                    </div>
                    
                    ${cursorHtml}
                    
                    <div class="workspace-activity">
                        <span class="activity-label">Last activity:</span>
                        <span class="activity-time">${this.escapeHtml(workspace.relativeTime)}</span>
                    </div>
                    
                    <a href="/Workspace" class="workspace-link">Manage Workspace →</a>
                </div>
            `;
        }

        /**
         * Update recent runs list
         */
        updateRecentRuns(runs) {
            const runsEl = this.container?.querySelector('#recent-runs-content');
            if (!runsEl) return;

            if (!runs || runs.length === 0) {
                runsEl.innerHTML = `
                    <div class="runs-empty">
                        <span class="empty-icon">▶</span>
                        <p>No recent runs</p>
                        <span class="hint">Execute a command to start a run</span>
                    </div>
                `;
                return;
            }

            const runsHtml = runs.map(run => `
                <li class="run-item ${this.escapeHtml(run.statusClass)}" data-run-id="${this.escapeHtml(run.runId)}">
                    <div class="run-status-icon">${run.statusIcon}</div>
                    <div class="run-info">
                        <span class="run-description" title="${this.escapeHtml(run.runId)}">${this.escapeHtml(run.description)}</span>
                        <div class="run-meta">
                            <span class="run-time">${this.escapeHtml(run.relativeTime)}</span>
                            ${run.duration ? `<span class="run-duration">• ${this.escapeHtml(run.duration)}</span>` : ''}
                        </div>
                    </div>
                </li>
            `).join('');

            runsEl.innerHTML = `
                <ul class="recent-runs-list">
                    ${runsHtml}
                </ul>
                <a href="/Runs" class="view-all-link">View all runs →</a>
            `;

            // Re-bind click handlers
            runsEl.querySelectorAll('.run-item').forEach(item => {
                item.addEventListener('click', () => {
                    const runId = item.dataset.runId;
                    if (runId) {
                        this.viewRun(runId);
                    }
                });
            });
        }

        /**
         * Handle conversation context updates from ContextAwarePanels
         */
        handleContextUpdate(detail) {
            const { mentions, recentContext, allMentions } = detail || {};
            if (!mentions || mentions.length === 0) return;

            // Highlight mentioned items in sidebar
            this.highlightMentionedItems(mentions);

            // Update mentioned entities section if it exists
            this.updateMentionedEntitiesSection(allMentions);
        }

        /**
         * Highlight items that were mentioned in chat
         */
        highlightMentionedItems(mentions) {
            // Clear existing highlights
            this.clearHighlights();

            // Highlight runs
            mentions.filter(m => m.type === 'run').forEach(mention => {
                const runItem = this.container?.querySelector(`.run-item[data-run-id*="${mention.id}"]`);
                if (runItem) {
                    runItem.classList.add('mention-highlight');
                    runItem.setAttribute('data-mention-highlight', 'true');

                    // Expand the runs section if collapsed
                    const runsSection = runItem.closest('.sidebar-section[data-section="runs"]');
                    if (runsSection?.classList.contains('collapsed')) {
                        runsSection.classList.remove('collapsed');
                        const toggle = runsSection.querySelector('.section-toggle');
                        if (toggle) {
                            toggle.setAttribute('aria-expanded', 'true');
                        }
                    }

                    // Scroll into view smoothly
                    setTimeout(() => {
                        runItem.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                    }, 100);
                }
            });

            // Store current highlights for reapplication after refresh
            this.storeActiveHighlights(mentions);
        }

        /**
         * Store active highlights for reapplication
         */
        storeActiveHighlights(mentions) {
            try {
                const highlightData = mentions.map(m => ({ type: m.type, id: m.id }));
                sessionStorage.setItem('gmsd_active_highlights', JSON.stringify({
                    mentions: highlightData,
                    timestamp: Date.now()
                }));
            } catch (e) {
                console.warn('Failed to store highlights:', e);
            }
        }

        /**
         * Reapply highlights after content refresh
         */
        reapplyHighlights() {
            try {
                const stored = sessionStorage.getItem('gmsd_active_highlights');
                if (!stored) return;

                const data = JSON.parse(stored);
                // Only reapply if within last 5 seconds
                if (Date.now() - data.timestamp > 5000) {
                    sessionStorage.removeItem('gmsd_active_highlights');
                    return;
                }

                if (data.mentions) {
                    this.highlightMentionedItems(data.mentions);
                }
            } catch (e) {
                console.warn('Failed to reapply highlights:', e);
            }
        }

        /**
         * Clear all highlights
         */
        clearHighlights() {
            this.container?.querySelectorAll('.mention-highlight').forEach(el => {
                el.classList.remove('mention-highlight');
                el.removeAttribute('data-mention-highlight');
            });
        }

        /**
         * Update or create the mentioned entities section
         */
        updateMentionedEntitiesSection(allMentions) {
            if (!allMentions || allMentions.length === 0) return;

            // Sort by mention count, take top 5
            const topMentions = allMentions
                .sort((a, b) => b.mentionCount - a.mentionCount)
                .slice(0, 5);

            let section = this.container?.querySelector('.sidebar-section[data-section="mentions"]');

            // Create section if it doesn't exist
            if (!section) {
                section = document.createElement('div');
                section.className = 'sidebar-section';
                section.setAttribute('data-section', 'mentions');
                section.innerHTML = `
                    <h3 class="section-title">
                        <button class="section-toggle" aria-expanded="true">
                            <span class="toggle-icon">▼</span>
                            Mentioned in Chat
                        </button>
                    </h3>
                    <div class="section-content mentioned-entities-section" id="mentioned-entities-content"></div>
                `;

                // Insert after workspace section
                const workspaceSection = this.container?.querySelector('.sidebar-section[data-section="workspace"]');
                if (workspaceSection && workspaceSection.nextSibling) {
                    workspaceSection.parentNode?.insertBefore(section, workspaceSection.nextSibling);
                } else {
                    this.container?.insertBefore(section, this.container.firstChild);
                }

                // Bind toggle handler
                const toggle = section.querySelector('.section-toggle');
                if (toggle) {
                    toggle.addEventListener('click', (e) => {
                        const sectionEl = e.currentTarget.closest('.sidebar-section');
                        const sectionName = sectionEl?.dataset.section;
                        if (!sectionEl || !sectionName) return;

                        const isCollapsed = sectionEl.classList.toggle('collapsed');
                        e.currentTarget.setAttribute('aria-expanded', !isCollapsed);
                        this.saveSectionState(sectionName, isCollapsed);
                    });
                }
            }

            // Update content
            const contentEl = section.querySelector('#mentioned-entities-content');
            if (contentEl) {
                contentEl.innerHTML = topMentions.map(mention => {
                    const icon = this.getEntityIcon(mention.type);
                    return `
                        <div class="mentioned-entity-item" data-entity-type="${mention.type}" data-entity-id="${mention.id}">
                            <span class="entity-icon">${icon}</span>
                            <span class="entity-name">${mention.type}: ${mention.id}</span>
                            <span class="mention-count">${mention.mentionCount}</span>
                        </div>
                    `;
                }).join('');

                // Bind click handlers
                contentEl.querySelectorAll('.mentioned-entity-item').forEach(item => {
                    item.addEventListener('click', () => {
                        const type = item.dataset.entityType;
                        const id = item.dataset.entityId;
                        if (type && id && window.DetailPanel) {
                            window.DetailPanel.showEntity(type, id);
                        }
                    });
                });
            }
        }

        /**
         * Get icon for entity type
         */
        getEntityIcon(type) {
            const icons = {
                run: '▶',
                project: '📁',
                task: '☐',
                spec: '📄',
                issue: '🐛',
                milestone: '🎯',
                phase: '🗓',
                checkpoint: '✓'
            };
            return icons[type] || '📋';
        }

        /**
         * Escape HTML entities
         */
        escapeHtml(text) {
            if (!text) return '';
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        /**
         * Destroy the controller
         */
        destroy() {
            this.stopPolling();
        }
    }

    // ============================================
    // Public API
    // ============================================
    window.ContextSidebar = {
        controller: null,

        /**
         * Initialize the context sidebar
         */
        init() {
            this.controller = new ContextSidebarController();
            return this.controller;
        },

        /**
         * Refresh the sidebar content
         */
        refresh() {
            this.controller?.refresh();
        },

        /**
         * Update workspace status
         */
        updateWorkspace(workspace) {
            this.controller?.updateWorkspaceStatus(workspace);
        },

        /**
         * Update recent runs
         */
        updateRuns(runs) {
            this.controller?.updateRecentRuns(runs);
        },

        /**
         * Collapse/expand a section
         */
        toggleSection(section, collapsed) {
            const sectionEl = document.querySelector(`.sidebar-section[data-section="${section}"]`);
            if (sectionEl) {
                sectionEl.classList.toggle('collapsed', collapsed);
                const toggle = sectionEl.querySelector('.section-toggle');
                if (toggle) {
                    toggle.setAttribute('aria-expanded', !collapsed);
                }
                this.controller?.saveSectionState(section, collapsed);
            }
        },

        /**
         * Highlight mentioned items in sidebar
         */
        highlightMentions(mentions) {
            this.controller?.highlightMentionedItems(mentions);
        },

        /**
         * Clear all highlights
         */
        clearHighlights() {
            this.controller?.clearHighlights();
        },

        /**
         * Destroy the controller
         */
        destroy() {
            this.controller?.destroy();
            this.controller = null;
        }
    };

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            window.ContextSidebar.init();
        });
    } else {
        window.ContextSidebar.init();
    }
})();
