using Aurora.Modules.Organizations.Application;
using Aurora.Modules.Organizations.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Data;

public sealed class OrganizationDirectory : IOrganizationDirectory
{
    private readonly AuroraDbContext _db;
    public OrganizationDirectory(AuroraDbContext db) => _db = db;

    public Task<Organization?> GetByIdAsync(Guid organizationId, CancellationToken ct = default) =>
        _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct);

    public async Task<IReadOnlyList<Organization>> GetAllActiveAsync(CancellationToken ct = default) =>
        await _db.Organizations.AsNoTracking().Where(o => o.IsActive).ToListAsync(ct);
}
