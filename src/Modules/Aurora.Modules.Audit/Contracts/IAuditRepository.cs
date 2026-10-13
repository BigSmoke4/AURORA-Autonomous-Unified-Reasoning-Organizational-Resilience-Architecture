using Aurora.Modules.Audit.Domain;

namespace Aurora.Modules.Audit.Contracts;

public interface IAuditRepository
{
    void Add(AuditEntry entry);

    /// <summary>Most recent entries for one organization. Never returns another tenant's rows.</summary>
    Task<IReadOnlyList<AuditEntry>> GetRecentAsync(Guid organizationId, int take, CancellationToken ct = default);

    /// <summary>Commits staged entries. Used where no other unit of work saves, such as sign-in.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
