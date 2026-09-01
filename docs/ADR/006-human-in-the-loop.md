# ADR 006: Decisions Always Require an Explicit, Audited Human Action

## Status
Accepted

## Decision
The `Decisions` module's `Decision` aggregate starts in `PendingApproval` and can only move to
`Approved` or `Rejected` through `Decision.Approve(approverId)` / `Decision.Reject(approverId)`,
each of which stamps the approver's id and a timestamp. No code path in AURORA transitions a
`Decision` to `Approved` automatically, regardless of how high the AI's computed confidence is.
Executing an approved decision (`RecordActualOutcome`) is a separate, later step, keeping
"approved" and "executed" as distinct, independently audited states per spec section 32/33.
