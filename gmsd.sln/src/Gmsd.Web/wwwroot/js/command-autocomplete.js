/**
 * @fileoverview Command Autocomplete Component
 * @description Provides slash command autocomplete with hints and descriptions
 */

/**
 * Command autocomplete manager for chat input
 */
class CommandAutocomplete {
    constructor(inputElement, options = {}) {
        this.inputElement = inputElement;
        this.options = {
            minChars: 1,
            maxSuggestions: 10,
            debounceMs: 100,
            ...options
        };

        this.dropdownElement = null;
        this.selectedIndex = -1;
        this.suggestions = [];
        this.debounceTimer = null;

        this._initializeEventListeners();
    }

    /**
     * Sets available commands for autocomplete
     * @param {Array<{name: string, syntax: string, description: string, args?: string[]}>} commands
     */
    setCommands(commands) {
        this.commands = commands || [];
    }

    /**
     * Initializes event listeners on the input element
     * @private
     */
    _initializeEventListeners() {
        this.inputElement.addEventListener('input', (e) => this._onInput(e));
        this.inputElement.addEventListener('keydown', (e) => this._onKeyDown(e));
        this.inputElement.addEventListener('blur', () => this._closeDropdown());
    }

    /**
     * Handles input event with debouncing
     * @private
     */
    _onInput(event) {
        clearTimeout(this.debounceTimer);
        this.debounceTimer = setTimeout(() => {
            this._updateSuggestions();
        }, this.options.debounceMs);
    }

    /**
     * Handles keyboard navigation
     * @private
     */
    _onKeyDown(event) {
        if (!this.dropdownElement || this.suggestions.length === 0) {
            return;
        }

        switch (event.key) {
            case 'ArrowDown':
                event.preventDefault();
                this._selectNext();
                break;
            case 'ArrowUp':
                event.preventDefault();
                this._selectPrevious();
                break;
            case 'Enter':
                if (this.selectedIndex >= 0) {
                    event.preventDefault();
                    this._insertSelected();
                }
                break;
            case 'Escape':
                this._closeDropdown();
                break;
        }
    }

    /**
     * Updates suggestions based on current input
     * @private
     */
    _updateSuggestions() {
        const input = this.inputElement.value;
        const cursorPos = this.inputElement.selectionStart;
        const textBeforeCursor = input.substring(0, cursorPos);

        // Check if we're at a slash command
        const lastSlashIndex = textBeforeCursor.lastIndexOf('/');
        if (lastSlashIndex === -1) {
            this._closeDropdown();
            return;
        }

        const commandText = textBeforeCursor.substring(lastSlashIndex + 1);
        if (commandText.length < this.options.minChars) {
            this._closeDropdown();
            return;
        }

        // Filter commands
        this.suggestions = this.commands
            .filter(cmd => cmd.name.toLowerCase().startsWith(commandText.toLowerCase()))
            .slice(0, this.options.maxSuggestions);

        if (this.suggestions.length === 0) {
            this._closeDropdown();
            return;
        }

        this.selectedIndex = -1;
        this._renderDropdown();
    }

    /**
     * Renders the autocomplete dropdown
     * @private
     */
    _renderDropdown() {
        if (!this.dropdownElement) {
            this.dropdownElement = document.createElement('div');
            this.dropdownElement.className = 'command-autocomplete-dropdown';
            this.inputElement.parentElement.appendChild(this.dropdownElement);
        }

        const html = this.suggestions.map((cmd, index) => `
            <div class="command-autocomplete-item" data-index="${index}">
                <div class="command-autocomplete-item__name">${this._escapeHtml(cmd.syntax)}</div>
                <div class="command-autocomplete-item__description">${this._escapeHtml(cmd.description)}</div>
            </div>
        `).join('');

        this.dropdownElement.innerHTML = html;
        this.dropdownElement.style.display = 'block';

        // Add click handlers
        this.dropdownElement.querySelectorAll('.command-autocomplete-item').forEach(item => {
            item.addEventListener('click', () => {
                this.selectedIndex = parseInt(item.dataset.index);
                this._insertSelected();
            });
        });
    }

    /**
     * Closes the dropdown
     * @private
     */
    _closeDropdown() {
        if (this.dropdownElement) {
            this.dropdownElement.style.display = 'none';
        }
        this.selectedIndex = -1;
    }

    /**
     * Selects the next suggestion
     * @private
     */
    _selectNext() {
        this.selectedIndex = Math.min(this.selectedIndex + 1, this.suggestions.length - 1);
        this._updateSelection();
    }

    /**
     * Selects the previous suggestion
     * @private
     */
    _selectPrevious() {
        this.selectedIndex = Math.max(this.selectedIndex - 1, -1);
        this._updateSelection();
    }

    /**
     * Updates visual selection in dropdown
     * @private
     */
    _updateSelection() {
        this.dropdownElement.querySelectorAll('.command-autocomplete-item').forEach((item, index) => {
            if (index === this.selectedIndex) {
                item.classList.add('command-autocomplete-item--selected');
            } else {
                item.classList.remove('command-autocomplete-item--selected');
            }
        });
    }

    /**
     * Inserts the selected command
     * @private
     */
    _insertSelected() {
        if (this.selectedIndex < 0 || this.selectedIndex >= this.suggestions.length) {
            return;
        }

        const selected = this.suggestions[this.selectedIndex];
        const input = this.inputElement.value;
        const cursorPos = this.inputElement.selectionStart;
        const textBeforeCursor = input.substring(0, cursorPos);

        const lastSlashIndex = textBeforeCursor.lastIndexOf('/');
        const textAfterCursor = input.substring(cursorPos);

        const newValue = input.substring(0, lastSlashIndex) + selected.syntax + ' ' + textAfterCursor;
        this.inputElement.value = newValue;
        this.inputElement.selectionStart = lastSlashIndex + selected.syntax.length + 1;
        this.inputElement.selectionEnd = this.inputElement.selectionStart;

        this._closeDropdown();
        this.inputElement.focus();
    }

    /**
     * Escapes HTML special characters
     * @private
     */
    _escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}
