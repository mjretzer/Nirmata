/**
 * DetailPanel - Right Panel Entity Detail Controller
 * Handles entity display, tab switching, and auto-population from chat context
 */

(function() {
    'use strict';

    const STORAGE_KEY = 'gmsd_detailpanel_state';
    const CONTEXT_KEY = 'gmsd_detailpanel_context';

    class DetailPanelController {
        constructor() {
            this.container = document.getElementById('detail-panel');
            this.contentContainer = document.getElementById('detail-panel-content');
            this.emptyState = document.getElementById('detail-empty-state');
            this.currentEntity = null;
            this.activeTab = 'properties';
            this.init();
        }

        init() {
            if (!this.container) {
                console.warn('Detail panel container not found');
                return;
            }

            this.loadState();
            this.bindEvents();
            this.restoreFromContext();
        }

        /**
         * Load saved panel state from localStorage
         */
        loadState() {
            try {
                const stored = localStorage.getItem(STORAGE_KEY);
                if (stored) {
                    const state = JSON.parse(stored);
                    this.activeTab = state.activeTab || 'properties';
                }
            } catch (e) {
                console.warn('Failed to load detail panel state:', e);
            }
        }

        /**
         * Save panel state to localStorage
         */
        saveState() {
            try {
                const state = {
                    activeTab: this.activeTab,
                    lastEntity: this.currentEntity
                };
                localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
            } catch (e) {
                console.warn('Failed to save detail panel state:', e);
            }
        }

        /**
         * Bind event handlers
         */
        bindEvents() {
            // Listen for entity selection events from chat or sidebar
            document.addEventListener('entity-selected', (e) => {
                const { entityType, entityId, entityName } = e.detail || {};
                if (entityType && entityId) {
                    this.showEntity(entityType, entityId, entityName);
                }
            });

            // Listen for HTMX afterSwap to rebind events
            this.container?.addEventListener('htmx:afterSwap', () => {
                this.rebindDynamicEvents();
            });

            // Collapsed panel expand button
            this.container?.querySelectorAll('.collapsed-btn[data-panel="detail"]').forEach(btn => {
                btn.addEventListener('click', () => {
                    if (window.mainLayout) {
                        window.mainLayout.togglePanel('detail');
                    }
                });
            });
        }

        /**
         * Rebind events after HTMX content swap
         */
        rebindDynamicEvents() {
            // Tab buttons are handled by inline script in _DetailPanel.cshtml
            // This is for any additional dynamic binding needed
        }

        /**
         * Show entity details in the panel
         */
        async showEntity(entityType, entityId, entityName = null) {
            if (!entityType || !entityId) {
                console.warn('Cannot show entity: missing type or id');
                return;
            }

            // Store current entity reference
            this.currentEntity = { type: entityType, id: entityId, name: entityName };
            this.saveContext();

            // Expand panel if collapsed
            if (this.container?.classList.contains('collapsed') && window.mainLayout) {
                window.mainLayout.togglePanel('detail');
            }

            // Show loading state
            this.showLoading();

            try {
                // Fetch entity details via API
                const response = await fetch(`/api/entities/${encodeURIComponent(entityType)}/${encodeURIComponent(entityId)}`);
                if (!response.ok) {
                    throw new Error(`Failed to load entity: ${response.status}`);
                }

                const data = await response.json();
                this.renderEntity(data);
            } catch (error) {
                console.error('Failed to load entity details:', error);
                this.showError(error.message);
            }
        }

        /**
         * Show a run in the detail panel
         */
        showRun(runId) {
            return this.showEntity('run', runId);
        }

        /**
         * Show a project in the detail panel
         */
        showProject(projectId) {
            return this.showEntity('project', projectId);
        }

        /**
         * Show a task in the detail panel
         */
        showTask(taskId) {
            return this.showEntity('task', taskId);
        }

        /**
         * Show a spec in the detail panel
         */
        showSpec(specId) {
            return this.showEntity('spec', specId);
        }

        /**
         * Render entity data in the panel
         */
        renderEntity(entityData) {
            // Trigger HTMX to load the rendered partial
            if (window.htmx && this.contentContainer) {
                window.htmx.trigger(this.contentContainer, 'entity-selected');
            }

            // Dispatch event for other components
            document.dispatchEvent(new CustomEvent('detail-panel-updated', {
                detail: { entity: entityData }
            }));
        }

        /**
         * Show loading state
         */
        showLoading() {
            if (this.contentContainer) {
                this.contentContainer.innerHTML = `
                    <div class="detail-loading">
                        <span class="loading-spinner"></span>
                        <p>Loading details...</p>
                    </div>
                `;
            }
        }

        /**
         * Show error state
         */
        showError(message) {
            if (this.contentContainer) {
                this.contentContainer.innerHTML = `
                    <div class="detail-error">
                        <span class="error-icon">⚠</span>
                        <p>Failed to load details</p>
                        <span class="error-message">${this.escapeHtml(message)}</span>
                        <button class="retry-btn" onclick="window.DetailPanel.refresh()">Retry</button>
                    </div>
                `;
            }
        }

        /**
         * Clear the panel and show empty state
         */
        clear() {
            this.currentEntity = null;
            localStorage.removeItem(CONTEXT_KEY);

            if (this.contentContainer) {
                this.contentContainer.innerHTML = `
                    <div class="detail-empty-state">
                        <span class="empty-icon">📋</span>
                        <p>Select an item to view details</p>
                        <span class="empty-hint">Click any link in chat or sidebar</span>
                    </div>
                `;
            }
        }

        /**
         * Refresh current entity data
         */
        refresh() {
            if (this.currentEntity) {
                this.showEntity(this.currentEntity.type, this.currentEntity.id, this.currentEntity.name);
            }
        }

        /**
         * Save current entity context to localStorage
         */
        saveContext() {
            try {
                if (this.currentEntity) {
                    localStorage.setItem(CONTEXT_KEY, JSON.stringify(this.currentEntity));
                }
            } catch (e) {
                console.warn('Failed to save detail panel context:', e);
            }
        }

        /**
         * Restore entity from saved context
         */
        restoreFromContext() {
            try {
                const stored = localStorage.getItem(CONTEXT_KEY);
                if (stored) {
                    const context = JSON.parse(stored);
                    if (context && context.type && context.id) {
                        // Don't auto-load on init, just restore the reference
                        this.currentEntity = context;
                    }
                }
            } catch (e) {
                console.warn('Failed to restore detail panel context:', e);
            }
        }

        /**
         * Get currently displayed entity
         */
        getCurrentEntity() {
            return this.currentEntity;
        }

        /**
         * Check if panel is currently showing an entity
         */
        hasEntity() {
            return this.currentEntity !== null;
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
            this.currentEntity = null;
        }
    }

    // ============================================
    // Public API
    // ============================================
    window.DetailPanel = {
        controller: null,

        /**
         * Initialize the detail panel
         */
        init() {
            this.controller = new DetailPanelController();
            return this.controller;
        },

        /**
         * Show an entity in the detail panel
         */
        showEntity(entityType, entityId, entityName) {
            return this.controller?.showEntity(entityType, entityId, entityName);
        },

        /**
         * Show a run
         */
        showRun(runId) {
            return this.controller?.showRun(runId);
        },

        /**
         * Show a project
         */
        showProject(projectId) {
            return this.controller?.showProject(projectId);
        },

        /**
         * Show a task
         */
        showTask(taskId) {
            return this.controller?.showTask(taskId);
        },

        /**
         * Show a spec
         */
        showSpec(specId) {
            return this.controller?.showSpec(specId);
        },

        /**
         * Refresh current entity
         */
        refresh() {
            return this.controller?.refresh();
        },

        /**
         * Clear the panel
         */
        clear() {
            return this.controller?.clear();
        },

        /**
         * Get current entity
         */
        getCurrentEntity() {
            return this.controller?.getCurrentEntity();
        },

        /**
         * Check if showing an entity
         */
        hasEntity() {
            return this.controller?.hasEntity() || false;
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
            window.DetailPanel.init();
        });
    } else {
        window.DetailPanel.init();
    }
})();
