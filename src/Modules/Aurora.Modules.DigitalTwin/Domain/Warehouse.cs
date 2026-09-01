using Aurora.Shared.Kernel;

namespace Aurora.Modules.DigitalTwin.Domain;

public class Warehouse : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public decimal CapacityUtilizationPct { get; private set; }
    public OperationalStatus Status { get; private set; } = OperationalStatus.Healthy;

    protected Warehouse() { }

    public Warehouse(Guid organizationId, string name, string location, decimal capacityUtilizationPct)
    {
        OrganizationId = organizationId;
        Name = name;
        Location = location;
        CapacityUtilizationPct = capacityUtilizationPct;
    }
}
