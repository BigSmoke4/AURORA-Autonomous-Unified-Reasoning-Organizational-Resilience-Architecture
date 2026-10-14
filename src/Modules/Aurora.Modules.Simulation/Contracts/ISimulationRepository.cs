using Aurora.Modules.Simulation.Domain;

namespace Aurora.Modules.Simulation.Contracts;

public interface ISimulationRepository
{
    Task AddRunAsync(SimulationRun run, CancellationToken ct = default);
    Task AddStepsAsync(IEnumerable<SimulationStep> steps, CancellationToken ct = default);
    Task AddResultAsync(SimulationResult result, CancellationToken ct = default);
    /// <summary>Tenant-scoped lookup: returns null for runs that belong to another organization.</summary>
    Task<SimulationRun?> GetRunAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<SimulationResult?> GetResultAsync(Guid simulationRunId, CancellationToken ct = default);
    Task<IReadOnlyList<SimulationStep>> GetStepsAsync(Guid simulationRunId, CancellationToken ct = default);
    Task<IReadOnlyList<SimulationRun>> GetRunsByOrganizationAsync(Guid organizationId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
