# ADR 007: Operational Modules Use Deterministic Calculators and Direct Context Access

## Status
Accepted

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
3. **Controllers use `AuroraDbContext` directly.** The module controllers inherit `ModuleControllerBase`,
   which resolves the tenant, stages audit entries through `AuditRecorder`, and commits with one
   `SaveChangesAsync`, so the change and its audit row commit together. The tenant query filter applies
   to every read.
4. **The executive report composes the calculators.** `ExecutiveReportBuilder` calls the same calculators
   the module pages use, so the two cannot disagree. Customers and Reporting have no tables of their own.
5. **CSV export guards formulas.** Fields that begin with `=`, `+`, `-`, `@`, tab, or carriage return
   are prefixed with an apostrophe. A spreadsheet then reads them as text, so an exported value cannot
   run a formula. The guard also changes negative numbers, which appear as `'-5.00`. The report page
   states this.

## Consequences
- Module boundaries are enforced by project references and by the calculators' lack of I/O. Controllers
  reaching into `AuroraDbContext` is a deliberate departure from a strict controller-to-service layering.
  Adding repositories later would be a non-breaking refactor, because the calculators are unchanged.
- Customers and Reporting do not persist. Figures in those pages come from other modules' tables.
- Monitoring readings are entered by hand. There is no sensor or feed ingestion, so the monitoring
  alerts reflect only the values that users record.
- The formula guard trades exact negative-number display in the CSV for safe spreadsheet opening.
