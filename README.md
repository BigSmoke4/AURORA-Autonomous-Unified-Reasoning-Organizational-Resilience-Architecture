# AURORA

**Autonomous Unified Reasoning & Organizational Resilience Architecture**

AURORA is an enterprise digital twin and decision-intelligence platform: it models an
organization as a graph of suppliers, factories, products, customers, and technology assets, lets
you run "what if" scenarios through a deterministic simulation engine, and surfaces
evidence-backed AI investigations — without ever letting a language model invent a number that
matters.

Built with ASP.NET Core MVC, Razor Views, EF Core, and PostgreSQL as a modular monolith.

> **Scope note.** This repository implements a real, working vertical slice of the much larger
> AURORA specification: the Graph Intelligence engine, the deterministic Simulation engine, Risk
> scoring, the Decision approval workflow, and an evidence-based AI investigation pipeline, all
> wired end-to-end through a working Command Center UI. It does **not** claim to implement every
> one of the specification's 102 sections (full Optimization solver, statistical Forecasting,
> RAG/document ingestion, multi-agent debate, and the complete ~30-entity digital twin catalog are
> scaffolded as extension points, not faked). See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
> for exactly what's real today and [Roadmap](#roadmap) below for what's next. This is an honest
> engineering decision, not a shortcut: the project's own rules (spec section 99) forbid
> placeholder business logic, and a "complete" implementation of the full spec is realistically a
> multi-quarter effort.

---

## What's actually implemented

| Capability | Where | Notes |
|---|---|---|
| Modular monolith (DDD-flavored modules) | `src/Modules/*` | One deployable, clear module boundaries via `Contracts` interfaces |
| PostgreSQL schema via EF Core | `src/Modules/*/Infrastructure`, `src/Web/Aurora.Web/Data` | Decimal money columns, indexes, FKs, optimistic concurrency scaffolding |
| Graph Intelligence Engine | `Aurora.Modules.Graph.Application.GraphTraversalService` | Real BFS/DFS, Dijkstra shortest path, upstream/downstream impact propagation, single-point-of-failure detection, bottleneck scoring — unit tested |
| Deterministic Simulation Engine | `Aurora.Modules.Simulation.Application.SimulationEngine` | Day-by-day impact/revenue propagation over the graph, fully documented model, unit tested |
| Risk scoring | `Aurora.Modules.Risk.Application.RiskScoringService` | Configurable weighted formula |
| Decision scoring & human-in-the-loop approval | `Aurora.Modules.Decisions` | Approve/Reject are explicit, audited actions — nothing auto-executes |
| AI investigation pipeline | `Aurora.Modules.AI.Application.InvestigationOrchestrator` | Composes real tool calls (graph + risk) into evidence; LLM only narrates, never calculates — works fully with zero AI provider configured |
| Natural-language scenario drafting | `Aurora.Modules.Scenarios.Application.NaturalLanguageScenarioExtractor` | Honest rule-based extractor, not an LLM call — flags missing fields instead of guessing |
| Neural graph visualization | `wwwroot/js/aurora.graph.js` | Hand-rolled force-directed layout + SVG rendering, click-to-focus, search |
| Real-time simulation progress | SignalR (`Hubs/SimulationHub.cs`) | |
| RBAC | `Aurora.Modules.Identity` + ASP.NET Core Identity | Roles + granular permission claims |
| Multi-tenancy | `ITenantScoped`, `CurrentOrganizationAccessor` | Enforced server-side per request, not by UI filtering |
| Realistic seed data | `Data/Seed/DbInitializer.cs` | "Meridian Industrial Group" — intentionally concentrated supplier risk, ~40 graph nodes |
| Unit tests | `tests/Aurora.Tests` | Graph algorithms, simulation engine, risk scoring, NL extractor |
| Docker Compose (Postgres + app) | `docker-compose.yml`, `Dockerfile` | |

---

## Getting started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PostgreSQL 16 (or use the provided Docker Compose)
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`

### 1. Start PostgreSQL
```bash
docker compose up -d postgres
```
Or point `ConnectionStrings:AuroraDb` in `src/Web/Aurora.Web/appsettings.json` (or a user-secret)
at any PostgreSQL 16 instance.

### 2. Create the initial migration

This repository ships **without** a pre-generated EF Core migration, because generating one
requires the .NET SDK to actually run the EF Core design-time tooling against the model — it
can't be authored by hand safely. Generate it once, from the repo root:

```bash
dotnet ef migrations add InitialCreate \
  --project src/Web/Aurora.Web/Aurora.Web.csproj \
  --startup-project src/Web/Aurora.Web/Aurora.Web.csproj \
  --output-dir Data/Migrations
```

### 3. Run the app
```bash
dotnet run --project src/Web/Aurora.Web/Aurora.Web.csproj
```
On first run, `DbInitializer` applies the migration and seeds a realistic demo enterprise
("Meridian Industrial Group": 6 suppliers, 3 factories, 2 warehouses, 3 products, 3 customers, 4
technology assets, ~40 graph nodes and 25+ edges with intentionally concentrated supplier risk).

Sign in at `https://localhost:7101` with:
```
admin@aurora.local / Aurora!Demo2026
```
**Change this credential before deploying anywhere but a local sandbox.**

### 4. Or run everything in Docker
```bash
docker compose up --build
```
The app will be available at `http://localhost:8080`.

### Run the tests
```bash
dotnet test
```

---

## A guided tour

1. **Command Center** (`/`) — the neural digital twin graph. Click any node (try "Northbridge
   Components", the intentionally over-concentrated supplier) to focus the graph around it, then
   ask the AI Command Panel *"What happens if this fails?"* — the response is built from real
   graph traversal and risk scoring, not generated text (see `docs/ARCHITECTURE.md` for the exact
   call chain).
2. **Scenario Lab** (`/Scenarios`) — create a scenario against any graph node (e.g. "Northbridge
   Components loses 60% capacity for 90 days"), then run it.
3. **Simulation Results** — day-by-day impact and revenue exposure chart, computed entirely by
   `SimulationEngine`, with the most-affected downstream entities ranked by revenue impact.
4. **Digital Twin** (`/DigitalTwin`) — tabular view of the underlying suppliers, factories,
   products, customers, and technology assets the graph is projected from.

---

## Configuration

All configuration is strongly typed and lives in `appsettings.json` / environment variables /
user-secrets — see `src/Web/Aurora.Web/appsettings.json`:

- `ConnectionStrings:AuroraDb` — PostgreSQL connection string.
- `Risk:ScoringWeights` — the four weights in the risk formula (spec section 18), editable without
  a code change.
- `Decisions:ScoringWeights` — the five weights in the decision scoring formula (spec section 84).
- `AI:Provider` — left as `"None"` by default. AURORA runs fully functional with zero AI provider
  configured (`NullAiProvider`); wire in a real LLM by implementing `IAiProvider` (see
  [ADR 004](docs/ADR/004-ai-provider-abstraction.md)) and registering it in `Program.cs`.

## Security notes

- ASP.NET Core Identity with account lockout, secure cookie flags, and CSRF tokens on every
  mutating form.
- RBAC via role + granular permission claims (`Aurora.Modules.Identity.Domain.AuroraPermissions`),
  enforced as ASP.NET Core authorization policies.
- The seeded admin password (`Aurora!Demo2026`) is a local-sandbox convenience only — rotate it
  (or better, delete the seeded user and create your own) before any shared or internet-facing
  deployment.
- No secrets are committed; `appsettings.json` ships a local-only default connection string
  pointing at the Docker Compose Postgres instance.

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — layering, module shape, and two full request
  walkthroughs.
- [`docs/ADR/`](docs/ADR) — the architecture decisions that most shaped this codebase, including
  *why the LLM is never the calculation engine* (ADR 004) and *why simulation is deterministic*
  (ADR 005).

## Roadmap

Concrete next slices, in the order they'd unlock the most additional spec coverage:

1. **Optimization module** — multi-objective solver (minimize cost/risk/recovery time subject to
   capacity/budget constraints) reading the same `GraphSnapshot`/`SimulationEngine` outputs.
2. **Forecasting module** — statistical models (even a simple exponential smoothing / linear
   trend to start) producing `Prediction / LowerBound / UpperBound / Confidence`, stored with a
   model version for reproducibility, matching the pattern already established by
   `SimulationRun.ModelVersion`.
3. **RAG / organizational knowledge** — document upload → chunking → embedding → retrieval, feeding
   `AiEvidence` the same way graph/risk tools do today, so uploaded contracts and policies become
   additional evidence sources for `InvestigationOrchestrator`.
4. **Real `IAiProvider` implementation** — an OpenAI- or Anthropic-backed provider behind the
   existing interface; zero changes needed anywhere else.
5. **Cascade Lab & Strategy Lab UIs** — thin views over capabilities that already exist
   (`SimulationEngine`, `DecisionScoringService`) but don't yet have a dedicated screen.
6. **Background job queue** for simulation execution (Hangfire/Quartz) instead of the current
   inline-but-SignalR-instrumented execution — a non-breaking swap, see
   `docs/ADR/005-simulation-engine.md`.

## License

Provided as-is for evaluation and further development.
