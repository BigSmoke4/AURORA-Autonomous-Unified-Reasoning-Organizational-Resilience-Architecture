# ADR 011: Feed Adapters, Vulnerability Linkage, and the Shared Cache

## Status
Accepted.

## Context
The specification asks for external feeds and model endpoints, for patch policies to be linked to vulnerability
data, and for the product to run on more than one instance. Those needs cannot be verified against live services
in this project, because the test environment has no credentials and no outbound access to the feed or model
providers. Every decision below is therefore testable with fixtures and stubs, and each live path is opt-in.

## Decision

### One adapter per feed, chosen at startup
- `IExternalFeedAdapter` exposes `Kind` (metrics or vulnerabilities), `SourceKey`, and `FetchAsync`. An adapter only
  fetches a raw payload. It never parses, validates, or writes data.
- Each feed has exactly one adapter. It is the live HTTP adapter when `ExternalFeeds:<Feed>:BaseUrl` is set, and the
  bundled fixture adapter otherwise. `FeedSourceCatalog` chooses the same key when a job is queued, and the job
  handler finds the adapter by kind and key. Registering both adapters for one feed would let the fixture silently
  win, which is why `Program.cs` registers only one.
- `ExternalFeedOptionsValidator` runs at startup (`ValidateOnStart`). A live endpoint must use https, and its
  timeout must be between 1 and 120 seconds. The host does not start otherwise, so an API key is never sent in the clear.
- Each live request has an attempt timeout equal to the configured timeout, a total budget of twice that, and a
  circuit breaker that samples over three times that. Throttling (HTTP 429) and server errors are retried by the
  standard resilience handler.
- The API key is sent as a bearer token. It never appears in an error message, a log line, or a response. A payload
  larger than 5 MB is rejected while it is read, so a misbehaving source cannot exhaust memory.

### Parsing and ingestion
- `MetricFeedParser` and `VulnerabilityFeedParser` each enforce a fixed schema version. Invalid rows are rejected one
  at a time with a row number and a reason, and the valid rows still import. The job summary lists at most three reasons.
- Metric readings go through `MetricReadingRecorder`, the same path as manual entry, so alerts behave identically.
- Vulnerability advisories are upserted by `(OrganizationId, CveId)`. Re-importing an unchanged feed creates no rows,
  and a changed advisory updates its own row. Each import is audited (`VulnerabilityFeedImported`).

### Patch policies linked to vulnerabilities
- A patch policy may name a `SoftwareProduct`. The match is case-insensitive.
- An advisory is exposed when it names that product and was published after the policy's `LastPatchedOnUtc`.
  Its deadline is its publication date plus `PatchSlaDays`.
- Severity comes from the CVSS base score: Critical at 9.0 or above, High at 7.0 or above, Medium at 4.0 or above,
  and Low otherwise.
- `PatchExposureCalculator` is a pure function over values. The model is never involved in these numbers.

### Shared cache for narratives
- `CachingAiProvider` decorates the model provider. The key is a SHA-256 of the model version, the system prompt,
  and the evidence JSON, under the prefix `ai-narrative:v1:`. Changing the model therefore invalidates every entry.
- Entries live for `AI:CacheMinutes` (default 60). Zero disables the cache.
- Only a successful answer is cached. A failure from the cache is logged and ignored, so a Redis outage never fails
  a request.
- When `ConnectionStrings:Redis` is set, the cache is Redis, with the instance name `aurora:` so several
  applications can share one server. Otherwise a process-local memory cache is used.

### Forwarded headers
- The sign-in rate limit is keyed by client address. Headers are trusted only from loopback and from the addresses
  listed in `Security:TrustedProxies`, so a client cannot choose its own address to avoid the limit.

### Verification
- Parsers, the HTTP adapter (with a stub handler), option validation, the exposure calculator, the ingestor against
  PostgreSQL (re-import is idempotent, and a changed advisory updates its row), the caching decorator, and the Redis
  shared-read check (CI starts Redis) are all tested.
- Live feed and model checks are in `tests/Aurora.Tests/Live`. Each one runs only when its endpoint is set in the
  environment, and none runs in a normal test run. They have not been run against a real provider.
- The container job runs `tests/load/aurora-smoke.js` with k6 at a constant 20 requests per second for two minutes.
  The run fails if the 95th percentile exceeds 800 ms, more than 1 percent of requests fail, or fewer than 99 percent
  of checks pass.

## Consequences
- Adding a feed means writing one adapter and one parser. The ingestion, job, and validation paths are shared.
- A live feed can be enabled by configuration alone. Its correctness against the real provider's format is not
  established until someone runs the live test with that provider's credentials.
- The two-minute k6 run is a smoke load test, not a soak. A soak needs a longer run on a dedicated environment.
