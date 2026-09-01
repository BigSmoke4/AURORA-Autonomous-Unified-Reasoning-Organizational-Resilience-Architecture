using Aurora.Shared.Kernel;

namespace Aurora.Modules.DigitalTwin.Domain;

public class Factory : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public decimal CapacityUtilizationPct { get; private set; }
    public int Criticality { get; private set; } = 3;
    public decimal AnnualRevenueExposure { get; private set; }
    public OperationalStatus Status { get; private set; } = OperationalStatus.Healthy;

    protected Factory() { }

    public Factory(Guid organizationId, string name, string location, decimal capacityUtilizationPct,
        int criticality, decimal annualRevenueExposure)
    {
        OrganizationId = organizationId;
        Name = name;
        Location = location;
        CapacityUtilizationPct = capacityUtilizationPct;
        Criticality = Math.Clamp(criticality, 1, 5);
        AnnualRevenueExposure = annualRevenueExposure;
    }
}
