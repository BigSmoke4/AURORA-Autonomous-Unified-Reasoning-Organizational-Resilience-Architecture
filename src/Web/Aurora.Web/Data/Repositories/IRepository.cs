using System.Linq.Expressions;
using Aurora.Shared.Kernel;

namespace Aurora.Web.Data.Repositories;

/// <summary>
/// Persistence boundary for one entity type. Tenant filters are applied by AuroraDbContext, so a repository never
/// returns another organization's rows. Reads are untracked unless the method says otherwise. Writes are staged with
/// Add and committed by the caller's SaveChangesAsync, which is the unit of work.
/// </summary>
public interface IRepository<TEntity> where TEntity : Entity
{
    /// <summary>Tracked lookup by primary key, for flows that update the entity.</summary>
    Task<TEntity?> FindAsync(Guid id, CancellationToken ct);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct);

    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? order,
        CancellationToken ct);

    /// <summary>Untracked rows. A null <paramref name="limit"/> returns every matching row.</summary>
    Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? filter,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? order,
        CancellationToken ct,
        int? limit = null);

    Task<IReadOnlyList<TResult>> SelectAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? filter,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? order,
        CancellationToken ct);

    void Add(TEntity entity);
}
