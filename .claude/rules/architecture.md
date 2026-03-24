# Architecture & Design

Architectural guidelines and system design principles
- **Clean Architecture / Hexagonal Architecture**: The core domain logic (`nirmata.Aos`, `nirmata.Agents`) should not depend on infrastructure details or external frameworks. Interfaces are defined in the core, implementations reside at the edge.
- **Dependency Injection**: Heavy reliance on `Microsoft.Extensions.DependencyInjection`. All services, agents, and external integrations should be registered and injected via constructor injection.

## Backend (C#)
- **Agent Orchestration System (AOS)**: The core engine for routing and managing LLM interactions. 
- **Semantic Kernel**: Used for LLM abstraction and agent execution.
- **State Management**: Agents should be as stateless as possible. Any state required should be passed explicitly through the message envelope or managed via dedicated state stores.

## Frontend (React)
- **Component Architecture**: Prefer atomic design. 
  - `components/ui`: Reusable, generic UI components (e.g., shadcn/ui).
  - `components/features`: Domain-specific components.
- **State Management**: Prefer local component state or React Context over heavy global state managers unless the complexity demands it.
- **Data Fetching**: Handled at the feature/page level, passing data down as props.

## Security
- Never log sensitive API keys or PII.
- Ensure proper validation on API endpoints before passing data to the LLM or AOS core.
