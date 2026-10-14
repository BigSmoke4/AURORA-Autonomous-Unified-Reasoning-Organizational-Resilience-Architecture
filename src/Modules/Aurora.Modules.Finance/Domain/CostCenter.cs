using Aurora.Shared.Kernel;

namespace Aurora.Modules.Finance.Domain;

/// <summary>A budgeted cost center. Money is decimal and never floating point (spec section 10).</summary>
public class CostCenter : Entity, ITenantScoped
{
    public const int CodeMaxLength = 30;
    public const int NameMaxLength = 200;

    public Guid OrganizationId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public decimal AnnualBudget { get; private set; }
    public decimal ActualSpend { get; private set; }

    protected CostCenter()
    {
    }

    public CostCenter(Guid organizationId, string code, string name, decimal annualBudget, decimal actualSpend)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        Code = DomainGuard.RequireText(code, CodeMaxLength, nameof(code));
        Name = DomainGuard.RequireText(name, NameMaxLength, nameof(name));
        AnnualBudget = DomainGuard.RequireNonNegative(annualBudget, nameof(annualBudget));
        ActualSpend = DomainGuard.RequireNonNegative(actualSpend, nameof(actualSpend));
    }
}
