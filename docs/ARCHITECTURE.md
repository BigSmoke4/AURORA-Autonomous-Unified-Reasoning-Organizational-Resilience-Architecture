# AURORA Architecture

## Layering

```
Browser
  |
Aurora.Web (ASP.NET Core MVC, Razor Views, SignalR, API controllers)
  |
Module Application layers (GraphTraversalService, SimulationEngine, RiskScoringService,
                            InvestigationOrchestrator, NaturalLanguageScenarioExtractor, ...)
  |
Module Domain layers (Organization, Supplier, Factory, GraphNode, Scenario, SimulationRun, ...)
  |
Module Infrastructure layers (EF Core IEntityTypeConfiguration<T> classes)
  |
Aurora.Web/Data (composite AuroraDbContext + repository implementations of module Contracts)
  |
PostgreSQL
```

Every module (`src/Modules/Aurora.Modules.*`) follows the same internal shape:

```
Aurora.Modules.<Name>/
  Domain/            entities, value objects, enums — no EF Core, no ASP.NET dependency
  Application/        services, orchestration logic operating on Domain types
  Infrastructure/      IEntityTypeConfiguration<T> classes (schema ownership)
  Contracts/           interfaces other modules and Aurora.Web depend on
```

`Aurora.Web` is the only project that references every module and the only place a concrete
`DbContext` exists (`Aurora.Web/Data/AuroraDbContext.cs`). It implements each module's
`Contracts` interfaces against that shared context (e.g. `Aurora.Web/Data/GraphRepository.cs`
implements `Aurora.Modules.Graph.Contracts.IGraphRepository`) and wires everything together via
dependency injection in `Program.cs`. See [ADR 001](ADR/001-modular-monolith.md) for why this is
a modular monolith rather than microservices.

## Request flow: "What would be affected if Northbridge Components failed?"

1. Browser: user clicks the "Northbridge Components" node in the neural graph
   (`wwwroot/js/aurora.graph.js`), then submits a question in the AI Command Panel
   (`wwwroot/js/aurora.ai.js`), which POSTs to `/api/ai/investigate`.
2. `AiController.Investigate` resolves the current tenant (`CurrentOrganizationAccessor`) and
   calls `InvestigationOrchestrator.InvestigateNodeRiskAsync`.
3. The orchestrator (`Aurora.Modules.AI.Application.InvestigationOrchestrator`) runs a **fixed**
   pipeline of real tools, each producing `AiEvidence`:
   - `IGraphProjectionService.BuildLiveGraphAsync` — one DB round trip, builds a `GraphSnapshot`.
   - `GraphTraversalService.DownstreamImpact` — pure in-memory graph algorithm.
   - `GraphTraversalService.DetectSinglePointsOfFailure` — pure in-memory graph algorithm.
   - `RiskScoringService.Score` — pure deterministic formula.
4. Only after all four tools have produced real numbers does the orchestrator optionally call
   `IAiProvider.ExplainAsync` to phrase a narrative — and if no provider is configured
   (`NullAiProvider`, the default), it falls back to a templated sentence built from the same
   evidence. Either way the evidence itself is identical.
5. The `AiInvestigation` aggregate (with all `AiEvidence` children) is persisted and returned as
   JSON; the browser renders the narrative, confidence, and each evidence line.

See [ADR 004](ADR/004-ai-provider-abstraction.md) for why this shape is non-negotiable.

## Request flow: running a simulation

1. `ScenariosController.Create` persists a `Scenario` (pure configuration: target node id, event
   type, severity, duration — no calculation).
2. `SimulationController.Run` builds a `GraphSnapshot`, constructs a `SimulationRun`, and calls
   `SimulationEngine.Run(runId, scenario, snapshot)`.
3. `SimulationEngine` (see [ADR 005](ADR/005-simulation-engine.md)) calls
   `GraphTraversalService.DownstreamImpact` to find every node reachable from the scenario's
   target, then computes a day-by-day `ImpactScore` and `RevenueImpact` per affected node using
   only fields already on the graph snapshot (`Strength`, `Criticality`, `RevenueExposure`) and
   the scenario's own `Severity`/`DurationDays`.
4. Steps and an aggregated `SimulationResult` are persisted; `SimulationController.Results`
   renders them, including a hand-rolled SVG chart (`wwwroot/js/aurora.simulation.js`) — no
   charting library dependency.

## Tenant isolation

Every organization-scoped entity implements `Aurora.Shared.Kernel.ITenantScoped` (an
`OrganizationId` property). `CurrentOrganizationAccessor` resolves the caller's organization from
a claim added at sign-in (`AuroraClaimsTransformation`) rather than trusting anything from the
request body or query string, and every repository method takes an explicit `organizationId`
parameter and filters by it — there is no "ambient" tenant context that could be forgotten.

## What is intentionally not implemented in this vertical slice

This codebase implements a real, working core: Graph Intelligence, deterministic Simulation, Risk
scoring, Decision approval workflow, and an evidence-based AI investigation pipeline, wired
through a working ASP.NET Core MVC UI. The full 102-section specification also describes
Optimization (multi-objective solver), Forecasting (statistical/ML models with confidence
intervals), a full RAG/document pipeline, multi-agent debate, and a much larger digital-twin
entity catalog. Those are scaffolded as clear extension points (module folders, `Contracts`
interfaces, and this document) rather than implemented with placeholder logic, in line with the
project's own rule against fake implementations (spec section 99). See `README.md` → "Roadmap"
for the concrete next slice of work.
