# AURORA Architecture

## Layering

```
Browser (Razor pages, centralized CSS and JS under wwwroot; no inline script or style)
  |
Aurora.Web (ASP.NET Core MVC, Razor Views, SignalR hub, API controllers, health checks)
  |
Module Application layers (GraphTraversalService, SimulationEngine, CounterfactualAnalyzer,
                            HoltForecaster, SupplyAllocationOptimizer, DecisionProposalFactory,
                            InvestigationOrchestrator, AuditRecorder, ...)
  |
Module Domain layers (Organization, Supplier, GraphNode, Scenario, SimulationRun, Decision,
                       AnalysisRecord, AuditEntry, AiInvestigation, ...)
  |
Module Infrastructure layers (IEntityTypeConfiguration<T> classes, OpenAiCompatibleAiProvider)
  |
Aurora.Web/Data (composite AuroraDbContext, repositories that implement module Contracts)
  |
PostgreSQL
```

Every module (`src/Modules/Aurora.Modules.*`) follows the same internal shape:

```
Aurora.Modules.<Name>/
  Domain/            entities, value objects, enums. No EF Core and no ASP.NET dependency.
  Application/       services and orchestration that operate on Domain types
  Infrastructure/    IEntityTypeConfiguration<T> classes (schema ownership), where a module has persistence
  Contracts/         interfaces that other modules and Aurora.Web depend on
```

Modules: Identity, Organizations, DigitalTwin, Graph, Scenarios, Simulation, Risk, AI, Decisions,
Analysis, Forecasting, Optimization, Audit.

`Aurora.Web` is the only project that references every module and the only place a concrete
`DbContext` exists (`Aurora.Web/Data/AuroraDbContext.cs`). It implements each module's `Contracts`
interfaces against that shared context (for example `Aurora.Web/Data/AuditRepository.cs` implements
`Aurora.Modules.Audit.Contracts.IAuditRepository`). It wires everything together through dependency
injection in `Program.cs`. See [ADR 001](ADR/001-modular-monolith.md) for why this is a modular
monolith and not microservices.

## Persistence and migrations

- `AuroraDbContext` is the single composite context. Each module contributes its own configurations.
- Migrations: `InitialCreate` (EF Core 8, `20260901062452`) and `Phase2AuditAnalysisDecisionsAi`
  (`20261013120000`, hand-written, with a Designer file). The snapshot matches the model. CI runs
  `dotnet ef migrations has-pending-model-changes` after the build, so a model change without a
  migration fails the build.
- `Phase2AuditAnalysisDecisionsAi` adds decision provenance and prediction columns, AI narrative
  provenance columns, and the `AnalysisRecords` and `AuditEntries` tables. Existing rows receive
  safe defaults. Its `Down` reverts all of it. The integration test applies the chain, reverts
  Phase 2, and applies it again.
- Money uses `decimal` columns with explicit precision. Timestamps are UTC (`timestamp with time zone`).

## Tenant isolation

Tenant isolation is enforced in three layers:

1. **Tenant source.** `RequestTenantContext` reads the organization id only from the signed-in
   principal's claim. It never reads a request body, query string, or route value.
2. **Query filter.** Every `ITenantScoped` entity gets a global query filter in `AuroraDbContext` on
   the current tenant. With no tenant (anonymous request or design-time tool) the filter compares
   against `Guid.Empty`, which matches no rows. Administrative reads that need rows from every tenant
   must call `IgnoreQueryFilters()` explicitly. The seeder does this when it counts graph edges.
   `AuditEntry` is the one exception to the `ITenantScoped` rule, because platform-level sign-in events
   have no organization. It has its own filter, written explicitly in `OnModelCreating`, that matches
   only the caller's organization. Those null-organization events are not visible to any tenant.
   Child rows without an organization id (simulation steps and results, AI evidence) are reached only
   through their tenant-scoped parents.
3. **Explicit scope.** Repositories also take an `organizationId` parameter and filter by it, so
   correctness does not depend on the filter alone. `CurrentOrganizationAccessor` resolves the
   caller's organization for controllers.

The integration test checks that a second tenant sees no decisions, audit entries, or analysis
records, and that an anonymous context sees none either. It also checks that the unfiltered count
shows the rows exist, so the zero counts mean something.


## Transport and container

- **HTTPS.** `Security:RequireHttps` (default `true`) controls the HTTPS redirect and HSTS. Auth cookies are
  `Secure` outside Development. The local Compose stack sets the flag to `false` because it serves HTTP on
  localhost. Shared deployments must terminate TLS in front of the app and keep the default.
- **Container.** The image runs as the unprivileged `app` user from the aspnet base image. `.dockerignore`
  excludes `.env`, build output, and test results.
- **CI.** Every push runs two jobs. `build-and-test` restores, runs a vulnerable-package audit (failing on any
  known advisory), builds with warnings as errors, checks the EF model against the migrations, and runs the tests.
  `container-smoke` builds the image, starts it in Production mode against a PostgreSQL service, waits for
  `/health/ready`, and checks `/health/live`, the login page, and the Content Security Policy header.

## Optimistic concurrency

`Entity.Version` is a `long` (PostgreSQL `bigint`) on every entity, and it is a concurrency token in
the model. `AuroraDbContext.SaveChanges` increments the version of each modified entity after change
detection. Concurrent updates therefore fail with `DbUpdateConcurrencyException` instead of silently
overwriting each other.

## Audit

`AuditRecorder` stages an `AuditEntry` through `IAuditRepository`. It does not save. The entry commits
in the same `SaveChanges` as the change it describes, so an audit row cannot exist without its
change, and the change cannot commit without its audit row. Sign-in is the exception: the account
controller calls `IAuditRepository.SaveChangesAsync` directly, because there is no other unit of
work there. Each entry records the actor, action, entity, outcome, and a bounded details string.
Each entry also carries the current trace id as its correlation id. Entries never store passwords or
other secrets.

## Request flow: "What would be affected if Northbridge Components failed?"

1. The browser selects the "Northbridge Components" node in the neural graph
   (`wwwroot/js/aurora.graph.js`) and submits a question from the AI Command Panel
   (`wwwroot/js/aurora.ai.js`). The request POSTs to `/api/ai/investigate` with the anti-forgery
   token in the `RequestVerificationToken` header (`wwwroot/js/aurora.api.js`).
2. `AiController.Investigate` validates the anti-forgery token, resolves the current tenant, and
   calls `InvestigationOrchestrator.InvestigateNodeRiskAsync`.
3. The orchestrator runs a **fixed** pipeline of deterministic tools. Each tool records an
   `AiEvidence` row:
   - `IGraphProjectionService.BuildLiveGraphAsync`: one database round trip that builds a `GraphSnapshot`.
   - `GraphTraversalService.DownstreamImpact`: in-memory propagation (max-product of edge strengths along the strongest path, pruned below `MinimumSignal`).
   - `GraphTraversalService.DetectSingleSourcedNodes`: a local single-sourcing screen. It is a heuristic, not a minimum-cut analysis.
   - `RiskScoringService.Score`: a deterministic weighted formula.
4. Only after the tools have produced their numbers does the orchestrator ask
   `IAiProvider.TryExplainAsync` for a narrative. The provider receives the evidence as delimited
   data, with an instruction to treat it as data and not as instructions. With no provider
   configured (`NullAiProvider`, the default), or if the provider fails or returns nothing, the
   orchestrator uses its deterministic narrative built from the same evidence. The investigation
   records which path produced the text (`NarrativeSource`) and the model version.
5. The `AiInvestigation` aggregate, with its evidence, is persisted and returned as JSON. The
   browser renders the narrative, the confidence, and each evidence line.

See [ADR 004](ADR/004-ai-provider-abstraction.md) for why the model is never the source of numbers.

## Request flow: a counterfactual decision

1. `StrategyController.Counterfactual` loads the scenario and the live graph, and calls
   `CounterfactualAnalyzer.Analyze(scenario, snapshot, mitigationShare)`. The analyzer runs the
   same `SimulationEngine` twice: once as modelled, and once with the target's outgoing dependency
   strengths reduced by the mitigation share. It returns the avoided revenue impact and the
   reduction in peak daily impact. A zero share reproduces the actual run exactly.
2. The result is saved as an `AnalysisRecord` with its model version, inputs, and results, and
   the run is audited.
3. `DecisionsController.Propose` reads that record and the human judgements (feasibility,
   alignment, days to benefit, confidence). `DecisionProposalFactory` converts them into factor
   scores, and `DecisionScoringService` combines them with `DecisionScoringWeights`. The decision is
   saved in `PendingApproval` with its factor JSON, so the score can be audited.
4. A different user approves or rejects it. `Decision.Approve` and `Decision.Reject` throw if the
   approver is the proposer (separation of duties). Only an approved decision can start execution.
   Recording the outcome computes the prediction error against the predicted impact.

## Request flow: running a simulation

1. `ScenariosController.Create` persists a `Scenario`. This is configuration only and includes no calculation.
2. `SimulationController.Run` builds a `GraphSnapshot`, creates a `SimulationRun`, and calls
   `SimulationEngine.Run(runId, scenario, snapshot)`. Failures are audited and shown with a message.
   Before and after the engine call, and after results are saved, it sends a phase progress message
   (1 of 3, 2 of 3, 3 of 3) to the scenario's SignalR group, `sim-{orgId}-{scenarioId}`. The browser
   joins that group through `SimulationHub.JoinScenario` before the form is posted. The hub's group
   name is built from the caller's own organization claim, so tenants cannot subscribe to each other.
   Progress is best-effort: a delivery failure is logged and does not change the run.
3. `SimulationEngine` (see [ADR 005](ADR/005-simulation-engine.md)) uses
   `GraphTraversalService.DownstreamImpact` to find the affected nodes. It then computes day-by-day
   impact and revenue from the snapshot's `Strength`, `Criticality`, and `RevenueExposure`, and from
   the scenario's `Severity` and `DurationDays`. The model version is `aurora-sim-v1`.
4. Steps and the aggregated result are persisted, and the results page renders them with a
   hand-rolled SVG chart (`wwwroot/js/aurora.simulation.js`). There is no charting library.

## Forecasting and optimization

- `HoltForecaster` (`holt-linear-v1`) fits Holt's linear method by grid search over the smoothing
  parameters and minimizes the sum of squared one-step errors. It reports prediction intervals from
  the residual standard deviation scaled by the square root of the horizon, and a backtest MAPE on a
  holdout of recent observations. The backtest is null when the holdout has no non-zero actuals.
- `SupplyAllocationOptimizer` (`allocation-greedy-v1`) allocates demand across candidate suppliers
  under capacity and share caps. It sweeps a grid of cost-versus-risk weights, and it reports a
  recommended plan and the Pareto-efficient frontier over total cost and risk exposure.

Both services are pure functions. Their outputs are stored with their model version so they can be
reproduced.

## What is intentionally not implemented

These parts of the specification are not built. The code contains no stubs or placeholder logic for them.

- **Domain events.** No domain event infrastructure exists. An earlier `Events/` abstraction was
  removed because nothing published or handled events. Side effects are direct calls in the same
  unit of work. An outbox and event dispatch are future work, to be added when a second consumer
  needs them.
- **Modules** named in the specification but not built: Workforce, Finance, Operations, Technology,
  Customers, Markets, Assets, Contracts, Monitoring, Alerts, Reporting, and SupplyChain. The
  Customers and Technology data that exists today lives in the DigitalTwin module.
- **Historical graph states.** There are no point-in-time graph snapshots. Counterfactuals use the
  current graph.
- **Minimum-cut and max-flow analysis.** The single-sourcing screen is a local heuristic.
- **Retrieval over documents, multi-agent debate, and background job queues.** Simulation and
  analysis run inline.
- **Distributed caching.** Nothing needs Redis at the current scale.
- **FluentValidation.** Validation uses data annotations and explicit domain checks.

See `README.md` for the roadmap.
