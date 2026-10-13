using Aurora.Shared.Kernel;

namespace Aurora.Modules.Workforce.Domain;

/// <summary>
/// A role the organization cannot operate without. Coverage is measured by counting the digital-twin
/// employees whose Role equals <see cref="RoleTitle"/> (spec section 19).
/// </summary>
public class CriticalRole : Entity, ITenantScoped
{
    public const int NameMaxLength = 200;
    public const int MinHeadcount = 1;
    public const int MaxHeadcount = 10000;
    public const int MinCriticality = 1;
    public const int MaxCriticality = 5;

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string RoleTitle { get; private set; } = string.Empty;
    public int RequiredHeadcount { get; private set; }
    public int Criticality { get; private set; }

    protected CriticalRole()
    {
    }

    public CriticalRole(Guid organizationId, string name, string roleTitle, int requiredHeadcount, int criticality)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        Name = DomainGuard.RequireText(name, NameMaxLength, nameof(name));
        RoleTitle = DomainGuard.RequireText(roleTitle, NameMaxLength, nameof(roleTitle));
        RequiredHeadcount = DomainGuard.RequireRange(requiredHeadcount, MinHeadcount, MaxHeadcount, nameof(requiredHeadcount));
        Criticality = DomainGuard.RequireRange(criticality, MinCriticality, MaxCriticality, nameof(criticality));
    }
}
