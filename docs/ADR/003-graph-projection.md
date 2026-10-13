# ADR 003: Graph Projection Instead of Querying PostgreSQL Per Interaction

## Status
Accepted

## Context
The neural graph visualization and graph intelligence queries (shortest path, downstream impact,
single-sourced node screening) need to run many small traversal steps. Issuing a SQL query per hop would be slow
and would not scale to an interactive UI.

## Decision
`IGraphProjectionService.BuildLiveGraphAsync` loads all `GraphNode`/`GraphEdge` rows for an
organization once per request/operation into an in-memory `GraphSnapshot` (adjacency lists keyed
by node id). Every traversal algorithm in `GraphTraversalService` operates purely against this
in-memory structure — no further database round trips.

## Consequences
- The single-sourced node screen (`DetectSingleSourcedNodes`) is a local heuristic. A node is flagged
  when one of its dependents has no other incoming supplier of the same node type. It does not search
  for alternate paths beyond that dependent, so it can over-report. It is not a minimum-cut
  computation, and the docs and UI must not describe it as one.
- One database round trip regardless of how many algorithms run against the snapshot in a single
  request (e.g. the AI investigation orchestrator runs four different graph algorithms off one
  snapshot).
- The snapshot is a point-in-time read; if the graph changes mid-request, the snapshot does not
  see it. This is acceptable for AURORA's read-mostly interactive queries.
- Historical/Scenario/Simulation graph "states" (spec section 10) can be built by taking a live
  snapshot and applying deltas in memory, without a second physical data source.
