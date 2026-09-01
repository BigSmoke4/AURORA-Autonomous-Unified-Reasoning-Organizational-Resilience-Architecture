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

    /// <summary>Optimistic concurrency token (maps to a PostgreSQL xmin-style rowversion column).</summary>
    public uint Version { get; protected set; }

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
