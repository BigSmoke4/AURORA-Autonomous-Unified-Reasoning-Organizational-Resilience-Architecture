# ADR 007: Operational Modules Use Deterministic Calculators and Repository Access

## Status
Accepted. Decision 3 is refined by ADR 008. Its consequences on feeds and reporting are updated by ADRs 008 to 010.

## Context
Phase 3 adds twelve operational modules: Workforce, Finance, Operations, Technology, Markets, Assets,
Contracts, SupplyChain, Monitoring, Alerts, Customers, and Reporting. Most of them store a small set of
inputs and show figures derived from them. Each module already has a domain model, so a separate
repository layer for each one would add files without adding a second consumer.

The executive report needs figures from every module. A CSV export is a second output path, and
spreadsheet applications can execute formulas embedded in cell text.

## Decision
1. **Calculators are pure.** Each module's `Application` folder holds static calculators that take
   values and return result records. They use `decimal` for money and percentages, take the evaluation
   date as an argument, and never read the clock or the database. Every calculator is unit tested.
2. **Entities guard their own invariants.** Constructors and methods reject invalid values through
   `DomainGuard`. For example, an alert can be acknowledged only while it is open, and a patch date
   cannot precede the recorded one.
3. **Controllers use the repository layer.** The module controllers inherit `ModuleControllerBase`, which
   resolves the tenant, reads and stages entities through `Entities<T>()` (ADR 008), stages audit entries
   through `AuditRecorder`, and commits with one `SaveChangesAsync`, so the change and its audit row commit
   together. The tenant query filter applies to every read.
4. **The executive report composes the calculators.** `ExecutiveReportBuilder` calls the same calculators
   the module pages use, so the two cannot disagree. Customers has no tables of its own. Reporting records each export run in its own table.
5. **CSV export guards formulas.** Fields that begin with `=`, `+`, `-`, `@`, tab, or carriage return
   are prefixed with an apostrophe. A spreadsheet then reads them as text, so an exported value cannot
   run a formula. The guard also changes negative numbers, which appear as `'-5.00`. The report page
   states this.

## Consequences
- Module boundaries are enforced by project references and by the calculators' lack of I/O. Controllers
  depend on the repository layer rather than on a service layer, which keeps the controllers thin.
  The repositories were added without changing the calculators.
- Customers does not persist. Its figures come from the digital twin's tables. Reporting persists only
  the run history of each export.
- Monitoring readings come from manual entry or from the fixture feed (ADR 009). No live feed has been
  exercised, so the monitoring alerts reflect the values that are recorded.
- The formula guard trades exact negative-number display in the CSV for safe spreadsheet opening.
