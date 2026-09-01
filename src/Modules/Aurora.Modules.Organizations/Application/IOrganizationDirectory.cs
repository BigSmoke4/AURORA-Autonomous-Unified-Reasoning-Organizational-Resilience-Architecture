using Aurora.Modules.Organizations.Domain;

namespace Aurora.Modules.Organizations.Application;

/// <summary>Read-only contract other modules depend on instead of referencing
/// Organizations.Infrastructure directly — keeps module boundaries explicit (spec section 4).</summary>
public interface IOrganizationDirectory
{
    Task<Organization?> GetByIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Organization>> GetAllActiveAsync(CancellationToken ct = default);
}
