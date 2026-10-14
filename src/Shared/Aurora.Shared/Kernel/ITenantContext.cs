namespace Aurora.Shared.Kernel;

/// <summary>
/// The tenant (organization) of the current unit of work. Resolved by the host from
/// authenticated claims, never from request input. A null value means "no tenant": the
/// database context then matches no tenant-scoped rows (fail closed).
/// </summary>
public interface ITenantContext
{
    Guid? OrganizationId { get; }
}
