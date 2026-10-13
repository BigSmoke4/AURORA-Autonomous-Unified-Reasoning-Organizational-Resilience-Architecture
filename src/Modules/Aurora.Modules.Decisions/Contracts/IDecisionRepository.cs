using Aurora.Modules.Decisions.Domain;

namespace Aurora.Modules.Decisions.Contracts;

public interface IDecisionRepository
{
    void Add(Decision decision);

    /// <summary>Tenant-scoped lookup: returns null for decisions that belong to another organization.</summary>
    Task<Decision?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    /// <summary>Pending decisions first, then by score.</summary>
    Task<IReadOnlyList<Decision>> GetQueueAsync(Guid organizationId, int take, CancellationToken ct = default);

    /// <summary>The decision already proposed from an analysis, if any. Prevents duplicate proposals.</summary>
    Task<Decision?> GetBySourceAnalysisAsync(Guid organizationId, Guid analysisRecordId, CancellationToken ct = default);

    Task<int> CountPendingAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Commits the caller's unit of work, including any audit entries staged with it.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
