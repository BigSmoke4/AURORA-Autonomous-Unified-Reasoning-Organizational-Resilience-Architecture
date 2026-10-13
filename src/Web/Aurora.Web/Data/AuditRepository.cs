using Aurora.Modules.Audit.Contracts;
using Aurora.Modules.Audit.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Data;

public sealed class AuditRepository : IAuditRepository
{
    private readonly AuroraDbContext _db;

    public AuditRepository(AuroraDbContext db) => _db = db;

    public void Add(AuditEntry entry) => _db.AuditEntries.Add(entry);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<AuditEntry>> GetRecentAsync(Guid organizationId, int take, CancellationToken ct = default) =>
        await _db.AuditEntries.AsNoTracking()
            .Where(e => e.OrganizationId == organizationId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);
}
