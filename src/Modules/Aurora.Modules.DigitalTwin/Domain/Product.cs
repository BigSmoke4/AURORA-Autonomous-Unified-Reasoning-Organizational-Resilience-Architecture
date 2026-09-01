using Aurora.Shared.Kernel;

namespace Aurora.Modules.DigitalTwin.Domain;

public class Product : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public decimal AnnualRevenueExposure { get; private set; }
    public OperationalStatus Status { get; private set; } = OperationalStatus.Healthy;

    protected Product() { }

    public Product(Guid organizationId, string name, string sku, decimal annualRevenueExposure)
    {
        OrganizationId = organizationId;
        Name = name;
        Sku = sku;
        AnnualRevenueExposure = annualRevenueExposure;
    }
}
