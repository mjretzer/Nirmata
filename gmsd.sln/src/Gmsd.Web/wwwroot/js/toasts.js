// GMSD Toast Notification System
// Provides global toast notifications with auto-dismiss and manual dismiss support

(function () {
    'use strict';

    // Toast configuration
    const DEFAULT_DURATION = 5000; // 5 seconds
    const TOAST_CONTAINER_ID = 'toast-container';

    // Toast type definitions with styling
    const TOAST_TYPES = {
        success: {
            icon: '✓',
            className: 'toast-success',
            defaultTitle: 'Success'
        },
        error: {
            icon: '✕',
            className: 'toast-error',
            defaultTitle: 'Error'
        },
        warning: {
            icon: '⚠',
            className: 'toast-warning',
            defaultTitle: 'Warning'
        },
        info: {
            icon: 'ℹ',
            className: 'toast-info',
            defaultTitle: 'Info'
        }
    };

    let toastCounter = 0;
    let container = null;

    /**
     * Initialize the toast container
     */
    function initContainer() {
        if (container) return;

        container = document.getElementById(TOAST_CONTAINER_ID);
        if (!container) {
            container = document.createElement('div');
            container.id = TOAST_CONTAINER_ID;
            container.className = 'toast-container';
            document.body.appendChild(container);
        }
    }

    /**
     * Create a toast notification
     * @param {string} message - The message to display
     * @param {Object} options - Toast options
     * @param {string} options.type - Toast type: 'success', 'error', 'warning', 'info'
     * @param {string} options.title - Custom title (optional)
     * @param {number} options.duration - Duration in milliseconds (default: 5000, null for persistent)
     * @param {boolean} options.dismissible - Whether to show dismiss button (default: true)
     * @returns {string} Toast ID
     */
    function show(message, options) {
        options = options || {};
        const type = options.type || 'info';
        const typeConfig = TOAST_TYPES[type] || TOAST_TYPES.info;

        initContainer();

        const toastId = 'toast-' + (++toastCounter);
        const duration = options.duration !== undefined ? options.duration : DEFAULT_DURATION;
        const dismissible = options.dismissible !== false;

        // Create toast element
        const toast = document.createElement('div');
        toast.id = toastId;
        toast.className = 'toast ' + typeConfig.className;
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'polite');

        // Toast content
        const title = options.title || typeConfig.defaultTitle;
        const icon = typeConfig.icon;

        toast.innerHTML = [
            '<div class="toast-icon">' + icon + '</div>',
            '<div class="toast-content">',
            '<div class="toast-title">' + escapeHtml(title) + '</div>',
            '<div class="toast-message">' + escapeHtml(message) + '</div>',
            '</div>',
            dismissible ? '<button class="toast-close" aria-label="Dismiss">×</button>' : ''
        ].join('');

        // Add close handler
        if (dismissible) {
            const closeBtn = toast.querySelector('.toast-close');
            closeBtn.addEventListener('click', function () {
                dismiss(toastId);
            });
        }

        // Add to container
        container.appendChild(toast);

        // Trigger animation (next frame)
        requestAnimationFrame(function () {
            toast.classList.add('toast-visible');
        });

        // Auto-dismiss
        let dismissTimer = null;
        if (duration !== null && duration > 0) {
            dismissTimer = setTimeout(function () {
                dismiss(toastId);
            }, duration);
        }

        // Store dismiss timer for cleanup
        toast._dismissTimer = dismissTimer;

        return toastId;
    }

    /**
     * Dismiss a specific toast by ID
     * @param {string} toastId - The toast ID to dismiss
     */
    function dismiss(toastId) {
        const toast = document.getElementById(toastId);
        if (!toast) return;

        // Clear auto-dismiss timer
        if (toast._dismissTimer) {
            clearTimeout(toast._dismissTimer);
        }

        // Trigger exit animation
        toast.classList.remove('toast-visible');
        toast.classList.add('toast-hiding');

        // Remove after animation
        setTimeout(function () {
            if (toast.parentNode) {
                toast.parentNode.removeChild(toast);
            }
        }, 300);
    }

    /**
     * Dismiss all toasts
     */
    function dismissAll() {
        const toasts = document.querySelectorAll('.toast');
        toasts.forEach(function (toast) {
            dismiss(toast.id);
        });
    }

    /**
     * Convenience methods for each toast type
     */
    function success(message, options) {
        options = options || {};
        options.type = 'success';
        return show(message, options);
    }

    function error(message, options) {
        options = options || {};
        options.type = 'error';
        return show(message, options);
    }

    function warning(message, options) {
        options = options || {};
        options.type = 'warning';
        return show(message, options);
    }

    function info(message, options) {
        options = options || {};
        options.type = 'info';
        return show(message, options);
    }

    /**
     * Escape HTML to prevent XSS
     * @param {string} text - Text to escape
     * @returns {string} Escaped text
     */
    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Process server-side toasts from TempData
     * Looks for window.GmsdServerToasts array and displays them
     */
    function processServerToasts() {
        if (window.GmsdServerToasts && Array.isArray(window.GmsdServerToasts)) {
            window.GmsdServerToasts.forEach(function (toast) {
                show(toast.message, {
                    type: toast.type || 'info',
                    title: toast.title,
                    duration: toast.duration
                });
            });
            // Clear processed toasts
            window.GmsdServerToasts = [];
        }
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            initContainer();
            processServerToasts();
        });
    } else {
        initContainer();
        processServerToasts();
    }

    // Expose public API
    window.GmsdToasts = {
        show: show,
        success: success,
        error: error,
        warning: warning,
        info: info,
        dismiss: dismiss,
        dismissAll: dismissAll
    };

})();
