# ADR 008: Repository Layer and Unit of Work for Module Persistence

## Status
Accepted. Refines the persistence decision in ADR 007.

## Context
ADR 007 let module controllers use `AuroraDbContext` directly and noted that a repository layer could be added
later without changing the calculators. Since then the executive report, feed ingestion, the job queue, and the
knowledge and debate features all need the same tenant rules in more places. Duplicating query code in each
controller would make it easy to forget the tenant filter or the audit row.

## Decision
- `IRepository<TEntity>` in `Data/Repositories` is the persistence boundary for one entity type. `EfRepository<T>`
  implements it over `AuroraDbContext`.
- `ModuleControllerBase.Entities<T>()` returns a repository for the request's context. Module controllers do not
  query `AuroraDbContext` sets directly; a grep of the Phase 3 controllers finds only `SaveChangesAsync`.
- Reads are untracked unless the method is documented as tracked (`FindAsync`). Writes are staged with `Add`.
- The request commits once with `SaveChangesAsync`. That call is the unit of work, so a change and its audit row
  commit in the same transaction.
- Repositories never bypass the tenant filter. `AuroraDbContext` applies it to every `ITenantScoped` entity, and the
  only explicit bypass is the background job dispatcher (ADR 009).

## Consequences
- Tenant isolation does not depend on each controller remembering a filter.
- Calculators still take values and never touch the repository, so their unit tests need no database.
- The repository is a thin wrapper over EF Core. It is not a second query language, and it does not hide
  `IQueryable` from the module code that needs projections.
