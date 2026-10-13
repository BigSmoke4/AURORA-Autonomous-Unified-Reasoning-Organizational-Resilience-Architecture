namespace Aurora.Shared.Kernel;

/// <summary>
/// Base class for all entities that are persisted to PostgreSQL.
/// Provides the identity + audit columns every AURORA table shares.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; protected set; }

    /// <summary>
    /// Optimistic concurrency token, stored as bigint. AuroraDbContext advances it on every update, and
    /// the WHERE clause of an UPDATE checks the version that was read.
    /// </summary>
    public long Version { get; protected set; }

    public void Touch() => UpdatedAtUtc = DateTime.UtcNow;

    public override bool Equals(object? obj) => obj is Entity other && other.GetType() == GetType() && other.Id == Id;
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

/// <summary>
/// An entity that belongs to a single tenant (Organization). Every module that stores
/// organization-scoped data implements this so tenant isolation can be enforced centrally
/// (see Shared.Infrastructure.TenantQueryFilter) instead of relying on ad-hoc UI filtering.
/// </summary>
public interface ITenantScoped
{
    Guid OrganizationId { get; }
}
