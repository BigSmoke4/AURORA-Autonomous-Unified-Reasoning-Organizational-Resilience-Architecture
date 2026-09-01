using Aurora.Shared.Kernel;

namespace Aurora.Modules.Organizations.Domain;

public class Department : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid BusinessUnitId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? CostCenter { get; private set; }

    protected Department() { }

    public Department(Guid organizationId, Guid businessUnitId, string name, string? costCenter = null)
    {
        OrganizationId = organizationId;
        BusinessUnitId = businessUnitId;
        Name = name;
        CostCenter = costCenter;
    }
}
