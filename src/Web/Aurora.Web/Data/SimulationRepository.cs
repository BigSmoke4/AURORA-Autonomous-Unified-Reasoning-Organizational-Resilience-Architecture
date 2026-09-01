using Aurora.Modules.Simulation.Contracts;
using Aurora.Modules.Simulation.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Data;

public sealed class SimulationRepository : ISimulationRepository
{
    private readonly AuroraDbContext _db;
    public SimulationRepository(AuroraDbContext db) => _db = db;

    public async Task AddRunAsync(SimulationRun run, CancellationToken ct = default) => await _db.SimulationRuns.AddAsync(run, ct);

    public async Task AddStepsAsync(IEnumerable<SimulationStep> steps, CancellationToken ct = default) =>
        await _db.SimulationSteps.AddRangeAsync(steps, ct);

    public async Task AddResultAsync(SimulationResult result, CancellationToken ct = default) =>
        await _db.SimulationResults.AddAsync(result, ct);

    public Task<SimulationRun?> GetRunAsync(Guid id, CancellationToken ct = default) =>
        _db.SimulationRuns.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<SimulationResult?> GetResultAsync(Guid simulationRunId, CancellationToken ct = default) =>
        _db.SimulationResults.FirstOrDefaultAsync(r => r.SimulationRunId == simulationRunId, ct);

    public async Task<IReadOnlyList<SimulationStep>> GetStepsAsync(Guid simulationRunId, CancellationToken ct = default) =>
        await _db.SimulationSteps.AsNoTracking().Where(s => s.SimulationRunId == simulationRunId)
            .OrderBy(s => s.DayNumber).ToListAsync(ct);

    public async Task<IReadOnlyList<SimulationRun>> GetRunsByOrganizationAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.SimulationRuns.AsNoTracking().Where(r => r.OrganizationId == organizationId)
            .OrderByDescending(r => r.CreatedAtUtc).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
