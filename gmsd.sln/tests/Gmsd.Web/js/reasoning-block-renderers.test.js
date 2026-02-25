/**
 * @fileoverview Reasoning Block Renderer Tests
 * @description Unit tests for IntentClassifiedRenderer and GateSelectedRenderer
 * Run with: node --test tests/Gmsd.Web/js/reasoning-block-renderers.test.js
 */

const { describe, it, beforeEach } = require('node:test');
const assert = require('node:assert');

// Setup global mocks and dependencies
global.document = {
    createElement: function(tag) {
        return {
            textContent: '',
            get innerHTML() { return this.textContent; },
            set textContent(value) { this._text = value; },
            get textContent() { return this._text || ''; }
        };
    }
};

// Mock CustomEvent for Node.js
global.CustomEvent = class CustomEvent extends Event {
    constructor(type, options = {}) {
        super(type);
        this.detail = options.detail;
    }
};

// Load dependencies in order
const eventTypes = require('../../../Gmsd.Web/wwwroot/js/event-types.js');
global.StreamingEventType = eventTypes.StreamingEventType;
global.EventCategory = eventTypes.EventCategory;

const ieventRenderer = require('../../../Gmsd.Web/wwwroot/js/ievent-renderer.js');
global.IEventRenderer = ieventRenderer.IEventRenderer;
global.EventRendererBase = ieventRenderer.EventRendererBase;

// Override escapeHtml for Node.js testing
global.EventRendererBase.prototype.escapeHtml = function(text) {
    if (!text) return '';
    return text
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
};

const {
    IntentClassifiedRenderer,
    GateSelectedRenderer,
    createReasoningBlockRenderers,
    initializeReasoningBlocks
} = require('../../../Gmsd.Web/wwwroot/js/reasoning-block-renderers.js');

describe('IntentClassifiedRenderer', () => {
    let renderer;

    beforeEach(() => {
        renderer = new IntentClassifiedRenderer();
    });

    describe('Metadata', () => {
        it('should have correct metadata', () => {
            const metadata = renderer.getMetadata();
            assert.strictEqual(metadata.eventType, StreamingEventType.IntentClassified);
            assert.strictEqual(metadata.name, 'IntentClassifiedRenderer');
            assert.strictEqual(metadata.version, '1.0.0');
            assert.strictEqual(metadata.priority, 10);
        });
    });

    describe('canRender', () => {
        it('should render intent.classified events', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.IntentClassified,
                timestamp: new Date().toISOString(),
                payload: { classification: 'Chat', confidence: 0.95, reasoning: 'Test' }
            };
            assert.strictEqual(renderer.canRender(event), true);
        });

        it('should not render other event types', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.GateSelected,
                timestamp: new Date().toISOString(),
                payload: {}
            };
            assert.strictEqual(renderer.canRender(event), false);
        });
    });

    describe('render', () => {
        it('should render classification with confidence visualization', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.IntentClassified,
                timestamp: new Date().toISOString(),
                payload: {
                    classification: 'Chat',
                    confidence: 0.95,
                    reasoning: 'This is a conversational query'
                }
            };

            const result = renderer.render(event);

            assert.ok(result.html.includes('reasoning-block--intent'));
            assert.ok(result.html.includes('Intent: Conversation'));
            assert.ok(result.html.includes('confidence-bar'));
            assert.ok(result.html.includes('95%'));
            assert.ok(result.html.includes('This is a conversational query'));
            assert.ok(result.elementId.includes('test-1'));
        });

        it('should render different classification types', () => {
            const classifications = [
                { type: 'Chat', label: 'Conversation', icon: '💬' },
                { type: 'ReadOnly', label: 'Read-Only Query', icon: '👁️' },
                { type: 'Write', label: 'Write Operation', icon: '✏️' }
            ];

            for (const { type, label, icon } of classifications) {
                const event = {
                    id: `test-${type}`,
                    type: StreamingEventType.IntentClassified,
                    timestamp: new Date().toISOString(),
                    payload: { classification: type, confidence: 0.8, reasoning: 'Test' }
                };

                const result = renderer.render(event);
                assert.ok(result.html.includes(label), `Should include ${label}`);
                assert.ok(result.html.includes(icon), `Should include ${icon}`);
            }
        });

        it('should handle high confidence (>=80%)', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.IntentClassified,
                timestamp: new Date().toISOString(),
                payload: { classification: 'Chat', confidence: 0.85, reasoning: 'Test' }
            };

            const result = renderer.render(event);
            assert.ok(result.html.includes('confidence-bar--high'));
        });

        it('should handle medium confidence (50-79%)', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.IntentClassified,
                timestamp: new Date().toISOString(),
                payload: { classification: 'Chat', confidence: 0.65, reasoning: 'Test' }
            };

            const result = renderer.render(event);
            assert.ok(result.html.includes('confidence-bar--medium'));
        });

        it('should handle low confidence (<50%)', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.IntentClassified,
                timestamp: new Date().toISOString(),
                payload: { classification: 'Chat', confidence: 0.3, reasoning: 'Test' }
            };

            const result = renderer.render(event);
            assert.ok(result.html.includes('confidence-bar--low'));
        });

        it('should include collapsible toggle button', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.IntentClassified,
                timestamp: new Date().toISOString(),
                payload: { classification: 'Chat', confidence: 0.95, reasoning: 'Test' }
            };

            const result = renderer.render(event);
            assert.ok(result.html.includes('reasoning-block__toggle'));
            assert.ok(result.html.includes('aria-expanded'));
            assert.ok(result.html.includes('reasoning-block__chevron'));
        });

        it('should escape HTML in reasoning text', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.IntentClassified,
                timestamp: new Date().toISOString(),
                payload: { classification: 'Chat', confidence: 0.95, reasoning: '<script>alert("xss")</script>' }
            };

            const result = renderer.render(event);
            assert.ok(!result.html.includes('<script>'));
            assert.ok(result.html.includes('&lt;script&gt;'));
        });

        it('should handle missing reasoning', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.IntentClassified,
                timestamp: new Date().toISOString(),
                payload: { classification: 'Chat', confidence: 0.95 }
            };

            const result = renderer.render(event);
            assert.ok(result.html.includes('No reasoning provided'));
        });
    });
});

describe('GateSelectedRenderer', () => {
    let renderer;

    beforeEach(() => {
        renderer = new GateSelectedRenderer();
    });

    describe('Metadata', () => {
        it('should have correct metadata', () => {
            const metadata = renderer.getMetadata();
            assert.strictEqual(metadata.eventType, StreamingEventType.GateSelected);
            assert.strictEqual(metadata.name, 'GateSelectedRenderer');
            assert.strictEqual(metadata.version, '1.0.0');
            assert.strictEqual(metadata.priority, 10);
        });
    });

    describe('canRender', () => {
        it('should render gate.selected events', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.GateSelected,
                timestamp: new Date().toISOString(),
                payload: { targetPhase: 'Planner', reasoning: 'Test' }
            };
            assert.strictEqual(renderer.canRender(event), true);
        });

        it('should not render other event types', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.IntentClassified,
                timestamp: new Date().toISOString(),
                payload: {}
            };
            assert.strictEqual(renderer.canRender(event), false);
        });
    });

    describe('render', () => {
        it('should render gate selection with phase name', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.GateSelected,
                timestamp: new Date().toISOString(),
                payload: {
                    targetPhase: 'Planner',
                    reasoning: 'Planning is required for this task',
                    requiresConfirmation: false
                }
            };

            const result = renderer.render(event);

            assert.ok(result.html.includes('reasoning-block--gate'));
            assert.ok(result.html.includes('Selected: Planner'));
            assert.ok(result.html.includes('Planning is required'));
            assert.ok(result.html.includes('Decision Reasoning'));
        });

        it('should render phase icons correctly', () => {
            const phases = [
                { phase: 'Chat', icon: '💬' },
                { phase: 'Planner', icon: '📋' },
                { phase: 'Coder', icon: '💻' },
                { phase: 'Reviewer', icon: '🔍' },
                { phase: 'Tester', icon: '🧪' },
                { phase: 'Runner', icon: '▶️' },
                { phase: 'GitCommitter', icon: '📦' }
            ];

            for (const { phase, icon } of phases) {
                const event = {
                    id: `test-${phase}`,
                    type: StreamingEventType.GateSelected,
                    timestamp: new Date().toISOString(),
                    payload: { targetPhase: phase, reasoning: 'Test', requiresConfirmation: false }
                };

                const result = renderer.render(event);
                assert.ok(result.html.includes(icon), `Should include icon for ${phase}`);
            }
        });

        it('should show confirmation badge when confirmation required', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.GateSelected,
                timestamp: new Date().toISOString(),
                payload: {
                    targetPhase: 'Runner',
                    reasoning: 'Execution requires user approval',
                    requiresConfirmation: true,
                    proposedAction: {
                        name: 'ExecutePlan',
                        description: 'Execute the generated plan',
                        parameters: { planId: '123' }
                    }
                }
            };

            const result = renderer.render(event);
            assert.ok(result.html.includes('Needs Confirmation'));
            assert.ok(result.html.includes('reasoning-block__badge--confirm'));
        });

        it('should render confirmation card with action details', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.GateSelected,
                timestamp: new Date().toISOString(),
                payload: {
                    targetPhase: 'Runner',
                    reasoning: 'Test',
                    requiresConfirmation: true,
                    proposedAction: {
                        name: 'ExecutePlan',
                        description: 'Execute the generated plan',
                        parameters: { planId: '123', steps: ['step1', 'step2'] }
                    }
                }
            };

            const result = renderer.render(event);
            assert.ok(result.html.includes('confirmation-card'));
            assert.ok(result.html.includes('Action Requires Confirmation'));
            assert.ok(result.html.includes('Execute the generated plan'));
            assert.ok(result.html.includes('planId'));
            assert.ok(result.html.includes('data-action="confirm"'));
            assert.ok(result.html.includes('data-action="cancel"'));
        });

        it('should include confirm and cancel buttons', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.GateSelected,
                timestamp: new Date().toISOString(),
                payload: {
                    targetPhase: 'Runner',
                    reasoning: 'Test',
                    requiresConfirmation: true,
                    proposedAction: {
                        name: 'ExecutePlan',
                        description: 'Execute the generated plan'
                    }
                }
            };

            const result = renderer.render(event);
            assert.ok(result.html.includes('confirmation-card__btn--confirm'));
            assert.ok(result.html.includes('confirmation-card__btn--cancel'));
            assert.ok(result.html.includes('Confirm'));
            assert.ok(result.html.includes('Cancel'));
        });

        it('should not render confirmation section when not required', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.GateSelected,
                timestamp: new Date().toISOString(),
                payload: {
                    targetPhase: 'Chat',
                    reasoning: 'Simple chat response',
                    requiresConfirmation: false
                }
            };

            const result = renderer.render(event);
            assert.ok(!result.html.includes('confirmation-card'));
            assert.ok(!result.html.includes('Needs Confirmation'));
        });

        it('should include collapsible toggle', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.GateSelected,
                timestamp: new Date().toISOString(),
                payload: { targetPhase: 'Planner', reasoning: 'Test' }
            };

            const result = renderer.render(event);
            assert.ok(result.html.includes('reasoning-block__toggle'));
            assert.ok(result.html.includes('aria-expanded'));
        });

        it('should escape HTML in reasoning text', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.GateSelected,
                timestamp: new Date().toISOString(),
                payload: {
                    targetPhase: 'Planner',
                    reasoning: '<b>Bold</b> reasoning',
                    requiresConfirmation: false
                }
            };

            const result = renderer.render(event);
            assert.ok(!result.html.includes('<b>Bold</b>'));
            assert.ok(result.html.includes('&lt;b&gt;Bold&lt;/b&gt;'));
        });

        it('should handle missing reasoning', () => {
            const event = {
                id: 'test-1',
                type: StreamingEventType.GateSelected,
                timestamp: new Date().toISOString(),
                payload: { targetPhase: 'Planner', requiresConfirmation: false }
            };

            const result = renderer.render(event);
            assert.ok(result.html.includes('No reasoning provided'));
        });

        it('should include timestamp', () => {
            const timestamp = new Date().toISOString();
            const event = {
                id: 'test-1',
                type: StreamingEventType.GateSelected,
                timestamp: timestamp,
                payload: { targetPhase: 'Planner', reasoning: 'Test' }
            };

            const result = renderer.render(event);
            assert.ok(result.html.includes('reasoning-block__timestamp'));
        });
    });
});

describe('createReasoningBlockRenderers', () => {
    it('should create both renderers', () => {
        const renderers = createReasoningBlockRenderers();
        assert.strictEqual(renderers.length, 2);

        const intentRenderer = renderers.find(r => r.eventType === StreamingEventType.IntentClassified);
        const gateRenderer = renderers.find(r => r.eventType === StreamingEventType.GateSelected);

        assert.ok(intentRenderer, 'Should include IntentClassifiedRenderer');
        assert.ok(gateRenderer, 'Should include GateSelectedRenderer');
        assert.ok(intentRenderer.renderer instanceof IntentClassifiedRenderer);
        assert.ok(gateRenderer.renderer instanceof GateSelectedRenderer);
    });

    it('should set correct priority for all renderers', () => {
        const renderers = createReasoningBlockRenderers();
        for (const reg of renderers) {
            assert.strictEqual(reg.priority, 10);
        }
    });
});

describe('initializeReasoningBlocks', () => {
    // Note: Full DOM testing would require a more comprehensive mock
    // These tests verify the function exists

    it('should be a function', () => {
        assert.strictEqual(typeof initializeReasoningBlocks, 'function');
    });
});
