using Aurora.Modules.Analysis.Domain;

namespace Aurora.Modules.Analysis.Contracts;

public interface IAnalysisRecordRepository
{
    void Add(AnalysisRecord record);

    /// <summary>Tenant-scoped lookup: returns null for records that belong to another organization.</summary>
    Task<AnalysisRecord?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<AnalysisRecord>> GetRecentAsync(Guid organizationId, int take, CancellationToken ct = default);

    /// <summary>Commits the caller's unit of work, including any audit entries staged with it.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
