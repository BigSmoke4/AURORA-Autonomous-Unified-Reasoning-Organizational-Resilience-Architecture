# ADR 002: PostgreSQL as the Sole, Authoritative Data Store

## Status
Accepted

## Context
AURORA needs to store relational business data (organizations, suppliers, factories, ...),
graph-shaped data (nodes/edges), and time-series simulation output. A tempting option is to
introduce a dedicated graph database (Neo4j) and/or a time-series database alongside a relational
store, but that means three data stores to operate, back up, and keep consistent.

## Decision
PostgreSQL is the single source of truth for all AURORA data, including the graph. Graph data is
stored in two ordinary tables, `GraphNodes` and `GraphEdges`, and "graph intelligence" is
implemented in the `Aurora.Modules.Graph.Application` layer as in-memory algorithms
(`GraphTraversalService`) running against a `GraphSnapshot` projected from those tables — see
ADR 003. We do not attempt to make PostgreSQL behave like a native graph database (no recursive
CTE gymnastics standing in for a graph engine, no attempt at Cypher-like querying).

## Consequences
- One database to operate, back up, and reason about consistency for.
- Graph queries beyond a few hops run in application memory rather than in the database, which
  is fine at the node/edge counts a single enterprise's digital twin realistically has (thousands,
  not billions) but would need revisiting (e.g. a real graph database, or a cached/precomputed
  index) if AURORA needed to support graphs several orders of magnitude larger.
- Financial figures use PostgreSQL `decimal` columns exclusively — floating-point types are never
  used for money anywhere in the schema.
