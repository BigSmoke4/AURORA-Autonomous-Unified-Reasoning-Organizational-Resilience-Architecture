using Aurora.Modules.AI.Domain;

namespace Aurora.Modules.AI.Contracts;

public interface IAiInvestigationRepository
{
    Task AddAsync(AiInvestigation investigation, CancellationToken ct = default);
    Task<AiInvestigation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AiInvestigation>> GetByOrganizationAsync(Guid organizationId, int take, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
