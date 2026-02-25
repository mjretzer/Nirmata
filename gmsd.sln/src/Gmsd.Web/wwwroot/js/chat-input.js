/**
 * ChatInput - Enhanced Chat Input Component
 * Features:
 * - Slash command autocomplete with descriptions
 * - Command history (up/down arrow navigation)
 * - Typing indicators
 * - Submit on Enter, newline on Shift+Enter
 * - Auto-resizing textarea
 */
(function () {
    'use strict';

    // ============================================
    // Slash Command Registry
    // ============================================
    const SlashCommandRegistry = {
        commands: new Map(),

        /**
         * Register a slash command
         * @param {string} command - The command name (e.g., '/help')
         * @param {string} description - Command description
         * @param {string} [category] - Command category (optional)
         * @param {Array<string>} [aliases] - Command aliases
         */
        register(command, description, category = 'general', aliases = []) {
            this.commands.set(command, {
                command,
                description,
                category,
                aliases
            });

            // Register aliases
            aliases.forEach(alias => {
                this.commands.set(alias, {
                    command,
                    description,
                    category,
                    aliases: [],
                    isAlias: true,
                    targetCommand: command
                });
            });
        },

        /**
         * Search commands by query
         * @param {string} query - Search query (starts with /)
         * @returns {Array} Filtered commands
         */
        search(query) {
            const normalizedQuery = query.toLowerCase().trim();

            if (!normalizedQuery || normalizedQuery === '/') {
                // Return all non-alias commands sorted by category
                return Array.from(this.commands.values())
                    .filter(cmd => !cmd.isAlias)
                    .sort((a, b) => a.category.localeCompare(b.category));
            }

            const searchTerm = normalizedQuery.replace(/^\//, '');

            return Array.from(this.commands.values())
                .filter(cmd => {
                    const cmdText = cmd.command.toLowerCase();
                    const descText = cmd.description.toLowerCase();
                    return cmdText.includes(searchTerm) || descText.includes(searchTerm);
                })
                .sort((a, b) => {
                    // Prioritize exact matches and starts with
                    const aCmd = a.command.toLowerCase();
                    const bCmd = b.command.toLowerCase();
                    const aStarts = aCmd.startsWith(normalizedQuery);
                    const bStarts = bCmd.startsWith(normalizedQuery);

                    if (aStarts && !bStarts) return -1;
                    if (!aStarts && bStarts) return 1;

                    // Then sort by category
                    if (a.category !== b.category) {
                        return a.category.localeCompare(b.category);
                    }

                    return aCmd.localeCompare(bCmd);
                });
        },

        /**
         * Get command by exact match
         * @param {string} command - Command to find
         * @returns {Object|null} Command definition
         */
        get(command) {
            return this.commands.get(command) || null;
        },

        /**
         * Get all registered commands
         * @returns {Array} All commands
         */
        getAll() {
            return Array.from(this.commands.values()).filter(cmd => !cmd.isAlias);
        }
    };

    // ============================================
    // Default Command Registration
    // ============================================
    function registerDefaultCommands() {
        // Status & Information
        SlashCommandRegistry.register('/status', 'Show current workspace and system status', 'status');
        SlashCommandRegistry.register('/help', 'Show available commands and usage help', 'help');
        SlashCommandRegistry.register('/version', 'Display version information', 'status');

        // Project Management
        SlashCommandRegistry.register('/list projects', 'List all projects in the workspace', 'projects');
        SlashCommandRegistry.register('/create project', 'Create a new project', 'projects');
        SlashCommandRegistry.register('/delete project', 'Delete a project', 'projects');

        // Task Management
        SlashCommandRegistry.register('/list tasks', 'List tasks with optional filters', 'tasks');
        SlashCommandRegistry.register('/create task', 'Create a new task', 'tasks');
        SlashCommandRegistry.register('/execute task', 'Execute a specific task by ID', 'tasks');

        // Runs & Execution
        SlashCommandRegistry.register('/run', 'Execute a command through the agent runner', 'execution');
        SlashCommandRegistry.register('/validate', 'Validate workspace configuration', 'execution');
        SlashCommandRegistry.register('/init', 'Initialize or reset workspace state', 'execution');

        // Phases & Milestones
        SlashCommandRegistry.register('/list phases', 'List all project phases', 'planning');
        SlashCommandRegistry.register('/list milestones', 'List all milestones', 'planning');

        // Specs & Documentation
        SlashCommandRegistry.register('/list specs', 'List all specification files', 'specs');
        SlashCommandRegistry.register('/view spec', 'View a specific specification', 'specs');

        // Context & State
        SlashCommandRegistry.register('/context', 'Show current context information', 'context');
        SlashCommandRegistry.register('/state', 'Display current system state', 'context');
        SlashCommandRegistry.register('/checkpoint', 'Create or restore a checkpoint', 'context');

        // Codebase
        SlashCommandRegistry.register('/search', 'Search the codebase', 'codebase');
        SlashCommandRegistry.register('/analyze', 'Analyze code patterns or structure', 'codebase');

        // Issues & Fixes
        SlashCommandRegistry.register('/list issues', 'List tracked issues', 'issues');
        SlashCommandRegistry.register('/fix', 'Apply an automated fix', 'issues');

        // UAT
        SlashCommandRegistry.register('/uat', 'Run User Acceptance Tests', 'uat');
        SlashCommandRegistry.register('/verify', 'Verify implementation against specs', 'uat');
    }

    // ============================================
    // Command History Manager
    // ============================================
    const CommandHistory = {
        STORAGE_KEY: 'gmsd_chat_command_history',
        MAX_HISTORY_SIZE: 50,
        history: [],
        currentIndex: -1,
        tempInput: '',

        /**
         * Load history from localStorage
         */
        load() {
            try {
                const stored = localStorage.getItem(this.STORAGE_KEY);
                if (stored) {
                    this.history = JSON.parse(stored);
                }
            } catch (e) {
                console.warn('Failed to load command history:', e);
                this.history = [];
            }
        },

        /**
         * Save history to localStorage
         */
        save() {
            try {
                localStorage.setItem(this.STORAGE_KEY, JSON.stringify(this.history));
            } catch (e) {
                console.warn('Failed to save command history:', e);
            }
        },

        /**
         * Add a command to history
         * @param {string} command - Command to add
         */
        add(command) {
            if (!command || !command.trim()) return;

            const trimmed = command.trim();

            // Don't add duplicates at the top
            if (this.history.length > 0 && this.history[0] === trimmed) {
                return;
            }

            // Remove if already exists (will be added to top)
            const existingIndex = this.history.indexOf(trimmed);
            if (existingIndex > -1) {
                this.history.splice(existingIndex, 1);
            }

            // Add to top
            this.history.unshift(trimmed);

            // Trim to max size
            if (this.history.length > this.MAX_HISTORY_SIZE) {
                this.history = this.history.slice(0, this.MAX_HISTORY_SIZE);
            }

            this.save();
        },

        /**
         * Navigate history up (older commands)
         * @param {string} currentInput - Current input value (to save as temp)
         * @returns {string|null} Previous command or null
         */
        navigateUp(currentInput) {
            // Save current input if starting navigation
            if (this.currentIndex === -1) {
                this.tempInput = currentInput;
            }

            if (this.currentIndex < this.history.length - 1) {
                this.currentIndex++;
                return this.history[this.currentIndex];
            }

            return null;
        },

        /**
         * Navigate history down (newer commands)
         * @returns {string|null} Next command or temp input or null
         */
        navigateDown() {
            if (this.currentIndex > 0) {
                this.currentIndex--;
                return this.history[this.currentIndex];
            } else if (this.currentIndex === 0) {
                this.currentIndex = -1;
                return this.tempInput;
            }

            return null;
        },

        /**
         * Reset navigation state
         */
        resetNavigation() {
            this.currentIndex = -1;
            this.tempInput = '';
        },

        /**
         * Get all history
         * @returns {Array} Command history
         */
        getAll() {
            return [...this.history];
        },

        /**
         * Clear history
         */
        clear() {
            this.history = [];
            this.currentIndex = -1;
            this.tempInput = '';
            this.save();
        }
    };

    // ============================================
    // Typing Indicator Manager
    // ============================================
    const TypingIndicator = {
        indicator: null,
        timeout: null,
        isVisible: false,

        /**
         * Create and show typing indicator
         * @param {HTMLElement} container - Container element
         */
        show(container) {
            if (this.isVisible) return;

            if (!this.indicator) {
                this.indicator = document.createElement('div');
                this.indicator.className = 'typing-indicator';
                this.indicator.innerHTML = `
                    <div class="typing-dots">
                        <span></span>
                        <span></span>
                        <span></span>
                    </div>
                    <span class="typing-text">AI is thinking...</span>
                `;
                this.indicator.style.display = 'none';

                if (container) {
                    container.appendChild(this.indicator);
                }
            }

            this.indicator.style.display = 'flex';
            this.isVisible = true;
        },

        /**
         * Hide typing indicator
         */
        hide() {
            if (this.indicator) {
                this.indicator.style.display = 'none';
            }
            this.isVisible = false;

            if (this.timeout) {
                clearTimeout(this.timeout);
                this.timeout = null;
            }
        },

        /**
         * Auto-hide after delay
         * @param {number} delay - Delay in milliseconds
         */
        autoHide(delay = 30000) {
            if (this.timeout) {
                clearTimeout(this.timeout);
            }

            this.timeout = setTimeout(() => {
                this.hide();
            }, delay);
        }
    };

    // ============================================
    // ChatInput Component
    // ============================================
    class ChatInput {
        constructor(options = {}) {
            this.options = {
                inputSelector: '#chat-input',
                formSelector: '#chat-form',
                autocompleteContainer: null,
                maxRows: 5,
                placeholder: 'Type a command or message... (Press / to focus)',
                onSubmit: null,
                onTypingStart: null,
                onTypingEnd: null,
                ...options
            };

            this.input = document.querySelector(this.options.inputSelector);
            this.form = document.querySelector(this.options.formSelector);
            this.autocompleteContainer = null;
            this.autocompleteList = null;
            this.isAutocompleteOpen = false;
            this.selectedAutocompleteIndex = -1;
            this.filteredCommands = [];
            this.isTyping = false;
            this.typingTimeout = null;

            if (!this.input) {
                console.warn('ChatInput: Input element not found');
                return;
            }

            this.init();
        }

        init() {
            this.setupAutocomplete();
            this.bindEvents();
            CommandHistory.load();
            registerDefaultCommands();

            // Set placeholder
            this.input.placeholder = this.options.placeholder;
        }

        setupAutocomplete() {
            // Create autocomplete container
            this.autocompleteContainer = document.createElement('div');
            this.autocompleteContainer.className = 'chat-input-autocomplete';
            this.autocompleteContainer.style.display = 'none';

            this.autocompleteList = document.createElement('div');
            this.autocompleteList.className = 'autocomplete-list';

            this.autocompleteContainer.appendChild(this.autocompleteList);

            // Insert after input container
            const inputContainer = this.input.closest('.input-container');
            if (inputContainer) {
                inputContainer.style.position = 'relative';
                inputContainer.appendChild(this.autocompleteContainer);
            }
        }

        bindEvents() {
            // Input handling
            this.input.addEventListener('input', (e) => this.handleInput(e));
            this.input.addEventListener('keydown', (e) => this.handleKeydown(e));
            this.input.addEventListener('focus', () => this.handleFocus());
            this.input.addEventListener('blur', () => this.handleBlur());

            // Form submission
            if (this.form) {
                this.form.addEventListener('submit', (e) => this.handleSubmit(e));
            }

            // Click outside to close autocomplete
            document.addEventListener('click', (e) => {
                if (!e.target.closest('.input-container')) {
                    this.closeAutocomplete();
                }
            });

            // HTMX events for typing indicators
            document.addEventListener('htmx:beforeRequest', () => {
                this.showTypingIndicator();
            });

            document.addEventListener('htmx:afterRequest', () => {
                this.hideTypingIndicator();
            });
        }

        handleInput(e) {
            const value = this.input.value;
            const cursorPosition = this.input.selectionStart;

            // Auto-resize textarea
            this.autoResize();

            // Handle autocomplete for slash commands
            const textBeforeCursor = value.substring(0, cursorPosition);
            const lastWord = textBeforeCursor.split(/\s+/).pop();

            if (lastWord.startsWith('/')) {
                this.openAutocomplete(lastWord);
            } else {
                this.closeAutocomplete();
            }

            // Typing indicator logic
            this.handleTyping();
        }

        handleKeydown(e) {
            // Autocomplete navigation
            if (this.isAutocompleteOpen) {
                switch (e.key) {
                    case 'ArrowDown':
                        e.preventDefault();
                        this.selectNextCommand();
                        return;
                    case 'ArrowUp':
                        e.preventDefault();
                        this.selectPreviousCommand();
                        return;
                    case 'Enter':
                    case 'Tab':
                        e.preventDefault();
                        this.insertSelectedCommand();
                        return;
                    case 'Escape':
                        e.preventDefault();
                        this.closeAutocomplete();
                        return;
                }
            }

            // Command history navigation (when not in autocomplete)
            if (!this.isAutocompleteOpen) {
                if (e.key === 'ArrowUp' && this.input.selectionStart === 0) {
                    e.preventDefault();
                    const prevCommand = CommandHistory.navigateUp(this.input.value);
                    if (prevCommand !== null) {
                        this.input.value = prevCommand;
                        this.input.setSelectionRange(prevCommand.length, prevCommand.length);
                        this.autoResize();
                    }
                    return;
                }

                if (e.key === 'ArrowDown' && CommandHistory.currentIndex !== -1) {
                    e.preventDefault();
                    const nextCommand = CommandHistory.navigateDown();
                    if (nextCommand !== null) {
                        this.input.value = nextCommand;
                        this.input.setSelectionRange(nextCommand.length, nextCommand.length);
                        this.autoResize();
                    }
                    return;
                }
            }

            // Submit on Enter (without Shift)
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                this.submit();
                return;
            }

            // Reset history navigation on other key presses
            if (e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) {
                CommandHistory.resetNavigation();
            }

            // Global shortcut: / to focus input
            if (e.key === '/' && document.activeElement !== this.input) {
                e.preventDefault();
                this.focus();
            }
        }

        handleFocus() {
            // Could show recent commands or other helpful info
        }

        handleBlur() {
            // Delay closing autocomplete to allow clicking on it
            setTimeout(() => {
                if (!this.autocompleteContainer.matches(':hover')) {
                    this.closeAutocomplete();
                }
            }, 200);
        }

        handleSubmit(e) {
            e.preventDefault();
            this.submit();
        }

        handleTyping() {
            if (!this.isTyping) {
                this.isTyping = true;
                if (this.options.onTypingStart) {
                    this.options.onTypingStart();
                }
            }

            if (this.typingTimeout) {
                clearTimeout(this.typingTimeout);
            }

            this.typingTimeout = setTimeout(() => {
                this.isTyping = false;
                if (this.options.onTypingEnd) {
                    this.options.onTypingEnd();
                }
            }, 1000);
        }

        autoResize() {
            // Reset height to auto to get proper scrollHeight
            this.input.style.height = 'auto';

            // Calculate new height
            const lineHeight = parseInt(getComputedStyle(this.input).lineHeight) || 24;
            const maxHeight = lineHeight * this.options.maxRows;
            const newHeight = Math.min(this.input.scrollHeight, maxHeight);
            const minHeight = lineHeight;

            this.input.style.height = Math.max(newHeight, minHeight) + 'px';

            // Add/remove scrollbar class
            if (this.input.scrollHeight > maxHeight) {
                this.input.classList.add('has-scrollbar');
            } else {
                this.input.classList.remove('has-scrollbar');
            }
        }

        openAutocomplete(query) {
            this.filteredCommands = SlashCommandRegistry.search(query);

            if (this.filteredCommands.length === 0) {
                this.closeAutocomplete();
                return;
            }

            this.isAutocompleteOpen = true;
            this.selectedAutocompleteIndex = -1;
            this.renderAutocomplete();
            this.autocompleteContainer.style.display = 'block';
        }

        closeAutocomplete() {
            this.isAutocompleteOpen = false;
            this.selectedAutocompleteIndex = -1;
            this.filteredCommands = [];
            if (this.autocompleteContainer) {
                this.autocompleteContainer.style.display = 'none';
            }
        }

        renderAutocomplete() {
            const categoryOrder = ['status', 'help', 'execution', 'projects', 'tasks', 'planning', 'specs', 'context', 'codebase', 'issues', 'uat', 'general'];
            const categoryLabels = {
                status: 'Status',
                help: 'Help',
                execution: 'Execution',
                projects: 'Projects',
                tasks: 'Tasks',
                planning: 'Planning',
                specs: 'Specs',
                context: 'Context',
                codebase: 'Codebase',
                issues: 'Issues',
                uat: 'UAT',
                general: 'General'
            };

            // Group by category
            const grouped = this.filteredCommands.reduce((acc, cmd) => {
                if (!acc[cmd.category]) acc[cmd.category] = [];
                acc[cmd.category].push(cmd);
                return acc;
            }, {});

            let html = '';
            categoryOrder.forEach(category => {
                const commands = grouped[category];
                if (!commands || commands.length === 0) return;

                html += `<div class="autocomplete-category">`;
                html += `<div class="autocomplete-category-header">${categoryLabels[category] || category}</div>`;

                commands.forEach((cmd, idx) => {
                    const globalIndex = this.filteredCommands.indexOf(cmd);
                    const isSelected = globalIndex === this.selectedAutocompleteIndex;

                    html += `
                        <div class="autocomplete-item ${isSelected ? 'selected' : ''}" data-index="${globalIndex}" data-command="${cmd.command}">
                            <code class="autocomplete-command">${this.escapeHtml(cmd.command)}</code>
                            <span class="autocomplete-description">${this.escapeHtml(cmd.description)}</span>
                        </div>
                    `;
                });

                html += `</div>`;
            });

            this.autocompleteList.innerHTML = html;

            // Bind click handlers
            this.autocompleteList.querySelectorAll('.autocomplete-item').forEach(item => {
                item.addEventListener('click', () => {
                    this.selectedAutocompleteIndex = parseInt(item.dataset.index);
                    this.insertSelectedCommand();
                });

                item.addEventListener('mouseenter', () => {
                    this.selectedAutocompleteIndex = parseInt(item.dataset.index);
                    this.updateSelection();
                });
            });
        }

        selectNextCommand() {
            if (this.selectedAutocompleteIndex < this.filteredCommands.length - 1) {
                this.selectedAutocompleteIndex++;
                this.updateSelection();
                this.scrollToSelected();
            }
        }

        selectPreviousCommand() {
            if (this.selectedAutocompleteIndex > 0) {
                this.selectedAutocompleteIndex--;
                this.updateSelection();
                this.scrollToSelected();
            }
        }

        updateSelection() {
            const items = this.autocompleteList.querySelectorAll('.autocomplete-item');
            items.forEach((item, idx) => {
                if (idx === this.selectedAutocompleteIndex) {
                    item.classList.add('selected');
                } else {
                    item.classList.remove('selected');
                }
            });
        }

        scrollToSelected() {
            const selected = this.autocompleteList.querySelector('.autocomplete-item.selected');
            if (selected) {
                selected.scrollIntoView({ block: 'nearest' });
            }
        }

        insertSelectedCommand() {
            if (this.selectedAutocompleteIndex === -1) return;

            const command = this.filteredCommands[this.selectedAutocompleteIndex];
            if (!command) return;

            const cursorPosition = this.input.selectionStart;
            const value = this.input.value;
            const textBeforeCursor = value.substring(0, cursorPosition);
            const textAfterCursor = value.substring(cursorPosition);

            // Find the partial command being typed
            const words = textBeforeCursor.split(/(\s+)/);
            const lastWordIndex = words.length - 1;

            if (words[lastWordIndex].startsWith('/')) {
                words[lastWordIndex] = command.command;
                const newTextBefore = words.join('');
                const newValue = newTextBefore + ' ' + textAfterCursor;

                this.input.value = newValue;
                this.input.setSelectionRange(newTextBefore.length + 1, newTextBefore.length + 1);
                this.autoResize();
            }

            this.closeAutocomplete();
            this.input.focus();
        }

        escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        submit() {
            const value = this.input.value.trim();
            if (!value) return;

            // Add to history
            CommandHistory.add(value);
            CommandHistory.resetNavigation();

            // Close autocomplete
            this.closeAutocomplete();

            // Call onSubmit callback if provided
            if (this.options.onSubmit) {
                this.options.onSubmit(value);
            } else if (this.form) {
                // Trigger form submission
                this.form.dispatchEvent(new Event('submit', { bubbles: true }));

                // For HTMX forms, trigger htmx request
                if (window.htmx) {
                    window.htmx.trigger(this.form, 'submit');
                }
            }

            // Clear input
            this.input.value = '';
            this.autoResize();
            this.input.focus();
        }

        showTypingIndicator() {
            // Find or create typing indicator container
            let container = document.querySelector('.chat-thread-content, .message-thread');
            if (container) {
                TypingIndicator.show(container);
                TypingIndicator.autoHide();
            }
        }

        hideTypingIndicator() {
            TypingIndicator.hide();
        }

        focus() {
            this.input.focus();
            this.input.select();
        }

        /**
         * Register a custom slash command
         * @param {string} command - Command name (e.g., '/custom')
         * @param {string} description - Command description
         * @param {string} category - Command category
         * @param {Array<string>} aliases - Command aliases
         */
        registerCommand(command, description, category = 'custom', aliases = []) {
            SlashCommandRegistry.register(command, description, category, aliases);
        }

        /**
         * Get command history
         * @returns {Array} Command history
         */
        getHistory() {
            return CommandHistory.getAll();
        }

        /**
         * Clear command history
         */
        clearHistory() {
            CommandHistory.clear();
        }

        /**
         * Destroy the component
         */
        destroy() {
            if (this.autocompleteContainer) {
                this.autocompleteContainer.remove();
            }

            if (this.typingTimeout) {
                clearTimeout(this.typingTimeout);
            }

            TypingIndicator.hide();
        }
    }

    // ============================================
    // Public API
    // ============================================
    window.ChatInput = {
        instance: null,

        /**
         * Initialize the ChatInput component
         * @param {Object} options - Configuration options
         * @returns {ChatInput} The ChatInput instance
         */
        init(options = {}) {
            this.instance = new ChatInput(options);
            return this.instance;
        },

        /**
         * Get the current instance
         * @returns {ChatInput|null} The current instance
         */
        getInstance() {
            return this.instance;
        },

        /**
         * Register a slash command
         * @param {string} command - Command name
         * @param {string} description - Command description
         * @param {string} category - Command category
         * @param {Array<string>} aliases - Command aliases
         */
        registerCommand(command, description, category, aliases) {
            if (this.instance) {
                this.instance.registerCommand(command, description, category, aliases);
            } else {
                SlashCommandRegistry.register(command, description, category, aliases);
            }
        },

        /**
         * Focus the chat input
         */
        focus() {
            if (this.instance) {
                this.instance.focus();
            }
        },

        /**
         * Insert text into the input
         * @param {string} text - Text to insert
         */
        insert(text) {
            if (this.instance && this.instance.input) {
                this.instance.input.value = text;
                this.instance.autoResize();
                this.instance.focus();
            }
        },

        /**
         * Show typing indicator
         * @param {HTMLElement} container - Container element
         */
        showTypingIndicator(container) {
            TypingIndicator.show(container);
        },

        /**
         * Hide typing indicator
         */
        hideTypingIndicator() {
            TypingIndicator.hide();
        },

        /**
         * Destroy the component
         */
        destroy() {
            if (this.instance) {
                this.instance.destroy();
                this.instance = null;
            }
        }
    };

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            // Only auto-initialize if there's a chat input on the page
            if (document.querySelector('#chat-input')) {
                window.ChatInput.init();
            }
        });
    } else {
        if (document.querySelector('#chat-input')) {
            window.ChatInput.init();
        }
    }
})();
