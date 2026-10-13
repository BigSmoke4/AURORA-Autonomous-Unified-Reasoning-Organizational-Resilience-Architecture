# ADR 005: Deterministic Simulation Model

## Status
Accepted

## Context
Spec section 13/74 requires the simulation engine to be deterministic and reproducible, with the
AI layer only ever explaining results it did not compute.

## Decision
`SimulationEngine.Run` implements a single documented model (see the XML doc comment on the class
for the full explanation): impact starts at the scenario's target node at the scenario's
`Severity`, propagates outward via `GraphTraversalService.DownstreamImpact` (so the interactive
"what would be affected" query and the simulation propagation share one implementation), ramps in
over up to 5 days, holds for the scenario's `DurationDays`, and recovers linearly over a window
proportional to each affected node's `Criticality`. Daily revenue impact is
`node.RevenueExposure / 365 * dailyImpactScore`, summed across all affected nodes and days.

This is a first, honestly-scoped model — not a full discrete-event supply chain simulator with
inventory buffers, lead times, and stochastic demand. It is deliberately simple enough to be
fully unit-tested and auditable end-to-end (see `Aurora.Tests/SimulationEngineTests.cs`).

## Consequences
- Every number the Simulation Lab displays can be traced back to: the scenario's own inputs, the
  target node's graph position, and each node's `RevenueExposure`/`Criticality` fields — nothing
  is estimated by an LLM (see ADR 004).
- The model is deliberately limited. It has no inventory, capacity, or financial sub-models yet
  (spec section 13) — those can be added as alternative `SimulationEngine`
  implementations selected by `ModelVersion` without breaking the stored `SimulationRun` schema,
  which already carries a `ModelVersion` string for exactly this purpose.
