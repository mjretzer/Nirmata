/**
 * UserFeedback - Client-side feedback collection and analytics
 * Features:
 * - In-app feedback button
 * - Task completion time tracking (opt-in)
 * - Navigation click analytics (opt-in)
 * - User satisfaction survey
 */
(function () {
    'use strict';

    // ============================================
    // Configuration
    // ============================================
    const CONFIG = {
        apiBasePath: '/api/feedback',
        surveyTriggerDelay: 5 * 60 * 1000, // 5 minutes of active usage
        surveyCooldownDays: 30,
        maxDismissCount: 3,
        storageKeys: {
            preferences: 'gmsd_feedback_preferences',
            sessionStart: 'gmsd_session_start',
            lastSurvey: 'gmsd_last_survey',
            dismissCount: 'gmsd_survey_dismiss_count',
            activeTasks: 'gmsd_active_tasks'
        }
    };

    // ============================================
    // User Preferences Management
    // ============================================
    const PreferencesManager = {
        get() {
            const stored = localStorage.getItem(CONFIG.storageKeys.preferences);
            if (stored) {
                try {
                    return JSON.parse(stored);
                } catch {
                    return this.getDefaults();
                }
            }
            return this.getDefaults();
        },

        getDefaults() {
            return {
                enableTaskTiming: false,
                enableClickAnalytics: false,
                enableDetailedLogging: false,
                lastSurveyDate: null,
                surveyDismissCount: 0
            };
        },

        save(preferences) {
            localStorage.setItem(CONFIG.storageKeys.preferences, JSON.stringify(preferences));
            // Also sync to server
            this.syncToServer(preferences);
        },

        async syncToServer(preferences) {
            try {
                await fetch(`${CONFIG.apiBasePath}/preferences`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        enableTaskTiming: preferences.enableTaskTiming,
                        enableClickAnalytics: preferences.enableClickAnalytics,
                        enableDetailedLogging: preferences.enableDetailedLogging,
                        lastSurveyDate: preferences.lastSurveyDate,
                        surveyDismissCount: preferences.surveyDismissCount
                    })
                });
            } catch (err) {
                console.debug('Failed to sync preferences:', err);
            }
        },

        async loadFromServer() {
            try {
                const response = await fetch(`${CONFIG.apiBasePath}/preferences`);
                if (response.ok) {
                    const serverPrefs = await response.json();
                    const localPrefs = this.get();
                    // Merge server data with local
                    const merged = { ...localPrefs, ...serverPrefs };
                    localStorage.setItem(CONFIG.storageKeys.preferences, JSON.stringify(merged));
                    return merged;
                }
            } catch (err) {
                console.debug('Failed to load preferences from server:', err);
            }
            return this.get();
        }
    };

    // ============================================
    // Task Timing Tracker (Opt-in)
    // ============================================
    const TaskTimer = {
        activeTasks: new Map(),

        start(taskId, taskType, description) {
            const preferences = PreferencesManager.get();
            if (!preferences.enableTaskTiming) return;

            const task = {
                taskId,
                taskType,
                description,
                startTime: Date.now(),
                steps: 0
            };

            this.activeTasks.set(taskId, task);
            this.saveActiveTasks();

            console.debug(`[TaskTimer] Started: ${taskType} (${taskId})`);
        },

        step(taskId) {
            const task = this.activeTasks.get(taskId);
            if (task) {
                task.steps++;
            }
        },

        end(taskId, success = true, errorMessage = null) {
            const task = this.activeTasks.get(taskId);
            if (!task) return;

            const endTime = Date.now();
            const duration = endTime - task.startTime;

            // Remove from active tasks
            this.activeTasks.delete(taskId);
            this.saveActiveTasks();

            // Send to server
            this.submitTiming({
                taskId: task.taskId,
                taskType: task.taskType,
                description: task.description,
                startTime: new Date(task.startTime).toISOString(),
                endTime: new Date(endTime).toISOString(),
                wasSuccessful: success,
                errorMessage: errorMessage,
                stepCount: task.steps
            });

            console.debug(`[TaskTimer] Completed: ${task.taskType} (${taskId}) in ${duration}ms`);
        },

        async submitTiming(data) {
            try {
                await fetch(`${CONFIG.apiBasePath}/timing`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(data)
                });
            } catch (err) {
                console.debug('Failed to submit timing:', err);
            }
        },

        saveActiveTasks() {
            // Persist to session storage in case of page refresh
            const tasks = Array.from(this.activeTasks.entries());
            sessionStorage.setItem(CONFIG.storageKeys.activeTasks, JSON.stringify(tasks));
        },

        restoreActiveTasks() {
            const stored = sessionStorage.getItem(CONFIG.storageKeys.activeTasks);
            if (stored) {
                try {
                    const tasks = JSON.parse(stored);
                    this.activeTasks = new Map(tasks);
                    // End any tasks that were running before refresh (they likely failed)
                    this.activeTasks.forEach((task, id) => {
                        this.end(id, false, 'Session ended unexpectedly');
                    });
                    this.activeTasks.clear();
                    sessionStorage.removeItem(CONFIG.storageKeys.activeTasks);
                } catch {
                    // Ignore
                }
            }
        }
    };

    // ============================================
    // Navigation Click Analytics (Opt-in)
    // ============================================
    const ClickAnalytics = {
        init() {
            const preferences = PreferencesManager.get();
            if (!preferences.enableClickAnalytics) return;

            // Track clicks on interactive elements
            document.addEventListener('click', this.handleClick.bind(this), true);
            console.debug('[ClickAnalytics] Initialized');
        },

        handleClick(e) {
            const preferences = PreferencesManager.get();
            if (!preferences.enableClickAnalytics) return;

            const target = e.target.closest('[data-track], button, a, [role="button"]');
            if (!target) return;

            const elementId = target.id ||
                target.dataset.track ||
                target.getAttribute('aria-label') ||
                target.textContent?.substring(0, 50);

            const elementType = target.tagName.toLowerCase();
            const pageUrl = window.location.href;
            const screen = document.body.dataset.screen || 'unknown';

            // Debounce rapid clicks
            clearTimeout(this.debounceTimer);
            this.debounceTimer = setTimeout(() => {
                this.submitEvent({
                    eventType: 'click',
                    elementId: elementId,
                    elementType: elementType,
                    pageUrl: pageUrl,
                    screen: screen,
                    timestamp: new Date().toISOString(),
                    metadata: {
                        path: target.dataset.trackPath || null,
                        command: target.dataset.command || null,
                        panel: target.closest('[data-panel]')?.dataset.panel || null
                    }
                });
            }, 100);
        },

        async submitEvent(data) {
            try {
                await fetch(`${CONFIG.apiBasePath}/analytics`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(data)
                });
            } catch (err) {
                console.debug('Failed to submit analytics:', err);
            }
        }
    };

    // ============================================
    // Feedback UI Components
    // ============================================
    const FeedbackUI = {
        init() {
            this.createFeedbackButton();
            this.createSurveyModal();
            this.createFeedbackModal();
            this.createPreferencesPanel();
        },

        createFeedbackButton() {
            // Check for inline feedback button first
            const inlineBtn = document.getElementById('feedback-btn-inline');
            if (inlineBtn) {
                inlineBtn.addEventListener('click', () => this.showFeedbackModal());
                return;
            }

            // Create floating feedback button if no inline button exists
            const button = document.createElement('button');
            button.id = 'feedback-btn';
            button.className = 'feedback-btn';
            button.setAttribute('aria-label', 'Send feedback');
            button.setAttribute('title', 'Send feedback');
            button.innerHTML = '<span aria-hidden="true">💬</span>';
            button.addEventListener('click', () => this.showFeedbackModal());

            // Insert into chat input bar
            const chatInputBar = document.getElementById('chat-input-bar');
            if (chatInputBar) {
                chatInputBar.querySelector('.chat-input-wrapper')?.appendChild(button);
            }
        },

        createSurveyModal() {
            const modal = document.createElement('div');
            modal.id = 'survey-modal';
            modal.className = 'feedback-modal survey-modal';
            modal.setAttribute('role', 'dialog');
            modal.setAttribute('aria-modal', 'true');
            modal.setAttribute('aria-labelledby', 'survey-title');
            modal.style.display = 'none';

            modal.innerHTML = `
                <div class="feedback-modal-overlay"></div>
                <div class="feedback-modal-content">
                    <header class="feedback-modal-header">
                        <h2 id="survey-title">How's your experience?</h2>
                        <button class="feedback-modal-close" aria-label="Close survey">&times;</button>
                    </header>
                    <div class="feedback-modal-body">
                        <p>Your feedback helps us improve GMSD.</p>
                        <div class="survey-rating">
                            <label>Rate your experience:</label>
                            <div class="star-rating" role="radiogroup" aria-label="Rating">
                                ${[1, 2, 3, 4, 5].map(i => `
                                    <button type="button" class="star-btn" data-rating="${i}" 
                                            aria-label="${i} star${i > 1 ? 's' : ''}" role="radio" aria-checked="false">
                                        <span aria-hidden="true">★</span>
                                    </button>
                                `).join('')}
                            </div>
                        </div>
                        <div class="survey-comment">
                            <label for="survey-comment">Additional comments (optional):</label>
                            <textarea id="survey-comment" rows="3" maxlength="1000" 
                                      placeholder="Tell us what you like or how we can improve..."></textarea>
                        </div>
                        <div class="survey-followup">
                            <label>
                                <input type="checkbox" id="survey-followup">
                                Allow us to follow up about your feedback
                            </label>
                            <input type="email" id="survey-email" placeholder="Your email (optional)" 
                                   style="display: none; margin-top: 0.5rem;">
                        </div>
                    </div>
                    <footer class="feedback-modal-footer">
                        <button type="button" class="btn btn-secondary survey-dismiss">Not now</button>
                        <button type="button" class="btn btn-primary survey-submit" disabled>Submit Feedback</button>
                    </footer>
                </div>
            `;

            document.body.appendChild(modal);
            this.bindSurveyEvents(modal);
        },

        createFeedbackModal() {
            const modal = document.createElement('div');
            modal.id = 'feedback-modal';
            modal.className = 'feedback-modal';
            modal.setAttribute('role', 'dialog');
            modal.setAttribute('aria-modal', 'true');
            modal.setAttribute('aria-labelledby', 'feedback-title');
            modal.style.display = 'none';

            modal.innerHTML = `
                <div class="feedback-modal-overlay"></div>
                <div class="feedback-modal-content">
                    <header class="feedback-modal-header">
                        <h2 id="feedback-title">Send Feedback</h2>
                        <button class="feedback-modal-close" aria-label="Close feedback form">&times;</button>
                    </header>
                    <div class="feedback-modal-body">
                        <div class="feedback-type-selector">
                            <label>Feedback type:</label>
                            <div class="feedback-types">
                                <button type="button" class="feedback-type-btn active" data-type="general">
                                    <span aria-hidden="true">💡</span> General
                                </button>
                                <button type="button" class="feedback-type-btn" data-type="bug">
                                    <span aria-hidden="true">🐛</span> Bug Report
                                </button>
                                <button type="button" class="feedback-type-btn" data-type="feature">
                                    <span aria-hidden="true">✨</span> Feature Request
                                </button>
                            </div>
                        </div>
                        <div class="feedback-message">
                            <label for="feedback-message">Your feedback:</label>
                            <textarea id="feedback-message" rows="5" maxlength="2000" required
                                      placeholder="Describe your feedback in detail..."></textarea>
                        </div>
                    </div>
                    <footer class="feedback-modal-footer">
                        <button type="button" class="btn btn-secondary feedback-close">Cancel</button>
                        <button type="button" class="btn btn-primary feedback-submit">Submit</button>
                    </footer>
                </div>
            `;

            document.body.appendChild(modal);
            this.bindFeedbackEvents(modal);
        },

        createPreferencesPanel() {
            const panel = document.createElement('div');
            panel.id = 'feedback-preferences-panel';
            panel.className = 'feedback-preferences-panel';
            panel.style.display = 'none';

            panel.innerHTML = `
                <div class="preferences-content">
                    <h3>Feedback Preferences</h3>
                    <div class="preference-item">
                        <label class="toggle-label">
                            <input type="checkbox" id="pref-task-timing">
                            <span class="toggle-slider"></span>
                            <span class="toggle-text">
                                <strong>Task Completion Tracking</strong>
                                <small>Help us understand how long tasks take to complete</small>
                            </span>
                        </label>
                    </div>
                    <div class="preference-item">
                        <label class="toggle-label">
                            <input type="checkbox" id="pref-click-analytics">
                            <span class="toggle-slider"></span>
                            <span class="toggle-text">
                                <strong>Usage Analytics</strong>
                                <small>Anonymously track which features you use most</small>
                            </span>
                        </label>
                    </div>
                    <div class="preference-actions">
                        <button type="button" class="btn btn-primary pref-save">Save Preferences</button>
                    </div>
                </div>
            `;

            document.body.appendChild(panel);
            this.bindPreferencesEvents(panel);
        },

        bindSurveyEvents(modal) {
            const overlay = modal.querySelector('.feedback-modal-overlay');
            const closeBtn = modal.querySelector('.feedback-modal-close');
            const dismissBtn = modal.querySelector('.survey-dismiss');
            const submitBtn = modal.querySelector('.survey-submit');
            const stars = modal.querySelectorAll('.star-btn');
            const comment = modal.querySelector('#survey-comment');
            const followup = modal.querySelector('#survey-followup');
            const email = modal.querySelector('#survey-email');

            let selectedRating = 0;

            const close = () => {
                modal.style.display = 'none';
                document.body.style.overflow = '';
            };

            overlay.addEventListener('click', close);
            closeBtn.addEventListener('click', close);
            dismissBtn.addEventListener('click', () => {
                this.recordSurveyDismissal();
                close();
            });

            stars.forEach(star => {
                star.addEventListener('click', () => {
                    selectedRating = parseInt(star.dataset.rating);
                    stars.forEach((s, i) => {
                        s.classList.toggle('active', i < selectedRating);
                        s.setAttribute('aria-checked', i < selectedRating ? 'true' : 'false');
                    });
                    submitBtn.disabled = selectedRating === 0;
                });
            });

            followup.addEventListener('change', () => {
                email.style.display = followup.checked ? 'block' : 'none';
            });

            submitBtn.addEventListener('click', async () => {
                if (selectedRating === 0) return;

                await this.submitSurvey({
                    rating: selectedRating,
                    comment: comment.value,
                    allowFollowUp: followup.checked,
                    contactEmail: email.value || null
                });

                close();
            });
        },

        bindFeedbackEvents(modal) {
            const overlay = modal.querySelector('.feedback-modal-overlay');
            const closeBtn = modal.querySelector('.feedback-modal-close');
            const closeBtn2 = modal.querySelector('.feedback-close');
            const submitBtn = modal.querySelector('.feedback-submit');
            const typeBtns = modal.querySelectorAll('.feedback-type-btn');
            const message = modal.querySelector('#feedback-message');

            let selectedType = 'general';

            const close = () => {
                modal.style.display = 'none';
                document.body.style.overflow = '';
            };

            overlay.addEventListener('click', close);
            closeBtn.addEventListener('click', close);
            closeBtn2.addEventListener('click', close);

            typeBtns.forEach(btn => {
                btn.addEventListener('click', () => {
                    selectedType = btn.dataset.type;
                    typeBtns.forEach(b => b.classList.remove('active'));
                    btn.classList.add('active');
                });
            });

            submitBtn.addEventListener('click', async () => {
                if (!message.value.trim()) {
                    message.focus();
                    return;
                }

                await this.submitGeneralFeedback({
                    type: selectedType,
                    message: message.value,
                    screen: document.body.dataset.screen || 'unknown'
                });

                message.value = '';
                close();
            });
        },

        bindPreferencesEvents(panel) {
            const taskTiming = panel.querySelector('#pref-task-timing');
            const clickAnalytics = panel.querySelector('#pref-click-analytics');
            const saveBtn = panel.querySelector('.pref-save');

            // Load current preferences
            const prefs = PreferencesManager.get();
            taskTiming.checked = prefs.enableTaskTiming;
            clickAnalytics.checked = prefs.enableClickAnalytics;

            saveBtn.addEventListener('click', () => {
                const newPrefs = {
                    ...prefs,
                    enableTaskTiming: taskTiming.checked,
                    enableClickAnalytics: clickAnalytics.checked
                };
                PreferencesManager.save(newPrefs);
                panel.style.display = 'none';

                // Re-initialize analytics if enabled
                if (newPrefs.enableClickAnalytics) {
                    ClickAnalytics.init();
                }

                if (window.showToast) {
                    window.showToast('Preferences saved', 'success');
                }
            });
        },

        showSurvey() {
            const modal = document.getElementById('survey-modal');
            if (modal) {
                modal.style.display = 'block';
                document.body.style.overflow = 'hidden';
            }
        },

        showFeedbackModal() {
            const modal = document.getElementById('feedback-modal');
            if (modal) {
                modal.style.display = 'block';
                document.body.style.overflow = 'hidden';
            }
        },

        showPreferences() {
            const panel = document.getElementById('feedback-preferences-panel');
            if (panel) {
                panel.style.display = 'block';
            }
        },

        async submitSurvey(data) {
            try {
                const response = await fetch(`${CONFIG.apiBasePath}/survey`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(data)
                });

                if (response.ok) {
                    // Update local preferences
                    const prefs = PreferencesManager.get();
                    prefs.lastSurveyDate = new Date().toISOString();
                    PreferencesManager.save(prefs);

                    if (window.showToast) {
                        window.showToast('Thank you for your feedback!', 'success');
                    }
                }
            } catch (err) {
                console.error('Failed to submit survey:', err);
                if (window.showToast) {
                    window.showToast('Failed to submit feedback', 'error');
                }
            }
        },

        async submitGeneralFeedback(data) {
            try {
                const response = await fetch(`${CONFIG.apiBasePath}/general`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(data)
                });

                if (response.ok && window.showToast) {
                    window.showToast('Feedback submitted. Thank you!', 'success');
                }
            } catch (err) {
                console.error('Failed to submit feedback:', err);
                if (window.showToast) {
                    window.showToast('Failed to submit feedback', 'error');
                }
            }
        },

        async recordSurveyDismissal() {
            try {
                await fetch(`${CONFIG.apiBasePath}/survey-dismiss`, { method: 'POST' });
                const prefs = PreferencesManager.get();
                prefs.surveyDismissCount++;
                PreferencesManager.save(prefs);
            } catch (err) {
                console.debug('Failed to record dismissal:', err);
            }
        },

        async checkSurveyEligibility() {
            try {
                const response = await fetch(`${CONFIG.apiBasePath}/survey-eligible`);
                if (response.ok) {
                    const data = await response.json();
                    return data.isEligible;
                }
            } catch (err) {
                console.debug('Failed to check eligibility:', err);
            }
            return false;
        }
    };

    // ============================================
    // Survey Trigger Logic
    // ============================================
    const SurveyTrigger = {
        init() {
            // Check for survey eligibility after user has been active for a while
            setTimeout(() => this.checkAndTrigger(), CONFIG.surveyTriggerDelay);
        },

        async checkAndTrigger() {
            const isEligible = await FeedbackUI.checkSurveyEligibility();
            if (isEligible) {
                FeedbackUI.showSurvey();
            }
        }
    };

    // ============================================
    // Public API
    // ============================================
    window.UserFeedback = {
        // Preferences
        getPreferences: () => PreferencesManager.get(),
        savePreferences: (prefs) => PreferencesManager.save(prefs),
        showPreferences: () => FeedbackUI.showPreferences(),

        // Task Timing
        startTask: (id, type, description) => TaskTimer.start(id, type, description),
        taskStep: (id) => TaskTimer.step(id),
        endTask: (id, success, error) => TaskTimer.end(id, success, error),

        // Feedback
        showFeedback: () => FeedbackUI.showFeedbackModal(),
        showSurvey: () => FeedbackUI.showSurvey(),

        // Initialization
        init: async () => {
            await PreferencesManager.loadFromServer();
            FeedbackUI.init();
            ClickAnalytics.init();
            SurveyTrigger.init();
            TaskTimer.restoreActiveTasks();
            console.log('[UserFeedback] Initialized');
        }
    };

    // Auto-initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', window.UserFeedback.init);
    } else {
        window.UserFeedback.init();
    }
})();
