using Aurora.Modules.Scenarios.Domain;

namespace Aurora.Modules.Scenarios.Contracts;

public interface IScenarioRepository
{
    Task<Scenario?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Scenario>> GetByOrganizationAsync(Guid organizationId, CancellationToken ct = default);
    Task AddAsync(Scenario scenario, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
