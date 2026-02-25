/**
 * @fileoverview Streaming Performance Tests (JavaScript)
 * @description UI render latency benchmarks for the streaming event system.
 * Validates latency target: < 50ms UI render per event.
 * 
 * Run with: node --test tests/Gmsd.Web/js/streaming-performance.test.js
 */

const { describe, it, beforeEach } = require('node:test');
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

// Now load the renderer modules
const { IntentClassifiedRenderer, GateSelectedRenderer } = require('../../../Gmsd.Web/wwwroot/js/reasoning-block-renderers.js');
const { AssistantDeltaRenderer } = require('../../../Gmsd.Web/wwwroot/js/assistant-event-renderers.js');
const { EventSequencer, EventSequencerManager } = require('../../../Gmsd.Web/wwwroot/js/event-sequencer.js');

// Simple DOM mock for server-side testing
function setupDomMock() {
    if (typeof document === 'undefined') {
        global.document = {
            createElement: function(tag) {
                return {
                    tagName: tag,
                    attributes: {},
                    childNodes: [],
                    innerHTML: '',
                    textContent: '',
                    setAttribute: function(key, value) { this.attributes[key] = value; },
                    getAttribute: function(key) { return this.attributes[key]; },
                    appendChild: function(child) { this.childNodes.push(child); },
                    querySelector: function() { return null; },
                    querySelectorAll: function() { return []; },
                    closest: function() { return null; },
                    classList: {
                        add: function() {},
                        remove: function() {},
                        toggle: function() {}
                    },
                    style: {}
                };
            }
        };

        // Mock escapeHtml for Node.js environment
        if (global.EventRendererBase && global.EventRendererBase.prototype) {
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
}

// Performance measurement helper
function measureRenderLatency(renderer, event, iterations = 1) {
    const times = [];
    
    for (let i = 0; i < iterations; i++) {
        const start = performance.now();
        const result = renderer.render(event);
        const end = performance.now();
        times.push(end - start);
    }
    
    return {
        times,
        average: times.reduce((a, b) => a + b, 0) / times.length,
        min: Math.min(...times),
        max: Math.max(...times),
        p95: times.sort((a, b) => a - b)[Math.floor(times.length * 0.95)]
    };
}

describe('T22-2: UI Render Latency (< 50ms per event target)', () => {
    beforeEach(() => {
        setupDomMock();
    });

    describe('IntentClassifiedRenderer', () => {
        it('should render single event under 50ms', () => {
            const renderer = new IntentClassifiedRenderer();
            const event = {
                id: 'test-1',
                type: StreamingEventType.IntentClassified,
                timestamp: new Date().toISOString(),
                payload: {
                    category: 'Write',
                    classification: 'Write',
                    confidence: 0.87,
                    reasoning: 'User wants to create a new file with specific requirements'
                }
            };

            const metrics = measureRenderLatency(renderer, event);

            assert.ok(metrics.average < 50, 
                `Average render time should be under 50ms (actual: ${metrics.average.toFixed(2)}ms)`);
            assert.ok(metrics.max < 100, 
                `Max render time should be under 100ms (actual: ${metrics.max.toFixed(2)}ms)`);
        });

        it('should render 100 events with average under 50ms each', () => {
            const renderer = new IntentClassifiedRenderer();
            const iterations = 100;
            const event = {
                id: 'test-batch',
                type: StreamingEventType.IntentClassified,
                timestamp: new Date().toISOString(),
                payload: {
                    category: 'Chat',
                    classification: 'Chat',
                    confidence: 0.95,
                    reasoning: 'User is engaging in conversation'
                }
            };

            const metrics = measureRenderLatency(renderer, event, iterations);

            assert.ok(metrics.average < 50, 
                `Average render time for ${iterations} events should be under 50ms (actual: ${metrics.average.toFixed(2)}ms)`);
            assert.ok(metrics.p95 < 75, 
                `95th percentile should be under 75ms (actual: ${metrics.p95.toFixed(2)}ms)`);
        });
    });

    describe('GateSelectedRenderer', () => {
        it('should render single event under 50ms', () => {
            const renderer = new GateSelectedRenderer();
            const event = {
                id: 'test-2',
                type: StreamingEventType.GateSelected,
                timestamp: new Date().toISOString(),
                payload: {
                    targetPhase: 'Planner',
                    phase: 'Planner',
                    reasoning: 'User input requires planning phase',
                    requiresConfirmation: false
                }
            };

            const start = performance.now();
            const result = renderer.render(event);
            const end = performance.now();
            const latency = end - start;

            assert.ok(latency < 50, 
                `Render time should be under 50ms (actual: ${latency.toFixed(2)}ms)`);
            assert.ok(result.html && result.html.length > 0, 'Result should contain HTML');
        });

        it('should render confirmation events under 50ms', () => {
            const renderer = new GateSelectedRenderer();
            const event = {
                id: 'test-confirm',
                type: StreamingEventType.GateSelected,
                timestamp: new Date().toISOString(),
                payload: {
                    targetPhase: 'Runner',
                    phase: 'Runner',
                    reasoning: 'User wants to execute a command',
                    requiresConfirmation: true,
                    proposedAction: {
                        name: 'execute',
                        description: 'Execute shell command',
                        parameters: { command: 'echo test' }
                    }
                }
            };

            const metrics = measureRenderLatency(renderer, event, 50);

            assert.ok(metrics.average < 50, 
                `Average render time for confirmation events should be under 50ms (actual: ${metrics.average.toFixed(2)}ms)`);
        });
    });

    describe('AssistantDeltaRenderer', () => {
        it('should render single delta under 50ms', () => {
            const renderer = new AssistantDeltaRenderer();
            const event = {
                id: 'test-3',
                type: StreamingEventType.AssistantDelta,
                timestamp: new Date().toISOString(),
                payload: {
                    messageId: 'msg-123',
                    content: 'Hello, this is a test message chunk.',
                    index: 0
                }
            };

            const start = performance.now();
            const result = renderer.render(event);
            const end = performance.now();
            const latency = end - start;

            assert.ok(latency < 50, 
                `Render time should be under 50ms (actual: ${latency.toFixed(2)}ms)`);
        });

        it('should render 100 deltas with average under 50ms each', () => {
            const renderer = new AssistantDeltaRenderer();
            const iterations = 100;
            
            const times = [];
            for (let i = 0; i < iterations; i++) {
                const event = {
                    id: `test-delta-${i}`,
                    type: StreamingEventType.AssistantDelta,
                    timestamp: new Date().toISOString(),
                    payload: {
                        messageId: 'streaming-msg',
                        content: `Token ${i} `,
                        index: i
                    }
                };

                const start = performance.now();
                renderer.render(event);
                const end = performance.now();
                times.push(end - start);
            }

            const average = times.reduce((a, b) => a + b, 0) / times.length;
            const max = Math.max(...times);

            assert.ok(average < 50, 
                `Average render time for ${iterations} deltas should be under 50ms (actual: ${average.toFixed(2)}ms)`);
            assert.ok(max < 100, 
                `Max render time should be under 100ms (actual: ${max.toFixed(2)}ms)`);
        });
    });
});

describe('T22-3: Event Sequencer Performance (100+ events)', () => {
    beforeEach(() => {
        setupDomMock();
    });

    it('should sequence 100 events in under 200ms', () => {
        const sequencer = new EventSequencer({
            bufferSize: 100,
            maxWaitMs: 1000,
            validateSequence: true
        });

        const receivedEvents = [];
        sequencer.onRelease(e => receivedEvents.push(e));

        // Create events with sequence numbers (shuffle them)
        const events = [];
        for (let i = 1; i <= 100; i++) {
            events.push({
                id: `seq-${i}`,
                type: StreamingEventType.AssistantDelta,
                timestamp: new Date().toISOString(),
                sequenceNumber: i,
                payload: {
                    messageId: 'sequence-test',
                    content: `Event ${i}`,
                    index: i
                }
            });
        }

        // Shuffle events to simulate out-of-order arrival
        for (let i = events.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [events[i], events[j]] = [events[j], events[i]];
        }

        const start = performance.now();
        
        // Add all events
        events.forEach(event => sequencer.add(event));
        
        const end = performance.now();
        const latency = end - start;

        // Should process quickly
        assert.ok(latency < 200, 
            `Sequencing 100 events should take under 200ms (actual: ${latency.toFixed(2)}ms)`);

        // Cleanup
        sequencer.dispose();
    });

    it('should handle 500 events without unbounded memory growth', () => {
        const sequencer = new EventSequencer({
            bufferSize: 50, // Small buffer
            maxWaitMs: 10,
            validateSequence: true
        });

        const receivedEvents = [];
        sequencer.onRelease(e => receivedEvents.push(e));

        const startMemory = process.memoryUsage().heapUsed;

        // Add 500 events
        for (let i = 1; i <= 500; i++) {
            sequencer.add({
                id: `mem-test-${i}`,
                type: StreamingEventType.AssistantDelta,
                timestamp: new Date().toISOString(),
                sequenceNumber: i,
                payload: {
                    messageId: 'memory-test',
                    content: `Event ${i} with some content`,
                    index: i
                }
            });
        }

        const endMemory = process.memoryUsage().heapUsed;
        const memoryGrowth = (endMemory - startMemory) / 1024; // KB

        // With buffer size 50, memory growth should be bounded
        assert.ok(memoryGrowth < 500, 
            `Memory growth with limited buffer should be under 500KB (actual: ${memoryGrowth.toFixed(2)}KB)`);

        // Cleanup
        sequencer.dispose();
    });
});

describe('T22: Performance Summary', () => {
    it('all renderers meet 50ms latency target', () => {
        setupDomMock();
        
        const renderers = [
            { name: 'IntentClassifiedRenderer', renderer: new IntentClassifiedRenderer(), eventType: StreamingEventType.IntentClassified },
            { name: 'GateSelectedRenderer', renderer: new GateSelectedRenderer(), eventType: StreamingEventType.GateSelected },
            { name: 'AssistantDeltaRenderer', renderer: new AssistantDeltaRenderer(), eventType: StreamingEventType.AssistantDelta }
        ];

        const results = [];
        
        for (const { name, renderer, eventType } of renderers) {
            const event = {
                id: `summary-${name}`,
                type: eventType,
                timestamp: new Date().toISOString(),
                payload: {
                    messageId: 'summary-test',
                    content: 'Test content',
                    category: 'Write',
                    classification: 'Write',
                    confidence: 0.9,
                    reasoning: 'Test reasoning',
                    targetPhase: 'Planner',
                    phase: 'Planner'
                }
            };

            const metrics = measureRenderLatency(renderer, event, 50);
            results.push({
                name,
                average: metrics.average,
                p95: metrics.p95,
                passes: metrics.average < 50 && metrics.p95 < 75
            });
        }

        console.log('\n=== T22 Performance Summary ===');
        results.forEach(r => {
            console.log(`${r.name}: avg=${r.average.toFixed(2)}ms, p95=${r.p95.toFixed(2)}ms, passes=${r.passes}`);
        });

        const allPass = results.every(r => r.passes);
        assert.ok(allPass, 'All renderers should meet 50ms average / 75ms p95 targets');
    });
});
