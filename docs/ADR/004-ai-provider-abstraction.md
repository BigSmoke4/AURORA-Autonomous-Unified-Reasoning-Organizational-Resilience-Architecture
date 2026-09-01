# ADR 004: The LLM Is Never the Calculation Engine

## Status
Accepted

## Context
It is easy to build an "AI enterprise platform" that is actually a thin prompt wrapped around a
chat model, with the model asked to estimate risk scores, revenue impact, or simulation outcomes
directly. That approach cannot be trusted for enterprise decision-making, cannot be audited, and
is explicitly rejected by the specification (section 100).

## Decision
1. All numeric calculations — graph traversal results, risk scores, simulation impact/revenue
   figures — are produced by deterministic C# services (`GraphTraversalService`,
   `RiskScoringService`, `SimulationEngine`) that take no dependency on any AI provider.
2. `IAiProvider` is a narrow abstraction with one real capability: turning already-computed
   evidence into a natural-language explanation (`ExplainAsync`). It is never asked to produce a
   number that flows back into a calculation.
3. `InvestigationOrchestrator` composes calls to the deterministic services first, records each
   result as `AiEvidence`, and only then (optionally) asks `IAiProvider` to narrate what was
   found.
4. The default `IAiProvider` implementation, `NullAiProvider`, has `IsConfigured = false` and
   returns a templated explanation instead of calling out to anything — meaning AURORA's graph,
   risk, and simulation features are fully functional with zero external AI dependency
   configured. This directly satisfies the resilience requirement in spec section 71.

## Consequences
- A real LLM (OpenAI, Anthropic, Azure OpenAI, etc.) can be wired in later purely by implementing
  `IAiProvider` and registering it in `Program.cs` — no other class needs to change, and no
  existing guarantee about where numbers come from is weakened by doing so.
- The natural-language scenario parser (`NaturalLanguageScenarioExtractor`) is intentionally a
  deterministic regex/keyword extractor rather than an LLM call, documented plainly as such —
  it can be replaced or augmented by an `IAiProvider`-backed version later, but it must continue
  to report `MissingFields` rather than inventing values, per spec section 16.
