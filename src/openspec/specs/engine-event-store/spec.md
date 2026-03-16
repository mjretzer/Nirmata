# engine-event-store Specification

## Purpose

Defines the public DI/interface surface for appending and reading AOS state events. Canonical event log semantics for `.aos/state/events.ndjson` and state-layer invariants are defined by `aos-state-store`, and this capability MUST conform.

- **Lives in:** `nirmata.Aos/Public/IEventStore.cs`, `nirmata.Aos/Public/Composition/*`, `.aos/state/events.ndjson`
- **Owns:** Public interface shape and DI registration for event append/tail/list operations
- **Does not own:** State reduction semantics (`state.json`) or plane workflow control
## Requirements
### Requirement: Event store interface exists
The system SHALL define `IEventStore` as a public interface in `nirmata.Aos/Public/`.

The interface SHALL provide methods to append, tail, and list events from `.aos/state/events.ndjson`.

#### Scenario: Append event adds to events.ndjson
- **GIVEN** an initialized AOS workspace
- **WHEN** `IEventStore.AppendEvent(aosEvent)` is called
- **THEN** the event is appended to `.aos/state/events.ndjson` as a single NDJSON line

#### Scenario: Append event validates event structure
- **GIVEN** an event missing required fields
- **WHEN** `IEventStore.AppendEvent(aosEvent)` is called
- **THEN** a deterministic, actionable exception is thrown before writing

#### Scenario: Tail returns last N events
- **GIVEN** a workspace with 100 events in events.ndjson
- **WHEN** `IEventStore.Tail(10)` is called
- **THEN** the last 10 events are returned in chronological order

#### Scenario: Tail with N larger than count returns all events
- **GIVEN** a workspace with 5 events
- **WHEN** `IEventStore.Tail(10)` is called
- **THEN** all 5 events are returned

#### Scenario: List events with filter by type
- **GIVEN** a workspace with events of various types
- **WHEN** `IEventStore.ListEvents(filter: e => e.Type == "task.started")` is called
- **THEN** only events matching the filter are returned

#### Scenario: List events with pagination
- **GIVEN** a workspace with many events
- **WHEN** `IEventStore.ListEvents(skip: 0, take: 20)` is called
- **THEN** the first 20 events are returned

### Requirement: Event schema validation is enforced
The interface SHALL validate events against the event schema before appending.

#### Scenario: Invalid event is rejected
- **GIVEN** an event that does not conform to the event schema
- **WHEN** `IEventStore.AppendEvent(aosEvent)` is called
- **THEN** a schema validation error is thrown with actionable details

### Requirement: Events are append-only
The interface SHALL enforce append-only semantics.

#### Scenario: Existing events cannot be modified
- **GIVEN** any existing implementation of `IEventStore`
- **WHEN** inspection of the interface is performed
- **THEN** no methods exist for modifying or deleting existing events

### Requirement: Service is registered in DI
The system SHALL register `IEventStore` as a Singleton in `AddnirmataAos()`.

#### Scenario: Plane resolves the service via DI
- **GIVEN** a configured service collection with `AddnirmataAos()` called
- **WHEN** `serviceProvider.GetRequiredService<IEventStore>()` is called
- **THEN** a non-null implementation is returned

