using Aurora.Modules.Decisions.Contracts;
using Aurora.Modules.Decisions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Data;

public sealed class DecisionRepository : IDecisionRepository
{
    private readonly AuroraDbContext _db;

    public DecisionRepository(AuroraDbContext db) => _db = db;

    public void Add(Decision decision) => _db.Decisions.Add(decision);

    public Task<Decision?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.Decisions.FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.Id == id, ct);

    public async Task<IReadOnlyList<Decision>> GetQueueAsync(Guid organizationId, int take, CancellationToken ct = default) =>
        await _db.Decisions.AsNoTracking()
            .Where(d => d.OrganizationId == organizationId)
            .OrderBy(d => d.ApprovalState)
            .ThenByDescending(d => d.DecisionScore)
            .ThenByDescending(d => d.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public Task<Decision?> GetBySourceAnalysisAsync(Guid organizationId, Guid analysisRecordId, CancellationToken ct = default) =>
        _db.Decisions.AsNoTracking()
            .FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.SourceAnalysisId == analysisRecordId, ct);

    public Task<int> CountPendingAsync(Guid organizationId, CancellationToken ct = default) =>
        _db.Decisions.CountAsync(d => d.OrganizationId == organizationId && d.ApprovalState == DecisionApprovalState.PendingApproval, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
