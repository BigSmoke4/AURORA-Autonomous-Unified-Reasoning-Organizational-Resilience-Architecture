using System.Linq.Expressions;
using Aurora.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Data.Repositories;

/// <summary>EF Core implementation of <see cref="IRepository{TEntity}"/> over the tenant-filtered AuroraDbContext.</summary>
public sealed class EfRepository<TEntity>(AuroraDbContext db) : IRepository<TEntity> where TEntity : Entity
{
    private DbSet<TEntity> Set => db.Set<TEntity>();

    public Task<TEntity?> FindAsync(Guid id, CancellationToken ct) =>
        Set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct) =>
        Set.AsNoTracking().AnyAsync(predicate, ct);

    public Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? order,
        CancellationToken ct)
    {
        IQueryable<TEntity> query = Set.Where(predicate);
        if (order is not null) query = order(query);
        return query.FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? filter,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? order,
        CancellationToken ct,
        int? limit = null)
    {
        IQueryable<TEntity> query = Set.AsNoTracking();
        if (filter is not null) query = query.Where(filter);
        if (order is not null) query = order(query);
        if (limit is not null) query = query.Take(limit.Value);
        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TResult>> SelectAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? filter,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? order,
        CancellationToken ct)
    {
        IQueryable<TEntity> query = Set.AsNoTracking();
        if (filter is not null) query = query.Where(filter);
        if (order is not null) query = order(query);
        return await query.Select(selector).ToListAsync(ct);
    }

    public void Add(TEntity entity) => Set.Add(entity);
}
