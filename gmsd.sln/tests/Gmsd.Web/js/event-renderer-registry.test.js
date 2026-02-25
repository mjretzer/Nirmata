/**
 * @fileoverview Event Renderer Registry Tests
 * @description Unit tests for EventRendererRegistry and related components
 * Run with: node --test tests/Gmsd.Web/js/event-renderer-registry.test.js
 * Or with Jest: jest tests/Gmsd.Web/js/event-renderer-registry.test.js
 */

const { describe, it, beforeEach, afterEach } = require('node:test');
const assert = require('node:assert');

// Load dependencies in order (browser files expect global scope)
const eventTypes = require('../../../Gmsd.Web/wwwroot/js/event-types.js');
global.StreamingEventType = eventTypes.StreamingEventType;
global.EventCategory = eventTypes.EventCategory;
global.EventTypeToCategory = eventTypes.EventTypeToCategory;
global.getEventCategory = eventTypes.getEventCategory;

const ieventRenderer = require('../../../Gmsd.Web/wwwroot/js/ievent-renderer.js');
global.IEventRenderer = ieventRenderer.IEventRenderer;
global.EventRendererBase = ieventRenderer.EventRendererBase;

// Now load the modules that depend on the above
const { DefaultEventRenderer } = require('../../../Gmsd.Web/wwwroot/js/default-event-renderer.js');
const { EventRendererRegistry, getEventRendererRegistry } = require('../../../Gmsd.Web/wwwroot/js/event-renderer-registry.js');

// Minimal DOM mock for escapeHtml testing in Node.js
function setupDomMock() {
    if (typeof document === 'undefined') {
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
        // Simple HTML entities encoder for testing
        const originalEscapeHtml = global.EventRendererBase.prototype.escapeHtml;
        global.EventRendererBase.prototype.escapeHtml = function(text) {
            if (!text) return '';
            return text
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#039;');
        };
    }
}

// Test renderer implementations
class TestIntentRenderer extends EventRendererBase {
    constructor(priority = 0) {
        super({
            eventType: StreamingEventType.IntentClassified,
            name: 'TestIntentRenderer',
            version: '1.0.0',
            priority
        });
    }

    render(event) {
        return this.createRenderResult('<div class="intent">Intent</div>');
    }
}

class TestToolRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: StreamingEventType.ToolCall,
            name: 'TestToolRenderer',
            version: '1.0.0'
        });
    }

    render(event) {
        return this.createRenderResult('<div class="tool">Tool</div>');
    }
}

class HighPriorityRenderer extends EventRendererBase {
    constructor(eventType) {
        super({
            eventType,
            name: 'HighPriorityRenderer',
            version: '1.0.0',
            priority: 100
        });
    }

    render(event) {
        return this.createRenderResult('<div class="high-priority">High Priority</div>');
    }
}

describe('EventRendererRegistry', () => {
    let registry;

    beforeEach(() => {
        // Reset singleton before each test
        EventRendererRegistry.reset();
        registry = EventRendererRegistry.getInstance();
    });

    afterEach(() => {
        // Clean up
        EventRendererRegistry.reset();
    });

    describe('Singleton Pattern', () => {
        it('should return the same instance', () => {
            const instance1 = EventRendererRegistry.getInstance();
            const instance2 = EventRendererRegistry.getInstance();
            assert.strictEqual(instance1, instance2);
        });

        it('should create new instance after reset', () => {
            const instance1 = EventRendererRegistry.getInstance();
            EventRendererRegistry.reset();
            const instance2 = EventRendererRegistry.getInstance();
            assert.notStrictEqual(instance1, instance2);
        });

        it('getEventRendererRegistry should return singleton', () => {
            const registry1 = getEventRendererRegistry();
            const registry2 = getEventRendererRegistry();
            assert.strictEqual(registry1, registry2);
        });
    });

    describe('Registration', () => {
        it('should register a renderer for an event type', () => {
            const renderer = new TestIntentRenderer();
            registry.register(StreamingEventType.IntentClassified, renderer);
            
            assert.strictEqual(registry.getRendererCount(StreamingEventType.IntentClassified), 1);
            assert.ok(registry.hasRenderer(StreamingEventType.IntentClassified));
        });

        it('should throw when event type is missing', () => {
            const renderer = new TestIntentRenderer();
            assert.throws(() => {
                registry.register(null, renderer);
            }, /Event type is required/);
        });

        it('should throw when renderer is missing', () => {
            assert.throws(() => {
                registry.register(StreamingEventType.IntentClassified, null);
            }, /Renderer is required/);
        });

        it('should support method chaining', () => {
            const renderer = new TestIntentRenderer();
            const result = registry
                .register(StreamingEventType.IntentClassified, renderer)
                .register(StreamingEventType.ToolCall, new TestToolRenderer());
            
            assert.strictEqual(result, registry);
            assert.strictEqual(registry.getRendererCount(StreamingEventType.IntentClassified), 1);
            assert.strictEqual(registry.getRendererCount(StreamingEventType.ToolCall), 1);
        });

        it('should register multiple renderers for same type', () => {
            registry.register(StreamingEventType.IntentClassified, new TestIntentRenderer());
            registry.register(StreamingEventType.IntentClassified, new TestIntentRenderer());
            
            assert.strictEqual(registry.getRendererCount(StreamingEventType.IntentClassified), 2);
        });

        it('should registerAll multiple renderers at once', () => {
            registry.registerAll([
                { eventType: StreamingEventType.IntentClassified, renderer: new TestIntentRenderer() },
                { eventType: StreamingEventType.ToolCall, renderer: new TestToolRenderer() }
            ]);
            
            assert.ok(registry.hasRenderer(StreamingEventType.IntentClassified));
            assert.ok(registry.hasRenderer(StreamingEventType.ToolCall));
        });
    });

    describe('Priority Ordering', () => {
        it('should sort renderers by priority (highest first)', () => {
            const lowPriority = new TestIntentRenderer(10);
            const highPriority = new HighPriorityRenderer(StreamingEventType.IntentClassified);
            
            registry.register(StreamingEventType.IntentClassified, lowPriority, { priority: 10 });
            registry.register(StreamingEventType.IntentClassified, highPriority, { priority: 100 });
            
            const renderers = registry.getRenderersForType(StreamingEventType.IntentClassified);
            assert.strictEqual(renderers[0], highPriority);
            assert.strictEqual(renderers[1], lowPriority);
        });

        it('should resolve by priority', () => {
            const lowPriority = new TestIntentRenderer(0);
            const highPriority = new HighPriorityRenderer(StreamingEventType.IntentClassified);
            
            registry.register(StreamingEventType.IntentClassified, lowPriority, { priority: 0 });
            registry.register(StreamingEventType.IntentClassified, highPriority, { priority: 100 });
            
            const event = {
                id: 'test-1',
                type: StreamingEventType.IntentClassified,
                timestamp: new Date().toISOString()
            };
            
            const resolved = registry.resolve(event);
            assert.strictEqual(resolved, highPriority);
        });
    });

    describe('Resolution', () => {
        it('should resolve renderer by event type', () => {
            const intentRenderer = new TestIntentRenderer();
            registry.register(StreamingEventType.IntentClassified, intentRenderer);
            
            const event = {
                id: 'test-1',
                type: StreamingEventType.IntentClassified,
                timestamp: new Date().toISOString(),
                payload: { classification: 'Chat', confidence: 0.95 }
            };
            
            const resolved = registry.resolve(event);
            assert.strictEqual(resolved, intentRenderer);
        });

        it('should resolve by type without full event', () => {
            const intentRenderer = new TestIntentRenderer();
            registry.register(StreamingEventType.IntentClassified, intentRenderer);
            
            const resolved = registry.resolveByType(StreamingEventType.IntentClassified);
            assert.strictEqual(resolved, intentRenderer);
        });

        it('should return default renderer for unknown types', () => {
            const defaultRenderer = new DefaultEventRenderer();
            registry.setDefaultRenderer(defaultRenderer);
            
            const event = {
                id: 'test-1',
                type: 'unknown.type',
                timestamp: new Date().toISOString()
            };
            
            const resolved = registry.resolve(event);
            assert.strictEqual(resolved, defaultRenderer);
        });

        it('should return null when no renderer found and no default', () => {
            const event = {
                id: 'test-1',
                type: 'unknown.type',
                timestamp: new Date().toISOString()
            };
            
            const resolved = registry.resolve(event);
            assert.strictEqual(resolved, null);
        });

        it('should return default renderer for null event', () => {
            const defaultRenderer = new DefaultEventRenderer();
            registry.setDefaultRenderer(defaultRenderer);
            
            const resolved = registry.resolve(null);
            assert.strictEqual(resolved, defaultRenderer);
        });

        it('should return default renderer for event without type', () => {
            const defaultRenderer = new DefaultEventRenderer();
            registry.setDefaultRenderer(defaultRenderer);
            
            const resolved = registry.resolve({ id: 'test-1' });
            assert.strictEqual(resolved, defaultRenderer);
        });
    });

    describe('Unregistration', () => {
        it('should unregister a renderer', () => {
            const renderer = new TestIntentRenderer();
            registry.register(StreamingEventType.IntentClassified, renderer);
            
            const removed = registry.unregister(StreamingEventType.IntentClassified, renderer);
            assert.strictEqual(removed, true);
            assert.strictEqual(registry.getRendererCount(StreamingEventType.IntentClassified), 0);
        });

        it('should return false when unregistering non-existent renderer', () => {
            const removed = registry.unregister(StreamingEventType.IntentClassified, new TestIntentRenderer());
            assert.strictEqual(removed, false);
        });
    });

    describe('Default Renderer', () => {
        it('should set and get default renderer', () => {
            const defaultRenderer = new DefaultEventRenderer();
            registry.setDefaultRenderer(defaultRenderer);
            
            assert.strictEqual(registry.getDefaultRenderer(), defaultRenderer);
        });

        it('should support chaining setDefaultRenderer', () => {
            const result = registry.setDefaultRenderer(new DefaultEventRenderer());
            assert.strictEqual(result, registry);
        });
    });

    describe('Registry State', () => {
        it('should get registered types', () => {
            registry.register(StreamingEventType.IntentClassified, new TestIntentRenderer());
            registry.register(StreamingEventType.ToolCall, new TestToolRenderer());
            
            const types = registry.getRegisteredTypes();
            assert.ok(types.includes(StreamingEventType.IntentClassified));
            assert.ok(types.includes(StreamingEventType.ToolCall));
            assert.strictEqual(types.length, 2);
        });

        it('should get all renderers for a type', () => {
            const renderer1 = new TestIntentRenderer();
            const renderer2 = new TestIntentRenderer();
            
            registry.register(StreamingEventType.IntentClassified, renderer1);
            registry.register(StreamingEventType.IntentClassified, renderer2);
            
            const renderers = registry.getRenderersForType(StreamingEventType.IntentClassified);
            assert.strictEqual(renderers.length, 2);
            assert.ok(renderers.includes(renderer1));
            assert.ok(renderers.includes(renderer2));
        });

        it('should clear all registrations', () => {
            registry.register(StreamingEventType.IntentClassified, new TestIntentRenderer());
            registry.setDefaultRenderer(new DefaultEventRenderer());
            registry.freeze();
            
            registry.clear();
            
            assert.strictEqual(registry.getRegisteredTypes().length, 0);
            assert.strictEqual(registry.getDefaultRenderer(), null);
            assert.strictEqual(registry.isFrozen(), false);
        });
    });

    describe('Freeze/Unfreeze', () => {
        it('should freeze registry', () => {
            registry.freeze();
            assert.strictEqual(registry.isFrozen(), true);
        });

        it('should throw when registering to frozen registry', () => {
            registry.freeze();
            assert.throws(() => {
                registry.register(StreamingEventType.IntentClassified, new TestIntentRenderer());
            }, /registry is frozen/);
        });

        it('should throw when unregistering from frozen registry', () => {
            const renderer = new TestIntentRenderer();
            registry.register(StreamingEventType.IntentClassified, renderer);
            registry.freeze();
            
            assert.throws(() => {
                registry.unregister(StreamingEventType.IntentClassified, renderer);
            }, /registry is frozen/);
        });

        it('should throw when setting default on frozen registry', () => {
            registry.freeze();
            assert.throws(() => {
                registry.setDefaultRenderer(new DefaultEventRenderer());
            }, /registry is frozen/);
        });

        it('should allow operations after unfreeze', () => {
            registry.freeze();
            registry.unfreeze();
            
            assert.doesNotThrow(() => {
                registry.register(StreamingEventType.IntentClassified, new TestIntentRenderer());
            });
        });
    });

    describe('Statistics', () => {
        it('should return correct statistics', () => {
            registry.register(StreamingEventType.IntentClassified, new TestIntentRenderer());
            registry.register(StreamingEventType.IntentClassified, new TestIntentRenderer());
            registry.register(StreamingEventType.ToolCall, new TestToolRenderer());
            registry.setDefaultRenderer(new DefaultEventRenderer());
            registry.freeze();
            
            const stats = registry.getStats();
            assert.strictEqual(stats.totalTypes, 2);
            assert.strictEqual(stats.totalRenderers, 3);
            assert.strictEqual(stats.hasDefault, true);
            assert.strictEqual(stats.isFrozen, true);
        });
    });
});

describe('DefaultEventRenderer', () => {
    beforeEach(() => {
        setupDomMock();
    });

    it('should have lowest priority', () => {
        const renderer = new DefaultEventRenderer();
        const metadata = renderer.getMetadata();
        assert.strictEqual(metadata.priority, -1000);
    });

    it('should render any event type', () => {
        const renderer = new DefaultEventRenderer();
        
        const event = {
            id: 'test-1',
            type: 'custom.unknown',
            timestamp: new Date().toISOString(),
            payload: { data: 'test' }
        };
        
        assert.strictEqual(renderer.canRender(event), true);
    });

    it('should include event JSON in output', () => {
        const renderer = new DefaultEventRenderer();
        
        const event = {
            id: 'test-1',
            type: 'custom.unknown',
            timestamp: new Date().toISOString(),
            payload: { data: 'test' }
        };
        
        const result = renderer.render(event);
        assert.ok(result.html.includes('custom.unknown'));
        assert.ok(result.html.includes('test-1'));
        assert.ok(result.html.includes('event-card--unknown'));
    });

    it('should escape HTML in event data', () => {
        const renderer = new DefaultEventRenderer();
        
        const event = {
            id: 'test-1',
            type: '<script>alert("xss")</script>',
            timestamp: new Date().toISOString()
        };
        
        const result = renderer.render(event);
        assert.ok(!result.html.includes('<script>'));
        assert.ok(result.html.includes('&lt;script&gt;'));
    });
});

describe('EventRendererBase', () => {
    beforeEach(() => {
        setupDomMock();
    });

    it('should escape HTML entities', () => {
        const renderer = new TestIntentRenderer();
        
        assert.strictEqual(renderer.escapeHtml('<script>'), '&lt;script&gt;');
        assert.strictEqual(renderer.escapeHtml('&test'), '&amp;test');
        assert.strictEqual(renderer.escapeHtml('"quoted"'), '&quot;quoted&quot;');
    });

    it('should format timestamps', () => {
        const renderer = new TestIntentRenderer();
        const timestamp = new Date().toISOString();
        
        const formatted = renderer.formatTimestamp(timestamp);
        assert.ok(typeof formatted === 'string');
        assert.ok(formatted.length > 0);
    });

    it('should generate element IDs', () => {
        const renderer = new TestIntentRenderer();
        
        const id1 = renderer.generateElementId('abc-123');
        assert.ok(id1.includes('abc-123'));
        
        const id2 = renderer.generateElementId('abc-123', 'suffix');
        assert.ok(id2.includes('abc-123'));
        assert.ok(id2.includes('suffix'));
    });

    it('should sanitize element IDs', () => {
        const renderer = new TestIntentRenderer();
        
        const id = renderer.generateElementId('abc<>123!@#');
        assert.ok(!id.includes('<'));
        assert.ok(!id.includes('>'));
        assert.ok(!id.includes('!'));
        assert.ok(!id.includes('@'));
    });

    it('should create render results', () => {
        const renderer = new TestIntentRenderer();
        
        const result = renderer.createRenderResult('<div>test</div>', {
            elementId: 'test-id',
            append: false
        });
        
        assert.strictEqual(result.html, '<div>test</div>');
        assert.strictEqual(result.elementId, 'test-id');
        assert.strictEqual(result.append, false);
    });

    it('should use default append=true', () => {
        const renderer = new TestIntentRenderer();
        
        const result = renderer.createRenderResult('<div>test</div>');
        assert.strictEqual(result.append, true);
    });
});
