/**
 * GMSD Artifact Links System
 * Automatically converts artifact references (TSK-123, PH-5, etc.) into clickable links
 */

(function () {
    'use strict';

    // Artifact prefix to route mapping
    const ARTIFACT_ROUTES = {
        'TSK': { route: '/Tasks/Details/', name: 'Task', color: '#3498db' },
        'PH': { route: '/Phases/Details/', name: 'Phase', color: '#9b59b6' },
        'MS': { route: '/Milestones/Details/', name: 'Milestone', color: '#e67e22' },
        'UAT': { route: '/Uat/Details/', name: 'UAT', color: '#27ae60' },
        'RUN': { route: '/Runs/Details/', name: 'Run', color: '#e74c3c' }
    };

    // Regex pattern to match artifact references
    // Matches: TSK-123, PH-5, MS-2026-01, UAT-42, RUN-789
    const ARTIFACT_PATTERN = /\b(TSK|PH|MS|UAT|RUN)-([\w-]+)\b/g;

    // CSS class names
    const CSS_CLASSES = {
        link: 'artifact-link',
        prefix: 'artifact-prefix',
        id: 'artifact-id'
    };

    /**
     * Initialize the artifact links system
     */
    function init() {
        // Process existing content
        processDocument();

        // Watch for dynamically added content
        observeMutations();

        // Expose API for manual processing
        window.GmsdArtifactLinks = {
            process: processElement,
            processText: processText,
            getRoute: getRoute,
            ARTIFACT_ROUTES: ARTIFACT_ROUTES
        };
    }

    /**
     * Process the entire document for artifact references
     */
    function processDocument() {
        const containers = document.querySelectorAll('[data-artifact-links]');
        if (containers.length === 0) {
            // If no specific containers marked, process main content areas
            const main = document.querySelector('main');
            if (main) {
                processElement(main);
            }
        } else {
            containers.forEach(processElement);
        }
    }

    /**
     * Observe DOM mutations to process dynamically added content
     */
    function observeMutations() {
        if (!window.MutationObserver) return;

        const observer = new MutationObserver(function (mutations) {
            mutations.forEach(function (mutation) {
                mutation.addedNodes.forEach(function (node) {
                    if (node.nodeType === Node.ELEMENT_NODE) {
                        processElement(node);
                    }
                });
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    /**
     * Process an element and its children for artifact references
     * @param {Element} element - The element to process
     */
    function processElement(element) {
        if (!element || element.nodeType !== Node.ELEMENT_NODE) return;

        // Skip if already processed or inside a script/style/code/pre
        if (element.hasAttribute('data-artifacts-processed')) return;
        if (isSkippedElement(element)) return;

        // Process text nodes
        const walker = document.createTreeWalker(
            element,
            NodeFilter.SHOW_TEXT,
            null,
            false
        );

        const nodesToProcess = [];
        let textNode;
        while ((textNode = walker.nextNode())) {
            if (ARTIFACT_PATTERN.test(textNode.textContent)) {
                nodesToProcess.push(textNode);
            }
        }

        // Process collected nodes (reverse order to avoid index issues)
        nodesToProcess.reverse().forEach(processTextNode);

        element.setAttribute('data-artifacts-processed', 'true');
    }

    /**
     * Check if element should be skipped
     * @param {Element} element
     * @returns {boolean}
     */
    function isSkippedElement(element) {
        const skipTags = ['SCRIPT', 'STYLE', 'CODE', 'PRE', 'TEXTAREA', 'INPUT'];
        return skipTags.includes(element.tagName) ||
               element.closest('code') ||
               element.closest('pre') ||
               element.classList.contains('no-artifact-links');
    }

    /**
     * Process a text node and convert artifact references to links
     * @param {Text} textNode
     */
    function processTextNode(textNode) {
        const text = textNode.textContent;
        if (!ARTIFACT_PATTERN.test(text)) return;

        // Reset regex lastIndex
        ARTIFACT_PATTERN.lastIndex = 0;

        const parent = textNode.parentNode;
        const fragment = document.createDocumentFragment();

        let lastIndex = 0;
        let match;

        while ((match = ARTIFACT_PATTERN.exec(text)) !== null) {
            // Add text before match
            if (match.index > lastIndex) {
                fragment.appendChild(
                    document.createTextNode(text.slice(lastIndex, match.index))
                );
            }

            // Create artifact link
            const link = createArtifactLink(match[1], match[2]);
            fragment.appendChild(link);

            lastIndex = ARTIFACT_PATTERN.lastIndex;
        }

        // Add remaining text
        if (lastIndex < text.length) {
            fragment.appendChild(document.createTextNode(text.slice(lastIndex)));
        }

        // Replace original node with fragment
        parent.replaceChild(fragment, textNode);
    }

    /**
     * Create an artifact link element
     * @param {string} prefix - The artifact prefix (TSK, PH, etc.)
     * @param {string} id - The artifact ID
     * @returns {HTMLElement}
     */
    function createArtifactLink(prefix, id) {
        const config = ARTIFACT_ROUTES[prefix];
        const link = document.createElement('a');

        link.href = config.route + encodeURIComponent(id);
        link.className = CSS_CLASSES.link;
        link.setAttribute('data-artifact-prefix', prefix);
        link.setAttribute('data-artifact-id', id);
        link.title = `${config.name} ${id}`;

        // Create prefix badge
        const prefixSpan = document.createElement('span');
        prefixSpan.className = CSS_CLASSES.prefix;
        prefixSpan.textContent = prefix;
        prefixSpan.style.backgroundColor = config.color;

        // Create ID span
        const idSpan = document.createElement('span');
        idSpan.className = CSS_CLASSES.id;
        idSpan.textContent = id;

        link.appendChild(prefixSpan);
        link.appendChild(document.createTextNode('-'));
        link.appendChild(idSpan);

        // Add click handler
        link.addEventListener('click', handleLinkClick);

        return link;
    }

    /**
     * Handle artifact link click
     * @param {Event} e
     */
    function handleLinkClick(e) {
        const link = e.currentTarget;
        const prefix = link.getAttribute('data-artifact-prefix');
        const id = link.getAttribute('data-artifact-id');
        const config = ARTIFACT_ROUTES[prefix];

        // Allow default navigation, but could add custom handling here
        console.log(`Navigating to ${config.name} ${id}`);
    }

    /**
     * Get route information for an artifact
     * @param {string} prefix - The artifact prefix
     * @param {string} id - The artifact ID
     * @returns {Object|null}
     */
    function getRoute(prefix, id) {
        const config = ARTIFACT_ROUTES[prefix.toUpperCase()];
        if (!config) return null;

        return {
            url: config.route + encodeURIComponent(id),
            name: config.name,
            color: config.color
        };
    }

    /**
     * Process text string and return HTML with artifact links
     * @param {string} text
     * @returns {string}
     */
    function processText(text) {
        ARTIFACT_PATTERN.lastIndex = 0;
        return text.replace(ARTIFACT_PATTERN, function (match, prefix, id) {
            const config = ARTIFACT_ROUTES[prefix];
            if (!config) return match;

            return `<a href="${config.route}${encodeURIComponent(id)}" ` +
                   `class="${CSS_CLASSES.link}" ` +
                   `data-artifact-prefix="${prefix}" ` +
                   `data-artifact-id="${id}" ` +
                   `title="${config.name} ${id}">` +
                   `<span class="${CSS_CLASSES.prefix}" style="background-color: ${config.color}">${prefix}</span>` +
                   `-` +
                   `<span class="${CSS_CLASSES.id}">${id}</span>` +
                   `</a>`;
        });
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
