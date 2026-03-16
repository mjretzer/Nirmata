## 1. Composition Root
- [x] 1.1 Create `nirmata.Agents/Composition/ServiceCollectionExtensions.cs` with `AddnirmataAgents()` method
- [x] 1.2 `AddnirmataAgents()` calls `AddnirmataAos()` to register Engine services
- [x] 1.3 `AddnirmataAgents()` registers Plane-specific services (LLM provider, prompt loader, etc.)
- [x] 1.4 Verify: `dotnet build nirmata.Agents.csproj` succeeds

## 2. Configuration
- [x] 2.1 Create `nirmata.Agents/Configuration/AgentsOptions.cs` with Plane settings
- [x] 2.2 Add configuration binding in `AddnirmataAgents()`
- [x] 2.3 Document `appsettings.json` schema in proposal artifacts
- [x] 2.4 Verify: Options bind correctly from configuration

## 3. Runtime Models
- [x] 3.1 Create `nirmata.Agents/Models/RunRequest.cs` — encapsulates run initiation parameters
- [x] 3.2 Create `nirmata.Agents/Models/RunResponse.cs` — encapsulates run result data
- [x] 3.3 Create `nirmata.Agents/Models/RunContext.cs` — execution context for a run
- [x] 3.4 Verify: compile + minimal smoke

## 4. Persistence Abstractions
- [x] 4.1 Create `nirmata.Agents/Persistence/IRunRepository.cs` — abstract run storage
- [x] 4.2 Create `nirmata.Agents/Persistence/RunRepository.cs` — wraps Engine stores
- [x] 4.3 Verify: compile + minimal smoke

## 5. Observability
- [x] 5.1 Create `nirmata.Agents/Observability/ICorrelationIdProvider.cs`
- [x] 5.2 Create `nirmata.Agents/Observability/RunCorrelationIdProvider.cs` — formats as RUN-*
- [x] 5.3 Create `nirmata.Agents/Observability/AgentsLoggerExtensions.cs` — structured logging helpers
- [x] 5.4 Verify: logs include correlation ID in RUN-* format during test run
