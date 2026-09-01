using Aurora.Shared.Kernel;

namespace Aurora.Modules.DigitalTwin.Domain;

public class Customer : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Segment { get; private set; } = string.Empty;
    public decimal AnnualRevenueExposure { get; private set; }
    public OperationalStatus Status { get; private set; } = OperationalStatus.Healthy;

    protected Customer() { }

    public Customer(Guid organizationId, string name, string segment, decimal annualRevenueExposure)
    {
        OrganizationId = organizationId;
        Name = name;
        Segment = segment;
        AnnualRevenueExposure = annualRevenueExposure;
    }
}
