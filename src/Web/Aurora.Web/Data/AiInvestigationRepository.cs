using Aurora.Modules.AI.Contracts;
using Aurora.Modules.AI.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Data;

public sealed class AiInvestigationRepository : IAiInvestigationRepository
{
    private readonly AuroraDbContext _db;
    public AiInvestigationRepository(AuroraDbContext db) => _db = db;

    public async Task AddAsync(AiInvestigation investigation, CancellationToken ct = default) =>
        await _db.AiInvestigations.AddAsync(investigation, ct);

    public Task<AiInvestigation?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.AiInvestigations.AsNoTracking().Include(i => i.Evidence)
            .FirstOrDefaultAsync(i => i.OrganizationId == organizationId && i.Id == id, ct);

    public async Task<IReadOnlyList<AiInvestigation>> GetByOrganizationAsync(Guid organizationId, int take, CancellationToken ct = default) =>
        await _db.AiInvestigations.AsNoTracking().Where(i => i.OrganizationId == organizationId)
            .OrderByDescending(i => i.StartedAtUtc).Take(take).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
