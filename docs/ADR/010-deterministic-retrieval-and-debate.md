# ADR 010: Deterministic Retrieval and Rule-Based Decision Debate

## Status
Accepted.

## Context
The product offers document search and a structured debate over a decision. Both could be built on a language
model. The specification requires that the model is never the source of truth for numbers, that AI reaches the data
only through approved tools, and that results can be reproduced. A model call would break reproducibility, so both
features are deterministic.

## Decision

### Document retrieval
- `DocumentChunker` splits text at blank lines, keeps paragraphs together up to 800 characters, and cuts longer
  paragraphs at the last space before the limit. The same text always produces the same chunks.
- `DocumentRetriever` ranks chunks with Okapi BM25 (k1 1.5, b 0.75). Tokens are letters and digits, at least two
  characters long. Scores are rounded to four places. Ties are ordered by document title, then by chunk ordinal.
- A search returns at most 5 chunks with a 240-character snippet. A query can be at most 200 characters.
- Every search loads all of the organization's chunks into memory. This is the current limit. It is acceptable for a
  corpus of modest size, and a full-text index in PostgreSQL would replace it without changing the ranking contract.

### Decision debate
- `DecisionDebateEngine` runs an advocate and a critic over the same recorded numbers. The advocate meets four
  criteria: predicted revenue impact above zero, decision score at least 60, confidence at least 70, and a
  counterfactual-backed recommendation. The critic raises five objections: confidence below 70, score below 60,
  revenue impact not positive, a recorded prediction error above 25 percent, and no counterfactual backing.
- The judge recommends approval only when the advocate's count is strictly higher. A tie goes to revision, which is
  the cautious outcome.
- Every argument states the figure it checked and, where there is one, the threshold. The debate is stored as a
  `DebateSession` with its arguments, so a result can be audited later.
- The debate contains no model call and no free text generated from a model. The judge's rationale is a sentence
  built from the same counts.

## Consequences
- Results are reproducible from the stored numbers, and the tests pin the thresholds, the tie rule, and the ordering.
- The thresholds are fixed constants. Changing one is a visible code change with a test update, not a hidden
  configuration value.
- A future model-assisted narrative would be allowed only as phrasing over these deterministic results, as ADR 004
  already requires for the investigation narrative.
