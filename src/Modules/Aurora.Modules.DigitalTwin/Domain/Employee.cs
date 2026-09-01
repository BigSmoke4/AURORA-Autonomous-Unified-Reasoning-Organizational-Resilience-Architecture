using Aurora.Shared.Kernel;

namespace Aurora.Modules.DigitalTwin.Domain;

public class Employee : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public Guid? ManagerId { get; private set; }
    public OperationalStatus Status { get; private set; } = OperationalStatus.Healthy;

    protected Employee() { }

    public Employee(Guid organizationId, string fullName, string role, Guid? departmentId = null, Guid? managerId = null)
    {
        OrganizationId = organizationId;
        FullName = fullName;
        Role = role;
        DepartmentId = departmentId;
        ManagerId = managerId;
    }
}
