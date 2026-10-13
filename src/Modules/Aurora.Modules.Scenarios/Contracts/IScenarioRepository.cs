using Aurora.Modules.Scenarios.Domain;

namespace Aurora.Modules.Scenarios.Contracts;

public interface IScenarioRepository
{
    /// <summary>Tenant-scoped lookup: returns null for scenarios that belong to another organization.</summary>
    Task<Scenario?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Scenario>> GetByOrganizationAsync(Guid organizationId, CancellationToken ct = default);
    Task AddAsync(Scenario scenario, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
