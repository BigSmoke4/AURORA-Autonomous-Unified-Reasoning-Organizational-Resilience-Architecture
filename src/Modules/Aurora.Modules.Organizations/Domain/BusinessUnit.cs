using Aurora.Shared.Kernel;

namespace Aurora.Modules.Organizations.Domain;

public class BusinessUnit : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    protected BusinessUnit() { }

    public BusinessUnit(Guid organizationId, string name, string? description = null)
    {
        OrganizationId = organizationId;
        Name = name;
        Description = description;
    }

    private readonly List<Department> _departments = new();
    public IReadOnlyCollection<Department> Departments => _departments.AsReadOnly();
}
