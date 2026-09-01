using Aurora.Modules.DigitalTwin.Contracts;
using Aurora.Modules.DigitalTwin.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Data;

public sealed class DigitalTwinReadService : IDigitalTwinReadService
{
    private readonly AuroraDbContext _db;
    public DigitalTwinReadService(AuroraDbContext db) => _db = db;

    public async Task<IReadOnlyList<Supplier>> GetSuppliersAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Suppliers.AsNoTracking().Where(s => s.OrganizationId == organizationId).ToListAsync(ct);

    public async Task<IReadOnlyList<Factory>> GetFactoriesAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Factories.AsNoTracking().Where(f => f.OrganizationId == organizationId).ToListAsync(ct);

    public async Task<IReadOnlyList<Warehouse>> GetWarehousesAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Warehouses.AsNoTracking().Where(w => w.OrganizationId == organizationId).ToListAsync(ct);

    public async Task<IReadOnlyList<Product>> GetProductsAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Products.AsNoTracking().Where(p => p.OrganizationId == organizationId).ToListAsync(ct);

    public async Task<IReadOnlyList<Customer>> GetCustomersAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Customers.AsNoTracking().Where(c => c.OrganizationId == organizationId).ToListAsync(ct);

    public async Task<IReadOnlyList<Employee>> GetEmployeesAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Employees.AsNoTracking().Where(e => e.OrganizationId == organizationId).ToListAsync(ct);

    public async Task<IReadOnlyList<TechnologyAsset>> GetTechnologyAssetsAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.TechnologyAssets.AsNoTracking().Where(t => t.OrganizationId == organizationId).ToListAsync(ct);

    public async Task<decimal?> GetRevenueExposureAsync(Guid organizationId, string entityType, Guid entityId, CancellationToken ct = default)
    {
        return entityType switch
        {
            nameof(Supplier) => (await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == entityId && s.OrganizationId == organizationId, ct))?.AnnualRevenueExposure,
            nameof(Factory) => (await _db.Factories.AsNoTracking().FirstOrDefaultAsync(f => f.Id == entityId && f.OrganizationId == organizationId, ct))?.AnnualRevenueExposure,
            nameof(Product) => (await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == entityId && p.OrganizationId == organizationId, ct))?.AnnualRevenueExposure,
            nameof(Customer) => (await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == entityId && c.OrganizationId == organizationId, ct))?.AnnualRevenueExposure,
            _ => null
        };
    }
}
