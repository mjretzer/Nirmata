// GMSD Site JavaScript

(function () {
    'use strict';

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', function () {
        console.log('GMSD Web application initialized');
        initializeNavigation();
        initializeCommandPaletteShortcut();
    });

    // Command Palette keyboard shortcut (Ctrl+K)
    function initializeCommandPaletteShortcut() {
        document.addEventListener('keydown', function (e) {
            // Ctrl+K or Cmd+K to toggle command palette
            if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                e.preventDefault();
                if (window.GmsdCommandPalette) {
                    window.GmsdCommandPalette.toggle();
                }
            }
        });
    }

    // Navigation active state
    function initializeNavigation() {
        function updateActiveNav() {
            const path = window.location.pathname.toLowerCase();

            // Clear all active states
            document.querySelectorAll('.nav-link').forEach(function(link) {
                link.classList.remove('active');
            });

            // Find best match (longest href that path starts with)
            let bestMatch = null;
            let bestLength = 0;

            document.querySelectorAll('.nav-link').forEach(function(link) {
                let href = (link.getAttribute('href') || '').toLowerCase().replace('~/', '/');
                if (!href || href === '/') return;

                if (path.startsWith(href) && href.length > bestLength) {
                    bestMatch = link;
                    bestLength = href.length;
                }
            });

            // Set active or fallback to home
            if (bestMatch) {
                bestMatch.classList.add('active');
            } else if (path === '/' || path === '/index') {
                const homeLink = document.querySelector('.nav-link[href="~/"], .nav-link[href="/"]');
                if (homeLink) homeLink.classList.add('active');
            }
        }

        // Run on load
        updateActiveNav();

        // Re-run after HTMX navigation
        document.addEventListener('htmx:afterSettle', updateActiveNav);
        document.addEventListener('htmx:pushedIntoHistory', updateActiveNav);
    }

    // Utility function for confirming actions
    window.confirmAction = function (message) {
        return confirm(message || 'Are you sure you want to proceed?');
    };

    // Format date for display
    window.formatDate = function (dateString) {
        if (!dateString) return 'N/A';
        const date = new Date(dateString);
        return date.toLocaleDateString() + ' ' + date.toLocaleTimeString();
    };
})();
