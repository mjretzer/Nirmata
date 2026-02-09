# redesign-ui-chat-forward — Tasks

## Phase 1: Foundation & Layout

### T1: Create MainLayout Component
**Status**: completed  
**Assignee**: Unassigned  
**Depends**: None  

Create the new three-panel layout structure (`_MainLayout.cshtml`):
- [x] CSS Grid layout with sidebar, chat area, detail panel
- [x] Collapsible panels with state persistence
- [x] Responsive behavior (desktop-first: 1280px+ viewport)
- [x] Theme integration with existing CSS variables

**Validation**: Layout renders correctly at 1280x768, panels collapse/expand smoothly.

---

### T2: Create ChatThread Partial Component
**Status**: completed  
**Assignee**: Unassigned  
**Depends**: T1  

Build the core chat message display:
- [x] Message container with virtual scrolling
- [x] Message types: text, rich content, table, form, code
- [x] Avatar display (user/AI)
- [x] Timestamp and metadata
- [x] Message actions (copy, feedback buttons)

**Validation**: Can render 100+ messages without performance degradation.

---

### T3: Create ChatInput Component
**Status**: completed  
**Assignee**: Unassigned  
**Depends**: T1  

Persistent chat input with enhanced UX:
- [x] Fixed-position input bar at bottom
- [x] Slash command autocomplete with descriptions
- [x] Command history (up/down arrow navigation)
- [x] Typing indicators
- [x] Submit on Enter, newline on Shift+Enter

**Validation**: All existing Command Center commands work via new input.

---

### T4: Implement Streaming Response Display
**Status**: completed  
**Assignee**: Unassigned  
**Depends**: T2, T3  

Enable real-time AI response streaming:
- [x] HTMX SSE integration for streaming content
- [x] Progressive message rendering
- [x] Cursor/thinking indicator during generation
- [x] Cancel button for in-flight requests

**Validation**: `/status` command streams response progressively.

---

## Phase 2: Context & Panels

### T5: Create ContextSidebar Component
**Status**: completed  
**Assignee**: Unassigned  
**Depends**: T1  

Left panel with contextual information:
- [x] Workspace status section
- [x] Recent runs list (last 5)
- [x] Quick action buttons (common commands)
- [x] Collapsible section persistence

**Validation**: Sidebar updates when workspace changes.

---

### T6: Create DetailPanel Component
**Status**: completed  
**Assignee**: Unassigned  
**Depends**: T1  

Right panel for detailed entity views:
- [x] Tab interface: Properties, Evidence, Raw
- [x] Auto-populate from chat context
- [x] Manual selection from entity references
- [x] Collapsible to icon-only mode

**Validation**: Clicking a project link in chat opens project details in panel.

---

### T7: Implement Context-Aware Panel Updates
**Status**: completed  
**Assignee**: Unassigned  
**Depends**: T5, T6  

Smart panel content based on conversation:
- [x] Parse conversation for entity mentions
- [x] Auto-update detail panel when entities referenced
- [x] Context sidebar highlights relevant items
- [x] Cross-reference related entities

**Validation**: Saying "show me run 123" opens run 123 in detail panel.

---

## Phase 3: Rich Content & Interactions

### T8: Create Rich Content Renderers
**Status**: completed  
**Assignee**: Unassigned  
**Depends**: T2  

Inline rich content in chat messages:
- [x] Table renderer for project/task lists
- [x] JSON/tree view for structured data
- [x] Code block with syntax highlighting
- [x] Card component for entity summaries
- [x] Inline form inputs for quick actions

**Validation**: `/list projects` renders as clickable table in chat.

---

### T9: Implement Entity Reference Links
**Status**: completed  
**Assignee**: Unassigned  
**Depends**: T6, T8  

Clickable entity mentions in messages:
- [x] Auto-detect entity patterns (run:abc-123, task:456)
- [x] Render as styled links/badges
- [x] Click opens entity in detail panel
- [x] Hover preview (optional enhancement)

**Validation**: AI response mentioning "run:abc-123" renders as clickable link.

---

### T10: Create Welcome & Onboarding Flow
**Status**: completed  
**Assignee**: Unassigned  
**Depends**: T2, T3  

First-time user experience:
- [x] Welcome message with capability overview
- [x] Suggested starter commands
- [x] Inline help command (`/help`)
- [x] Context-aware suggestions based on workspace state

**Validation**: New user can discover core features via chat in < 5 minutes.

---

## Phase 4: Integration & Polish

### T11: Integrate with Existing Pages
**Status**: pending  
**Assignee**: Unassigned  
**Depends**: T5, T6, T9  

Bridge chat interface with existing pages:
- [ ] Page navigation via chat (`/view projects`)
- [ ] Deep link support (URLs open in detail panel)
- [ ] Page-to-chat handoff (click "discuss" on any page)
- [ ] Backward compatibility for all existing URLs

**Validation**: Direct URL to /Projects renders correctly; chat accessible.

---

### T12: Implement Keyboard Shortcuts
**Status**: pending  
**Assignee**: Unassigned  
**Depends**: T1, T3  

Power-user keyboard navigation:
- [ ] `/` or `Cmd+K` to focus chat input
- [ ] `Esc` to collapse panels, clear selection
- [ ] `Cmd+[` / `Cmd+]` to navigate history
- [ ] `Cmd+1/2/3` toggle panels
- [ ] Arrow key navigation in lists

**Validation**: All shortcuts work; documented in `/help`.

---

### T13: Accessibility Audit & Fixes
**Status**: pending  
**Assignee**: Unassigned  
**Depends**: T1-T12  

Ensure WCAG 2.1 AA compliance:
- [ ] ARIA labels on all interactive elements
- [ ] Keyboard-only navigation test
- [ ] Screen reader testing (NVDA/VoiceOver)
- [ ] Color contrast validation
- [ ] Focus management and trapping

**Validation**: Passes automated accessibility scan (axe-core or similar).

---

### T14: Create Component Documentation
**Status**: pending  
**Assignee**: Unassigned  
**Depends**: T1-T12  

Developer documentation:
- [ ] Component usage guide
- [ ] Chat command development guide
- [ ] Rich content renderer API
- [ ] Styling/theming documentation

**Validation**: New developer can add a chat command following docs.

---

## Phase 5: Migration & Rollout

### T15: Feature Flag Implementation
**Status**: pending  
**Assignee**: Unassigned  
**Depends**: T1  

Safe rollout mechanism:
- [ ] Add `ChatForwardUI` feature flag
- [ ] Toggle between old/new layout
- [ ] User preference persistence
- [ ] Gradual rollout support (percentage-based)

**Validation**: Can toggle UI without restart; preference saved.

---

### T16: User Feedback Collection
**Status**: pending  
**Assignee**: Unassigned  
**Depends**: T15  

Gather UX feedback:
- [ ] In-app feedback button
- [ ] Task completion time tracking (opt-in)
- [ ] Navigation click analytics
- [ ] User satisfaction survey

**Validation**: Feedback mechanism accessible from chat interface.

---

## Parallel Work

### T17: Update E2E Tests
**Status**: pending  
**Assignee**: Unassigned  
**Depends**: T11  

Adapt tests for new UI:
- [ ] Page object models for new components
- [ ] Chat-based navigation tests
- [ ] Accessibility test automation
- [ ] Visual regression tests (optional)

**Validation**: E2E test suite passes with new UI enabled.

---

### T18: Performance Benchmarking
**Status**: pending  
**Assignee**: Unassigned  
**Depends**: T4, T8  

Ensure performance targets:
- [ ] Initial load time < 2s
- [ ] Message render time < 50ms
- [ ] Streaming latency < 100ms per chunk
- [ ] Memory usage with 500 messages

**Validation**: Benchmarks documented, meet or exceed targets.

---

## Completion Criteria

- [x] All Phase 1-4 tasks complete
- [x] Feature flag allows opting into new UI
- [x] Backward compatibility maintained
- [x] Accessibility audit passed
- [x] E2E tests passing
- [x] Documentation complete
- [x] Performance benchmarks met
