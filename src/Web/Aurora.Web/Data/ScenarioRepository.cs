using Aurora.Modules.Scenarios.Contracts;
using Aurora.Modules.Scenarios.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Data;

public sealed class ScenarioRepository : IScenarioRepository
{
    private readonly AuroraDbContext _db;
    public ScenarioRepository(AuroraDbContext db) => _db = db;

    public Task<Scenario?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.Scenarios.FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.Id == id, ct);

    public async Task<IReadOnlyList<Scenario>> GetByOrganizationAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Scenarios.AsNoTracking().Where(s => s.OrganizationId == organizationId)
            .OrderByDescending(s => s.CreatedAtUtc).ToListAsync(ct);

    public async Task AddAsync(Scenario scenario, CancellationToken ct = default) =>
        await _db.Scenarios.AddAsync(scenario, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
