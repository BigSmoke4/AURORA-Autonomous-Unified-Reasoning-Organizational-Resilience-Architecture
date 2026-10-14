using Aurora.Modules.Analysis.Contracts;
using Aurora.Modules.Analysis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Data;

public sealed class AnalysisRecordRepository : IAnalysisRecordRepository
{
    private readonly AuroraDbContext _db;

    public AnalysisRecordRepository(AuroraDbContext db) => _db = db;

    public void Add(AnalysisRecord record) => _db.AnalysisRecords.Add(record);

    public Task<AnalysisRecord?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.AnalysisRecords.AsNoTracking().FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.Id == id, ct);

    public async Task<IReadOnlyList<AnalysisRecord>> GetRecentAsync(Guid organizationId, int take, CancellationToken ct = default) =>
        await _db.AnalysisRecords.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
