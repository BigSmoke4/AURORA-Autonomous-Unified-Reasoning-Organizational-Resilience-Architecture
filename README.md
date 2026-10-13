# AURORA

**Autonomous Unified Reasoning & Organizational Resilience Architecture**

AURORA is an enterprise digital twin and decision-intelligence platform. It models an organization
as a graph of suppliers, factories, products, customers, and technology assets. It runs "what if"
scenarios through a deterministic simulation engine, forecasts and optimizes with documented
models, and records human-approved decisions. Its AI layer explains evidence that the deterministic
code has already computed. A language model never produces a number that feeds a calculation.

Built with ASP.NET Core MVC and Razor Views, EF Core, PostgreSQL, ASP.NET Core Identity, SignalR,
and OpenTelemetry, as a modular monolith on .NET 10.

> **Scope note.** AURORA implements a working subset of the AURORA specification. The implemented
> modules are listed below. Parts of the 102-section specification are **not** implemented, and
> the code does not contain placeholder versions of them. The gaps are listed under
> [Not implemented](#not-implemented). [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) gives the
> detail, and the ADRs record the reasoning behind the main choices.

---

## What is implemented

| Capability | Where | Notes |
|---|---|---|
| Modular monolith | `src/Modules/Aurora.Modules.*` | Each module has `Domain`, `Application`, `Infrastructure`, and `Contracts` folders. Only `Aurora.Web` references all of them. |
| Identity, RBAC, sign-in | `Aurora.Modules.Identity` | ASP.NET Core Identity with lockout. Granular permission claims in `AuroraPermissions`. Deactivated users cannot sign in. |
| Multi-tenancy | `ITenantScoped`, `AuroraDbContext` query filters, `CurrentOrganizationAccessor` | The tenant is read only from the signed-in principal's claim. With no tenant the filter matches no rows. |
| Optimistic concurrency | `Entity.Version` (`bigint`) | Every entity's `Version` is a concurrency token and is incremented on update in `SaveChanges`. |
| Digital twin and knowledge graph | `Aurora.Modules.DigitalTwin`, `Aurora.Modules.Graph` | Suppliers, factories, warehouses, products, customers, and technology assets are projected into a graph. |
| Graph intelligence | `GraphTraversalService` | breadth-first traversal, Dijkstra shortest dependency path, upstream and downstream impact with max-product propagation, and a single-sourced node screen. Unit tested. |
| Deterministic simulation (`aurora-sim-v1`) | `SimulationEngine` | Day-by-day impact and revenue propagation over the graph. The model is documented in ADR 005. |
| Counterfactual analysis | `CounterfactualAnalyzer` | Compares a scenario with and without a mitigation share on the target's outgoing dependencies. Reports avoided revenue impact and the peak-impact reduction. |
| Risk scoring | `Aurora.Modules.Risk` | A configurable weighted formula (`Risk:ScoringWeights`). |
| Forecasting (`holt-linear-v1`) | `HoltForecaster` | Holt linear exponential smoothing with fitted smoothing parameters, prediction intervals from residual standard deviation, and a holdout MAPE backtest. Horizon 1 to 24. |
| Supply allocation (`allocation-greedy-v1`) | `SupplyAllocationOptimizer` | Weighted greedy allocation across capacity and share caps, swept over a cost/risk weight grid. Returns a recommended plan and a Pareto frontier. |
| Decisions and human approval | `Aurora.Modules.Decisions` | Proposals come from counterfactual evidence and a weighted score. Approve and reject are explicit. The proposer cannot approve or reject their own decision. Execution and the actual outcome are recorded separately, and the prediction error is computed. |
| Analysis records | `Aurora.Modules.Analysis` | Counterfactual, optimization, and forecast results are stored with their model version, inputs, and outputs. |
| AI investigation | `InvestigationOrchestrator`, `OpenAiCompatibleAiProvider` | Four deterministic tools run first and produce evidence. A model, if configured, only phrases the narrative. Without a model, a deterministic narrative is used. |
| Audit trail | `Aurora.Modules.Audit` | Records sign-ins, scenario and simulation runs, analyses, decision transitions, and AI investigations. Each entry carries the trace id as its correlation id. Entries commit in the same transaction as the change they describe. |
| Natural-language scenario drafting | `NaturalLanguageScenarioExtractor` | A rule-based extractor, not an LLM call. It reports missing fields and does not guess them. |
| Command Center | `CommandCenterController` | Live counts from the database and the neural graph view (`wwwroot/js/aurora.graph.js`). |
| Real-time simulation progress | SignalR, `Hubs/SimulationHub.cs` | Authenticated users join a group for one scenario. The group name includes the caller's organization, so tenants cannot join each other's groups. Progress is reported in three real phases: snapshot loaded, impact propagated, results saved. It is best-effort and never changes the run's outcome. |
| Security hardening | `SecurityHeadersMiddleware`, `RateLimitPolicies` | A strict Content Security Policy that forbids inline script and style. Rate limits on sign-in, AI investigation, and analysis endpoints. |
| Observability | `AuroraTelemetry`, OpenTelemetry, Serilog | Tracing and metrics for ASP.NET Core and outgoing HTTP calls, with AURORA's own activity source and meter. Structured request logs through Serilog. Console exporters are used only in Development. Health endpoints are `/health/live`, `/health/ready`, and `/health`. |
| Seed data | `Data/Seed/DbInitializer.cs` | The "Meridian Industrial Group" demo, with a deliberate single-source supplier dependency. Idempotent. |
| Tests | `tests/Aurora.Tests` | Unit tests for the graph, simulation, counterfactual, risk, forecasting, optimization, decision scoring, AI provider and orchestrator, and audit. An integration test runs the migrations against PostgreSQL and checks tenant isolation. |
| Docker | `Dockerfile`, `docker-compose.yml` | PostgreSQL 16 plus the web app. Secrets come from the environment. |

---

## Not implemented

These parts of the specification are **not** built. None is faked, and none has a placeholder class.

- **Domain events.** There is no domain event infrastructure. The earlier `Events/` code was removed
  because nothing used it. Module side effects are direct calls, and audit entries are written
  synchronously.
- **Modules.** Workforce, Finance, Operations, Technology, Customers, Markets, Assets, Contracts,
  Monitoring, Alerts, Reporting, and SupplyChain are not built as modules.
- **Historical counterfactuals.** Analyses run against the current graph. There are no historical
  graph snapshots, so "what if this had happened six months ago" is not supported.
- **Minimum-cut analysis.** The single-sourced node screen is a local heuristic. It is not a
  minimum-cut or max-flow computation.
- **Knowledge retrieval (RAG), document ingestion, and multi-agent debate.**
- **Background job queue.** Simulation and analysis run inline. Progress is pushed by SignalR while the request runs. A queue is
  a planned, non-breaking change (ADR 005).
- **Redis or any distributed cache.** Nothing needs one at the current scale.

---

## Getting started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- PostgreSQL 16, or Docker with Compose
- `dotnet-ef` only if you add migrations: `dotnet tool install --global dotnet-ef --version 10.0.12`

### 1. Start PostgreSQL
```bash
docker compose up -d postgres
```
`docker-compose.yml` reads `AURORA_DB_PASSWORD` from the environment or a `.env` file. Copy
`.env.example` to `.env` and set it. The database is bound to `127.0.0.1` only.

### 2. Configure secrets
Connection strings and passwords are never stored in source. Use user-secrets or environment
variables:

```bash
dotnet user-secrets set "ConnectionStrings:AuroraDb" "Host=localhost;Port=5432;Database=aurora;Username=aurora;Password=<local password>" --project src/Web/Aurora.Web
dotnet user-secrets set "Seed:AdminPassword" "<12+ characters with upper, lower, digit, and symbol>" --project src/Web/Aurora.Web
```

`Seed:AdminPassword` creates the bootstrap administrator `admin@aurora.local` on the first run,
when no administrator exists yet. If the setting is empty, no administrator is created.

### 3. Run the app
```bash
dotnet run --project src/Web/Aurora.Web/Aurora.Web.csproj
```
On startup the app applies the migrations and seeds the demo organization. Sign in with
`admin@aurora.local` and the password you set. The development profile listens on the URL printed
by `dotnet run`.

### 4. Or run everything in Docker
```bash
docker compose up --build
```
The app is available at `http://localhost:8080`. Set `AURORA_ADMIN_PASSWORD` in `.env` before the
first start if you want the bootstrap administrator.

### Run the tests
```bash
dotnet test Aurora.sln
```
The integration test in `tests/Aurora.Tests/Persistence` needs a PostgreSQL database. Set
`AURORA_TEST_CONNECTION` to a connection string whose database name contains `test`, for example
`Host=localhost;Port=5432;Database=aurora_test;Username=aurora;Password=<password>`. The test drops
and recreates that database, so the name guard is deliberate. Without the variable the test fails
with a clear message. CI provides the variable from a PostgreSQL service container.

---

## Database migrations

The migration set is:

| Migration | Source | Contents |
|---|---|---|
| `20260901062452_InitialCreate` | Generated by EF Core 8 | Identity tables, organizations, the digital twin, graph, risk, scenarios, and simulation tables. |
| `20261013120000_Phase2AuditAnalysisDecisionsAi` | Hand-written, with a Designer file | Decision provenance and prediction tracking columns, AI narrative provenance, the `AnalysisRecords` and `AuditEntries` tables, and their indexes. Existing rows get safe defaults. |

The snapshot in `Migrations/AuroraDbContextModelSnapshot.cs` matches the model. CI runs
`dotnet ef migrations has-pending-model-changes`, which fails the build if the model and the
migrations differ. The integration test applies the whole chain to an empty database, reverts the
Phase 2 migration, and applies it again.

To add a migration:
```bash
dotnet ef migrations add <Name> \
  --project src/Web/Aurora.Web/Aurora.Web.csproj \
  --startup-project src/Web/Aurora.Web/Aurora.Web.csproj
```

---

## A guided tour

1. **Command Center** (`/`): the neural graph of the organization. Click a node, such as
   "Northbridge Components", and ask the AI panel "What happens if this fails?" The answer comes
   from graph traversal and risk scoring. The model, if any, only phrases it.
2. **Scenario Lab** (`/Scenarios`): create a scenario against a graph node and run it. The
   simulation results show day-by-day impact and revenue exposure.
3. **Strategy Lab** (`/Strategy`): compare a mitigation share against the actual run, or compute an
   optimized supply allocation from a candidate list.
4. **Forecasting** (`/Forecasting`): forecast a numeric series with intervals and a backtest. Every
   result shows its model version.
5. **Decisions** (`/Decisions`): propose a decision from a stored counterfactual analysis. A
   different person approves or rejects it. After execution, record the actual outcome to see the
   prediction error.
6. **Audit** (`/Audit`): the recent audit trail for your organization. You need the audit-read
   permission to see it.
7. **Digital Twin** (`/DigitalTwin`): the tabular view of the suppliers, factories, products,
   customers, and technology assets that the graph is built from.

---

## Configuration

Settings are strongly typed. They live in `appsettings.json`, environment variables, or user-secrets.
The defaults in `appsettings.json` contain no secrets.

- `ConnectionStrings:AuroraDb`: PostgreSQL connection string. Set it with user-secrets or an environment variable.
- `Seed:AdminPassword`: bootstrap administrator password. Empty means no administrator is created.
- `Risk:ScoringWeights`: the four risk formula weights (spec section 18).
- `Decisions:ScoringWeights`: the five decision scoring weights (spec section 84).
- `AI:Provider`: `None` (default, deterministic narrative only) or `OpenAICompatible`. For
  `OpenAICompatible`, also set `AI:Endpoint`, `AI:Model`, and `AI:ApiKey`. The key must come from
  user-secrets or the environment. `AI:TimeoutSeconds` bounds each attempt.

To add another LLM provider, implement `IAiProvider` (see [ADR 004](docs/ADR/004-ai-provider-abstraction.md)).
The provider never receives tools and never computes numbers.

---

## Security notes

- Secrets are never committed. The bootstrap administrator password is supplied at runtime.
- Sign-in is rate limited and locks out repeated failures. Deactivated users are refused at sign-in.
- Every mutating form uses an anti-forgery token. Cookies are `HttpOnly` and `SameSite`.
- Authorization uses permission claims (`Aurora.Modules.Identity.Domain.AuroraPermissions`) through policies. Tenant
  scope comes from the signed-in principal, never from the request body or query string.
- The Content Security Policy forbids inline script and style, so application code lives only in
  `wwwroot/js` and `wwwroot/css`.
- The AI layer receives only the evidence the orchestrator computed, wrapped as delimited data.
  The model is never given database access, and it cannot call tools.

---

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md): layering, module shape, request flows, tenant isolation, and what is not built.
- [`docs/ADR/`](docs/ADR): the architecture decisions, including why the LLM is never the calculation
  engine (ADR 004), why simulation is deterministic (ADR 005), and why decisions require a human (ADR 006).

## Roadmap

1. Domain events, with an outbox, once a second consumer needs them.
2. The remaining specification modules, starting with Finance, Operations, and SupplyChain.
3. Historical graph snapshots, which enable point-in-time counterfactuals.
4. Retrieval over organizational documents, feeding the same evidence model as the graph tools.
5. A background job queue for long-running simulations and analyses (ADR 005).

## License

AURORA is open-source software licensed under the MIT License.

Copyright © 2026 Ahanaf Mokammel.
