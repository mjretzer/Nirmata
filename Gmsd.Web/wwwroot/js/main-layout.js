/**
 * MainLayout - Three Panel Layout Controller
 * Handles panel collapse/expand, state persistence, and keyboard navigation
 */

(function() {
    'use strict';

    const STORAGE_KEY = 'gmsd_mainlayout_state';
    
    // Default state - left sidebar expanded, right panel collapsed
    const DEFAULT_STATE = {
        sidebar: { collapsed: false },
        detail: { collapsed: true }
    };

    class MainLayoutController {
        constructor() {
            this.sidebar = document.getElementById('sidebar');
            this.detailPanel = document.getElementById('detail-panel');
            this.sidebarToggle = document.getElementById('sidebar-toggle');
            this.detailToggle = document.getElementById('detail-toggle');
            this.chatInput = document.getElementById('chat-input');
            
            this.state = this.loadState();
            this.init();
        }

        init() {
            this.applyState();
            this.bindEvents();
            this.setupKeyboardShortcuts();
        }

        loadState() {
            try {
                const stored = localStorage.getItem(STORAGE_KEY);
                if (stored) {
                    return { ...DEFAULT_STATE, ...JSON.parse(stored) };
                }
            } catch (e) {
                console.warn('Failed to load layout state:', e);
            }
            return { ...DEFAULT_STATE };
        }

        saveState() {
            try {
                localStorage.setItem(STORAGE_KEY, JSON.stringify(this.state));
            } catch (e) {
                console.warn('Failed to save layout state:', e);
            }
        }

        applyState() {
            // Apply sidebar state
            if (this.state.sidebar.collapsed) {
                this.sidebar.classList.add('collapsed');
            } else {
                this.sidebar.classList.remove('collapsed');
            }

            // Apply detail panel state
            if (this.state.detail.collapsed) {
                this.detailPanel.classList.add('collapsed');
            } else {
                this.detailPanel.classList.remove('collapsed');
            }
        }

        togglePanel(panelName) {
            const panel = panelName === 'sidebar' ? this.sidebar : this.detailPanel;
            const isCollapsed = panel.classList.toggle('collapsed');
            
            this.state[panelName].collapsed = isCollapsed;
            this.saveState();
        }

        bindEvents() {
            // Toggle buttons
            if (this.sidebarToggle) {
                this.sidebarToggle.addEventListener('click', () => this.togglePanel('sidebar'));
            }
            
            if (this.detailToggle) {
                this.detailToggle.addEventListener('click', () => this.togglePanel('detail'));
            }

            // Collapsed panel expand buttons
            document.querySelectorAll('.collapsed-btn[data-panel]').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    const panelName = e.currentTarget.dataset.panel;
                    this.togglePanel(panelName);
                });
            });

            // Quick action buttons
            document.querySelectorAll('.quick-action-btn[data-command]').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    const command = e.currentTarget.dataset.command;
                    this.insertCommand(command);
                });
            });

            // Collapsed quick action buttons
            document.querySelectorAll('.collapsed-btn[data-command]').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    const command = e.currentTarget.dataset.command;
                    this.insertCommand(command);
                });
            });

            // Chat input handling - ChatInput component handles Enter/Shift+Enter
            if (this.chatInput) {
                // Auto-resize textarea as user types
                this.chatInput.addEventListener('input', () => {
                    this.autoResizeTextarea();
                });

                // Focus handling - clear input hints when focused
                this.chatInput.addEventListener('focus', () => {
                    const hints = document.querySelector('.input-hints');
                    if (hints) {
                        hints.classList.add('visible');
                    }
                });
            }
        }

        setupKeyboardShortcuts() {
            document.addEventListener('keydown', (e) => {
                // Don't trigger shortcuts when typing in inputs (except specific cases)
                const isInput = e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA';
                
                // / or Cmd+K to focus chat input
                if ((e.key === '/' && !isInput) || (e.metaKey && e.key === 'k')) {
                    e.preventDefault();
                    this.focusChatInput();
                    return;
                }

                // Escape to clear focus and return to chat
                if (e.key === 'Escape') {
                    if (this.chatInput) {
                        this.chatInput.blur();
                        this.chatInput.value = '';
                        this.autoResizeTextarea();
                    }
                    return;
                }

                // Cmd+1 to toggle sidebar
                if (e.metaKey && e.key === '1') {
                    e.preventDefault();
                    this.togglePanel('sidebar');
                    return;
                }

                // Cmd+2 to toggle detail panel
                if (e.metaKey && e.key === '2') {
                    e.preventDefault();
                    this.togglePanel('detail');
                    return;
                }

                // Cmd+3 to toggle both panels
                if (e.metaKey && e.key === '3') {
                    e.preventDefault();
                    const bothCollapsed = this.state.sidebar.collapsed && this.state.detail.collapsed;
                    if (bothCollapsed) {
                        // Expand both
                        this.sidebar.classList.remove('collapsed');
                        this.detailPanel.classList.remove('collapsed');
                        this.state.sidebar.collapsed = false;
                        this.state.detail.collapsed = false;
                    } else {
                        // Collapse both
                        this.sidebar.classList.add('collapsed');
                        this.detailPanel.classList.add('collapsed');
                        this.state.sidebar.collapsed = true;
                        this.state.detail.collapsed = true;
                    }
                    this.saveState();
                    return;
                }
            });
        }

        focusChatInput() {
            if (this.chatInput) {
                this.chatInput.focus();
                this.chatInput.select();
            }
        }

        insertCommand(command) {
            if (this.chatInput) {
                this.chatInput.value = command;
                this.chatInput.focus();
                this.autoResizeTextarea();
            }
        }

        submitChat() {
            if (this.chatInput && this.chatInput.value.trim()) {
                // Trigger form submission via HTMX or regular form
                const form = document.getElementById('chat-form');
                if (form) {
                    form.dispatchEvent(new Event('submit'));
                }
                this.chatInput.value = '';
                this.autoResizeTextarea();
            }
        }

        autoResizeTextarea() {
            if (!this.chatInput) return;
            
            // Reset height to auto to get proper scrollHeight
            this.chatInput.style.height = 'auto';
            
            // Set height to scrollHeight (clamped between min and max)
            const newHeight = Math.min(
                Math.max(this.chatInput.scrollHeight, 24),
                200
            );
            this.chatInput.style.height = newHeight + 'px';
        }
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            window.mainLayout = new MainLayoutController();
        });
    } else {
        window.mainLayout = new MainLayoutController();
    }
})();
