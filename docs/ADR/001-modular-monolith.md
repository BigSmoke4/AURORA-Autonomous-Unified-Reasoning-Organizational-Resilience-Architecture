# ADR 001: Modular Monolith, Not Microservices

## Status
Accepted

## Context
The AURORA specification describes ~20 conceptual domains (Identity, Organizations, DigitalTwin,
Graph, Scenarios, Simulation, Risk, Decisions, AI, ...). Splitting these into separate deployable
services on day one would multiply operational complexity (network calls, distributed
transactions, service discovery, separate deployments) without a corresponding benefit at this
stage of the product's life.

## Decision
Each domain is implemented as an independent class library ("module") with its own
Domain/Application/Infrastructure/Contracts folders and its own `IEntityTypeConfiguration<T>`
classes. All modules are referenced by a single ASP.NET Core Web project (`Aurora.Web`) and share
one physical PostgreSQL database through one composite `AuroraDbContext`. The application is
built, deployed, and scaled as a single unit.

Modules do not reference each other's `Infrastructure` folders or query each other's tables
directly — they depend on `Contracts` interfaces (e.g. `IDigitalTwinReadService`,
`IGraphRepository`) which are implemented once, in `Aurora.Web/Data`, and injected via DI. This
keeps a real seam between modules so that, if a specific module's load profile someday justifies
it, it can be extracted into its own service by moving its Domain/Application code, standing up
its own database, and replacing the in-process `Contracts` implementation with an HTTP or
message-based client — without having to redesign the module's internals.

## Consequences
- Simpler operations: one deployable, one connection string, one set of migrations.
- Cross-module calls are plain C# method calls (fast, easy to debug, transactional).
- Module boundaries are enforced by code review and project reference discipline, not a network
  boundary — a determined developer *could* violate them. This is an accepted tradeoff for
  the current stage.
