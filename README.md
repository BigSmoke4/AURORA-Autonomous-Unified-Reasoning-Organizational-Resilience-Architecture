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
| Domain events and outbox | `AggregateRoot`, `OutboxMessage`, `OutboxDispatcher`, `OrganizationHub` | Decision and alert transitions are written to the outbox in the same transaction as the change. A background dispatcher delivers them to idempotent handlers (at least once) and pushes notifications over SignalR. Integration tested against PostgreSQL. |
| Digital twin and knowledge graph | `Aurora.Modules.DigitalTwin`, `Aurora.Modules.Graph` | Suppliers, factories, warehouses, products, customers, and technology assets are projected into a graph. |
| Graph intelligence | `GraphTraversalService` | breadth-first traversal, Dijkstra shortest dependency path, upstream and downstream impact with max-product propagation, and vertex minimum-cut analysis (Edmonds-Karp max-flow) for single points of failure. Unit tested. |
| Deterministic simulation (`aurora-sim-v1`) | `SimulationEngine` | Day-by-day impact and revenue propagation over the graph. The model is documented in ADR 005. |
| Counterfactual analysis | `CounterfactualAnalyzer` | Compares a scenario with and without a mitigation share on the target's outgoing dependencies. Reports avoided revenue impact and the peak-impact reduction. |
| Risk scoring | `Aurora.Modules.Risk` | A configurable weighted formula (`Risk:ScoringWeights`). |
| Forecasting (`holt-linear-v1`) | `HoltForecaster` | Holt linear exponential smoothing with fitted smoothing parameters, prediction intervals from residual standard deviation, and a holdout MAPE backtest. Horizon 1 to 24. |
| Supply allocation (`allocation-greedy-v1`) | `SupplyAllocationOptimizer` | Weighted greedy allocation across capacity and share caps, swept over a cost/risk weight grid. Returns a recommended plan and a Pareto frontier. |
| Decisions and human approval | `Aurora.Modules.Decisions` | Proposals come from counterfactual evidence and a weighted score. Approve and reject are explicit. The proposer cannot approve or reject their own decision. Execution and the actual outcome are recorded separately, and the prediction error is computed. |
| Analysis records | `Aurora.Modules.Analysis` | Counterfactual, optimization, and forecast results are stored with their model version, inputs, and outputs. |
| AI investigation | `InvestigationOrchestrator`, `OpenAiCompatibleAiProvider` | Four deterministic tools run first and produce evidence. A model, if configured, only phrases the narrative. Without a model, a deterministic narrative is used. |
| Repository layer | `Data/Repositories/IRepository.cs`, `EfRepository<T>` | Module controllers read and stage writes through `Entities<T>()`, a generic repository over the tenant-filtered context. The request's `SaveChangesAsync` commits each change with its audit row. See ADR 008. |
| Graph snapshots for counterfactuals | `GraphSnapshotStore`, `GraphSnapshotCodec`, `GraphSnapshotRecord` | The live graph can be captured as versioned JSON, keeping every node and edge id, so scenarios still resolve. A counterfactual can run against a stored capture instead of the current graph. Captures are audited. The system does not rebuild past graphs from history: a capture shows the graph as it was when it was taken. See ADR 009. |
| Metric feed ingestion | `Integration/Feeds`, `Services/MetricReadingRecorder` | Adapters only fetch raw payloads. One shared parser validates a fixed schema and rejects bad rows one at a time, with row numbers. Accepted rows go through the same recorder as manual entry, so alerts behave the same way. Each feed uses one adapter: the HTTP adapter when `ExternalFeeds` configures its endpoint (https only, validated at startup), otherwise the bundled fixture. The live path is tested with stubs and has not been run against a provider. See ADRs 009 and 011. |
| Vulnerability linkage | `Aurora.Modules.Technology` (`VulnerabilityAdvisory`, `PatchExposureCalculator`), `VulnerabilityFeedIngestor` | Patch policies can name a software product. Advisories from the vulnerability feed are matched by product, and an advisory published after the last patch is exposed, with a deadline of its publication date plus the policy's SLA. Severity follows CVSS bands (Critical 9.0 and above, High 7.0, Medium 4.0). Re-importing an unchanged feed creates no rows. See ADR 011. |
| Shared narrative cache | `Infrastructure/Ai/CachingAiProvider`, Redis | Model narratives are cached under a hash of the model version, prompt, and evidence, so a model change invalidates the cache. Redis is used when `ConnectionStrings:Redis` is set, and a process-local cache otherwise. A cache failure never fails a request. See ADR 011. |
| Background job queue | `Infrastructure/Jobs`, `Data/Jobs` | Tenant-scoped job rows with at-least-once delivery. A failed job retries after 30 seconds, up to 3 attempts. A job whose 5-minute lease expires is claimed again, so handlers must be idempotent. The dispatcher reads jobs across tenants explicitly, and a job's tenant comes only from the job row. See ADR 009. |
| Knowledge retrieval | `Aurora.Modules.AI` (`DocumentChunker`, `DocumentRetriever`), `KnowledgeController` | Documents are split into chunks and ranked with Okapi BM25 (k1 1.5, b 0.75). Scores are deterministic and no model is involved. Each search returns at most 5 chunks. Every search loads all of the organization's chunks into memory, so the corpus is intended to stay modest in size. See ADR 010. |
| Multi-agent decision debate | `DecisionDebateEngine`, `Services/DecisionDebateService`, `DecisionsController.Debate` | An advocate and a critic test the decision's recorded numbers against named thresholds, and a count-based judge gives the verdict. A tie goes to revision. The debate is rule-based and contains no language model. See ADR 010. |
| Executive report run history | `Aurora.Modules.Reporting`, `ExecutiveReportRuns` | Each CSV export is recorded with the requester, the line count, and the SHA-256 hash of the content. |
| Customers view | `CustomersController`, `Views/Customers` | Customer concentration, with the Herfindahl-Hirschman index and a detail page for each customer. |
| Audit trail | `Aurora.Modules.Audit` | Records sign-ins, scenario and simulation runs, analyses, decision transitions, and AI investigations. Each entry carries the trace id as its correlation id. Entries commit in the same transaction as the change they describe. |
| Natural-language scenario drafting | `NaturalLanguageScenarioExtractor` | A rule-based extractor, not an LLM call. It reports missing fields and does not guess them. |
| Command Center | `CommandCenterController` | Live counts from the database and the neural graph view (`wwwroot/js/aurora.graph.js`). |
| Real-time simulation progress | SignalR, `Hubs/SimulationHub.cs` | Authenticated users join a group for one scenario. The group name includes the caller's organization, so tenants cannot join each other's groups. Progress is reported in three real phases: snapshot loaded, impact propagated, results saved. It is best-effort and never changes the run's outcome. |
| Security hardening | `SecurityHeadersMiddleware`, `RateLimitPolicies` | A strict Content Security Policy that forbids inline script and style. Rate limits on sign-in, AI investigation, and analysis endpoints. |
| Observability | `AuroraTelemetry`, OpenTelemetry, Serilog | Tracing and metrics for ASP.NET Core and outgoing HTTP calls, with AURORA's own activity source and meter. Structured request logs through Serilog. Console exporters are used only in Development. Health endpoints are `/health/live`, `/health/ready`, and `/health`. |
| Seed data | `Data/Seed/DbInitializer.cs`, `Data/Seed/OperationalModuleSeeder.cs` | The "Meridian Industrial Group" demo, with a deliberate single-source supplier dependency. Phase 3 demo records (roles, cost centers, production, patch policies, markets, assets, contracts, inventory, metrics, one open alert) are added for new organizations only. Idempotent. |
| Tests | `tests/Aurora.Tests` | Vulnerability parsing and exposure rules, HTTP feed adapters against stub handlers, option validation, the vulnerability ingestor against PostgreSQL, the caching decorator, and a Redis shared-read check that CI runs against a Redis service. Live feed and model checks are in `tests/Aurora.Tests/Live`, and each one runs only when its endpoint is set. CI also runs a k6 smoke load test (`tests/load`) and CodeQL. Unit tests for the graph, simulation, counterfactual, risk, forecasting, optimization, decision scoring, AI provider and orchestrator, and audit. An integration test runs the migrations against PostgreSQL and checks tenant isolation. The web host is started in Production mode and must serve its health, readiness, and login endpoints. Claim resolution is tested against ASP.NET Core Identity: active users get their organization, and deactivated or deleted users get none. Phase 3 calculators, entity rules, alert lifecycle, and CSV escaping have unit tests under `tests/Aurora.Tests/Modules`. HTTP tests in `tests/Aurora.Tests/Web` sign in through the login form and check that a deactivated user is refused, that every module page redirects anonymous users, that each page renders seeded data, that a recorded role stays within its own tenant, and that the CSV export works. HTTP tests also post every Phase 3 create form and check that exactly one row is saved for the tenant, so a form that fails to bind is caught. Phase 5 unit tests cover the chunker, BM25 retrieval, the debate engine, the snapshot codec, the feed parser against its fixture, and the job state machine, under `tests/Aurora.Tests/{Knowledge,Graph,Feeds,Jobs}`. |
| Docker | `Dockerfile`, `docker-compose.yml` | PostgreSQL 16 plus the web app. The image runs as the non-root `app` user, and `.dockerignore` keeps `.env` and test output out of the build. Secrets come from the environment. CI builds the image, starts it against PostgreSQL, and checks readiness and security headers on every push. The compose stack serves plain HTTP for local use (`Security__RequireHttps=false`). Any shared deployment must put TLS in front and leave that setting at its default. |

---

## Not implemented

These parts of the specification are **not** complete. None is faked, and none has a placeholder class.

- **Live external feeds and model endpoints are unverified.** The HTTP adapters for feeds and the OpenAI-compatible model client are built and tested with fixtures and stubs. The opt-in live tests in `tests/Aurora.Tests/Live` have not been run against a real provider, so their response formats are unconfirmed.
- **Penetration testing and dynamic scanning.** [`docs/SECURITY-REVIEW.md`](docs/SECURITY-REVIEW.md) is a code and CI review. No external penetration test or DAST scan has been run.
- **Soak testing.** CI runs a two-minute smoke load test. A multi-hour soak needs a dedicated environment and has not been run.

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
| `20261013140000_Phase3OperationalModules` | Generated, with a Designer file | Eleven tables for the operational modules, each tenant-scoped and indexed by `OrganizationId`: critical roles, cost centers, production records, patch policies, market observations, physical assets, supplier contracts, inventory positions, metric definitions, metric readings, and alerts. Additive only; no existing table changes. |

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
  user-secrets or the environment. `AI:TimeoutSeconds` bounds each attempt. `AI:Endpoint` must be https, or http
  on a loopback host. `AI:CacheMinutes` (default 60, zero disables) sets how long an identical narrative is reused.
- `ExternalFeeds:Metrics` and `ExternalFeeds:Vulnerabilities`: `BaseUrl` (https only), `Path`, `ApiKey`, and
  `TimeoutSeconds` (1 to 120). A feed with no `BaseUrl` uses its bundled fixture. The host refuses to start with an
  invalid endpoint.
- `ConnectionStrings:Redis`: optional. When set, narratives are cached in Redis, shared by every instance.
- `Security:TrustedProxies`: addresses of reverse proxies whose `X-Forwarded-For` header is trusted. Without it,
  the sign-in limit sees only the proxy's address.
- Live test variables (opt-in, never set in CI): `AURORA_LIVE_METRIC_FEED_URL`, `AURORA_LIVE_METRIC_FEED_KEY`,
  `AURORA_LIVE_VULNERABILITY_FEED_URL`, `AURORA_LIVE_VULNERABILITY_FEED_KEY`, `AURORA_LIVE_MODEL_ENDPOINT`,
  `AURORA_LIVE_MODEL_NAME`, `AURORA_LIVE_MODEL_KEY`.

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
- Feed and model API keys are sent only over https (or to a loopback host for the model). The host refuses to start otherwise.
- The review of these controls, with its findings and accepted residual risks, is in [`docs/SECURITY-REVIEW.md`](docs/SECURITY-REVIEW.md).

---

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md): layering, module shape, request flows, tenant isolation, and what is not built.
- [`docs/SECURITY-REVIEW.md`](docs/SECURITY-REVIEW.md): the security review, its findings, and accepted residual risks.
- [`docs/ADR/`](docs/ADR): the architecture decisions, including why the LLM is never the calculation
  engine (ADR 004), why simulation is deterministic (ADR 005), and why decisions require a human (ADR 006).

## Roadmap

1. Run the opt-in live feed and model tests against real endpoints, with credentials held outside the repository.
2. Commission an external penetration test and a DAST scan of the running application.
3. Run a multi-hour soak test in a dedicated environment, and record the results.
4. Add an explicit WebSocket origin check to the hubs (see residual risk 8 in the security review).

## License

AURORA is open-source software licensed under the MIT License.

Copyright © 2026 Ahanaf Mokammel.
