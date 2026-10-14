using Aurora.Shared.Kernel;

namespace Aurora.Modules.Assets.Domain;

/// <summary>A depreciable physical asset such as plant, vehicles or equipment (spec section 24).</summary>
public class PhysicalAsset : Entity, ITenantScoped
{
    public const int NameMaxLength = 200;
    public const int CategoryMaxLength = 100;
    public const int MinUsefulLifeYears = 1;
    public const int MaxUsefulLifeYears = 100;

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public decimal AcquisitionCost { get; private set; }
    public decimal ResidualValue { get; private set; }
    public DateTime InServiceOnUtc { get; private set; }
    public int UsefulLifeYears { get; private set; }

    protected PhysicalAsset()
    {
    }

    public PhysicalAsset(Guid organizationId, string name, string category, decimal acquisitionCost, decimal residualValue, DateTime inServiceOnUtc, int usefulLifeYears)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        Name = DomainGuard.RequireText(name, NameMaxLength, nameof(name));
        Category = DomainGuard.RequireText(category, CategoryMaxLength, nameof(category));
        AcquisitionCost = DomainGuard.RequirePositive(acquisitionCost, nameof(acquisitionCost));
        ResidualValue = DomainGuard.RequireNonNegative(residualValue, nameof(residualValue));
        if (ResidualValue >= AcquisitionCost) throw new ArgumentException("The residual value must be below the acquisition cost.", nameof(residualValue));
        InServiceOnUtc = DomainGuard.ToUtc(inServiceOnUtc);
        UsefulLifeYears = DomainGuard.RequireRange(usefulLifeYears, MinUsefulLifeYears, MaxUsefulLifeYears, nameof(usefulLifeYears));
    }
}
