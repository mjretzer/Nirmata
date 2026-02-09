/**
 * GMSD Command Palette
 * Global keyboard-driven navigation (Ctrl+K)
 */
(function () {
    'use strict';

    // ============================================
    // Command Registry
    // ============================================
    const CommandRegistry = {
        commands: [],

        /**
         * Register a command
         * @param {string} id - Unique command identifier
         * @param {string} title - Display title
         * @param {string} category - Command category (pages, artifacts, runs, commands)
         * @param {Function} action - Function to execute when selected
         * @param {string} [shortcut] - Optional keyboard shortcut hint
         * @param {string} [icon] - Optional icon character
         */
        register(id, title, category, action, shortcut, icon) {
            this.commands.push({
                id,
                title,
                category,
                action,
                shortcut: shortcut || null,
                icon: icon || null
            });
        },

        /**
         * Get all commands
         * @returns {Array} All registered commands
         */
        getAll() {
            return this.commands;
        },

        /**
         * Search commands by query string
         * @param {string} query - Search query
         * @returns {Array} Filtered and scored commands
         */
        search(query) {
            const normalizedQuery = query.toLowerCase().trim();
            if (!normalizedQuery) {
                return this.commands;
            }

            return this.commands
                .filter(cmd => {
                    return cmd.title.toLowerCase().includes(normalizedQuery) ||
                           cmd.category.toLowerCase().includes(normalizedQuery) ||
                           cmd.id.toLowerCase().includes(normalizedQuery);
                })
                .sort((a, b) => {
                    const aTitle = a.title.toLowerCase();
                    const bTitle = b.title.toLowerCase();
                    const aStarts = aTitle.startsWith(normalizedQuery);
                    const bStarts = bTitle.startsWith(normalizedQuery);

                    if (aStarts && !bStarts) return -1;
                    if (!aStarts && bStarts) return 1;
                    return aTitle.localeCompare(bTitle);
                });
        },

        /**
         * Clear all registered commands
         */
        clear() {
            this.commands = [];
        }
    };

    // ============================================
    // Default Command Registration
    // ============================================
    function registerDefaultCommands() {
        // Page Navigation Commands
        CommandRegistry.register('nav-home', 'Home', 'pages', () => navigateTo('~/'), null, '🏠');
        CommandRegistry.register('nav-workspace', 'Workspace', 'pages', () => navigateTo('~/Workspace'), null, '💼');
        CommandRegistry.register('nav-projects', 'Projects', 'pages', () => navigateTo('~/Projects'), null, '📁');
        CommandRegistry.register('nav-runs', 'Runs', 'pages', () => navigateTo('~/Runs'), null, '▶️');
        CommandRegistry.register('nav-orchestrator', 'Orchestrator', 'pages', () => navigateTo('~/Orchestrator'), null, '💬');
        CommandRegistry.register('nav-specs', 'Specs', 'pages', () => navigateTo('~/Specs'), null, '📋');
        CommandRegistry.register('nav-roadmap', 'Roadmap', 'pages', () => navigateTo('~/Roadmap'), null, '🗺️');
        CommandRegistry.register('nav-milestones', 'Milestones', 'pages', () => navigateTo('~/Milestones'), null, '🎯');
        CommandRegistry.register('nav-phases', 'Phases', 'pages', () => navigateTo('~/Phases'), null, '📅');
        CommandRegistry.register('nav-tasks', 'Tasks', 'pages', () => navigateTo('~/Tasks'), null, '✅');
        CommandRegistry.register('nav-uat', 'UAT', 'pages', () => navigateTo('~/Uat'), null, '🧪');
        CommandRegistry.register('nav-issues', 'Issues', 'pages', () => navigateTo('~/Issues'), null, '🐛');
        CommandRegistry.register('nav-fix', 'Fix', 'pages', () => navigateTo('~/Fix'), null, '🔧');
        CommandRegistry.register('nav-codebase', 'Codebase', 'pages', () => navigateTo('~/Codebase'), null, '💻');
        CommandRegistry.register('nav-context', 'Context', 'pages', () => navigateTo('~/Context'), null, '📚');
        CommandRegistry.register('nav-state', 'State', 'pages', () => navigateTo('~/State'), null, '📊');
        CommandRegistry.register('nav-checkpoints', 'Checkpoints', 'pages', () => navigateTo('~/Checkpoints'), null, '💾');
        CommandRegistry.register('nav-validation', 'Validation', 'pages', () => navigateTo('~/Validation'), null, '✓');

        // Quick Action Commands
        CommandRegistry.register('cmd-new-run', 'Start New Run', 'commands', () => navigateTo('~/Runs/Create'), 'Ctrl+N', '🚀');
        CommandRegistry.register('cmd-new-task', 'Create Task', 'commands', () => navigateTo('~/Tasks/Create'), null, '📝');
        CommandRegistry.register('cmd-new-phase', 'Create Phase', 'commands', () => navigateTo('~/Phases/Create'), null, '📅');
        CommandRegistry.register('cmd-new-milestone', 'Create Milestone', 'commands', () => navigateTo('~/Milestones/Create'), null, '🎯');

        // Artifact Commands (placeholder actions - actual artifact IDs would be dynamic)
        CommandRegistry.register('art-view-tasks', 'View All Tasks', 'artifacts', () => navigateTo('~/Tasks'), null, 'TSK');
        CommandRegistry.register('art-view-runs', 'View All Runs', 'artifacts', () => navigateTo('~/Runs'), null, 'RUN');
        CommandRegistry.register('art-view-phases', 'View All Phases', 'artifacts', () => navigateTo('~/Phases'), null, 'PH');
        CommandRegistry.register('art-view-milestones', 'View All Milestones', 'artifacts', () => navigateTo('~/Milestones'), null, 'MS');
        CommandRegistry.register('art-view-uat', 'View All UAT', 'artifacts', () => navigateTo('~/Uat'), null, 'UAT');
    }

    // ============================================
    // Command Palette UI
    // ============================================
    const CommandPalette = {
        isOpen: false,
        selectedIndex: 0,
        filteredCommands: [],
        container: null,
        input: null,
        results: null,

        /**
         * Initialize the command palette
         */
        init() {
            this.createElements();
            this.bindEvents();
            registerDefaultCommands();
        },

        /**
         * Create palette DOM elements
         */
        createElements() {
            // Main container
            this.container = document.createElement('div');
            this.container.id = 'command-palette';
            this.container.className = 'command-palette-overlay';
            this.container.style.display = 'none';

            // Palette content
            const palette = document.createElement('div');
            palette.className = 'command-palette';

            // Search input
            this.input = document.createElement('input');
            this.input.type = 'text';
            this.input.className = 'command-palette-input';
            this.input.placeholder = 'Search commands... (Ctrl+K to close)';
            this.input.setAttribute('autocomplete', 'off');

            // Results container
            this.results = document.createElement('div');
            this.results.className = 'command-palette-results';

            // Category labels
            const categories = document.createElement('div');
            categories.className = 'command-palette-categories';

            palette.appendChild(this.input);
            palette.appendChild(categories);
            palette.appendChild(this.results);
            this.container.appendChild(palette);

            document.body.appendChild(this.container);
        },

        /**
         * Bind DOM events
         */
        bindEvents() {
            // Keyboard shortcuts
            document.addEventListener('keydown', (e) => {
                // Ctrl+K or Cmd+K to toggle
                if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                    e.preventDefault();
                    this.toggle();
                }

                // Escape to close
                if (e.key === 'Escape' && this.isOpen) {
                    this.close();
                }

                // Navigation when open
                if (this.isOpen) {
                    if (e.key === 'ArrowDown') {
                        e.preventDefault();
                        this.selectNext();
                    } else if (e.key === 'ArrowUp') {
                        e.preventDefault();
                        this.selectPrevious();
                    } else if (e.key === 'Enter') {
                        e.preventDefault();
                        this.executeSelected();
                    }
                }
            });

            // Input filtering
            this.input.addEventListener('input', () => {
                this.filter(this.input.value);
            });

            // Click outside to close
            this.container.addEventListener('click', (e) => {
                if (e.target === this.container) {
                    this.close();
                }
            });
        },

        /**
         * Toggle palette visibility
         */
        toggle() {
            if (this.isOpen) {
                this.close();
            } else {
                this.open();
            }
        },

        /**
         * Open the palette
         */
        open() {
            this.isOpen = true;
            this.container.style.display = 'flex';
            this.input.value = '';
            this.filter('');
            this.input.focus();
            document.body.style.overflow = 'hidden';
        },

        /**
         * Close the palette
         */
        close() {
            this.isOpen = false;
            this.container.style.display = 'none';
            document.body.style.overflow = '';
        },

        /**
         * Filter commands based on query
         * @param {string} query - Search query
         */
        filter(query) {
            this.filteredCommands = CommandRegistry.search(query);
            this.selectedIndex = 0;
            this.renderResults();
        },

        /**
         * Render filtered results
         */
        renderResults() {
            if (this.filteredCommands.length === 0) {
                this.results.innerHTML = '<div class="command-palette-empty">No commands found</div>';
                return;
            }

            // Group by category
            const grouped = this.filteredCommands.reduce((acc, cmd) => {
                if (!acc[cmd.category]) acc[cmd.category] = [];
                acc[cmd.category].push(cmd);
                return acc;
            }, {});

            const categoryOrder = ['pages', 'commands', 'artifacts', 'runs'];
            const categoryLabels = {
                pages: 'Pages',
                commands: 'Commands',
                artifacts: 'Artifacts',
                runs: 'Runs'
            };

            let html = '';
            categoryOrder.forEach(category => {
                const commands = grouped[category];
                if (!commands || commands.length === 0) return;

                html += `<div class="command-category">
                    <div class="command-category-header">${categoryLabels[category]}</div>`;

                commands.forEach((cmd, idx) => {
                    const globalIndex = this.filteredCommands.indexOf(cmd);
                    const isSelected = globalIndex === this.selectedIndex;
                    const shortcut = cmd.shortcut ? `<span class="command-shortcut">${cmd.shortcut}</span>` : '';
                    const icon = cmd.icon ? `<span class="command-icon">${cmd.icon}</span>` : '';

                    html += `<div class="command-item ${isSelected ? 'selected' : ''}" data-index="${globalIndex}">
                        ${icon}
                        <span class="command-title">${this.highlightMatch(cmd.title, this.input.value)}</span>
                        ${shortcut}
                    </div>`;
                });

                html += '</div>';
            });

            this.results.innerHTML = html;

            // Bind click handlers
            this.results.querySelectorAll('.command-item').forEach(item => {
                item.addEventListener('click', () => {
                    this.selectedIndex = parseInt(item.dataset.index);
                    this.executeSelected();
                });
            });
        },

        /**
         * Highlight matching text
         * @param {string} text - Original text
         * @param {string} query - Search query
         * @returns {string} HTML with highlighted text
         */
        highlightMatch(text, query) {
            if (!query) return text;
            const regex = new RegExp(`(${escapeRegex(query)})`, 'gi');
            return text.replace(regex, '<mark>$1</mark>');
        },

        /**
         * Select next item
         */
        selectNext() {
            if (this.selectedIndex < this.filteredCommands.length - 1) {
                this.selectedIndex++;
                this.renderResults();
                this.scrollToSelected();
            }
        },

        /**
         * Select previous item
         */
        selectPrevious() {
            if (this.selectedIndex > 0) {
                this.selectedIndex--;
                this.renderResults();
                this.scrollToSelected();
            }
        },

        /**
         * Scroll selected item into view
         */
        scrollToSelected() {
            const selected = this.results.querySelector('.command-item.selected');
            if (selected) {
                selected.scrollIntoView({ block: 'nearest' });
            }
        },

        /**
         * Execute the selected command
         */
        executeSelected() {
            const command = this.filteredCommands[this.selectedIndex];
            if (command && command.action) {
                this.close();
                command.action();
            }
        }
    };

    // ============================================
    // Utility Functions
    // ============================================
    function navigateTo(url) {
        // Handle ~ prefix by resolving against base URL
        if (url.startsWith('~/')) {
            const baseUrl = document.querySelector('base')?.getAttribute('href') || '/';
            window.location.href = baseUrl + url.substring(2);
        } else {
            window.location.href = url;
        }
    }

    function escapeRegex(string) {
        return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    // ============================================
    // Public API
    // ============================================
    window.GmsdCommandPalette = {
        /**
         * Initialize the command palette
         */
        init() {
            CommandPalette.init();
        },

        /**
         * Open the command palette
         */
        open() {
            CommandPalette.open();
        },

        /**
         * Close the command palette
         */
        close() {
            CommandPalette.close();
        },

        /**
         * Toggle the command palette
         */
        toggle() {
            CommandPalette.toggle();
        },

        /**
         * Register a new command
         * @param {string} id - Command identifier
         * @param {string} title - Display title
         * @param {string} category - Command category
         * @param {Function} action - Action to execute
         * @param {string} [shortcut] - Optional shortcut hint
         * @param {string} [icon] - Optional icon
         */
        register(id, title, category, action, shortcut, icon) {
            CommandRegistry.register(id, title, category, action, shortcut, icon);
        },

        /**
         * Get the command registry for direct access
         */
        get registry() {
            return CommandRegistry;
        }
    };

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            window.GmsdCommandPalette.init();
        });
    } else {
        window.GmsdCommandPalette.init();
    }
})();
