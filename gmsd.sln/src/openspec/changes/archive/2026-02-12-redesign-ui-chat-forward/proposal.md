# redesign-ui-chat-forward — Proposal

## Summary
Redesign the GMSD web interface to be **chat-box forward** — centering the entire user experience around an AI-powered chat interface. The current UI has fragmented navigation across 15+ pages with no clear flow. This redesign unifies all functionality through a conversational interface while maintaining context awareness and providing rich visual feedback.

## Problem Statement

### Current Pain Points
1. **Fragmented Navigation**: 15+ top-level nav items create cognitive overload
2. **No Clear Flow**: Users must manually navigate between Projects, Runs, Tasks, Specs, etc.
3. **Disconnected Experience**: The Command Center chat is isolated from other pages
4. **Context Loss**: Users lose context when switching between views
5. **Steep Learning Curve**: New users must learn the domain model before being productive

### Goals
1. **Unified Interface**: Single chat-driven interface for all operations
2. **Conversational UX**: Users interact naturally via chat, AI handles complexity
3. **Context Preservation**: UI maintains and displays relevant context automatically
4. **Progressive Disclosure**: Rich visualizations appear inline when needed
5. **Reduced Cognitive Load**: Eliminate navigation decisions, focus on intent

## Related Changes
- `add-cross-cutting-ui-components` — Base component library (Complete)
- Archive: `add-web-razor-shell` — Original UI scaffolding

## Sequencing
This change builds on the existing `Gmsd.Web` project and leverages the `WorkflowClassifier` for in-process agent execution. The existing Command Center chat implementation will be evolved and promoted to the primary interface pattern.

## Capabilities
1. **chat-interface** — Core conversational UI with message threading, command input, and streaming responses
2. **layout-redesign** — New layout structure with collapsible sidebar, persistent chat bar, and dynamic content panels
3. **context-aware-ui** — Smart panels that display relevant context (workspace, runs, specs) based on conversation state

## Out of Scope
- Mobile-responsive design (future change)
- Voice input/output (future change)
- Third-party chat integrations (Slack/Discord)
- Real-time collaborative editing

## Success Criteria
- Users can accomplish 90% of tasks through chat without manual navigation
- New users can onboard without reading documentation
- Navigation clicks reduced by 70% for common workflows
- User satisfaction score > 4/5 on UX feedback

## Validation Strategy
- Manual UX testing with task-based scenarios
- A/B testing against current interface (if feasible)
- Code review for accessibility compliance (WCAG 2.1 AA)
