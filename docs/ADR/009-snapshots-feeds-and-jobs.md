# ADR 009: Graph Snapshots, Feed Adapters, and the Background Job Queue

## Status
Accepted.

## Context
Three requirements arrived together. Counterfactuals should be able to run against the graph as it was at an
earlier time. Monitoring readings should be able to come from an external feed, not only from manual entry. Feed
ingestion and other slow work should run outside the request. The product is a modular monolith (ADR 001), so none
of these may add a new service or broker.

## Decision

### Graph snapshots
- `GraphSnapshotStore` captures the live graph into a `GraphSnapshotRecord`. The payload is JSON written by
  `GraphSnapshotCodec` with a schema version, ordered by id, so the same graph always serializes to the same text.
- Restoring a snapshot keeps every node and edge id, so scenarios that refer to those ids still resolve.
- A counterfactual takes an optional snapshot id. A snapshot that does not belong to the current tenant is reported
  as not found. Captures are audited.
- Snapshots are point-in-time captures. The system does not rebuild a past graph from history.

### Feed adapters
- `IExternalFeedAdapter` only fetches a raw payload and names its source. It does no parsing.
- `MetricFeedParser` is shared by every adapter. It enforces a fixed schema and a row limit. A malformed document
  fails the whole payload, and an invalid row is rejected alone with its row number.
- `MetricFeedIngestor` resolves each row to exactly one metric definition and records it through
  `MetricReadingRecorder`, the same path as manual entry. An unknown or ambiguous metric name is rejected.
- A reading that matches an existing one for the same metric, day, and stored value is counted as already recorded.
  A job that runs twice therefore records each observation once.
- The only adapter is `FixtureMetricFeedAdapter`, which reads `Data/Feeds/Fixtures/metrics-feed.json`. It stands in
  for an external source. No live feed has been exercised. A live adapter would be one more class that implements
  the interface, and it would be tested against recorded payloads like the fixture.

### Background jobs
- Jobs are rows in `BackgroundJobs`. The `BackgroundJobDispatcher` hosted service claims due jobs.
- Delivery is at least once. A job is retried after 30 seconds, up to 3 attempts. A running job whose 5-minute
  lease expires is claimed again. Handlers must therefore be idempotent.
- Each job runs in three scopes: claim, run, and finish. The claim and finish scopes read and write the job row
  with the tenant filter explicitly ignored, because the dispatcher serves every organization.
- The handler scope sets the tenant from the job row through `JobTenantScope`. A tenant never comes from a request.
- If the claim finds no job, it returns no work. It never falls back to a default tenant.

## Consequences
- Adding a job kind means adding one `IBackgroundJobHandler` and one enqueue call.
- Feed ingestion results are visible only through the job row and the audit trail. Failed attempts keep the
  exception type, not the stack trace.
- The queue is database-backed and polls every 2 seconds. It is suitable at the current scale, and a broker can
  replace it behind `BackgroundJobQueue` without changing handlers.
