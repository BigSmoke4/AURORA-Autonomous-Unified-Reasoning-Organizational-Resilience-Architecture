using Aurora.Modules.DigitalTwin.Domain;

namespace Aurora.Modules.DigitalTwin.Contracts;

/// <summary>Read contract exposed to other modules (Graph, Simulation, Web) so they never
/// query DigitalTwin's DbSets directly — see spec section 4, "no random module-to-module
/// database access".</summary>
public interface IDigitalTwinReadService
{
    Task<IReadOnlyList<Supplier>> GetSuppliersAsync(Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Factory>> GetFactoriesAsync(Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Warehouse>> GetWarehousesAsync(Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetProductsAsync(Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Customer>> GetCustomersAsync(Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Employee>> GetEmployeesAsync(Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<TechnologyAsset>> GetTechnologyAssetsAsync(Guid organizationId, CancellationToken ct = default);
    Task<decimal?> GetRevenueExposureAsync(Guid organizationId, string entityType, Guid entityId, CancellationToken ct = default);
}
